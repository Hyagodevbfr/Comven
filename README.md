# Comven

Projeto desenvolvido para o desafio técnico da Comven, que consiste em criar um sistema de operações veiculares com saldo pré-pago e consulta de extrato.

## Tecnologias

- .NET 8
- Angular
- PostgreSQL
- Entity Framework Core

## Organização do projeto

Optei por utilizar uma arquitetura em camadas, principalmente por ser a abordagem com a qual tenho mais familiaridade e por conseguir desenvolver com mais agilidade dentro do prazo do desafio.

Acredito que uma arquitetura hexagonal também se encaixaria muito bem nesse cenário. Pelo contexto apresentado no documento, a Comven parece trabalhar com diferentes integrações e fornecedores. Nesse caso, a arquitetura hexagonal, junto com o padrão Strategy, poderia facilitar a implementação de novos fornecedores sem impactar diretamente as regras de negócio.

Porém, considerando o tempo disponível e minha menor experiência prática com essa arquitetura, preferi seguir com a arquitetura em camadas, mantendo as responsabilidades separadas e evitando complexidade desnecessária.

O backend foi dividido em:

- **Api:** controllers e configuração da aplicação.
- **Application:** serviços, DTOs e interfaces.
- **Domain:** entidades e regras de negócio.
- **Infrastructure:** persistência e integrações.
- **Tests:** testes unitários e de integração.

## Decisões tomadas

### Domínio

Criei três entidades principais: `Client`, `Operation` e `FinancialTransaction`.

Separei as movimentações financeiras das operações para manter o registro individual dos débitos e estornos.

### Validações

Optei por utilizar um `DomainResult` para tratar erros esperados de negócio sem precisar lançar exceções a cada validação.

### Operações

Defini três status para as operações: `Pending`, `Completed` e `Failed`. Também adicionei uma chave de idempotência para identificar requisições repetidas.

Os valores das operações são definidos no backend, com base no tipo selecionado, evitando que o cliente informe livremente o valor da cobrança.

### Datas e valores

Estou utilizando `DateTime.UtcNow` para as datas e `decimal` para os valores monetários.

### Banco de dados

Escolhi utilizar PostgreSQL com Entity Framework Core para a persistência dos dados, mantendo os mapeamentos das entidades separados do `DbContext`.

Como o desafio envolve movimentações financeiras, procurei adicionar algumas garantias diretamente no banco, sem depender apenas das validações da aplicação.

Configurei índices únicos para impedir operações com a mesma chave de idempotência para um cliente e evitar mais de um débito ou estorno para a mesma operação.

Também adicionei restrições para não permitir saldos negativos ou movimentações com valores inválidos.

### Fluxo financeiro

Para a cobrança, utilizei transações para garantir que o débito do saldo, a criação da operação e o registro da movimentação sejam gravados juntos. Se alguma gravação falhar, a transação é revertida.

Também utilizei atualizações condicionais diretamente no banco para evitar problemas quando duas requisições tentam utilizar o saldo do mesmo cliente ao mesmo tempo.

O fornecedor foi implementado de forma simulada, com aproximadamente 20% de chance de falha, conforme sugerido no desafio.

Optei por executar o fornecedor depois de confirmar a cobrança, para não manter uma transação aberta no banco durante o processamento.

Quando o fornecedor retorna uma falha, uma nova transação é responsável por atualizar o status da operação, devolver o saldo e registrar o estorno.

### Limitação conhecida

Um ponto que deixei de fora por enquanto foi a recuperação automática de operações pendentes.

Como a cobrança e a execução do fornecedor acontecem em etapas diferentes, se a aplicação for interrompida depois do débito, mas antes da finalização, a operação poderá permanecer como `Pending`.

O mesmo pode acontecer se não for possível determinar o resultado da execução do fornecedor.

Com mais tempo, implementaria um processamento em segundo plano para identificar e conciliar essas operações, tomando cuidado para não executar a mesma operação novamente de forma indevida.

## Como executar

Em desenvolvimento.