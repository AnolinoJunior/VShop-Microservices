
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Vshop.Web.Services;
using Vshop.Web.Services.Contracts;
using VShop.Web.Services;
using VShop.Web.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);

// ===========================
// MVC
// ===========================

builder.Services.AddControllersWithViews();
// Duende.AccessTokenManagement.OpenIdConnect é usado para gerenciar tokens de acesso
builder.Services.AddOpenIdConnectAccessTokenManagement();

// ===========================
// SERVICES URI
// ===========================

var productApiUri =
    builder.Configuration["ServicesUri:ProductApi"]
    ?? throw new InvalidOperationException(
        "ServicesUri:ProductApi não foi configurado.");

var cartApiUri =
    builder.Configuration["ServicesUri:CartApi"]
    ?? throw new InvalidOperationException(
        "ServicesUri:CartApi não foi configurado.");

var discountApiUri =
    builder.Configuration["ServicesUri:DiscountApi"]
    ?? throw new InvalidOperationException(
        "ServicesUri:DiscountApi não foi configurado.");

var identityServerUri =
    builder.Configuration["ServicesUri:IdentityServer"]
    ?? throw new InvalidOperationException(
        "ServicesUri:IdentityServer não foi configurado.");

if (!Uri.TryCreate(identityServerUri, UriKind.Absolute, out var identityServerAddress) ||
    (identityServerAddress.Scheme != Uri.UriSchemeHttps && identityServerAddress.Scheme != Uri.UriSchemeHttp) ||
    !string.IsNullOrEmpty(identityServerAddress.Query) || !string.IsNullOrEmpty(identityServerAddress.Fragment) ||
    !string.IsNullOrEmpty(identityServerAddress.UserInfo))
    throw new InvalidOperationException("ServicesUri:IdentityServer deve ser uma URL HTTP(S) absoluta, sem credenciais, consulta ou fragmento.");

var clientSecret =
    builder.Configuration["Client:Secret"]
    ?? throw new InvalidOperationException(
        "Client:Secret não foi configurado.");
if (string.IsNullOrWhiteSpace(clientSecret))
    throw new InvalidOperationException("Client:Secret não foi configurado.");

// ===========================
// HTTP CLIENTS
// ===========================

// ProductApi
builder.Services.AddHttpClient("ProductApi", client =>
{
    client.BaseAddress = new Uri(
        productApiUri.TrimEnd('/') + "/");
});

// CartApi
builder.Services.AddHttpClient("CartApi", client =>
{
    client.BaseAddress = new Uri(
        cartApiUri.TrimEnd('/') + "/");
});

builder.Services.AddHttpClient("DiscountApi", client =>
{
    client.BaseAddress = new Uri(discountApiUri.TrimEnd('/') + "/");
});

// ===========================
// SERVICES
// ===========================

// ProductApi
builder.Services.AddScoped<
    IProductService,
    ProductService>();

builder.Services.AddScoped<
    ICategoryService,
    CategoryService>();

// CartApi
builder.Services.AddScoped<
    ICartService,
    CartService>();

// ===========================
// AUTENTICAÇÃO
// ===========================

builder.Services
    .AddAuthentication(options =>
    {
        // Mantém a sessão do usuário
        options.DefaultScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        // Redireciona para o IdentityServer
        options.DefaultChallengeScheme =
            OpenIdConnectDefaults.AuthenticationScheme;
    })

    // ===========================
    // COOKIE
    // ===========================

    .AddCookie(options =>
    {
        options.ExpireTimeSpan =
            TimeSpan.FromMinutes(30);

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToAccessDenied = context =>
            {
                context.Response.Redirect(
                    "/Home/AccessDenied");

                return Task.CompletedTask;
            }
        };
    })

    // ===========================
    // OPENID CONNECT
    // ===========================

    .AddOpenIdConnect(options =>
    {
        options.Authority =
            identityServerUri;

        options.ClientId =
            "Vshop.Web";

        options.ClientSecret =
            clientSecret;

        // Authorization Code Flow + PKCE
        options.ResponseType =
            "code";

        options.UsePkce = true;

        options.GetClaimsFromUserInfoEndpoint = true;

        options.SaveTokens = true;

        // ===========================
        // EVENTOS
        // ===========================

        options.Events.OnRedirectToIdentityProvider = context =>
        {
            if (context.Properties.Items.Remove("vshop_register"))
            {
                context.ProtocolMessage.SetParameter("vshop_register", "1");
                context.ProtocolMessage.Prompt = "login";
            }
            return Task.CompletedTask;
        };

        options.Events.OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("OpenIdConnect");

            logger.LogError(
                context.Failure,
                "Falha na autenticação OpenID Connect.");

            context.HandleResponse();

            context.Response.Redirect("/");

            return Task.CompletedTask;
        };

        // ===========================
        // CLAIMS
        // ===========================

        options.ClaimActions.MapJsonKey(
            "role",
            "role");

        options.ClaimActions.MapJsonKey(
            "name",
            "name");

        options.ClaimActions.MapJsonKey(
            "sub",
            "sub");

        options.TokenValidationParameters.NameClaimType =
            "name";

        options.TokenValidationParameters.RoleClaimType =
            "role";

        // ===========================
        // SCOPES
        // ===========================

        options.Scope.Clear();

        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        // ProductApi
        options.Scope.Add("Vshop.ProductApi");

        // CartApi
        options.Scope.Add("Vshop.CartApi");
        options.Scope.Add("Vshop.DiscountApi");
        options.Scope.Add("Vshop.OrderApi");

        options.Scope.Add("read");
        options.Scope.Add("write");
        options.Scope.Add("delete");

        options.Scope.Add("offline_access");
    });

// ===========================
// CULTURA PT-BR
// ===========================

var cultureInfo =
    new CultureInfo("pt-BR");

CultureInfo.DefaultThreadCurrentCulture =
    cultureInfo;

CultureInfo.DefaultThreadCurrentUICulture =
    cultureInfo;

var app = builder.Build();

app.UseRequestLocalization(
    new RequestLocalizationOptions
    {
        DefaultRequestCulture =
            new RequestCulture("pt-BR"),

        SupportedCultures =
            new[] { cultureInfo },

        SupportedUICultures =
            new[] { cultureInfo }
    });

// ===========================
// TRATAMENTO DE ERROS
// ===========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ===========================
// MIDDLEWARE
// ===========================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

// ===========================
// ROTAS
// ===========================

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}");

app.Run(); builder.Services.AddControllersWithViews();

