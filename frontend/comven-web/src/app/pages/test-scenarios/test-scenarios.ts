import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ClientService } from '../../core/services/client.service';
import { OperationService } from '../../core/services/operation.service';
import { StatementService } from '../../core/services/statement.service';
import {
  Operation,
  OperationStatus,
  OperationType
} from '../../core/models/operation.model';
import {
  StatementItem,
  TransactionType
} from '../../core/models/statement.model';

type Verdict = 'passed' | 'failed' | 'inconclusive' | 'unavailable';

interface Check {
  label: string;
  ok: boolean;
  detail: string;
}

interface ApiCall {
  label: string;
  status: number;
  operation: Operation | null;
  detail: string;
}

interface Snapshot {
  balance: number;
  operations: Operation[];
  items: StatementItem[];
  debits: number;
  refunds: number;
}

interface ScenarioResult {
  verdict: Verdict;
  explanation: string;
  checks: Check[];
  calls: ApiCall[];
  operationIds: string[];
  initialBalance: number | null;
  finalBalance: number | null;
  operationCount: number | null;
  debitCount: number | null;
  refundCount: number | null;
}

interface Scenario {
  id: number;
  title: string;
  description: string;
  running: boolean;
  result: ScenarioResult | null;
  run: () => Promise<ScenarioResult>;
}

const PRICE = 12.5;
const FAILURE_HEADER = 'X-Force-Provider-Failure';

