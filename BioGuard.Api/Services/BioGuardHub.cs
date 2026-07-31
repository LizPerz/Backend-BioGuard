using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BioGuard.Api.Config;

namespace BioGuard.Api.Services;

[Authorize]
public class BioGuardHub : Hub
{
    private readonly OwnershipHelper _ownershipHelper;

    public BioGuardHub(OwnershipHelper ownershipHelper)
    {
        _ownershipHelper = ownershipHelper;
    }

    public async Task<bool> JoinPacienteGroup(string pacienteId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role)) return false;

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, userId, role))
            return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"paciente_{pacienteId}");
        return true;
    }

    public async Task LeavePacienteGroup(string pacienteId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"paciente_{pacienteId}");
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
