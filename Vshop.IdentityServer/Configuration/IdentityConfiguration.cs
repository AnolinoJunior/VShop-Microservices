
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityModel;

namespace Vshop.IdentityServer.Configuration
{
    public class IdentityConfiguration
    {
        // =====================================
        // 1 - ROLES
        // =====================================

        public const string Admin = "Admin";
        public const string Client = "Client";

        // =====================================
        // 2 - IDENTITY RESOURCES
        // =====================================

        public static IEnumerable<IdentityResource> IdentityResources =>
            new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Email(),
                new IdentityResources.Profile()
            };

        // =====================================
        // 3 - API SCOPES
        // =====================================

        public static IEnumerable<ApiScope> ApiScopes =>
            new List<ApiScope>
            {
                // ProductApi
                new ApiScope(
                    name: "Vshop.ProductApi",
                    displayName: "Product API"),

                // CartApi
                new ApiScope(
                    name: "Vshop.CartApi",
                    displayName: "Cart API"),

                new ApiScope(
                    name: "Vshop.DiscountApi",
                    displayName: "Discount API"),

                new ApiScope(
                    name: "Vshop.OrderApi",
                    displayName: "Order API"),

                new ApiScope("Vshop.OrderSaga.Stock", "Order saga stock operations"),
                new ApiScope("Vshop.OrderSaga.Coupon", "Order saga coupon operations"),

                // Permissões gerais
                new ApiScope(
                    name: "read",
                    displayName: "Read data."),

                new ApiScope(
                    name: "write",
                    displayName: "Write data."),

                new ApiScope(
                    name: "delete",
                    displayName: "Delete data.")
            };

        private static IEnumerable<Client> SagaClient(IConfiguration configuration)
        {
            var secret = configuration["VSHOP_SAGA_CLIENT_SECRET"];
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32) return Array.Empty<Client>();
            return new[]
            {
                new Client
                {
                    ClientId = "Vshop.OrderSaga",
                    ClientSecrets = { new Secret(secret.Sha256()) },
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = { "Vshop.OrderSaga.Stock", "Vshop.OrderSaga.Coupon" },
                    AccessTokenLifetime = 300
                }
            };
        }

        // =====================================
        // 4 - CLIENTS
        // =====================================

        public static IEnumerable<Client> Clients(IConfiguration configuration)
        {
            var serviceSecret = configuration["OAuth:ServiceClientSecret"];
            var webSecret = configuration["OAuth:WebClientSecret"];
            if (string.IsNullOrWhiteSpace(serviceSecret))
                throw new InvalidOperationException("OAuth:ServiceClientSecret não foi configurado.");
            if (string.IsNullOrWhiteSpace(webSecret))
                throw new InvalidOperationException("OAuth:WebClientSecret não foi configurado.");
            return new List<Client>
            {
                // =====================================
                // CLIENT - SERVIÇO PARA SERVIÇO
                // =====================================

                new Client
                {
                    ClientId = "client",

                    ClientSecrets =
                    {
                        new Secret(
                            serviceSecret.Sha256())
                    },

                    AllowedGrantTypes =
                        GrantTypes.ClientCredentials,

                    AllowedScopes =
                    {
                        "read",
                        "write",
                        "delete",
                        "Vshop.ProductApi",
                        "Vshop.CartApi",
                        "Vshop.DiscountApi"
                    }
                },

                // =====================================
                // VSHOP.WEB - LOGIN DO USUÁRIO
                // =====================================

                new Client
                {
                    ClientId = "Vshop.Web",

                    ClientName = "VShop Web",

                    ClientSecrets =
                    {
                        new Secret(
                            webSecret.Sha256())
                    },

                    AllowedGrantTypes =
                        GrantTypes.Code,

                    RequirePkce = true,

                    RequireClientSecret = true,

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
                        "Vshop.DiscountApi",
                        "Vshop.OrderApi",

                        "read",
                        "write",
                        "delete"
                    },

                    AllowOfflineAccess = true
                }
            }.Concat(SagaClient(configuration));
        }
    }
}
