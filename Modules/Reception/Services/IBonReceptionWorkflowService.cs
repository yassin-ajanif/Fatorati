namespace GestionCommerciale.Modules.Reception.Services;

public interface IBonReceptionWorkflowService
{
    Task ValiderAsync(int bonReceptionId, int? userId, CancellationToken cancellationToken = default);
}
