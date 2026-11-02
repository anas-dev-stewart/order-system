namespace OrderService.Worker.Clients;

public record PricingQuoteRequest(int ProductId, decimal BasePrice, int Quantity);

public record PricingQuoteResponse(decimal FinalUnitPrice, decimal FinalTotalPrice);
