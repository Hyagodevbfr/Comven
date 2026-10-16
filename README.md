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

Implementei três testes de integração utilizando o PostgreSQL real, cobrindo débito com saldo suficiente, tentativa de operação sem saldo e estorno.

Os testes utilizam os Services, Repositories e transações reais da aplicação. Apenas o fornecedor foi simulado para garantir resultados previsíveis nos cenários de sucesso e falha.

Ao final do desenvolvimento, a suíte ficou com 57 testes passando, sendo 54 unitários e 3 de integração.

Sei que os testes implementados ainda não cobrem todos os cenários de concorrência. Com mais tempo, ampliaria essa cobertura, principalmente para validar operações simultâneas no PostgreSQL.

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

### Frontend

Para o frontend, utilizei Angular e procurei manter a interface simples, seguindo a proposta apresentada no desafio.

A tela permite cadastrar e consultar clientes, visualizar o saldo disponível, criar operações e consultar o extrato financeiro com filtro por período e paginação.

Optei por não utilizar bibliotecas de componentes, já que a interface é relativamente pequena e os recursos nativos do Angular foram suficientes.

Também mantive os cálculos e as validações financeiras no backend. O frontend apenas envia as solicitações e apresenta os resultados retornados pela API.

### Testes de cenários

Durante os testes, percebi que alguns comportamentos eram difíceis de reproduzir manualmente, principalmente as requisições simultâneas e as falhas do fornecedor.

Por isso, tive a ideia de criar uma página separada no Angular para facilitar a validação desses cenários, permitindo executá-los diretamente pela interface.

Nela, é possível testar saldo insuficiente, falha do fornecedor com estorno, idempotência e concorrência entre operações que disputam o mesmo saldo.

Essa página foi implementada principalmente com auxílio de IA. A ideia de criar a funcionalidade foi minha, assim como a definição dos cenários e dos comportamentos esperados. Passei as instruções para a IA realizar a implementação, acompanhei o desenvolvimento, revisei o código e validei o funcionamento dos testes.

Optei por utilizar IA nessa parte para agilizar o desenvolvimento dentro do prazo do desafio, já que os fluxos principais do sistema estavam implementados e o objetivo dessa página era facilitar a demonstração das regras de negócio.

Também preferi manter essa funcionalidade separada do extrato, para não misturar os testes técnicos com o uso normal do sistema.

## Como executar

### Pré-requisitos

Para executar o projeto, é necessário ter instalado:

- .NET SDK 8
- Node.js
- Docker Desktop
- npm

### Banco de dados

O PostgreSQL está configurado para execução pelo Docker.

Na pasta onde está o arquivo `docker-compose.yml`, execute:

```bash
docker compose up -d
```

O PostgreSQL utiliza a porta `5433` na máquina local, pois a porta `5432` já estava sendo utilizada durante o desenvolvimento.

### Backend

### Configurar a conexão com o banco

Na pasta `backend/Comven.Api`, configure a connection string utilizando os User Secrets do .NET. Dessa forma, a senha do banco fica armazenada localmente, sem precisar adicioná-la ao repositório.

Abra um terminal na pasta backend/Comven.Api para configurar a conexão. Depois, volte à pasta backend antes de executar o comando das migrations.

Primeiro, inicialize os User Secrets, caso ainda não estejam configurados:

```powershell
dotnet user-secrets init
```

Depois, configure a conexão com o PostgreSQL:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=comven;Username=comven;Password=SUA_SENHA"
```

Substitua `SUA_SENHA` pela senha configurada no Docker Compose.

Para verificar se a configuração foi salva:

```powershell
dotnet user-secrets list
```

Com isso, a API poderá utilizar a connection string durante a execução em ambiente de desenvolvimento, sem precisar alterar o `appsettings.json`.

Com a conexão configurada, execute as migrations:

```bash
dotnet ef database update --project Comven.Infrastructure --startup-project Comven.Api
```

Depois, inicie a API:

```bash
cd Comven.Api
dotnet run
```

O Swagger estará disponível no endereço informado pelo terminal da API, acrescentando `/swagger`.

### Frontend

Em outro terminal, entre na pasta do Angular:

```bash
cd frontend/comven-web
npm install
```

Depois, execute:

```bash
npx ng serve --proxy-config proxy.conf.json
```

Acesse `http://localhost:4200`.

O proxy está configurado para encaminhar as chamadas `/api` para o backend local.

### Testes automatizados

Os testes de integração utilizam o PostgreSQL real. Para executá-los, configure a variável de ambiente `COMVEN_TEST_CONNECTION` com os dados do banco.

No PowerShell:

```powershell
$env:COMVEN_TEST_CONNECTION = "Host=localhost;Port=5433;Database=comven;Username=comven;Password=SUA_SENHA"
```

Substitua `SUA_SENHA` pela senha configurada no Docker.

Na pasta `backend`, execute todos os testes:

```powershell
dotnet test
```

Para executar apenas os testes unitários:

```powershell
dotnet test --filter "Category!=Integration"
```

Para executar apenas os testes de integração:

```powershell
dotnet test --filter "Category=Integration"
```

Os testes de integração criam seus próprios registros no PostgreSQL e removem esses dados ao finalizar.