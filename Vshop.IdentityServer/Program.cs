using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vshop.IdentityServer.Configuration;
using Vshop.IdentityServer.Data;
using Vshop.IdentityServer.SeedDataBase;
using Vshop.IdentityServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ===========================
// CONNECTION STRING
// ===========================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

// ===========================
// BANCO DO ASP.NET IDENTITY
// ===========================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ===========================
// ASP.NET CORE IDENTITY
// ===========================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ===========================
// DUENDE IDENTITYSERVER
// ===========================

builder.Services
    .AddIdentityServer()
    .AddInMemoryIdentityResources(
        IdentityConfiguration.IdentityResources)
    .AddInMemoryApiScopes(
        IdentityConfiguration.ApiScopes)
    .AddInMemoryClients(
        IdentityConfiguration.Clients(builder.Configuration))
    .AddAspNetIdentity<ApplicationUser>();

// ===========================
// PROFILE SERVICE
// ===========================

builder.Services.AddScoped<
    IProfileService,
    ProfileAppService>();

// ===========================
// SEED DO BANCO
// ===========================

builder.Services.AddScoped<
    IDataBaseSeedInitializer,
    DatabaseIdentityServerInitializer>();

var app = builder.Build();

// ===========================
// HTTP PIPELINE
// ===========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseIdentityServer();

app.UseAuthorization();

// ===========================
// SEED DE ROLES E USUÁRIOS
// ===========================

SeedDatabaseIdentityServer(app);

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ===========================
// MÉTODO DE SEED
// ===========================

void SeedDatabaseIdentityServer(WebApplication app)
{
    using var serviceScope =
        app.Services.CreateScope();

    var initializer =
        serviceScope.ServiceProvider
            .GetRequiredService<IDataBaseSeedInitializer>();

    initializer.InitializeSeedRoles();
    if (app.Environment.IsDevelopment())
        initializer.InitializeSeedUsers();
}
