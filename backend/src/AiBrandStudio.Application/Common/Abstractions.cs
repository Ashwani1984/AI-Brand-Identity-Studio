using AiBrandStudio.Domain.Entities;

namespace AiBrandStudio.Application.Common;

public interface IAppDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<Role> Roles { get; }
    IQueryable<Project> Projects { get; }
    IQueryable<Brand> Brands { get; }
    IQueryable<Logo> Logos { get; }
    IQueryable<Palette> Palettes { get; }
    IQueryable<FontRecommendation> Fonts { get; }
    IQueryable<Asset> Assets { get; }
    IQueryable<ExportJob> Exports { get; }
    IQueryable<CreditLedger> CreditLedgers { get; }
    IQueryable<SubscriptionPlan> SubscriptionPlans { get; }
    IQueryable<Subscription> Subscriptions { get; }
    IQueryable<Payment> Payments { get; }
    IQueryable<PromptRecord> Prompts { get; }
    IQueryable<AuditLog> AuditLogs { get; }
    IQueryable<RefreshToken> RefreshTokens { get; }
    IQueryable<AiProviderConfig> AiProviderConfigs { get; }
    IQueryable<GenerationJob> GenerationJobs { get; }
    IQueryable<Organization> Organizations { get; }
    IQueryable<OrganizationMember> OrganizationMembers { get; }
    IQueryable<Mockup> Mockups { get; }
    IQueryable<VideoJob> Videos { get; }
    IQueryable<Notification> Notifications { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IJwtTokenService
{
    string CreateAccessToken(User user, IEnumerable<string> roles);
    string CreateRefreshToken();
    string HashToken(string token);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
}

public interface ICreditService
{
    Task EnsureHasCreditsAsync(Guid userId, int cost, CancellationToken ct = default);
    Task DebitAsync(Guid userId, int cost, string reason, Guid? referenceId = null, CancellationToken ct = default);
    Task CreditAsync(Guid userId, int amount, string reason, CancellationToken ct = default);
    /// <summary>Debit shared org pool when organizationId set; otherwise personal user balance.</summary>
    Task DebitTenantAsync(Guid userId, Guid? organizationId, int cost, string reason, Guid? referenceId = null, CancellationToken ct = default);
    Task CreditOrganizationAsync(Guid organizationId, Guid actorUserId, int amount, string reason, CancellationToken ct = default);
}

public interface IAuditService
{
    Task WriteAsync(
        string action,
        Guid? actorUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        object? metadata = null,
        string? ipAddress = null,
        CancellationToken ct = default);
}

public interface ITenantAccess
{
    Task<Guid?> GetDefaultOrganizationIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task EnsureCanAccessProjectAsync(Guid userId, Project project, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetAccessibleOrganizationIdsAsync(Guid userId, CancellationToken ct = default);
}

public interface IFileStorage
{
    Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken ct = default);
    Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default);
    string GetAssetContentUrl(Guid assetId);
    string NormalizeStoragePath(string? pathOrUrl);
}

public interface IDownloadTokenService
{
    string CreateAssetToken(Guid userId, Guid assetId, TimeSpan lifetime);
    bool TryValidateAssetToken(string token, out Guid userId, out Guid assetId);
}

public interface IPaymentGateway
{
    Task<object> CreateOrderForPlanAsync(Guid userId, string planCode, CancellationToken ct = default);
    Task<object> HandleWebhookAsync(string rawBody, string? signatureHeader, bool isDevelopment, CancellationToken ct = default);
}

/// <summary>Raised when Razorpay keys are missing; mapped to HTTP 503.</summary>
public class PaymentsNotConfiguredException : InvalidOperationException
{
    public PaymentsNotConfiguredException()
        : base("PAYMENTS_NOT_CONFIGURED") { }
}

/// <summary>Raised when webhook signature verification fails; mapped to HTTP 400.</summary>
public class PaymentSignatureException : ArgumentException
{
    public PaymentSignatureException(string message)
        : base(message) { }
}

public interface ITextAiProvider
{
    string Name { get; }
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}

public interface IImageAiProvider
{
    string Name { get; }
    Task<(byte[] Bytes, string ContentType, string? Svg)> GenerateImageAsync(string prompt, CancellationToken ct = default);
}

public interface IAiProviderFactory
{
    ITextAiProvider GetTextProvider();
    IImageAiProvider GetImageProvider();
}

public interface IGenerationJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct);
}

public interface IGenerationNotifier
{
    Task NotifyUserAsync(Guid userId, object payload, CancellationToken ct = default);
}

public static class JobUserAmbient
{
    private static readonly AsyncLocal<Guid?> UserIdLocal = new();
    public static Guid? UserId
    {
        get => UserIdLocal.Value;
        set => UserIdLocal.Value = value;
    }
}

public record GenerationJobDto(
    Guid Id,
    string JobType,
    string Status,
    string? ResultJson,
    string? Error,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
public record AuthResponseDto(string AccessToken, string RefreshToken, Guid UserId, string Email, string FullName, int Credits, IReadOnlyList<string> Roles);
public record ProjectDto(Guid Id, string Name, string Status, DateTime CreatedAtUtc, Guid? BrandId, string? BusinessName, Guid? OrganizationId = null);
public record BrandDto(
    Guid Id, Guid ProjectId, string BusinessName, string? Industry, string? Audience, string? Competitors,
    string? Country, string? Language, string? Style, string? Mood, string? PreferredColors, string? PreferredFonts,
    string? Keywords, string? Mission, string? Vision, string? Usp,
    string? GeneratedName, string? Tagline, string? BrandStory, string? BrandVoice);
public record LogoDto(Guid Id, Guid BrandId, string Style, string PromptText, string? ImageUrl, string? SvgContent, string Provider);
public record PaletteDto(Guid Id, Guid BrandId, string PrimaryHex, string SecondaryHex, string AccentHex, string BackgroundHex, string TypographyHex, string? CssExport);
public record FontDto(Guid Id, Guid BrandId, string HeadlineFont, string BodyFont, string? MonoFont, string? GoogleFontsUrl);
public record AssetDto(Guid Id, string Kind, string FileName, string ContentType, string Url, long ByteSize, DateTime CreatedAtUtc);
public record PlanDto(Guid Id, string Code, string Name, decimal MonthlyPriceInr, int MonthlyCredits);
public record UserAdminDto(Guid Id, string Email, string FullName, int CreditBalance, bool IsActive, DateTime CreatedAtUtc);
public record OrganizationDto(Guid Id, string Name, string Slug, int CreditBalance, string MyRole, DateTime CreatedAtUtc);
public record OrganizationMemberDto(Guid UserId, string Email, string FullName, string Role, DateTime CreatedAtUtc);
public record SubscriptionDto(
    Guid Id,
    string Status,
    string PlanCode,
    string PlanName,
    DateTime? TrialEndsUtc,
    DateTime? CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd,
    DateTime? GracePeriodEndsUtc,
    Guid? OrganizationId);
public record AuditLogDto(Guid Id, Guid? UserId, string Action, string? EntityType, Guid? EntityId, string? MetadataJson, DateTime CreatedAtUtc);
