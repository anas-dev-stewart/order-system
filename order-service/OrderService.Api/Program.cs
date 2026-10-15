using Microsoft.EntityFrameworkCore;
using OrderService.Api.Clients;
using OrderService.Api.Data;
using OrderService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrderDb")));

builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    var baseUrl = builder.Configuration["ProductService:BaseUrl"]
        ?? throw new InvalidOperationException("ProductService:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<IOrderService, OrderProcessingService>();

const string AngularClient = "AngularClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClient, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(AngularClient);
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
