
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Vshop.IdentityServer
{
    public static class Config
    {
        // =====================================
        // 1 - RECURSOS DE IDENTIDADE
        // =====================================

        public static IEnumerable<IdentityResource> IdentityResources =>
            new IdentityResource[]
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email()
            };

        // =====================================
        // 2 - ESCOPOS DAS APIS
        // =====================================

        public static IEnumerable<ApiScope> ApiScopes =>
            new ApiScope[]
            {
                new ApiScope(
                    "Vshop.ProductApi",
                    "Product API"),

                new ApiScope(
                    "Vshop.CartApi",
                    "Cart API"),

                new ApiScope("read", "Read Access"),
                new ApiScope("write", "Write Access"),
                new ApiScope("delete", "Delete Access")
            };

        // =====================================
        // 3 - CLIENTE MVC
        // =====================================

        public static IEnumerable<Client> Clients =>
            new Client[]
            {
                new Client
                {
                    ClientId = "Vshop.Web",

                    ClientName = "Vshop Web",

                    AllowedGrantTypes = GrantTypes.Code,

                    RequirePkce = true,

                    RequireClientSecret = false,

                    RedirectUris =
                    {
                        "https://localhost:7116/signin-oidc"
                    },

                    PostLogoutRedirectUris =
                    {
                        "https://localhost:7116/signout-callback-oidc"
                    },

                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Email,

                        "Vshop.ProductApi",
                        "Vshop.CartApi",

                        "read",
                        "write",
                        "delete"
                    },

                    AllowOfflineAccess = true
                }
            };

        // =====================================
        // 4 - USUÁRIOS DE TESTE
        // =====================================

        public static List<TestUser> Users => new();
    }
}
