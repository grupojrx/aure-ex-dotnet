using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AureEx;

/// <summary>Erro tipado da API AureEX.</summary>
public sealed class AureExException : Exception
{
    public string? Code { get; }
    public object? Details { get; }
    public int StatusCode { get; }

    public AureExException(string message, string? code, object? details, int statusCode)
        : base(message)
    {
        Code = code;
        Details = details;
        StatusCode = statusCode;
    }
}

/// <summary>Facade principal da API AureEX para .NET (crypto only).</summary>
public sealed class AureExClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly int _maxRetries;

    public CrudResource Deposits { get; }
    public CrudResource Withdrawals { get; }
    public CrudResource Webhooks { get; }
    public CompanyResource Company { get; }
    public ConversionsResource Conversions { get; }

    /// <summary>Cria o cliente autenticado com <c>X-Api-Key</c> / <c>X-Api-Secret</c>.</summary>
    public AureExClient(string apiKey, string apiSecret, string? baseUrl = null, int maxRetries = 2, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new AureExException("apiKey and apiSecret are required.", null, null, 0);
        }

        _baseUrl = (baseUrl ?? "https://api.aure-ex.com/v1").TrimEnd('/');
        _maxRetries = maxRetries;
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.Remove("X-Api-Key");
        _http.DefaultRequestHeaders.Remove("X-Api-Secret");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey.Trim());
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Secret", apiSecret.Trim());
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        Deposits = new CrudResource(this, "/deposits");
        Withdrawals = new CrudResource(this, "/withdrawals");
        Webhooks = new CrudResource(this, "/webhooks");
        Company = new CompanyResource(this);
        Conversions = new ConversionsResource(this);
    }

    /// <summary>Executa requisição autenticada e desempacota o envelope <c>data</c>.</summary>
    internal async Task<JsonElement?> RequestAsync(HttpMethod method, string path, object? body = null, string? idempotencyKey = null, CancellationToken ct = default)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;
            using var request = new HttpRequestMessage(method, $"{_baseUrl}/{path.TrimStart('/')}");

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }

            if (body != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if ((int)response.StatusCode == 429 && attempt <= _maxRetries + 1)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 1;
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, retryAfter)), ct).ConfigureAwait(false);
                continue;
            }

            using var doc = string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text);

            if (!response.IsSuccessStatusCode)
            {
                string message = "Request failed.";
                string? code = null;
                object? details = null;

                if (doc?.RootElement.TryGetProperty("error", out var error) == true)
                {
                    if (error.TryGetProperty("message", out var msg))
                    {
                        message = msg.GetString() ?? message;
                    }

                    if (error.TryGetProperty("code", out var codeEl))
                    {
                        code = codeEl.GetString();
                    }

                    if (error.TryGetProperty("details", out var detailsEl))
                    {
                        details = detailsEl.Clone();
                    }
                }

                throw new AureExException(message, code, details, (int)response.StatusCode);
            }

            if (doc?.RootElement.TryGetProperty("data", out var data) == true)
            {
                return data.Clone();
            }

            return doc?.RootElement.Clone();
        }
    }
}

/// <summary>Recurso CRUD genérico (list/create/get/update/delete).</summary>
public sealed class CrudResource
{
    private readonly AureExClient _client;
    private readonly string _basePath;

    internal CrudResource(AureExClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    /// <summary>Lista recursos (GET).</summary>
    public Task<JsonElement?> ListAsync(CancellationToken ct = default) =>
        _client.RequestAsync(HttpMethod.Get, _basePath, ct: ct);

    /// <summary>Cria recurso (POST); opcional <c>Idempotency-Key</c>.</summary>
    public Task<JsonElement?> CreateAsync(object payload, string? idempotencyKey = null, CancellationToken ct = default) =>
        _client.RequestAsync(HttpMethod.Post, _basePath, payload, idempotencyKey, ct);

    /// <summary>Consulta por ID (GET).</summary>
    public Task<JsonElement?> GetAsync(string id, CancellationToken ct = default) =>
        _client.RequestAsync(HttpMethod.Get, $"{_basePath}/{Uri.EscapeDataString(id)}", ct: ct);

    /// <summary>Atualiza por ID (PUT).</summary>
    public Task<JsonElement?> UpdateAsync(string id, object payload, CancellationToken ct = default) =>
        _client.RequestAsync(HttpMethod.Put, $"{_basePath}/{Uri.EscapeDataString(id)}", payload, ct: ct);

    /// <summary>Remove por ID (DELETE).</summary>
    public Task<JsonElement?> DeleteAsync(string id, CancellationToken ct = default) =>
        _client.RequestAsync(HttpMethod.Delete, $"{_basePath}/{Uri.EscapeDataString(id)}", ct: ct);
}

/// <summary>Empresa autenticada e saldo.</summary>
public sealed class CompanyResource
{
    private readonly AureExClient _client;

    internal CompanyResource(AureExClient client) => _client = client;

    /// <summary>Dados da empresa (GET /company).</summary>
    public Task<JsonElement?> GetAsync(CancellationToken ct = default) =>
        _client.RequestAsync(HttpMethod.Get, "/company", ct: ct);

    /// <summary>Saldo disponível (GET /company/balance).</summary>
    public Task<JsonElement?> BalanceAsync(CancellationToken ct = default) =>
        _client.RequestAsync(HttpMethod.Get, "/company/balance", ct: ct);
}

/// <summary>Conversões crypto (ex.: USDT/BRL).</summary>
public sealed class ConversionsResource
{
    private readonly AureExClient _client;
    private readonly CrudResource _crud;

    internal ConversionsResource(AureExClient client)
    {
        _client = client;
        _crud = new CrudResource(client, "/conversions");
    }

    public Task<JsonElement?> ListAsync(CancellationToken ct = default) => _crud.ListAsync(ct);

    public Task<JsonElement?> CreateAsync(object payload, string? idempotencyKey = null, CancellationToken ct = default) =>
        _crud.CreateAsync(payload, idempotencyKey, ct);

    public Task<JsonElement?> GetAsync(string id, CancellationToken ct = default) => _crud.GetAsync(id, ct);

    /// <summary>Cotação de conversão (POST /conversions/quote).</summary>
    public Task<JsonElement?> QuoteAsync(object payload, CancellationToken ct = default) =>
        _client.RequestAsync(HttpMethod.Post, "/conversions/quote", payload, ct: ct);
}
