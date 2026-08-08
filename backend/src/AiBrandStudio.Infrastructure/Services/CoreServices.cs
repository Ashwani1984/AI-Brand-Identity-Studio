using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using AiBrandStudio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AiBrandStudio.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

public class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    public string CreateAccessToken(User user, IEnumerable<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var minutes = int.TryParse(config["Jwt:AccessTokenMinutes"], out var m) ? m : 30;
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            if (JobUserAmbient.UserId is Guid ambient)
                return ambient;

            var sub = accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? accessor.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsAdmin => accessor.HttpContext?.User?.IsInRole("Admin") == true;
}

public class CreditService(AppDbContext db) : ICreditService
{
    public async Task EnsureHasCreditsAsync(Guid userId, int cost, CancellationToken ct = default)
    {
        if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost), "Credit cost must be positive.");
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        if (user.CreditBalance < cost)
            throw new InvalidOperationException($"Insufficient credits. Need {cost}, have {user.CreditBalance}.");
    }

    public async Task DebitAsync(Guid userId, int cost, string reason, Guid? referenceId = null, CancellationToken ct = default)
    {
        if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost), "Credit cost must be positive.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Users
            .Where(u => u.Id == userId && u.CreditBalance >= cost)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.CreditBalance, u => u.CreditBalance - cost)
                    .SetProperty(u => u.UpdatedAtUtc, DateTime.UtcNow),
                ct);

        if (updated == 0)
            throw new InvalidOperationException("Insufficient credits.");

        db.CreditLedgersSet.Add(new CreditLedger
        {
            UserId = userId,
            Delta = -cost,
            Reason = reason,
            ReferenceId = referenceId
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task CreditAsync(Guid userId, int amount, string reason, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be positive.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.CreditBalance, u => u.CreditBalance + amount)
                    .SetProperty(u => u.UpdatedAtUtc, DateTime.UtcNow),
                ct);

        if (updated == 0)
            throw new KeyNotFoundException("User not found.");

        db.CreditLedgersSet.Add(new CreditLedger { UserId = userId, Delta = amount, Reason = reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DebitTenantAsync(
        Guid userId,
        Guid? organizationId,
        int cost,
        string reason,
        Guid? referenceId = null,
        CancellationToken ct = default)
    {
        if (organizationId is null)
        {
            await DebitAsync(userId, cost, reason, referenceId, ct);
            return;
        }

        if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost), "Credit cost must be positive.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Organizations
            .Where(o => o.Id == organizationId && o.CreditBalance >= cost)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.CreditBalance, o => o.CreditBalance - cost)
                    .SetProperty(o => o.UpdatedAtUtc, DateTime.UtcNow),
                ct);

        if (updated == 0)
            throw new InvalidOperationException("Insufficient organization credits.");

        db.CreditLedgersSet.Add(new CreditLedger
        {
            UserId = userId,
            OrganizationId = organizationId,
            Delta = -cost,
            Reason = reason,
            ReferenceId = referenceId
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task CreditOrganizationAsync(
        Guid organizationId,
        Guid actorUserId,
        int amount,
        string reason,
        CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be positive.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Organizations
            .Where(o => o.Id == organizationId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.CreditBalance, o => o.CreditBalance + amount)
                    .SetProperty(o => o.UpdatedAtUtc, DateTime.UtcNow),
                ct);

        if (updated == 0)
            throw new KeyNotFoundException("Organization not found.");

        db.CreditLedgersSet.Add(new CreditLedger
        {
            UserId = actorUserId,
            OrganizationId = organizationId,
            Delta = amount,
            Reason = reason
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

public class LocalFileStorage(IConfiguration config) : IFileStorage
{
    private string Root => Path.GetFullPath(config["Storage:LocalPath"] ?? "./storage");

    public async Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken ct = default)
    {
        var full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content, ct);
        return relativePath.Replace('\\', '/');
    }

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        var normalized = NormalizeStoragePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        var full = Path.Combine(Root, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return null;
        return await File.ReadAllBytesAsync(full, ct);
    }

    public string GetAssetContentUrl(Guid assetId) => $"/api/assets/{assetId}/content";

    public string NormalizeStoragePath(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return string.Empty;
        var value = pathOrUrl.Trim().Replace('\\', '/');
        if (value.StartsWith("/storage/", StringComparison.OrdinalIgnoreCase))
            value = value["/storage/".Length..];
        if (value.StartsWith("storage/", StringComparison.OrdinalIgnoreCase))
            value = value["storage/".Length..];
        // Legacy absolute API URLs are not storage paths.
        if (value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return value.TrimStart('/');
    }
}

public class DownloadTokenService(IConfiguration config) : IDownloadTokenService
{
    public string CreateAssetToken(Guid userId, Guid assetId, TimeSpan lifetime)
    {
        var exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var payload = $"{userId:N}.{assetId:N}.{exp}";
        var sig = Sign(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}.{sig}"));
    }

    public bool TryValidateAssetToken(string token, out Guid userId, out Guid assetId)
    {
        userId = Guid.Empty;
        assetId = Guid.Empty;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = raw.Split('.');
            if (parts.Length != 4) return false;
            var payload = $"{parts[0]}.{parts[1]}.{parts[2]}";
            if (!FixedEquals(Sign(payload), parts[3])) return false;
            if (!long.TryParse(parts[2], out var exp)) return false;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;
            if (!Guid.TryParseExact(parts[0], "N", out userId)) return false;
            if (!Guid.TryParseExact(parts[1], "N", out assetId)) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string Sign(string payload)
    {
        var key = Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? "DEV_ONLY_CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+");
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool FixedEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}

public class RazorpayPaymentGateway(
    AppDbContext db,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ICreditService credits,
    IAuditService audit) : IPaymentGateway
{
    public async Task<object> CreateOrderForPlanAsync(Guid userId, string planCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(planCode))
            throw new ArgumentException("planCode is required.");

        var code = planCode.Trim().ToLowerInvariant();
        if (code is "free")
            throw new ArgumentException("Cannot create a payment order for the free plan.");

        var plan = await db.SubscriptionPlansSet.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == code && p.IsActive, ct)
            ?? throw new KeyNotFoundException($"Unknown plan '{code}'.");

        if (plan.MonthlyPriceInr <= 0)
            throw new ArgumentException("Plan price must be greater than zero.");

        var keyId = config["Razorpay:KeyId"];
        var keySecret = config["Razorpay:KeySecret"];
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
            throw new PaymentsNotConfiguredException();

        var amountPaise = (int)Math.Round(plan.MonthlyPriceInr * 100m, MidpointRounding.AwayFromZero);
        var receipt = $"plan_{code}_{Guid.NewGuid():N}";
        if (receipt.Length > 40) receipt = receipt[..40];

        var client = httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders");
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
        req.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                amount = amountPaise,
                currency = "INR",
                receipt,
                notes = new { planCode = code, userId = userId.ToString() }
            }),
            Encoding.UTF8,
            "application/json");

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Razorpay order create failed: {(int)resp.StatusCode} {body}");

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var orderId = doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Razorpay response missing order id.");

        var payment = new Payment
        {
            UserId = userId,
            Provider = "Razorpay",
            ProviderOrderId = orderId,
            PlanCode = code,
            Amount = plan.MonthlyPriceInr,
            Currency = "INR",
            Status = "Created"
        };
        db.PaymentsSet.Add(payment);
        await db.SaveChangesAsync(ct);

        return new
        {
            orderId,
            amount = amountPaise,
            currency = "INR",
            razorpayKeyId = keyId,
            paymentId = payment.Id,
            planCode = code
        };
    }

    public async Task<object> HandleWebhookAsync(string rawBody, string? signatureHeader, bool isDevelopment, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            throw new PaymentSignatureException("Empty webhook body.");

        var webhookSecret = config["Razorpay:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            if (!isDevelopment)
                throw new PaymentsNotConfiguredException();
            webhookSecret = config["Razorpay:KeySecret"];
            if (string.IsNullOrWhiteSpace(webhookSecret))
                throw new PaymentsNotConfiguredException();
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
            throw new PaymentSignatureException("Missing X-Razorpay-Signature.");

        var expected = ComputeHmacHex(rawBody, webhookSecret);
        if (!FixedTimeEqualsHex(expected, signatureHeader.Trim()))
            throw new PaymentSignatureException("Invalid Razorpay webhook signature.");

        using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        var eventName = root.TryGetProperty("event", out var ev) ? ev.GetString() : null;
        var eventId = root.TryGetProperty("id", out var eid) ? eid.GetString() : null;

        if (string.Equals(eventName, "payment.captured", StringComparison.OrdinalIgnoreCase))
            await ProcessPaymentCapturedAsync(root, eventId, ct);

        return new { received = true };
    }

    private async Task ProcessPaymentCapturedAsync(System.Text.Json.JsonElement root, string? eventId, CancellationToken ct)
    {
        if (!root.TryGetProperty("payload", out var payload) ||
            !payload.TryGetProperty("payment", out var paymentWrapper) ||
            !paymentWrapper.TryGetProperty("entity", out var entity))
            return;

        var orderId = entity.TryGetProperty("order_id", out var oid) ? oid.GetString() : null;
        var providerPaymentId = entity.TryGetProperty("id", out var pid) ? pid.GetString() : null;
        if (string.IsNullOrWhiteSpace(orderId))
            return;

        var payment = await db.PaymentsSet.FirstOrDefaultAsync(p => p.ProviderOrderId == orderId, ct);
        if (payment is null)
            return;

        if (string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrWhiteSpace(eventId) &&
            await db.PaymentsSet.AnyAsync(p => p.WebhookEventId == eventId && p.Id != payment.Id, ct))
            return;

        var planCode = payment.PlanCode;
        var plan = planCode is null
            ? null
            : await db.SubscriptionPlansSet.FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive, ct);

        var creditReason = $"Payment:{payment.Id}";
        var alreadyCredited = await db.CreditLedgersSet.AnyAsync(l => l.Reason == creditReason, ct);

        payment.Status = "Paid";
        payment.ProviderPaymentId = providerPaymentId;
        payment.WebhookEventId = eventId;
        await db.SaveChangesAsync(ct);

        if (plan is not null)
        {
            if (!alreadyCredited)
                await credits.CreditAsync(payment.UserId, plan.MonthlyCredits, creditReason, ct);

            var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == payment.UserId, ct);
            var periodEnd = DateTime.UtcNow.AddDays(30);
            var orgId = await db.OrganizationMembers.AsNoTracking()
                .Where(m => m.UserId == payment.UserId && m.Role == OrganizationRoles.Owner)
                .Select(m => (Guid?)m.OrganizationId)
                .FirstOrDefaultAsync(ct);

            if (sub is null)
            {
                db.Subscriptions.Add(new Subscription
                {
                    UserId = payment.UserId,
                    OrganizationId = orgId,
                    PlanId = plan.Id,
                    Status = SubscriptionStatuses.Active,
                    CurrentPeriodEndUtc = periodEnd,
                    CancelAtPeriodEnd = false,
                    GracePeriodEndsUtc = null,
                    CanceledAtUtc = null,
                    DunningAttempts = 0
                });
            }
            else
            {
                sub.PlanId = plan.Id;
                sub.OrganizationId ??= orgId;
                sub.Status = SubscriptionStatuses.Active;
                sub.CurrentPeriodEndUtc = periodEnd;
                sub.CancelAtPeriodEnd = false;
                sub.GracePeriodEndsUtc = null;
                sub.CanceledAtUtc = null;
                sub.DunningAttempts = 0;
            }
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                "payment.captured",
                payment.UserId,
                "Payment",
                payment.Id,
                new { payment.PlanCode, payment.Amount, payment.ProviderPaymentId },
                ct: ct);
        }
    }

    private static string ComputeHmacHex(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
        var right = Encoding.UTF8.GetBytes(b.ToLowerInvariant());
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public static class SeedData
{
    public static async Task EnsureSeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsSqlServer())
            await db.Database.MigrateAsync();
        else if (db.Database.IsRelational())
            // Migrations are authored for SQL Server; Sqlite/local uses EnsureCreated.
            await db.Database.EnsureCreatedAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                new Role { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "User" },
                new Role { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Admin" });
            await db.SaveChangesAsync();
        }

        if (!await db.SubscriptionPlansSet.AnyAsync())
        {
            db.SubscriptionPlansSet.AddRange(
                new SubscriptionPlan { Code = "free", Name = "Starter", MonthlyPriceInr = 0, MonthlyCredits = 50 },
                new SubscriptionPlan { Code = "builder", Name = "Builder", MonthlyPriceInr = 1499, MonthlyCredits = 300 },
                new SubscriptionPlan { Code = "scale", Name = "Scale", MonthlyPriceInr = 4999, MonthlyCredits = 1200 });
            await db.SaveChangesAsync();
        }

        if (!await db.AiProviderConfigsSet.AnyAsync())
        {
            db.AiProviderConfigsSet.AddRange(
                new AiProviderConfig { ProviderName = "Mock", IsEnabled = true, ApiKeyMasked = "n/a" },
                new AiProviderConfig { ProviderName = "OpenAI", IsEnabled = false },
                new AiProviderConfig { ProviderName = "Gemini", IsEnabled = false },
                new AiProviderConfig { ProviderName = "Ideogram", IsEnabled = false });
            await db.SaveChangesAsync();
        }

        var adminEmail = "admin@aibrand.studio";
        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
            var userRole = await db.Roles.FirstAsync(r => r.Name == "User");
            var admin = new User
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Email = adminEmail,
                FullName = "Platform Admin",
                PasswordHash = hasher.Hash("Admin@12345"),
                CreditBalance = 1000
            };
            admin.UserRoles.Add(new UserRole { RoleId = adminRole.Id, User = admin });
            admin.UserRoles.Add(new UserRole { RoleId = userRole.Id, User = admin });
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            if (!await db.OrganizationMembers.AnyAsync(m => m.UserId == admin.Id))
            {
                var org = new Organization
                {
                    Name = "Platform Admin Workspace",
                    Slug = "platform-admin",
                    CreditBalance = 1000
                };
                db.Organizations.Add(org);
                db.OrganizationMembers.Add(new OrganizationMember
                {
                    Organization = org,
                    UserId = admin.Id,
                    Role = OrganizationRoles.Owner
                });
                var free = await db.SubscriptionPlansSet.FirstAsync(p => p.Code == "free");
                db.Subscriptions.Add(new Subscription
                {
                    UserId = admin.Id,
                    OrganizationId = org.Id,
                    PlanId = free.Id,
                    Status = SubscriptionStatuses.Active,
                    CurrentPeriodEndUtc = DateTime.UtcNow.AddYears(10)
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
