using AiBrandStudio.Application.Common;
using AiBrandStudio.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AiBrandStudio.Api.Services;

public class SignalRGenerationNotifier(IHubContext<GenerationHub> hub) : IGenerationNotifier
{
    public async Task NotifyUserAsync(Guid userId, object payload, CancellationToken ct = default)
    {
        var key = userId.ToString("D");
        await hub.Clients.User(key).SendAsync("generationProgress", payload, ct);
        await hub.Clients.Group($"user:{key}").SendAsync("generationProgress", payload, ct);
    }
}
