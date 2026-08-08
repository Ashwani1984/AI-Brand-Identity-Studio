using System.Text;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Infrastructure.AI;
using AiBrandStudio.Infrastructure.Jobs;
using AiBrandStudio.Infrastructure.Persistence;
using AiBrandStudio.Infrastructure.Services;
using AiBrandStudio.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AiBrandStudio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();

        services.AddHttpClient("openai", c => c.Timeout = TimeSpan.FromSeconds(60))
            .AddStandardResilienceHandler();
        services.AddHttpClient("gemini", c => c.Timeout = TimeSpan.FromSeconds(60))
            .AddStandardResilienceHandler();
        services.AddHttpClient("ideogram", c => c.Timeout = TimeSpan.FromSeconds(90))
            .AddStandardResilienceHandler();
        services.AddHttpClient();

        var cs = config.GetConnectionString("DefaultConnection") ?? "Sqlite";
        services.AddDbContext<AppDbContext>(opt =>
        {
            if (string.Equals(cs, "InMemory", StringComparison.OrdinalIgnoreCase))
                opt.UseInMemoryDatabase("AiBrandStudio");
            else if (string.Equals(cs, "Sqlite", StringComparison.OrdinalIgnoreCase) ||
                     cs.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                var sqlite = string.Equals(cs, "Sqlite", StringComparison.OrdinalIgnoreCase)
                    ? "Data Source=aibrandstudio.db"
                    : cs;
                opt.UseSqlite(sqlite);
            }
            else
                opt.UseSqlServer(cs);
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICreditService, CreditService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITenantAccess, TenantAccess>();

        var storageProvider = config["Storage:Provider"] ?? "Local";
        if (string.Equals(storageProvider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
        else
            services.AddScoped<IFileStorage, LocalFileStorage>();

        services.AddSingleton<IDownloadTokenService, DownloadTokenService>();
        services.AddScoped<IPaymentGateway, RazorpayPaymentGateway>();

        services.AddSingleton<IGenerationJobQueue, ChannelGenerationJobQueue>();
        services.AddHostedService<GenerationJobWorker>();
        services.AddHostedService<SubscriptionDunningWorker>();

        services.AddSingleton<MockTextAiProvider>();
        services.AddSingleton<MockImageAiProvider>();
        services.AddScoped<OpenAiTextProvider>();
        services.AddScoped<GeminiTextProvider>();
        services.AddScoped<IdeogramImageProvider>();
        services.AddScoped<IAiProviderFactory, AiProviderFactory>();

        var redis = config.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
            services.AddStackExchangeRedisCache(o => o.Configuration = redis);
        else
            services.AddDistributedMemoryCache();

        var jwtKey = config["Jwt:Key"] ?? "DEV_ONLY_CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+";
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
