# Testes de integração das reservas

Execute em uma sessão Windows que consiga autenticar no SQL Server por autenticação integrada e cujo usuário tenha permissão para criar e excluir bancos. Em ambientes que restringem o contexto SSPI, execute o comando fora desse isolamento; não é necessário alterar a configuração global do SQL Server ou do Windows.

```powershell
$env:VSHOP_RESERVATION_TEST_SQL = 'Server=localhost;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project VShop.Reservations.Tests
```

O exemplo usa `TrustServerCertificate=True` apenas nessa conexão local, como nas configurações de desenvolvimento existentes. Para uma instância com certificado confiável e nome válido, forneça uma connection string com validação de certificado habilitada. A suíte usa a conexão fornecida para criar dois bancos com nomes aleatórios, aplica suas migrations e remove somente os bancos criados pela própria execução. Não usa os bancos da aplicação.

Cada cenário informa PASSOU ou FALHOU; o processo retorna código diferente de zero se houver falhas. A criação e a remoção dos bancos exigem permissões de SQL Server apropriadas.
