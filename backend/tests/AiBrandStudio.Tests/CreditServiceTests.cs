using AiBrandStudio.Domain.Entities;
using AiBrandStudio.Infrastructure.Persistence;
using AiBrandStudio.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Tests;

public class CreditServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly CreditService _credits;

    public CreditServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _credits = new CreditService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Guid> SeedUserAsync(int balance)
    {
        var user = new User
        {
            Email = $"u{Guid.NewGuid():N}@test.local",
            FullName = "Test",
            PasswordHash = "x",
            CreditBalance = balance
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task DebitAsync_reduces_balance_and_writes_ledger()
    {
        var userId = await SeedUserAsync(10);
        await _credits.DebitAsync(userId, 3, "TestDebit");

        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        user.CreditBalance.Should().Be(7);
        (await _db.CreditLedgersSet.CountAsync(l => l.UserId == userId && l.Delta == -3)).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_debits_cannot_overspend()
    {
        var userId = await SeedUserAsync(5);

        async Task<bool> TryDebitAsync(string reason)
        {
            try
            {
                await _credits.DebitAsync(userId, 5, reason);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryDebitAsync("A"), TryDebitAsync("B"));

        results.Count(ok => ok).Should().Be(1);
        results.Count(ok => !ok).Should().Be(1);

        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        user.CreditBalance.Should().Be(0);
    }

    [Fact]
    public async Task CreditAsync_refunds_after_debit()
    {
        var userId = await SeedUserAsync(10);
        await _credits.DebitAsync(userId, 4, "LogoGenerate");
        await _credits.CreditAsync(userId, 4, "Refund:LogoGenerate");

        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        user.CreditBalance.Should().Be(10);
        (await _db.CreditLedgersSet.CountAsync(l => l.UserId == userId)).Should().Be(2);
    }

    [Fact]
    public async Task DebitAsync_throws_when_insufficient()
    {
        var userId = await SeedUserAsync(2);
        var act = () => _credits.DebitAsync(userId, 5, "TooMuch");
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId)).CreditBalance.Should().Be(2);
    }
}
