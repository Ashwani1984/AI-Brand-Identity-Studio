using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using AiBrandStudio.Infrastructure.Persistence;
using AiBrandStudio.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiBrandStudio.Tests;

file sealed class StubHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}

file sealed class StubHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}

public class RazorpayWebhookTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly CreditService _credits;
    private readonly AuditService _audit;
    private readonly RazorpayPaymentGateway _gateway;

    public RazorpayWebhookTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _credits = new CreditService(_db);
        _audit = new AuditService(_db, new StubHttpContextAccessor());

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Razorpay:KeyId"] = "rzp_test_key",
            ["Razorpay:KeySecret"] = "test_secret",
            ["Razorpay:WebhookSecret"] = "whsec_test"
        }).Build();

        _gateway = new RazorpayPaymentGateway(_db, config, new StubHttpClientFactory(), _credits, _audit);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    [Fact]
    public async Task Webhook_rejects_bad_signature()
    {
        var act = () => _gateway.HandleWebhookAsync("{}", "bad", isDevelopment: true);
        await act.Should().ThrowAsync<PaymentSignatureException>();
    }

    [Fact]
    public async Task Payment_captured_grants_credits_once()
    {
        var user = new User
        {
            Email = "pay@test.local",
            FullName = "Pay",
            PasswordHash = "x",
            CreditBalance = 10
        };
        _db.Users.Add(user);
        var plan = new SubscriptionPlan
        {
            Code = "builder",
            Name = "Builder",
            MonthlyPriceInr = 1499,
            MonthlyCredits = 300,
            IsActive = true
        };
        _db.SubscriptionPlansSet.Add(plan);
        var payment = new Payment
        {
            UserId = user.Id,
            ProviderOrderId = "order_abc",
            PlanCode = "builder",
            Amount = 1499,
            Status = "Created"
        };
        _db.PaymentsSet.Add(payment);
        await _db.SaveChangesAsync();

        var body = """
            {"id":"evt_1","event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","order_id":"order_abc"}}}}
            """;
        var sig = Sign(body, "whsec_test");

        await _gateway.HandleWebhookAsync(body, sig, isDevelopment: false);
        await _gateway.HandleWebhookAsync(body, sig, isDevelopment: false);

        var refreshed = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        refreshed.CreditBalance.Should().Be(310);
        (await _db.PaymentsSet.AsNoTracking().FirstAsync(p => p.Id == payment.Id)).Status.Should().Be("Paid");
        (await _db.Subscriptions.CountAsync(s => s.UserId == user.Id && s.Status == SubscriptionStatuses.Active)).Should().Be(1);
        (await _db.CreditLedgersSet.CountAsync(l => l.Reason == $"Payment:{payment.Id}")).Should().Be(1);
        (await _db.AuditLogsSet.CountAsync(a => a.Action == "payment.captured")).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateOrder_throws_when_keys_missing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Razorpay:KeyId"] = "",
            ["Razorpay:KeySecret"] = ""
        }).Build();
        var gateway = new RazorpayPaymentGateway(_db, config, new StubHttpClientFactory(), _credits, _audit);

        _db.SubscriptionPlansSet.Add(new SubscriptionPlan
        {
            Code = "builder",
            Name = "Builder",
            MonthlyPriceInr = 1499,
            MonthlyCredits = 300,
            IsActive = true
        });
        var user = new User { Email = "a@b.c", FullName = "A", PasswordHash = "x", CreditBalance = 0 };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var act = () => gateway.CreateOrderForPlanAsync(user.Id, "builder");
        await act.Should().ThrowAsync<PaymentsNotConfiguredException>();
    }
}
