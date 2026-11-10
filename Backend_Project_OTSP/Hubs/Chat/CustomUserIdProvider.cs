using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Backend_Project_OTSP.Hubs;

public sealed class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? connection.User?.FindFirstValue("sub")
            ?? connection.User?.FindFirstValue(ClaimTypes.Name);
    }
}
