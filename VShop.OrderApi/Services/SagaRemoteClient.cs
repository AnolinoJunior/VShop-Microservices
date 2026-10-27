using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace VShop.OrderApi.Services;

public sealed class SagaRemoteClient(IHttpClientFactory clients, IConfiguration configuration)
{
    private async Task<string> Token(CancellationToken ct)
    {
        var secret = configuration["VSHOP_SAGA_CLIENT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("Credencial da Saga não configurada.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "connect/token");
        var credentials = Uri.EscapeDataString("Vshop.OrderSaga") + ":" + Uri.EscapeDataString(secret);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["scope"] = "Vshop.OrderSaga.Stock Vshop.OrderSaga.Coupon"
        });
        using var response = await clients.CreateClient("SagaIdentity").SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Token da Saga ausente.");
    }

    public async Task<(HttpStatusCode Status, JsonElement? Body)> Post(string target, string path, object payload, CancellationToken ct)
    {
        var token = await Token(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await clients.CreateClient(target).SendAsync(request, ct);
        JsonElement? body = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
            body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if ((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new HttpRequestException($"{target} indisponível ou não autorizou a Saga ({(int)response.StatusCode}).");
        return (response.StatusCode, body);
    }
}
