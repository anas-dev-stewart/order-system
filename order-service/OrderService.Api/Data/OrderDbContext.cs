using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Models;

namespace OrderService.Api.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        // MassTransit's transactional outbox: messages sent inside the
        // same DbContext.SaveChangesAsync() as an Order insert are written
        // to these tables in the SAME database transaction, then relayed to
        // Azure Service Bus by a background delivery service. This
        // guarantees we never save an order without (eventually) sending
        // its OrderSubmitted event, and never send one for an order that
        // failed to save.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
