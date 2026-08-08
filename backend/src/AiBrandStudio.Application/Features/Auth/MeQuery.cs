using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Auth;

public record MeQuery() : IRequest<AuthResponseDto>;

public class MeHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<MeQuery, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var user = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException();
        var roles = user.UserRoles.Select(r => r.Role.Name).ToList();
        return new AuthResponseDto(string.Empty, string.Empty, user.Id, user.Email, user.FullName, user.CreditBalance, roles);
    }
}
