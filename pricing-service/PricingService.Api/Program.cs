var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Simulated external Pricing Service: stands in for a real third-party or
// internal pricing/customer API that the Order Worker depends on. On
// purpose it is NOT perfectly reliable — a bit of latency and an
// occasional 503 — so the worker's timeout/retry/circuit-breaker handling
// (AddStandardResilienceHandler in OrderService.Worker/Program.cs) has
// something real to react to instead of always succeeding instantly.
app.MapPost("/api/pricing/quote", async (PricingQuoteRequest request) =>
{
    // 150-600ms simulated latency, as if this were a real network hop.
    await Task.Delay(Random.Shared.Next(150, 600));

    // ~1 in 6 requests simulates a downstream failure.
    if (Random.Shared.Next(0, 6) == 0)
    {
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            detail: "Pricing Service is temporarily unavailable.");
    }

    // Final price = base price with a small simulated discount for larger
    // quantities (5%+ off at 10 units or more) — just enough logic to make
    // "final price can differ from the product's list price" visible.
    var discount = request.Quantity >= 10 ? 0.95m : 1m;
    var finalUnitPrice = Math.Round(request.BasePrice * discount, 2);
    var finalTotalPrice = Math.Round(finalUnitPrice * request.Quantity, 2);

    return Results.Ok(new PricingQuoteResponse(finalUnitPrice, finalTotalPrice));
});

app.Run();

public record PricingQuoteRequest(int ProductId, decimal BasePrice, int Quantity);

public record PricingQuoteResponse(decimal FinalUnitPrice, decimal FinalTotalPrice);

public partial class Program { }
