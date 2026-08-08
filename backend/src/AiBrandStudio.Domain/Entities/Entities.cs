namespace AiBrandStudio.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int CreditBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAtUtc { get; set; }
    /// <summary>When set, account is queued for erasure after retention window.</summary>
    public DateTime? DeletionRequestedAtUtc { get; set; }
    public DateTime? TermsAcceptedAtUtc { get; set; }
    public bool ConsentMarketing { get; set; }
    public int? DataRetentionDays { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<OrganizationMember> OrganizationMemberships { get; set; } = new List<OrganizationMember>();
}

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    /// <summary>Shared tenant credit pool (primary for org-scoped spend).</summary>
    public int CreditBalance { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class OrganizationMember
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>Owner | Admin | Member</summary>
    public string Role { get; set; } = "Member";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class OrganizationRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";
}

public static class SubscriptionStatuses
{
    public const string Trial = "Trial";
    public const string Active = "Active";
    public const string PastDue = "PastDue";
    public const string Canceled = "Canceled";
}

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

public class OAuthAccount : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
}

public class SubscriptionPlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyPriceInr { get; set; }
    public int MonthlyCredits { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Subscription : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    /// <summary>Trial | Active | PastDue | Canceled</summary>
    public string Status { get; set; } = SubscriptionStatuses.Trial;
    public DateTime? TrialEndsUtc { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CanceledAtUtc { get; set; }
    /// <summary>End of grace after PastDue before hard cancel (dunning).</summary>
    public DateTime? GracePeriodEndsUtc { get; set; }
    public int DunningAttempts { get; set; }
}

public class Payment : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Provider { get; set; } = "Razorpay";
    public string? ProviderOrderId { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? PlanCode { get; set; }
    public string? WebhookEventId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = "Created";
}

public class CreditLedger : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
}

public class Project : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime? UpdatedAtUtc { get; set; }
    public Brand? Brand { get; set; }
}

public class Brand : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string BusinessName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Audience { get; set; }
    public string? Competitors { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Style { get; set; }
    public string? Mood { get; set; }
    public string? PreferredColors { get; set; }
    public string? PreferredFonts { get; set; }
    public string? Keywords { get; set; }
    public string? Mission { get; set; }
    public string? Vision { get; set; }
    public string? Usp { get; set; }
    public string? GeneratedName { get; set; }
    public string? Tagline { get; set; }
    public string? BrandStory { get; set; }
    public string? BrandVoice { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<Logo> Logos { get; set; } = new List<Logo>();
    public ICollection<Palette> Palettes { get; set; } = new List<Palette>();
    public ICollection<FontRecommendation> Fonts { get; set; } = new List<FontRecommendation>();
}

public class BrandVersion : BaseEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = "{}";
}

public class PromptRecord : BaseEntity
{
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string InputText { get; set; } = string.Empty;
    public string? OutputText { get; set; }
}

public class Logo : BaseEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string Style { get; set; } = "Minimal";
    public string PromptText { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? SvgContent { get; set; }
    public string Provider { get; set; } = "Mock";
}

public class Palette : BaseEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string PrimaryHex { get; set; } = "#0F766E";
    public string SecondaryHex { get; set; } = "#334155";
    public string AccentHex { get; set; } = "#C2410C";
    public string BackgroundHex { get; set; } = "#F7F8FA";
    public string TypographyHex { get; set; } = "#0B1220";
    public string? CssExport { get; set; }
}

public class FontRecommendation : BaseEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string HeadlineFont { get; set; } = "Plus Jakarta Sans";
    public string BodyFont { get; set; } = "Plus Jakarta Sans";
    public string? MonoFont { get; set; } = "JetBrains Mono";
    public string? GoogleFontsUrl { get; set; }
}

public class Asset : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long ByteSize { get; set; }
}

public class Mockup : BaseEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string MockupType { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? PromptText { get; set; }
}

public class VideoJob : BaseEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string Provider { get; set; } = "Runway";
    public string PromptText { get; set; } = string.Empty;
    public string? StoryboardJson { get; set; }
    public string Status { get; set; } = "Draft";
}

public class ExportJob : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Format { get; set; } = "ZIP";
    public string StoragePath { get; set; } = string.Empty;
}

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string? MetadataJson { get; set; }
}

public class AiProviderConfig : BaseEntity
{
    public string ProviderName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? ApiKeyMasked { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Template : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
}

public class GenerationJob : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>LogoGenerate | MockupGenerate | ExportZip</summary>
    public string JobType { get; set; } = string.Empty;
    /// <summary>Queued | Running | Completed | Failed</summary>
    public string Status { get; set; } = "Queued";
    public string PayloadJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
