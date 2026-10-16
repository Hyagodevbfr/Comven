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

### Datas e valores

Estou utilizando `DateTime.UtcNow` para as datas e `decimal` para os valores monetários.

### Banco de dados

### Banco de dados

Escolhi utilizar PostgreSQL com Entity Framework Core para a persistência dos dados, mantendo os mapeamentos das entidades separados do `DbContext`.

Como o desafio envolve movimentações financeiras, procurei adicionar algumas garantias diretamente no banco, sem depender apenas das validações da aplicação.

Configurei índices únicos para impedir operações com a mesma chave de idempotência para um cliente e evitar mais de um débito ou estorno para a mesma operação.

Também adicionei restrições para não permitir saldos negativos ou movimentações com valores inválidos.

## Como executar

Em desenvolvimento.
