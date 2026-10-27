using System.Security.Claims;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Identity;
using Vshop.IdentityServer.Configuration;
using Vshop.IdentityServer.Data;

namespace Vshop.IdentityServer.SeedDataBase
{
    public class DatabaseIdentityServerInitializer
        : IDataBaseSeedInitializer
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public DatabaseIdentityServerInitializer(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _environment = environment;
        }

        public void InitializeSeedRoles()
        {
            // Role Admin
            if (!_roleManager
                .RoleExistsAsync(IdentityConfiguration.Admin)
                .Result)
            {
                var roleAdmin = new IdentityRole
                {
                    Name = IdentityConfiguration.Admin,
                    NormalizedName =
                        IdentityConfiguration.Admin.ToUpper()
                };

                _roleManager
                    .CreateAsync(roleAdmin)
                    .Wait();
            }

            // Role Client
            if (!_roleManager
                .RoleExistsAsync(IdentityConfiguration.Client)
                .Result)
            {
                var roleClient = new IdentityRole
                {
                    Name = IdentityConfiguration.Client,
                    NormalizedName =
                        IdentityConfiguration.Client.ToUpper()
                };

                _roleManager
                    .CreateAsync(roleClient)
                    .Wait();
            }
        }

        public void InitializeSeedUsers()
        {
            if (!_environment.IsDevelopment()) return;
            var adminPassword = _configuration["DevelopmentSeed:AdminPassword"];
            var clientPassword = _configuration["DevelopmentSeed:ClientPassword"];
            // ==========================
            // USUÁRIO ADMIN
            // ==========================

            if (!string.IsNullOrWhiteSpace(adminPassword) && _userManager
                .FindByEmailAsync("admin1@com.br")
                .Result == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin1",
                    NormalizedUserName = "ADMIN1",

                    Email = "admin1@com.br",
                    NormalizedEmail = "ADMIN1@COM.BR",

                    EmailConfirmed = true,
                    LockoutEnabled = false,

                    PhoneNumber = "+55 (11) 12345-6789",

                    FirstName = "Usuario",
                    LastName = "Admin1",

                    SecurityStamp =
                        Guid.NewGuid().ToString()
                };

                var resultAdmin =
                    _userManager
                        .CreateAsync(
                            admin,
                            adminPassword)
                        .Result;

                if (!resultAdmin.Succeeded)
                    throw new InvalidOperationException("Não foi possível criar o administrador de desenvolvimento. Verifique os requisitos de senha do ASP.NET Identity.");

                if (resultAdmin.Succeeded)
                {
                    // Adiciona à role Admin
                    _userManager
                        .AddToRoleAsync(
                            admin,
                            IdentityConfiguration.Admin)
                        .Wait();

                    // Claims do Admin
                    _userManager
                        .AddClaimsAsync(
                            admin,
                            new Claim[]
                            {
                                new Claim(
                                    JwtClaimTypes.Name,
                                    $"{admin.FirstName} {admin.LastName}"),

                                new Claim(
                                    JwtClaimTypes.GivenName,
                                    admin.FirstName),

                                new Claim(
                                    JwtClaimTypes.FamilyName,
                                    admin.LastName),

                                new Claim(
                                    JwtClaimTypes.Role,
                                    IdentityConfiguration.Admin)
                            })
                        .Wait();
                }
            }

            // ==========================
            // USUÁRIO CLIENT
            // ==========================

            if (!string.IsNullOrWhiteSpace(clientPassword) && _userManager
                .FindByEmailAsync("client1@com.br")
                .Result == null)
            {
                var client = new ApplicationUser
                {
                    UserName = "client1",
                    NormalizedUserName = "CLIENT1",

                    Email = "client1@com.br",
                    NormalizedEmail = "CLIENT1@COM.BR",

                    EmailConfirmed = true,
                    LockoutEnabled = false,

                    PhoneNumber = "+55 (11) 12345-6789",

                    FirstName = "Usuario",
                    LastName = "Client1",

                    SecurityStamp =
                        Guid.NewGuid().ToString()
                };

                var resultClient =
                    _userManager
                        .CreateAsync(
                            client,
                            clientPassword)
                        .Result;

                if (!resultClient.Succeeded)
                    throw new InvalidOperationException("Não foi possível criar o cliente de desenvolvimento. Verifique os requisitos de senha do ASP.NET Identity.");

                if (resultClient.Succeeded)
                {
                    // Adiciona à role Client
                    _userManager
                        .AddToRoleAsync(
                            client,
                            IdentityConfiguration.Client)
                        .Wait();

                    // Claims do Client
                    _userManager
                        .AddClaimsAsync(
                            client,
                            new Claim[]
                            {
                                new Claim(
                                    JwtClaimTypes.Name,
                                    $"{client.FirstName} {client.LastName}"),

                                new Claim(
                                    JwtClaimTypes.GivenName,
                                    client.FirstName),

                                new Claim(
                                    JwtClaimTypes.FamilyName,
                                    client.LastName),

                                new Claim(
                                    JwtClaimTypes.Role,
                                    IdentityConfiguration.Client)
                            })
                        .Wait();
                }
            }
        }
    }
}
