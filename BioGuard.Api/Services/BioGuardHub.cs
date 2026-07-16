using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BioGuard.Api.Services;

[Authorize]
public class BioGuardHub : Hub
{
    public async Task JoinPacienteGroup(string pacienteId)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"paciente_{pacienteId}");
    }

    public async Task LeavePacienteGroup(string pacienteId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"paciente_{pacienteId}");
    }

    public async Task SendLecturaUpdate(string pacienteId, object lectura)
    {
        await Clients.Group($"paciente_{pacienteId}").SendAsync("LecturaActualizada", lectura);
    }

    public async Task SendAlerta(string pacienteId, object alerta)
    {
        await Clients.Group($"paciente_{pacienteId}").SendAsync("AlertaRecibida", alerta);
    }

    public async Task SendUbicacionUpdate(string pacienteId, object ubicacion)
    {
        await Clients.Group($"paciente_{pacienteId}").SendAsync("UbicacionActualizada", ubicacion);
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
