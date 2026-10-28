using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Vshop.IdentityServer.Configuration;
using Vshop.IdentityServer.Data;

namespace Vshop.IdentityServer.Pages.Create;

[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ApplicationDbContext _db;
    private readonly IIdentityServerInteractionService _interaction;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Index(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles,
        ApplicationDbContext db, IIdentityServerInteractionService interaction)
    {
        _users = users;
        _roles = roles;
        _db = db;
        _interaction = interaction;
    }

    public async Task<IActionResult> OnGet(string? returnUrl)
    {
        if (!await IsSafeReturnUrl(returnUrl)) return BadRequest();
        Input = new InputModel { ReturnUrl = returnUrl };
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (!await IsSafeReturnUrl(Input.ReturnUrl)) return BadRequest();
        if (Input.Button != "create")
            return RedirectToPage("/Account/Login/Index", new { returnUrl = Input.ReturnUrl, registrationCompleted = true });
        if (!ModelState.IsValid) return Page();

        if (!await _roles.RoleExistsAsync(IdentityConfiguration.Client))
        {
            ModelState.AddModelError(string.Empty, "O cadastro está indisponível. Tente novamente mais tarde.");
            return Page();
        }

        // Serialize the email check and insert; Identity's default email index is not unique.
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await _users.FindByEmailAsync(Input.Email!.Trim()) != null)
        {
            ModelState.AddModelError("Input.Email", "Este e-mail já está cadastrado.");
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Username!.Trim(),
            Email = Input.Email!.Trim(),
            FirstName = Input.Name?.Trim() ?? string.Empty
        };
        var result = await _users.CreateAsync(user, Input.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        result = await _users.AddToRoleAsync(user, IdentityConfiguration.Client);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await transaction.CommitAsync();
        return RedirectToPage("/Account/Login/Index", new { returnUrl = Input.ReturnUrl, registrationCompleted = true });
    }

    private async Task<bool> IsSafeReturnUrl(string? returnUrl) =>
        string.IsNullOrEmpty(returnUrl) || Url.IsLocalUrl(returnUrl) ||
        await _interaction.GetAuthorizationContextAsync(returnUrl) != null;
}
