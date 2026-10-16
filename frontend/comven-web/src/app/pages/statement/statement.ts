
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ClientService } from '../../core/services/client.service';
import { OperationService } from '../../core/services/operation.service';
import { StatementService } from '../../core/services/statement.service';
import { Client } from '../../core/models/client.model';
import {
  Operation,
  OperationStatus,
  OperationType
} from '../../core/models/operation.model';
import {
  StatementItem,
  TransactionType
} from '../../core/models/statement.model';

@Component({
  selector: 'app-statement',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './statement.html',
  styleUrl: './statement.scss'
})
export class Statement implements OnInit {
  private readonly clients = inject(ClientService);
  private readonly operations = inject(OperationService);
  private readonly statements = inject(StatementService);

  private readonly cdr = inject(ChangeDetectorRef);

  clientId = '';
  client: Client | null = null;
  items: StatementItem[] = [];

  showClientForm = false;
  creatingClient = false;
  newClientName = '';
  newClientBalance: number | null = 0;
  clientError = '';

  startDate = '';
  endDate = '';
  page = 1;
  pageSize = 20;
  totalItems = 0;

  loading = false;
  loadingStatement = false;
  creatingOperation = false;
  showOperationForm = false;

  error = '';
  operationError = '';
  operationMessage = '';
  operationType = OperationType.VehicleConsultation;

  private pendingIdempotencyKey: string | null = null;
  lastOperation: Operation | null = null;

  readonly operationTypes = [
    { value: OperationType.VehicleConsultation, label: 'Consulta veicular', price: 12.50 },
    { value: OperationType.DocumentIssuance, label: 'Emissão de documento', price: 45.00 },
    { value: OperationType.LicensePlateConsultation, label: 'Consulta de placa', price: 8.90 }
  ];

  ngOnInit(): void {
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);

    this.startDate = this.toDateInput(firstDay);
    this.endDate = this.toDateInput(today);

    const savedId = localStorage.getItem('comven-client-id');
    if (savedId) {
      this.clientId = savedId;
      this.loadClient();
    }
  }

  loadClient(): void {
    const id = this.clientId.trim();

    if (!id) {
      this.error = 'Informe o identificador do cliente.';
      return;
    }

    this.loading = true;
    this.error = '';
    this.client = null;
    this.items = [];

    this.clients.getById(id).subscribe({
      next: client => {
        this.client = client;
        this.loading = false;
        this.page = 1;
        localStorage.setItem('comven-client-id', id);
        this.loadStatement();
        this.cdr.markForCheck();
      },
      error: error => {
        this.loading = false;
        this.error = this.getErrorMessage(error);
        this.cdr.markForCheck();
      }
    });
  }

  loadStatement(): void {
    if (!this.client) return;

    this.loadingStatement = true;
    this.error = '';

    this.statements.getByPeriod(
      this.client.id,
      this.startDate,
      this.endDate,
      this.page,
      this.pageSize
    ).subscribe({
      next: result => {
        this.items = result.items;
        this.totalItems = result.totalItems;
        this.loadingStatement = false;
        this.cdr.markForCheck()
      },
      error: error => {
        this.loadingStatement = false;
        this.error = this.getErrorMessage(error);
        this.cdr.markForCheck()
      }
    });
  }

  filterStatement(): void {
    this.page = 1;
    this.loadStatement();
  }

  changePage(direction: number): void {
    const newPage = this.page + direction;

    if (newPage < 1 || newPage > this.totalPages) return;

    this.page = newPage;
    this.loadStatement();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalItems / this.pageSize));
  }

  openOperationForm(): void {
    this.operationError = '';
    this.operationMessage = '';
    this.lastOperation = null;
    this.operationType = OperationType.VehicleConsultation;
    this.pendingIdempotencyKey = null;
    this.showOperationForm = true;
  }

  closeOperationForm(): void {
    if (this.creatingOperation) return;

    this.showOperationForm = false;
    this.pendingIdempotencyKey = null;
  }

  createOperation(): void {
    if (!this.client || this.creatingOperation) return;

    this.creatingOperation = true;
    this.operationError = '';
    this.operationMessage = '';

    this.pendingIdempotencyKey ??= crypto.randomUUID();

    this.operations.create({
      clientId: this.client.id,
      idempotencyKey: this.pendingIdempotencyKey,
      type: this.operationType
    }).subscribe({
      next: operation => {
        this.creatingOperation = false;
        this.lastOperation = operation;
        this.pendingIdempotencyKey = null;

        this.operationMessage = operation.status === OperationStatus.Completed
          ? 'Operação concluída com sucesso.'
          : operation.status === OperationStatus.Failed
            ? 'O fornecedor não concluiu a operação. O valor foi estornado.'
            : 'Operação registrada e aguardando processamento.';

        this.clients.getById(this.client!.id).subscribe({
          next: client => this.client = client
        });

        this.page = 1;
        this.loadStatement();
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        this.creatingOperation = false;
        this.operationError = this.getErrorMessage(error);
        this.cdr.markForCheck();

        if (error.status !== 0 && error.status !== 502 &&
            error.status !== 503 && error.status !== 504) {
          this.pendingIdempotencyKey = null;
        }
      }
    });
  }

  changeOperationType(value: OperationType): void {
    this.operationType = value;
    this.pendingIdempotencyKey = null;
    this.operationMessage = '';
    this.operationError = '';
    this.lastOperation = null;
  }

  getOperationName(type: OperationType): string {
    return this.operationTypes.find(item => item.value === type)?.label
      ?? 'Operação';
  }

  getStatusName(status: OperationStatus): string {
    switch (status) {
      case OperationStatus.Completed: return 'Concluída';
      case OperationStatus.Failed: return 'Falhou';
      default: return 'Pendente';
    }
  }

  isRefund(type: TransactionType): boolean {
    return type === TransactionType.Refund;
  }

  private toDateInput(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private getErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Não foi possível conectar à API. Verifique se o backend está em execução.';
    }

    return error.error?.detail
      || error.error?.title
      || 'Não foi possível concluir a solicitação.';
  }

  openClientForm(): void {
    this.newClientName = '';
    this.newClientBalance = 0;
    this.clientError = '';
    this.showClientForm = true;
  }

  closeClientForm(): void {
    if (this.creatingClient) return;
    this.showClientForm = false;
  }

  createClient(): void {
    const name = this.newClientName.trim();
    const balance = this.newClientBalance;

    if (!name) {
      this.clientError = 'Informe o nome do cliente.';
      return;
    }

    if (balance === null || !Number.isFinite(balance) || balance < 0) {
      this.clientError = 'Informe um saldo inicial válido.';
      return;
    }

    this.creatingClient = true;
    this.clientError = '';

    this.clients.create({
      name,
      initialBalance: balance
    }).subscribe({
      next: client => {
        this.creatingClient = false;
        this.showClientForm = false;

        this.clientId = client.id;
        this.client = client;
        this.page = 1;

        localStorage.setItem('comven-client-id', client.id);

        this.loadStatement();
        this.cdr.markForCheck();
      },
      error: error => {
        this.creatingClient = false;
        this.clientError = this.getErrorMessage(error);
        this.cdr.markForCheck();
      }
    });
  }
}
