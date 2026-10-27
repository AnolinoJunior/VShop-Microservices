using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Vshop.IdentityServer.Data;

namespace Vshop.IdentityServer.Services
{
    public class ProfileAppService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsPrincipalFactory;

        public ProfileAppService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _claimsPrincipalFactory = claimsPrincipalFactory;
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            // id do usuário no IS
            string id = context.Subject.GetSubjectId();
            // buscar o usuário no banco de dados
            ApplicationUser user = await _userManager.FindByIdAsync(id);
            // criar ClaimsPrincipal para o usuário
            ClaimsPrincipal principal = await _claimsPrincipalFactory.CreateAsync(user);
            //define uma coleção de claims para o usuário
            List<Claim> claims = principal.Claims.ToList();
            claims.Add(new Claim(JwtClaimTypes.FamilyName, user.LastName));
            claims.Add(new Claim(JwtClaimTypes.GivenName, user.FirstName));

            // se o userManager do  identity suportar role
            if (_userManager.SupportsUserRole)
            {
                // buscar as roles do usuário
                IList<string> roles = await _userManager.GetRolesAsync(user);
                foreach (string roleName in roles)
                {
                    claims.Add(new Claim(JwtClaimTypes.Role, roleName));
                    // se o roleManager do identity suportar claims
                    if (_roleManager.SupportsRoleClaims)
                    {
                        // buscar a role no banco de dados
                        IdentityRole identityRole = await _roleManager.FindByNameAsync(roleName);
                        if (identityRole != null)
                        {
                            // buscar as claims da role
                            claims.AddRange(await _roleManager.GetClaimsAsync(identityRole));

                        }
                        
                    }
                }
            }
            // retornar as claims para o IdentityServer
            context.IssuedClaims = claims;

        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            //obtem o id do usuário no IdentityServer
            string userid = context.Subject.GetSubjectId();
            //busca o usuário no banco de dados
            ApplicationUser user = await _userManager.FindByIdAsync(userid);

            // verifica se o usuário está ativo
            context.IsActive = user is not null;
        }
    }
}
