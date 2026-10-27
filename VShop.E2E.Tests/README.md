# Teste HTTP entre APIs reais

Execute a partir da raiz do repositório, em uma sessão Windows capaz de usar autenticação integrada no SQL Server. A conta precisa de permissão para criar e remover bancos temporários. O certificado de desenvolvimento HTTPS de localhost deve estar instalado e ser confiável; o teste não altera a configuração de certificados.

```powershell
dotnet restore VShop.E2E.Tests -m:1
dotnet build VShop.E2E.Tests --no-restore -m:1
$env:VSHOP_E2E_SQL = 'Server=localhost;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project VShop.E2E.Tests --no-build
```

O teste inicia IdentityServer, ProductApi, DiscountApi, CartiApi e OrderApi em portas locais temporárias, além de uma instância de IdentityServer em Production para verificar que o seed de usuários de desenvolvimento não é executado. Ele cria seis bancos com nomes aleatórios e cria seus esquemas com `EnsureCreated` sem executar migrations. Segredos OAuth e senhas do seed são gerados em memória para os processos de teste. Ao terminar, encerra os processos e remove os bancos criados. A conexão ao SQL Server acima confia no certificado somente nessa conexão, conforme a configuração de desenvolvimento existente.

O teste usa uma cópia de produto no carrinho com preço diferente do catálogo. Ele valida JWT e escopos, pedido pendente, aprovação, recusa, compensação e recuperação automática após interromper o DiscountApi e reiniciar o OrderApi. Uma credencial de serviço aleatória é fornecida somente aos processos de teste. Não é necessário alterar os bancos de desenvolvimento. Se o processo for encerrado à força, sua limpeza automática pode não ser executada; os bancos de teste têm prefixo `VShopE2E` e sufixo aleatório para identificá-los.

Para verificar os User Secrets já configurados nos projetos IdentityServer e OrderApi, defina também `$env:VSHOP_E2E_USE_USER_SECRETS = '1'` antes de executar. Nesse modo o teste remove a variável `VSHOP_SAGA_CLIENT_SECRET` dos processos de teste para que eles usem os User Secrets em Development.
