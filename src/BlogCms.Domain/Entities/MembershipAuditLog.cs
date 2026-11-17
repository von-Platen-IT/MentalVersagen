namespace BlogCms.Domain.Entities;

/// <summary>
/// Audit trail for membership changes (admin grants, revocations, syncs, webhooks).
/// </summary>
public class MembershipAuditLog : EntityBase
{
    public Guid TargetUserId { get; set; }

    /// <summary>Admin who triggered the change; <c>null</c> for system actions.</summary>
    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }
}