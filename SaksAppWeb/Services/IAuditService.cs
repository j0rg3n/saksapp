using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public interface IAuditService
{
    Task LogAsync(
        AuditAction action,
        string entityType,
        string entityId,
        object? before,
        object? after,
        string? reason = null,
        CancellationToken ct = default);

    string? GetActorUserId();
}
