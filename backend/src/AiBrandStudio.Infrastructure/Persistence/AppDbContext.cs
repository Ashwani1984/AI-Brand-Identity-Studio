using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokensSet => Set<RefreshToken>();
    public DbSet<OAuthAccount> OAuthAccounts => Set<OAuthAccount>();
    public DbSet<SubscriptionPlan> SubscriptionPlansSet => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> PaymentsSet => Set<Payment>();
    public DbSet<CreditLedger> CreditLedgersSet => Set<CreditLedger>();
    public DbSet<Project> ProjectsSet => Set<Project>();
    public DbSet<Brand> BrandsSet => Set<Brand>();
    public DbSet<BrandVersion> BrandVersions => Set<BrandVersion>();
    public DbSet<PromptRecord> PromptsSet => Set<PromptRecord>();
    public DbSet<Logo> LogosSet => Set<Logo>();
    public DbSet<Palette> PalettesSet => Set<Palette>();
    public DbSet<FontRecommendation> FontsSet => Set<FontRecommendation>();
    public DbSet<Asset> AssetsSet => Set<Asset>();
    public DbSet<Mockup> Mockups => Set<Mockup>();
    public DbSet<VideoJob> Videos => Set<VideoJob>();
    public DbSet<ExportJob> ExportsSet => Set<ExportJob>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogsSet => Set<AuditLog>();
    public DbSet<AiProviderConfig> AiProviderConfigsSet => Set<AiProviderConfig>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<GenerationJob> GenerationJobs => Set<GenerationJob>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    IQueryable<User> IAppDbContext.Users => Users;
    IQueryable<Role> IAppDbContext.Roles => Roles;
    IQueryable<Project> IAppDbContext.Projects => ProjectsSet;
    IQueryable<Brand> IAppDbContext.Brands => BrandsSet;
    IQueryable<Logo> IAppDbContext.Logos => LogosSet;
    IQueryable<Palette> IAppDbContext.Palettes => PalettesSet;
    IQueryable<FontRecommendation> IAppDbContext.Fonts => FontsSet;
    IQueryable<Asset> IAppDbContext.Assets => AssetsSet;
    IQueryable<ExportJob> IAppDbContext.Exports => ExportsSet;
    IQueryable<CreditLedger> IAppDbContext.CreditLedgers => CreditLedgersSet;
    IQueryable<SubscriptionPlan> IAppDbContext.SubscriptionPlans => SubscriptionPlansSet;
    IQueryable<Subscription> IAppDbContext.Subscriptions => Subscriptions;
    IQueryable<Payment> IAppDbContext.Payments => PaymentsSet;
    IQueryable<PromptRecord> IAppDbContext.Prompts => PromptsSet;
    IQueryable<AuditLog> IAppDbContext.AuditLogs => AuditLogsSet;
    IQueryable<RefreshToken> IAppDbContext.RefreshTokens => RefreshTokensSet;
    IQueryable<AiProviderConfig> IAppDbContext.AiProviderConfigs => AiProviderConfigsSet;
    IQueryable<GenerationJob> IAppDbContext.GenerationJobs => GenerationJobs;
    IQueryable<Organization> IAppDbContext.Organizations => Organizations;
    IQueryable<OrganizationMember> IAppDbContext.OrganizationMembers => OrganizationMembers;
    IQueryable<Mockup> IAppDbContext.Mockups => Mockups;
    IQueryable<VideoJob> IAppDbContext.Videos => Videos;
    IQueryable<Notification> IAppDbContext.Notifications => Notifications;

    void IAppDbContext.Add<T>(T entity) => Set<T>().Add(entity);
    void IAppDbContext.Remove<T>(T entity) => Set<T>().Remove(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
        });
        modelBuilder.Entity<Role>(e => e.HasIndex(x => x.Name).IsUnique());
        modelBuilder.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);
        });
        modelBuilder.Entity<Organization>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(80);
        });
        modelBuilder.Entity<OrganizationMember>(e =>
        {
            e.HasKey(x => new { x.OrganizationId, x.UserId });
            e.HasOne(x => x.Organization).WithMany(o => o.Members).HasForeignKey(x => x.OrganizationId);
            e.HasOne(x => x.User).WithMany(u => u.OrganizationMemberships).HasForeignKey(x => x.UserId);
            e.Property(x => x.Role).HasMaxLength(32);
            e.HasIndex(x => x.UserId);
        });
        modelBuilder.Entity<Project>(e =>
        {
            e.HasOne(p => p.Brand).WithOne(b => b.Project).HasForeignKey<Brand>(b => b.ProjectId);
            e.HasOne(p => p.Organization).WithMany(o => o.Projects).HasForeignKey(p => p.OrganizationId);
            e.HasIndex(p => p.OrganizationId);
        });
        modelBuilder.Entity<Subscription>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => new { x.Status, x.CurrentPeriodEndUtc });
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
        });
        modelBuilder.Entity<CreditLedger>(e =>
        {
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
            e.HasIndex(x => x.OrganizationId);
        });
        modelBuilder.Entity<SubscriptionPlan>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<AiProviderConfig>().HasIndex(x => x.ProviderName).IsUnique();
        modelBuilder.Entity<GenerationJob>(e =>
        {
            e.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            e.HasIndex(x => x.UserId);
            e.Property(x => x.JobType).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(32);
        });
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => x.Action);
            e.Property(x => x.Action).HasMaxLength(128);
        });

        // SQL Server rejects multiple cascade paths (e.g. User→Project→Export and User→Export).
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
