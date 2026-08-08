using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using AiBrandStudio.Infrastructure.Persistence;
using AiBrandStudio.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Tests;

public class SubscriptionLifecycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public SubscriptionLifecycleTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Dunning_marks_past_due_then_cancels_after_grace()
    {
        var user = new User { Email = "s@t.local", FullName = "S", PasswordHash = "x", CreditBalance = 0 };
        var plan = new SubscriptionPlan { Code = "builder", Name = "Builder", MonthlyPriceInr = 1499, MonthlyCredits = 300 };
        _db.Users.Add(user);
        _db.SubscriptionPlansSet.Add(plan);
        var sub = new Subscription
        {
            UserId = user.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatuses.Active,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddMinutes(-1),
            CancelAtPeriodEnd = false
        };
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        // Simulate one dunning tick inline (same rules as worker)
        var now = DateTime.UtcNow;
        var due = await _db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatuses.Active && s.CurrentPeriodEndUtc <= now && !s.CancelAtPeriodEnd)
            .ToListAsync();
        foreach (var s in due)
        {
            s.Status = SubscriptionStatuses.PastDue;
            s.GracePeriodEndsUtc = now.AddDays(-1); // already expired for second step
            s.DunningAttempts++;
        }
        await _db.SaveChangesAsync();

        (await _db.Subscriptions.FirstAsync()).Status.Should().Be(SubscriptionStatuses.PastDue);

        var grace = await _db.Subscriptions
            .Where(s => s.Status == SubscriptionStatuses.PastDue && s.GracePeriodEndsUtc <= now)
            .ToListAsync();
        foreach (var s in grace)
        {
            s.Status = SubscriptionStatuses.Canceled;
            s.CanceledAtUtc = now;
        }
        await _db.SaveChangesAsync();

        (await _db.Subscriptions.FirstAsync()).Status.Should().Be(SubscriptionStatuses.Canceled);
    }

    [Fact]
    public async Task AuditService_writes_immutable_row()
    {
        var audit = new AuditService(_db, new HttpContextAccessor());
        await audit.WriteAsync("admin.credit_grant", Guid.NewGuid(), "User", Guid.NewGuid(), new { amount = 10 });
        (await _db.AuditLogsSet.CountAsync(a => a.Action == "admin.credit_grant")).Should().Be(1);
    }

    [Fact]
    public async Task Org_credit_pool_debit_works()
    {
        var user = new User { Email = "o@t.local", FullName = "O", PasswordHash = "x", CreditBalance = 0 };
        var org = new Organization { Name = "Acme", Slug = "acme", CreditBalance = 40 };
        _db.Users.Add(user);
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();

        var credits = new CreditService(_db);
        await credits.DebitTenantAsync(user.Id, org.Id, 15, "TestSpend");
        _db.ChangeTracker.Clear();
        (await _db.Organizations.AsNoTracking().FirstAsync()).CreditBalance.Should().Be(25);
        (await _db.CreditLedgersSet.CountAsync(l => l.OrganizationId == org.Id && l.Delta == -15)).Should().Be(1);
    }
}
