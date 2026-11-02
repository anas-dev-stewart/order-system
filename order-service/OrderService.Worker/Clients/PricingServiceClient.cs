using System.Net.Http.Json;

namespace OrderService.Worker.Clients;

/// <summary>
/// Typed HttpClient for the simulated Pricing Service. Registered in
/// Program.cs via AddHttpClient&lt;IPricingServiceClient, PricingServiceClient&gt;
/// (one named HttpClient, reused and pooled by IHttpClientFactory — never
/// `new HttpClient()` per call, which would exhaust sockets under load).
/// Timeout + retry + circuit breaker come from AddStandardResilienceHandler
/// in Program.cs, not from code here.
/// </summary>
public class PricingServiceClient : IPricingServiceClient
{
    private readonly HttpClient _httpClient;

    public PricingServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PricingQuoteResponse> GetQuoteAsync(PricingQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/pricing/quote", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PricingQuoteResponse>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Pricing Service returned an empty response.");
    }
}
