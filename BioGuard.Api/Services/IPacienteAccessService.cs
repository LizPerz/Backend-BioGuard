using BioGuard.Api.Config;

namespace BioGuard.Api.Services;

public interface IPacienteAccessService
{
    Task<bool> CanAccessPacienteAsync(string pacienteId, string userId, string role);
    Task ValidateOrThrowAsync(string pacienteId, string userId, string role);
}

public class PacienteAccessService : IPacienteAccessService
{
    private readonly OwnershipHelper _ownershipHelper;

    public PacienteAccessService(OwnershipHelper ownershipHelper)
    {
        _ownershipHelper = ownershipHelper;
    }

    public async Task<bool> CanAccessPacienteAsync(string pacienteId, string userId, string role)
    {
        return await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, userId, role);
    }

    public async Task ValidateOrThrowAsync(string pacienteId, string userId, string role)
    {
        if (!await CanAccessPacienteAsync(pacienteId, userId, role))
            throw new UnauthorizedAccessException("No tienes acceso a este paciente");
    }
}
