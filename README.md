# 🛒 VShop — E-commerce com Microserviços em .NET

Projeto de estudo e portfólio desenvolvido em **C# e ASP.NET Core**, com o objetivo de explorar a construção de um sistema de e-commerce baseado em **arquitetura de microserviços**, autenticação, persistência de dados e comunicação entre serviços.

O projeto evoluiu de operações CRUD para um fluxo de checkout distribuído, utilizando o **padrão Saga com orquestração** para coordenar operações entre diferentes APIs.

## 🚀 Tecnologias utilizadas

- **C# / .NET 8**
- **ASP.NET Core Web API**
- **ASP.NET Core MVC**
- **Entity Framework Core**
- **SQL Server**
- **Duende IdentityServer**
- **ASP.NET Core Identity**
- **OAuth 2.0 / OpenID Connect**
- **JWT (JSON Web Tokens)**
- **Swagger / OpenAPI**
- **Git e GitHub**

## 🏗️ Arquitetura

A solução é dividida em serviços com responsabilidades específicas:

| Projeto | Responsabilidade |
|---|---|
| `Vshop.IdentityServer` | Autenticação, identidade e emissão de tokens |
| `Vshop.ProductApi` | Gerenciamento de produtos, categorias e reservas de estoque |
| `VShop.CartiApi` | Gerenciamento do carrinho e preparação do pedido |
| `VShop.DiscountApi` | Gerenciamento de cupons e reservas de descontos |
| `VShop.OrderApi` | Gerenciamento de pedidos e orquestração da Saga |
| `VShop.Web` | Interface web desenvolvida com ASP.NET Core MVC |

A solução também contém projetos de testes para validar regras de negócio, reservas e cenários de recuperação do checkout.

## 🔄 Checkout distribuído com Saga

Um dos principais recursos implementados é o **checkout distribuído utilizando o padrão Saga (Orchestration)**.

Em vez de depender de uma única transação envolvendo todos os serviços, a API de pedidos coordena as operações necessárias para concluir uma compra.

### Fluxo conceitual

```text
Cliente
   |
   v
VShop.Web
   |
   v
VShop.CartiApi
   |
   v
VShop.OrderApi (Orquestrador)
   |
   +----> ProductApi: reserva de estoque
   |
   +----> DiscountApi: reserva de cupom
   |
   +----> Simulação de pagamento
   |
   +----> Confirmação do pedido
   |
   v
Checkout concluído
```

Em situações de falha, a Saga utiliza mecanismos de compensação para desfazer reservas quando necessário.

### Funcionalidades da Saga

- Orquestração das etapas do checkout.
- Reserva de estoque.
- Reserva de cupons de desconto.
- Simulação de pagamento.
- Persistência do estado da Saga.
- Operações de compensação.
- Tratamento de falhas e tentativas de recuperação.
- Processamento de recuperação por serviço em segundo plano (*Background Worker*).

Esses mecanismos permitem estudar consistência de dados e tratamento de falhas em sistemas distribuídos.

## 🔐 Autenticação e autorização

O projeto utiliza **Duende IdentityServer** integrado ao **ASP.NET Core Identity**.

A autenticação é baseada em OAuth 2.0 e OpenID Connect, com utilização de tokens JWT para acesso às APIs protegidas.

Credenciais e segredos de desenvolvimento são configurados fora do código-fonte, utilizando mecanismos como **.NET User Secrets**.

## 🗄️ Persistência de dados

A persistência é implementada com **Entity Framework Core**, utilizando:

- Entidades e relacionamentos.
- `DbContext` e `DbSet`.
- Migrations.
- Configuração de modelos.
- Operações assíncronas.
- Persistência de pedidos e estados da Saga.

## 🧪 Testes

A solução contém projetos de testes específicos:

| Projeto | Objetivo |
|---|---|
| `VShop.CartiApi.Tests` | Validar regras do carrinho |
| `VShop.OrderApi.Tests` | Validar operações de pedidos e recuperação da Saga |
| `VShop.Reservations.Tests` | Validar regras relacionadas às reservas |
| `VShop.E2E.Tests` | Validar fluxos integrados do sistema |

Os testes contemplam cenários de regras de negócio, integração e recuperação de operações distribuídas.

## 📁 Estrutura do projeto

```text
VShop-Microservices/
│
├── Vshop.IdentityServer/
├── Vshop.ProductApi/
├── VShop.CartiApi/
├── VShop.DiscountApi/
├── VShop.OrderApi/
├── VShop.Web/
│
├── VShop.CartiApi.Tests/
├── VShop.OrderApi.Tests/
├── VShop.Reservations.Tests/
├── VShop.E2E.Tests/
│
└── VShop-Microservices.slnx
```

## ⚙️ Configuração do ambiente

Para executar o projeto localmente, são necessários:

- SDK do .NET 8.
- SQL Server.
- Visual Studio ou outra IDE compatível.
- Configuração das connection strings.
- Configuração dos segredos de autenticação.

Os serviços utilizam arquivos `appsettings.json`, configurações de desenvolvimento e segredos locais.

**Importante:** não publique senhas, chaves de assinatura ou segredos OAuth no repositório.

As configurações de autenticação do IdentityServer devem ser preparadas antes de executar o fluxo completo da aplicação.

## 📚 Conceitos estudados

Este projeto foi desenvolvido para aprofundar conhecimentos em:

- Arquitetura de microserviços.
- APIs RESTful.
- Separação de responsabilidades.
- Injeção de dependência.
- Autenticação e autorização.
- Comunicação entre serviços.
- Transações distribuídas.
- Padrão Saga.
- Consistência eventual.
- Operações de compensação.
- Tratamento de falhas.
- Testes automatizados.

## 🔮 Próximas evoluções

- [ ] Containerização dos serviços com Docker.
- [ ] Orquestração local com Docker Compose.
- [ ] Comunicação assíncrona utilizando RabbitMQ.
- [ ] Aprimoramento da observabilidade e monitoramento.

## 👨‍💻 Autor

**Anolino Junior**

Desenvolvedor em constante aprendizado, com foco em C#, .NET, APIs REST e arquitetura de sistemas.

GitHub: https://github.com/AnolinoJunior

---

⭐ **Projeto desenvolvido para fins educacionais e de portfólio, com foco na aplicação prática de conceitos de desenvolvimento backend e sistemas distribuídos.**
