using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SaksAppWeb.Data;
using SaksAppWeb.Models;

namespace SaksAppWeb.Services;

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetActorUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public async Task LogAsync(
        AuditAction action,
        string entityType,
        string entityId,
        object? before,
        object? after,
        string? reason = null,
        CancellationToken ct = default)
    {
        var evt = new AuditEvent
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = GetActorUserId(),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
            Reason = reason
        };

        _db.AuditEvents.Add(evt);
        await _db.SaveChangesAsync(ct);
    }
}
