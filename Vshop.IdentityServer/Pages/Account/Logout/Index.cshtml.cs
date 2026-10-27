// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vshop.IdentityServer.Data;

namespace Vshop.IdentityServer.Pages.Account.Logout;

[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;
    private readonly SignInManager<ApplicationUser> _signInManager;

    [BindProperty]
    public string? LogoutId { get; set; }

    public Index(
        IIdentityServerInteractionService interaction,
        IEventService events,
        SignInManager<ApplicationUser> signInManager)
    {
        _interaction = interaction;
        _events = events;
        _signInManager = signInManager;
    }

    public async Task<IActionResult> OnGet(string? logoutId)
    {
        LogoutId = logoutId;

        var showLogoutPrompt = true;

        if (User.Identity?.IsAuthenticated != true)
        {
            showLogoutPrompt = false;
        }
        else
        {
            var context =
                await _interaction.GetLogoutContextAsync(LogoutId);

            if (context?.ShowSignoutPrompt == false)
            {
                showLogoutPrompt = false;
            }
        }

        if (!showLogoutPrompt)
        {
            return await OnPost();
        }

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            LogoutId ??=
                await _interaction.CreateLogoutContextAsync();

            var idp =
                User.FindFirst(
                    JwtClaimTypes.IdentityProvider)?.Value;

            var subjectId = User.GetSubjectId();
            var displayName = User.GetDisplayName();

            // Remove o cookie do ASP.NET Core Identity
            await _signInManager.SignOutAsync();

            // Registra o evento de logout
            await _events.RaiseAsync(
                new UserLogoutSuccessEvent(
                    subjectId,
                    displayName));

            Telemetry.Metrics.UserLogout(idp);

            // Se o login veio de um provedor externo,
            // também tenta efetuar logout nele
            if (idp != null &&
                idp != Duende.IdentityServer
                    .IdentityServerConstants.LocalIdentityProvider)
            {
                if (await HttpContext
                    .GetSchemeSupportsSignOutAsync(idp))
                {
                    var url = Url.Page(
                        "/Account/Logout/LoggedOut",
                        new { logoutId = LogoutId });

                    return SignOut(
                        new AuthenticationProperties
                        {
                            RedirectUri = url
                        },
                        idp);
                }
            }
        }

        return RedirectToPage(
            "/Account/Logout/LoggedOut",
            new { logoutId = LogoutId });
    }
}