@Component({
  selector: 'app-test-scenarios',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './test-scenarios.html',
  styleUrl: './test-scenarios.scss'
})
export class TestScenarios {
  private readonly http = inject(HttpClient);
  private readonly clients = inject(ClientService);
  private readonly operations = inject(OperationService);
  private readonly statements = inject(StatementService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly scenarios: Scenario[] = [
    {
      id: 1,
      title: 'Saldo insuficiente',
      description:
        'Cliente com R$ 5,00 tenta uma consulta veicular de R$ 12,50. A API deve recusar e nada deve ser registrado.',
      running: false,
      result: null,
      run: () => this.insufficientBalance()
    },
    {
      id: 2,
      title: 'Falha do fornecedor e estorno',
      description:
        'Cliente com R$ 50,00 executa uma operação com falha forçada no fornecedor (somente em desenvolvimento). Deve haver débito, um único estorno e saldo final de R$ 50,00, mesmo repetindo a chave de idempotência.',
      running: false,
      result: null,
      run: () => this.providerFailureRefund()
    },
    {
      id: 3,
      title: 'Idempotência com requisições simultâneas',
      description:
        'Cliente com R$ 50,00. Duas requisições simultâneas com a mesma chave de idempotência para uma consulta de R$ 12,50. Deve existir uma única operação e um único débito.',
      running: false,
      result: null,
      run: () => this.concurrentSameKey()
    },
    {
      id: 4,
      title: 'Operações diferentes disputando o mesmo saldo',
      description:
        'Cliente com R$ 20,00. Duas requisições simultâneas com chaves diferentes, de R$ 12,50 cada. Apenas uma pode ser cobrada; a outra deve ser recusada por saldo insuficiente.',
      running: false,
      result: null,
      run: () => this.concurrentDifferentKeys()
    }
  ];

  async execute(scenario: Scenario): Promise<void> {
    if (scenario.running) return;

    scenario.running = true;
    scenario.result = null;
    this.cdr.markForCheck();

    try {
      scenario.result = await scenario.run();
    } catch (error) {
      scenario.result = this.emptyResult(
        'failed',
        'O teste não pôde ser concluído: ' + this.describeError(error)
      );
    }

    scenario.running = false;
    this.cdr.markForCheck();
  }

  verdictLabel(verdict: Verdict): string {
    switch (verdict) {
      case 'passed': return 'Passou';
      case 'failed': return 'Falhou';
      case 'unavailable': return 'Indisponível';
      default: return 'Inconclusivo';
    }
  }

  callText(call: ApiCall): string {
    return call.operation
      ? `HTTP ${call.status} - operação ${call.operation.id} (${this.statusName(call.operation.status)})`
      : `HTTP ${call.status} - ${call.detail}`;
  }

  // ---------- Cenário 1 ----------

  private async insufficientBalance(): Promise<ScenarioResult> {
    const client = await this.createClient('Teste 1 - saldo insuficiente', 5);
    const call = await this.postOperation('Consulta veicular R$ 12,50', client.id, crypto.randomUUID());
    const snap = await this.snapshot(client.id);

    const checks: Check[] = [
      {
        label: 'A API recusou a operação por saldo insuficiente',
        ok: call.status === 409 && call.detail === 'Saldo insuficiente.',
        detail: `HTTP ${call.status}: ${call.detail || 'sem mensagem'}`
      },
      this.balanceCheck('O saldo permaneceu em R$ 5,00', snap.balance, 5),
      {
        label: 'Nenhuma operação foi registrada',
        ok: snap.operations.length === 0,
        detail: `${snap.operations.length} operação(ões) no extrato`
      },
      {
        label: 'Nenhum débito foi criado',
        ok: snap.debits === 0 && snap.refunds === 0,
        detail: `${snap.debits} débito(s), ${snap.refunds} estorno(s)`
      }
    ];

    return this.buildResult(checks, [call], 5, snap, {
      passed: 'Saldo insuficiente recusado pela API; saldo, operações e movimentações permaneceram intactos.',
      failed: 'Alguma verificação falhou; veja os itens marcados abaixo.'
    });
  }

  // ---------- Cenário 2 ----------

  private async providerFailureRefund(): Promise<ScenarioResult> {
    const available = await this.isForcedFailureAvailable();

    if (!available) {
      return this.emptyResult(
        'unavailable',
        'A falha forçada do fornecedor só existe quando a API roda em ambiente Development (GET /api/dev/provider-failure não respondeu). O teste não foi executado.'
      );
    }

    const client = await this.createClient('Teste 2 - falha e estorno', 50);
    const key = crypto.randomUUID();
    const headers = { [FAILURE_HEADER]: 'true' };

    const first = await this.postOperation('Primeira chamada (falha forçada)', client.id, key, headers);
    const afterFirst = await this.snapshot(client.id);

    const repeated = await this.postOperation('Repetição da mesma chave', client.id, key, headers);
    const final = await this.snapshot(client.id);

    const op = final.operations[0];

    const checks: Check[] = [
      {
        label: 'A operação foi registrada como Failed',
        ok: first.operation?.status === OperationStatus.Failed && op?.status === OperationStatus.Failed,
        detail: `resposta: ${this.statusName(first.operation?.status)}; persistida: ${this.statusName(op?.status)}`
      },
      {
        label: 'O débito inicial foi registrado',
        ok: afterFirst.debits === 1 && this.sameMoney(this.debitTotal(afterFirst), PRICE),
        detail: `${afterFirst.debits} débito(s) de ${this.brl(this.debitTotal(afterFirst))}`
      },
      {
        label: 'O estorno foi registrado uma única vez',
        ok: afterFirst.refunds === 1,
        detail: `${afterFirst.refunds} estorno(s) após a primeira chamada`
      },
      this.balanceCheck('O saldo final voltou para R$ 50,00', final.balance, 50),
      {
        label: 'A repetição da chave devolveu a mesma operação',
        ok: repeated.status === 201 && repeated.operation?.id === first.operation?.id,
        detail: `HTTP ${repeated.status}; id ${repeated.operation?.id ?? '-'}`
      },
      {
        label: 'A repetição não criou outro estorno nem outra operação',
        ok: final.refunds === 1 && final.debits === 1 && final.operations.length === 1,
        detail: `${final.operations.length} operação(ões), ${final.debits} débito(s), ${final.refunds} estorno(s)`
      },
      this.consistencyCheck(final)
    ];

    return this.buildResult(checks, [first, repeated], 50, final, {
      passed: 'A falha do fornecedor gerou débito seguido de um único estorno; o saldo voltou a R$ 50,00 e a repetição da chave não duplicou nada.',
      failed: 'Alguma verificação falhou; veja os itens marcados abaixo.'
    });
  }

  // ---------- Cenário 3 ----------

  private async concurrentSameKey(): Promise<ScenarioResult> {
    const client = await this.createClient('Teste 3 - mesma chave', 50);
    const key = crypto.randomUUID();

    // As duas chamadas são iniciadas antes de aguardar qualquer resposta.
    const requests = [
      this.postOperation('Requisição A', client.id, key),
      this.postOperation('Requisição B', client.id, key)
    ];
    const calls = await Promise.all(requests);
    const snap = await this.snapshot(client.id);

    const ids = [...new Set(calls.map(c => c.operation?.id).filter((id): id is string => !!id))];
    const op = snap.operations[0];
    const expectedBalance = op?.status === OperationStatus.Failed ? 50 : 50 - PRICE;

    const checks: Check[] = [
      {
        label: 'Nenhuma requisição recebeu erro ou conflito',
        ok: calls.every(c => c.status === 201),
        detail: calls.map(c => `${c.label}: HTTP ${c.status}${c.status === 201 ? '' : ' ' + c.detail}`).join(' | ')
      },
      {
        label: 'As duas respostas apontam para a mesma operação',
        ok: ids.length === 1 && calls.every(c => c.operation !== null),
        detail: `${ids.length} id(s) distinto(s) retornado(s)`
      },
      {
        label: 'Apenas uma operação foi persistida',
        ok: snap.operations.length === 1 && (ids.length !== 1 || snap.operations[0].id === ids[0]),
        detail: `${snap.operations.length} operação(ões) no extrato`
      },
      {
        label: 'Apenas um débito de R$ 12,50 foi registrado',
        ok: snap.debits === 1 && this.sameMoney(this.debitTotal(snap), PRICE),
        detail: `${snap.debits} débito(s) de ${this.brl(this.debitTotal(snap))}`
      },
      {
        label: 'A operação terminou em estado final',
        ok: op?.status === OperationStatus.Completed || op?.status === OperationStatus.Failed,
        detail: `status persistido: ${this.statusName(op?.status)}`
      },
      this.balanceCheck(
        op?.status === OperationStatus.Failed
          ? 'Saldo final igual ao original (fornecedor falhou e estornou)'
          : 'Saldo final corresponde a uma única cobrança (R$ 37,50)',
        snap.balance,
        expectedBalance
      ),
      this.consistencyCheck(snap)
    ];

    return this.buildResult(checks, calls, 50, snap, {
      passed: op?.status === OperationStatus.Failed
        ? 'Uma única operação e um único débito; o fornecedor falhou e o valor foi estornado uma vez.'
        : 'Uma única operação e um único débito de R$ 12,50, sem cobrança duplicada.',
      failed: 'Alguma verificação falhou (inclusive respostas de erro, mesmo que o banco tenha evitado cobrança duplicada).'
    });
  }

  // ---------- Cenário 4 ----------

  private async concurrentDifferentKeys(): Promise<ScenarioResult> {
    const client = await this.createClient('Teste 4 - chaves diferentes', 20);

    const requests = [
      this.postOperation('Requisição A (chave 1)', client.id, crypto.randomUUID()),
      this.postOperation('Requisição B (chave 2)', client.id, crypto.randomUUID())
    ];
    const calls = await Promise.all(requests);
    const snap = await this.snapshot(client.id);

    const accepted = calls.filter(c => c.status === 201);
    const refused = calls.filter(c => c.status === 409 && c.detail === 'Saldo insuficiente.');
    const unexpected = calls.filter(c => !accepted.includes(c) && !refused.includes(c));
    const acceptedIds = accepted.map(c => c.operation?.id).filter((id): id is string => !!id);

    const persistedIds = snap.operations.map(o => o.id);
    const settled = snap.operations.filter(o => o.status !== OperationStatus.Pending);
    const completed = snap.operations.filter(o => o.status === OperationStatus.Completed).length;
    const expectedBalance = completed > 0 ? 20 - PRICE : 20;

    const checks: Check[] = [
      {
        label: 'Nenhuma requisição recebeu erro inesperado',
        ok: unexpected.length === 0,
        detail: unexpected.length === 0
          ? 'apenas 201 e 409 "Saldo insuficiente."'
          : unexpected.map(c => `${c.label}: HTTP ${c.status} ${c.detail}`).join(' | ')
      },
      {
        label: 'Somente uma operação foi aceita',
        ok: accepted.length === 1,
        detail: `${accepted.length} aceita(s) (201), ${refused.length} recusada(s) por saldo insuficiente`
      },
      {
        label: 'A outra requisição foi recusada por saldo insuficiente',
        ok: refused.length === 1,
        detail: `${refused.length} resposta(s) 409 "Saldo insuficiente."`
      },
      {
        label: 'Somente a operação aceita foi persistida',
        ok: persistedIds.length === acceptedIds.length
          && acceptedIds.every(id => persistedIds.includes(id)),
        detail: `${persistedIds.length} operação(ões) persistida(s)`
      },
      {
        label: 'Não existem dois débitos',
        ok: snap.debits === 1 && this.sameMoney(this.debitTotal(snap), PRICE),
        detail: `${snap.debits} débito(s) de ${this.brl(this.debitTotal(snap))}`
      },
      {
        label: 'A operação aceita terminou em estado final',
        ok: settled.length === snap.operations.length,
        detail: snap.operations.map(o => this.statusName(o.status)).join(', ') || 'nenhuma'
      },
      {
        label: 'O saldo não ficou negativo',
        ok: snap.balance >= 0,
        detail: this.brl(snap.balance)
      },
      this.balanceCheck(
        completed > 0
          ? 'Saldo final de R$ 7,50 (uma cobrança mantida)'
          : 'Saldo final de R$ 20,00 (cobrança seguida de estorno)',
        snap.balance,
        expectedBalance
      ),
      this.consistencyCheck(snap)
    ];

    const result = this.buildResult(checks, calls, 20, snap, {
      passed: completed > 0
        ? 'Uma operação foi cobrada e concluída (saldo R$ 7,50); a outra foi recusada por saldo insuficiente, sem nenhuma cobrança.'
        : 'Uma operação foi cobrada, o fornecedor falhou e o valor foi estornado (saldo R$ 20,00); a outra foi recusada por saldo insuficiente, sem nenhuma cobrança.',
      failed: 'Alguma verificação falhou; veja os itens marcados abaixo.'
    });

    // Duas operações aceitas só são possíveis se o estorno da primeira ocorreu
    // antes do débito da segunda: situação válida, mas que não exercita a disputa.
    if (result.verdict === 'failed' && accepted.length === 2 && unexpected.length === 0) {
      result.verdict = 'inconclusive';
      result.explanation =
        'As duas requisições foram aceitas: a primeira falhou no fornecedor e foi estornada antes da segunda reservar o saldo, então a disputa não ocorreu. Execute o teste novamente.';
    }

    return result;
  }

  // ---------- Verificações e utilitários ----------

  private balanceCheck(label: string, actual: number, expected: number): Check {
    return {
      label,
      ok: this.sameMoney(actual, expected),
      detail: `saldo persistido: ${this.brl(actual)}`
    };
  }

  /** Cada operação persistida deve ter exatamente um débito e, se Failed, exatamente um estorno. */
  private consistencyCheck(snap: Snapshot): Check {
    const problems: string[] = [];

    for (const op of snap.operations) {
      const debits = snap.items.filter(i => i.operationId === op.id && i.transactionType === TransactionType.Debit);
      const refunds = snap.items.filter(i => i.operationId === op.id && i.transactionType === TransactionType.Refund);
      const short = op.id.substring(0, 8);

      if (debits.length !== 1 || !this.sameMoney(debits[0].amount, op.amount))
        problems.push(`${short}: ${debits.length} débito(s)`);

      if (op.status === OperationStatus.Failed && refunds.length !== 1)
        problems.push(`${short}: Failed com ${refunds.length} estorno(s)`);

      if (op.status !== OperationStatus.Failed && refunds.length !== 0)
        problems.push(`${short}: ${this.statusName(op.status)} com estorno`);

      if (op.status === OperationStatus.Pending)
        problems.push(`${short}: ainda Pending`);
    }

    return {
      label: 'Registros financeiros completos (débito por operação; estorno só quando Failed)',
      ok: problems.length === 0,
      detail: problems.length === 0
        ? `${snap.operations.length} operação(ões) verificada(s)`
        : problems.join('; ')
    };
  }

  private buildResult(
    checks: Check[],
    calls: ApiCall[],
    initialBalance: number,
    snap: Snapshot,
    explanations: { passed: string; failed: string }
  ): ScenarioResult {
    const passed = checks.every(c => c.ok);

    return {
      verdict: passed ? 'passed' : 'failed',
      explanation: passed ? explanations.passed : explanations.failed,
      checks,
      calls,
      operationIds: snap.operations.map(o => o.id),
      initialBalance,
      finalBalance: snap.balance,
      operationCount: snap.operations.length,
      debitCount: snap.debits,
      refundCount: snap.refunds
    };
  }

  private emptyResult(verdict: Verdict, explanation: string): ScenarioResult {
    return {
      verdict,
      explanation,
      checks: [],
      calls: [],
      operationIds: [],
      initialBalance: null,
      finalBalance: null,
      operationCount: null,
      debitCount: null,
      refundCount: null
    };
  }

  private createClient(name: string, balance: number) {
    return firstValueFrom(this.clients.create({
      name: `${name} (${new Date().toLocaleTimeString('pt-BR')})`,
      initialBalance: balance
    }));
  }

  private async postOperation(
    label: string,
    clientId: string,
    idempotencyKey: string,
    headers?: Record<string, string>
  ): Promise<ApiCall> {
    try {
      const response = await firstValueFrom(this.operations.createWithResponse({
        clientId,
        idempotencyKey,
        type: OperationType.VehicleConsultation
      }, headers));

      return { label, status: response.status, operation: response.body, detail: '' };
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        return {
          label,
          status: error.status,
          operation: null,
          detail: error.status === 0
            ? 'sem conexão com a API'
            : error.error?.detail || error.error?.title || error.statusText
        };
      }
      throw error;
    }
  }

