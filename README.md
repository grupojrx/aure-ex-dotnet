# AureEx

SDK oficial da API AureEX para .NET.

## Instalação

```bash
dotnet add package AureEx
```

## Uso

```csharp
using AureEx;

var aureEx = new AureExClient("YOUR_API_KEY", "YOUR_API_SECRET");

await aureEx.Deposits.CreateAsync(new {
    method = "usdt",
    reference = "order-1",
    amount = 10000
});
await aureEx.Webhooks.ListAsync();
await aureEx.Company.BalanceAsync();
await aureEx.Conversions.QuoteAsync(new { from = "USDT", to = "BRL", amount = 100 });
```

## Mapa de métodos

| SDK | HTTP |
| --- | --- |
| `aureEx.Deposits` | `/v1/deposits` |
| `aureEx.Withdrawals` | `/v1/withdrawals` |
| `aureEx.Webhooks` | `/v1/webhooks` |
| `aureEx.Company.GetAsync` / `BalanceAsync` | `/v1/company`, `/v1/company/balance` |
| `aureEx.Conversions` / `QuoteAsync` | `/v1/conversions`, `/v1/conversions/quote` |

Docs: https://api.aure-ex.com/docs/sdks  
OpenAPI: https://api.aure-ex.com/openapi.yaml
