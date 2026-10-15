using Microsoft.EntityFrameworkCore;
using ProductService.Api.Data;
using ProductService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProductDb")));

builder.Services.AddScoped<IProductService, ProductCatalogService>();

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
