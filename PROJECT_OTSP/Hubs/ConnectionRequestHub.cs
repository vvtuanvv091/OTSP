using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PROJECT_OTSP.Hubs;

[Authorize]
public sealed class ConnectionRequestHub : Hub
{
}
