
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using VShop.CartiApi.Context;
using VShop.CartiApi.DTOs.Mappings;
using VShop.CartiApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

// =====================================
// CONTROLLERS + JSON
// =====================================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddHttpContextAccessor();

// =====================================
// HTTP CLIENTS
// =====================================

// DiscountApi
builder.Services.AddHttpClient("DiscountApi", client =>
{
    var discountApiUri =
        builder.Configuration["ServicesUri:DiscountApi"]
        ?? throw new InvalidOperationException(
            "ServicesUri:DiscountApi não foi configurado.");

    client.BaseAddress = new Uri(
        discountApiUri.TrimEnd('/') + "/");
});

// ProductApi
builder.Services.AddHttpClient("ProductApi", client =>
{
    var productApiUri =
        builder.Configuration["ServicesUri:ProductApi"]
        ?? throw new InvalidOperationException(
            "ServicesUri:ProductApi não foi configurado.");

    client.BaseAddress = new Uri(
        productApiUri.TrimEnd('/') + "/");
});

// OrderApi
builder.Services.AddHttpClient("OrderApi", client =>
{
    var orderApiUri =
        builder.Configuration["ServicesUri:OrderApi"]
        ?? throw new InvalidOperationException(
            "ServicesUri:OrderApi não foi configurado.");

    client.BaseAddress = new Uri(
        orderApiUri.TrimEnd('/') + "/");
});

// =====================================
// SWAGGER + AUTENTICAÇÃO JWT
// =====================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VShop.CartiApi",
        Version = "v1"
    });

    // Configuração HTTP Bearer
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Description =
                "Informe somente o token JWT, sem o prefixo Bearer.",

            Name = "Authorization",

            In = ParameterLocation.Header,

            Type = SecuritySchemeType.Http,

            Scheme = "bearer",

            BearerFormat = "JWT"
        });

    // Exige autenticação nos endpoints do Swagger
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

// =====================================
// BANCO DE DADOS - SQL SERVER
// =====================================

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' não foi configurada.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// =====================================
// AUTOMAPPER
// =====================================

builder.Services.AddAutoMapper(
    cfg => { },
    typeof(MappingProfile).Assembly);

// =====================================
// CORS
// =====================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// =====================================
// REPOSITORIES
// =====================================

builder.Services.AddScoped<
    ICartRepository,
    CartRepository>();

// =====================================
// AUTENTICAÇÃO JWT - IDENTITYSERVER
// =====================================

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority =
            builder.Configuration["ServicesUri:IdentityServer"]
            ?? throw new InvalidOperationException(
                "ServicesUri:IdentityServer não foi configurado.");

        // Mantém os nomes originais das claims
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateAudience = false,

                NameClaimType = "name",

                RoleClaimType = "role"
            };
    });

// =====================================
// AUTORIZAÇÃO
// =====================================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiScope", policy =>
    {
        policy.RequireAuthenticatedUser();

        // Exige o escopo da CartApi
        policy.RequireClaim(
            "scope",
            "Vshop.CartApi");
    });
});

// =====================================
// BUILD
// =====================================

var app = builder.Build();

// =====================================
// SWAGGER - APENAS DEVELOPMENT
// =====================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// =====================================
// HTTP REQUEST PIPELINE
// =====================================

app.UseHttpsRedirection();

// CORS
app.UseCors("CorsPolicy");

// Autenticação
app.UseAuthentication();

// Autorização
app.UseAuthorization();

// Controllers
app.MapControllers();

// =====================================
// EXECUTAR APLICAÇÃO
// =====================================

app.Run();