  /** Reconsulta cliente, extrato completo e cada operação diretamente na API. */
  private async snapshot(clientId: string): Promise<Snapshot> {
    const client = await firstValueFrom(this.clients.getById(clientId));

    const startDate = this.toDateInput(new Date(Date.now() - 86_400_000));
    const endDate = this.toDateInput(new Date(Date.now() + 86_400_000));

    const items: StatementItem[] = [];
    let total = 0;
    let page = 1;

    do {
      const response = await firstValueFrom(
        this.statements.getByPeriod(clientId, startDate, endDate, page, 100));
      items.push(...response.items);
      total = response.totalItems;
      page++;
    } while (items.length < total);

    const ids = [...new Set(items.map(i => i.operationId))];
    const operations = await Promise.all(
      ids.map(id => firstValueFrom(this.operations.getById(id))));

    return {
      balance: client.balance,
      operations,
      items,
      debits: items.filter(i => i.transactionType === TransactionType.Debit).length,
      refunds: items.filter(i => i.transactionType === TransactionType.Refund).length
    };
  }

  private async isForcedFailureAvailable(): Promise<boolean> {
    try {
      const response = await firstValueFrom(
        this.http.get<{ available: boolean }>('/api/dev/provider-failure'));
      return response?.available === true;
    } catch {
      return false;
    }
  }

  private debitTotal(snap: Snapshot): number {
    return snap.items
      .filter(i => i.transactionType === TransactionType.Debit)
      .reduce((sum, i) => sum + i.amount, 0);
  }

  private sameMoney(a: number, b: number): boolean {
    return Math.round(a * 100) === Math.round(b * 100);
  }

  private brl(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  private statusName(status: OperationStatus | undefined): string {
    switch (status) {
      case OperationStatus.Completed: return 'Completed';
      case OperationStatus.Failed: return 'Failed';
      case OperationStatus.Pending: return 'Pending';
      default: return 'desconhecido';
    }
  }

  private toDateInput(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.status === 0
        ? 'não foi possível conectar à API.'
        : `HTTP ${error.status} - ${error.error?.detail || error.error?.title || error.statusText}`;
    }
    return error instanceof Error ? error.message : String(error);
  }
}
