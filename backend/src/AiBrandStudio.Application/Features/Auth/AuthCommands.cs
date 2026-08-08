using AiBrandStudio.Application.Common;
using AiBrandStudio.Application.Features.Organizations;
using AiBrandStudio.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Auth;

public record RegisterCommand(string Email, string Password, string FullName, bool? AcceptTerms = null, bool ConsentMarketing = false) : IRequest<AuthResponseDto>;
public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;
public record RefreshCommand(string RefreshToken) : IRequest<AuthResponseDto>;
public record LogoutCommand(string RefreshToken) : IRequest;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AcceptTerms)
            .Must(v => v is null or true)
            .WithMessage("Terms must be accepted.");
    }
}

public class RegisterHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    IJwtTokenService jwt,
    ICreditService credits,
    IAuditService audit) : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new InvalidOperationException("Email already registered.");

        var userRole = await db.Roles.FirstAsync(r => r.Name == "User", cancellationToken);
        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            CreditBalance = 0,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            ConsentMarketing = request.ConsentMarketing,
            DataRetentionDays = 365
        };
        user.UserRoles.Add(new UserRole { RoleId = userRole.Id, User = user });
        db.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await PersonalOrgBootstrap.CreateForUserAsync(db, user, cancellationToken);
        await credits.CreditAsync(user.Id, 50, "SignupBonus", cancellationToken);
        await audit.WriteAsync("auth.register", user.Id, "User", user.Id,
            new { user.Email, consentMarketing = user.ConsentMarketing }, ct: cancellationToken);

        return await AuthHelpers.IssueAsync(db, jwt, user, cancellationToken);
    }
}

public class LoginHandler(IAppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt)
    : IRequestHandler<LoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");
        if (!user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");
        return await AuthHelpers.IssueAsync(db, jwt, user, cancellationToken);
    }
}

public class RefreshHandler(IAppDbContext db, IJwtTokenService jwt) : IRequestHandler<RefreshCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var hash = jwt.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAtUtc == null, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");
        if (token.ExpiresAtUtc < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired.");
        token.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await AuthHelpers.IssueAsync(db, jwt, token.User, cancellationToken);
    }
}

public class LogoutHandler(IAppDbContext db, IJwtTokenService jwt) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var hash = jwt.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null) return;
        token.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class AuthHelpers
{
    public static async Task<AuthResponseDto> IssueAsync(IAppDbContext db, IJwtTokenService jwt, User user, CancellationToken ct)
    {
        var roles = user.UserRoles.Select(r => r.Role.Name).ToList();
        if (roles.Count == 0)
        {
            var loaded = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstAsync(u => u.Id == user.Id, ct);
            roles = loaded.UserRoles.Select(r => r.Role.Name).ToList();
            user = loaded;
        }

        var access = jwt.CreateAccessToken(user, roles);
        var refresh = jwt.CreateRefreshToken();
        db.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = jwt.HashToken(refresh),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        });
        await db.SaveChangesAsync(ct);
        return new AuthResponseDto(access, refresh, user.Id, user.Email, user.FullName, user.CreditBalance, roles);
    }
}
