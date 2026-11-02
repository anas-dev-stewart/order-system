namespace OrderService.Worker.Clients;

/// <summary>
/// A DelegatingHandler that bounds how many requests on this HttpClient can
/// be in flight at once. Demonstrates the "limit concurrent calls when the
/// downstream service is slow or unavailable" requirement: without this, a
/// burst of OrderSubmitted messages (each processed concurrently, see
/// ConcurrentMessageLimit in Program.cs) could fire dozens of simultaneous
/// requests at Product Service and make a slow dependency worse instead of
/// better.
/// </summary>
public class ConcurrencyLimitingHandler : DelegatingHandler
{
    private readonly SemaphoreSlim _semaphore;

    public ConcurrencyLimitingHandler(int maxConcurrentRequests)
    {
        _semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
