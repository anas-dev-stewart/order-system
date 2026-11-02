using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Worker.Data;

/// <summary>
/// The worker's own lightweight database (SQLite, one file). It does NOT
/// store orders — Order Service still owns that. It only tracks which
/// MassTransit MessageIds have already been fully processed, so a
/// redelivered OrderSubmitted (Azure Service Bus is "at least once", not
/// "exactly once") can be detected and skipped before it does anything
/// like reducing stock a second time. This is the "inbox" half of the
/// outbox/inbox pattern — Order Service has the outbox, the Worker has
/// the inbox.
/// </summary>
public class WorkerDbContext : DbContext
{
    public WorkerDbContext(DbContextOptions<WorkerDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
}

public class ProcessedMessage
{
    [Key]
    public Guid MessageId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}
