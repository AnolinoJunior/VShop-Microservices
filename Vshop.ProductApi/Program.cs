using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Vshop.ProductApi.Context;
using Vshop.ProductApi.DTOs.Mappings;
using Vshop.ProductApi.Repositories;
using Vshop.ProductApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers + JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    });

// ===========================
// SWAGGER
// ===========================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Vshop.ProductApi",
        Version = "v1"
    });

    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Description =
                "Digite Bearer, espaço e depois o token JWT.",

            Name = "Authorization",

            In = ParameterLocation.Header,

            Type = SecuritySchemeType.ApiKey,

            Scheme = "Bearer"
        });

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
                    },

                    Scheme = "oauth2",

                    Name = "Bearer",

                    In = ParameterLocation.Header
                },

                new List<string>()
            }
        });
});

// ===========================
// AUTOMAPPER
// ===========================

builder.Services.AddAutoMapper(
    cfg => { },
    typeof(MappingProfile).Assembly);

// ===========================
// BANCO DE DADOS
// ===========================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' não foi configurada.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// ===========================
// REPOSITORIES
// ===========================

builder.Services.AddScoped<
    ICategoryRepository,
    CategoryRepository>();

builder.Services.AddScoped<
    IProductRepository,
    ProductRepository>();

// ===========================
// SERVICES
// ===========================

builder.Services.AddScoped<
    ICategoryService,
    CategoryService>();

builder.Services.AddScoped<
    IProductService,
    ProductService>();
builder.Services.AddScoped<StockReservationService>();
builder.Services.AddHostedService<StockExpirationWorker>();

// ===========================
// AUTENTICAÇÃO JWT
// ===========================

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)

    .AddJwtBearer(options =>
    {
        options.Authority =
            builder.Configuration[
                "ServicesUri:IdentityServer"];

        // Mantém os nomes originais das claims
        // recebidas no JWT:
        // role, name, sub etc.
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateAudience = false,

                NameClaimType = "name",

                RoleClaimType = "role"
            };
    });

// ===========================
// AUTORIZAÇÃO
// ===========================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrderSaga", policy => policy.RequireAuthenticatedUser()
        .RequireClaim("client_id", "Vshop.OrderSaga").RequireClaim("scope", "Vshop.OrderSaga.Stock"));
    options.AddPolicy(
        "ApiScope",
        policy =>
        {
            policy.RequireAuthenticatedUser();

            policy.RequireClaim(
                "scope",
                "Vshop.ProductApi");
        });
});

// ===========================
// APP
// ===========================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Primeiro valida o JWT
app.UseAuthentication();

// Depois verifica as permissões/roles
app.UseAuthorization();

app.MapControllers();

app.Run();
