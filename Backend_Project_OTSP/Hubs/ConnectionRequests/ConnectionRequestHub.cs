using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Hubs;

[Authorize]
public sealed class ConnectionRequestHub : Hub
{
    private readonly IUserPresenceService _presenceService;

    public ConnectionRequestHub(IUserPresenceService presenceService)
    {
        _presenceService = presenceService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _presenceService.OnConnected(userId, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        _presenceService.OnDisconnected(userId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
