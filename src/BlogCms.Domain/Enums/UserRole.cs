namespace BlogCms.Domain.Enums;

/// <summary>
/// Authorization role of a <c>User</c>. Premium access is additionally driven by
/// subscription status, not by this role alone (see 05-Benutzerverwaltung-Auth.md).
/// </summary>
public enum UserRole
{
    Reader,
    Premium,
    Moderator,
    Admin
}
