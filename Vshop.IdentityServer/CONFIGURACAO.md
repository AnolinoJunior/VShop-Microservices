# Configuração de credenciais

Não armazene valores de credenciais nos arquivos do projeto. Em Development, use `dotnet user-secrets` para cada projeto; em produção, forneça as configurações por variáveis de ambiente ou um gerenciador de segredos. Nas variáveis de ambiente, substitua `:` por `__`.

| Projeto | Chave | Uso |
| --- | --- | --- |
| IdentityServer | `OAuth:WebClientSecret` | Segredo do cliente OpenID Connect `Vshop.Web`. |
| IdentityServer | `OAuth:ServiceClientSecret` | Segredo independente do cliente `client` para client credentials. |
| VShop.Web | `Client:Secret` | Mesmo valor de `OAuth:WebClientSecret` do IdentityServer. |
| IdentityServer | `DevelopmentSeed:AdminPassword` | Senha opcional para criar a conta administrativa de desenvolvimento. |
| IdentityServer | `DevelopmentSeed:ClientPassword` | Senha opcional para criar a conta de cliente de desenvolvimento. |
| IdentityServer e OrderApi | `VSHOP_SAGA_CLIENT_SECRET` | Mesmo segredo da identidade de serviço da Saga, com pelo menos 32 caracteres. |

As duas credenciais OAuth são obrigatórias para iniciar o IdentityServer. A credencial da Web também é obrigatória para iniciar a Web. Os valores dos clientes OAuth devem ser independentes, gerados de forma aleatória e fornecidos a cada ambiente por seu próprio gerenciador de segredos.

O seed de usuários é executado somente em Development. Se uma das senhas opcionais não estiver configurada, a conta correspondente não é criada; uma senha configurada precisa atender à política do ASP.NET Identity. O seed não altera a senha de uma conta existente. Contas e credenciais anteriores precisam ser revisadas e rotacionadas separadamente.

Para configurar uma chave em Development, execute `dotnet user-secrets set` informando a chave, o valor pelo seu fluxo seguro e o projeto correspondente. Configure o mesmo segredo da Web nos dois projetos indicados. Evite incluir valores reais em scripts, documentação ou histórico de comandos compartilhado.

Os testes E2E geram segredos e senhas temporários independentes para seus próprios processos e bancos. O arquivo `VShop.E2E.Tests/README.md` descreve como executá-los.
