namespace BlogCms.Domain.Enums;

/// <summary>
/// Authorization role of a <c>User</c>. Premium access is additionally driven by
/// subscription status, not by this role alone (see 05-Benutzerverwaltung-Auth.md).
/// </summary>
public enum UserRole
{
    /// <summary>Viewer: may read, comment and rate (FeatureFix1 BR-012).</summary>
    Reader,

    /// <summary>Author: may manage their own articles (FeatureFix1 BR-013).</summary>
    Author,

    Premium,
    Moderator,
    Admin
}
