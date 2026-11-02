namespace OrderService.Worker.Clients;

public interface IPricingServiceClient
{
    Task<PricingQuoteResponse> GetQuoteAsync(PricingQuoteRequest request, CancellationToken cancellationToken = default);
}
