# Checkout da Saga

Antes de iniciar as APIs, configure `VSHOP_SAGA_CLIENT_SECRET` com o mesmo segredo no IdentityServer e na OrderApi, por User Secrets em Development ou por variável de ambiente. Use um segredo aleatório de pelo menos 32 caracteres, mantido no gerenciador de segredos do ambiente. Variáveis de ambiente têm precedência sobre User Secrets na configuração padrão. Quando não configurado, o IdentityServer não registra o cliente da Saga e os pedidos em andamento aguardam recuperação. Reinicie ambos os processos ao configurar o segredo.

Gere um pedido pendente pela CartiApi (`POST /api/cart/orders`). Depois inicie a Saga com o JWT do mesmo usuário:

```http
POST /api/cart/orders/{orderId}/checkout
Content-Type: application/json
Authorization: Bearer <token>

{ "simulateDecline": false }
```

Use `true` para testar recusa. A decisão é persistida e uma repetição com outra decisão retorna conflito. `202` indica trabalho pendente ou reconciliação; `200` indica Saga concluída ou compensada. O resultado inclui `phase`, `orderStatus`, `paymentStatus` e `lastError`. Repetir a chamada consulta e tenta avançar a mesma Saga.

Os IDs de reserva são derivados do pedido e dos detalhes e gravados antes das chamadas remotas. A decisão de confirmação é salva depois de todas as reservas serem preparadas. Reservas preparadas não expiram: o trabalhador retoma confirmação ou compensação a cada 10 segundos usando credenciais de serviço. Uma inconsistência persistente entra em `Reconciliation`, sem marcar o pedido como falho; o trabalhador continua tentando. Uma interrupção prolongada ou credencial inválida pode manter recursos bloqueados até que o serviço volte e exige monitoramento de `phase` e `lastError`.

As migrations geradas devem ser revisadas e aplicadas manualmente antes de iniciar a nova versão. O teste de ponta a ponta usa bancos temporários e está descrito em `VShop.E2E.Tests/README.md`.
