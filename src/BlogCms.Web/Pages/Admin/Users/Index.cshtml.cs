using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BlogCms.Web.Pages.Admin.Users;

/// <summary>
/// Administrative user & role management (FeatureFix1 BR-014).
/// Synchronizes the primary <see cref="User.Role"/> enum with ASP.NET Core Identity roles.
/// </summary>
[Authorize(Policy = Policies.RequireAdmin)]
public class IndexModel : PageModel
{
    private readonly UserManager<User> _userManager;

    public IndexModel(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public IReadOnlyList<User> Users { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Users = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.DeletedAt == null)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostSetRoleAsync(Guid userId, UserRole role)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletedAt is not null)
        {
            return NotFound();
        }

        // Synchronize Identity roles with the selected primary UserRole.
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        var roleName = role.ToString();
        await _userManager.AddToRoleAsync(user, roleName);

        user.Role = role;
        await _userManager.UpdateAsync(user);

        TempData["Message"] = $"Rolle für „{user.DisplayName}“ ({user.Email}) auf {roleName} gesetzt.";
        return RedirectToPage();
    }
}
