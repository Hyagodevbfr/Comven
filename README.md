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

### Testes

Optei por priorizar os testes unitários do domínio e dos serviços, validando as regras de negócio e os principais cenários do fluxo de operações.

Os testes dos serviços utilizam mocks para simular os repositórios e o fornecedor, sem depender de uma conexão com o banco.

Não implementei testes automatizados de integração com PostgreSQL neste momento. Considerando o prazo disponível, preferi concentrar o esforço na implementação das funcionalidades solicitadas.

Sei que os testes unitários não garantem, por si só, o comportamento das transações e da concorrência no banco. Com mais tempo, daria prioridade a esses cenários, principalmente para validar cobranças e estornos simultâneos.

Utilizei IA para auxiliar na escrita dos testes, mas revisei o código gerado, acompanhei os cenários testados e validei a execução para garantir que estavam de acordo com as regras implementadas.

### Cadastro de clientes

Para simplificar o cadastro, optei por permitir que o saldo inicial seja informado na criação do cliente, já que o desafio não especifica como esse saldo seria disponibilizado.

Por enquanto, implementei apenas o cadastro e a consulta de clientes. Não adicionei funcionalidades de edição, exclusão ou recarga de saldo, pois não fazem parte do fluxo principal solicitado.

Também mantive as validações de negócio no Domain e utilizei DTOs para separar as informações recebidas e retornadas pela API.

### API de operações

Implementei endpoints para criar operações e consultar seu resultado pelo identificador.

A criação utiliza o OperationService, onde já estão concentradas as validações, a cobrança do saldo e a execução do fornecedor simulado. Dessa forma, o Controller fica responsável apenas por receber a requisição e retornar a resposta HTTP, sem duplicar regras de negócio.

Também disponibilizei a consulta de uma operação pelo ID. Embora não seja uma exigência direta do desafio, considerei útil para consultar o resultado de uma operação já realizada ou verificar uma operação que permaneceu pendente.

Para as respostas, utilizei DTOs em vez de retornar diretamente as entidades do domínio. Isso evita expor informações internas e mantém o contrato da API separado das regras de negócio.

Utilizei ProblemDetails para padronizar os erros retornados pela API, facilitando o tratamento e a exibição dessas mensagens no frontend.

### Idempotência

A idempotência é controlada pela chave enviada na criação da operação, associada ao cliente. A intenção é impedir que uma mesma solicitação gere duas cobranças, mesmo quando o usuário repete o envio por um clique duplo ou por não ter recebido a primeira resposta.

Validei manualmente esse comportamento pelo Swagger. Ao enviar novamente a mesma requisição, a API retornou a operação já criada, com o mesmo identificador, e não realizou outro débito no banco.

Esse teste confirmou o comportamento para requisições repetidas sequencialmente. A proteção contra concorrência também foi considerada na implementação por meio das restrições do banco e das transações, mas não realizei um teste de carga ou de requisições simultâneas.

### Extrato financeiro

Implementei a consulta do extrato com filtro por período e paginação.

Decidi consultar diretamente as movimentações financeiras, e não apenas as operações, porque uma operação pode produzir mais de um lançamento. Quando ocorre uma falha do fornecedor, por exemplo, precisamos manter tanto o débito original quanto o estorno. Isso permite visualizar o histórico financeiro sem perder informações sobre o que aconteceu.

A consulta relaciona as movimentações com suas operações para recuperar o tipo e o status correspondente. Fiz isso utilizando um JOIN no Entity Framework Core, sem adicionar novos relacionamentos ou propriedades às entidades.

Também optei por realizar os filtros, a ordenação e a paginação diretamente no PostgreSQL. O desafio menciona clientes com milhares de operações por mês, então não faria sentido carregar todo o histórico na aplicação para depois selecionar apenas os registros necessários.

A paginação retorna 20 registros por padrão, com limite de 100 por requisição. Os lançamentos são ordenados dos mais recentes para os mais antigos, utilizando o identificador como segundo critério para manter uma ordenação estável quando duas movimentações possuem o mesmo horário.

## Como executar

Em desenvolvimento.