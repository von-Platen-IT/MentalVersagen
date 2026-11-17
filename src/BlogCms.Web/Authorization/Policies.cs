namespace BlogCms.Web.Authorization;

/// <summary>
/// Names of the application's authorization policies.
/// </summary>
public static class Policies
{
    /// <summary>Requires the <c>Author</c> or <c>Admin</c> role.</summary>
    public const string RequireAuthor = "RequireAuthor";

    /// <summary>Requires the <c>Moderator</c> or <c>Admin</c> role.</summary>
    public const string RequireModerator = "RequireModerator";

    /// <summary>Requires the <c>Admin</c> role.</summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>Requires an authenticated user (access level <c>Registered</c>).</summary>
    public const string RegisteredAccess = "RegisteredAccess";
}
