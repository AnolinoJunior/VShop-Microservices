
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using VShop.CartiApi.Context;
using VShop.CartiApi.DTOs.Mappings;
using VShop.CartiApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ===========================
// CONTROLLERS + JSON
// ===========================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("DiscountApi", client =>
{
    var discountApiUri = builder.Configuration["ServicesUri:DiscountApi"]
        ?? throw new InvalidOperationException("ServicesUri:DiscountApi não foi configurado.");
    client.BaseAddress = new Uri(discountApiUri.TrimEnd('/') + "/");
});
builder.Services.AddHttpClient("ProductApi", client =>
{
    var productApiUri = builder.Configuration["ServicesUri:ProductApi"]
        ?? throw new InvalidOperationException("ServicesUri:ProductApi não foi configurado.");
    client.BaseAddress = new Uri(productApiUri.TrimEnd('/') + "/");
});
builder.Services.AddHttpClient("OrderApi", client =>
{
    var uri = builder.Configuration["ServicesUri:OrderApi"]
        ?? throw new InvalidOperationException("ServicesUri:OrderApi não foi configurado.");
    client.BaseAddress = new Uri(uri.TrimEnd('/') + "/");
});

// ===========================
// SWAGGER
// ===========================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VShop.CartiApi",
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
                    }
                },
                new List<string>()
            }
        });
});

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
// AUTOMAPPER
// ===========================

builder.Services.AddAutoMapper(
    cfg => { },
    typeof(MappingProfile).Assembly);

// ===========================
// CORS
// ===========================

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ===========================
// REPOSITORIES
// ===========================

builder.Services.AddScoped<ICartRepository, CartRepository>();

// ===========================
// SERVICES
// ===========================

// Adicionaremos os serviços específicos
// quando criarmos essa camada no CartApi.

// ===========================
// AUTENTICAÇÃO JWT
// ===========================

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority =
            builder.Configuration["ServicesUri:IdentityServer"]
            ?? throw new InvalidOperationException(
                "ServicesUri:IdentityServer não foi configurado.");

        // Preserva os nomes originais das claims
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
    options.AddPolicy("ApiScope", policy =>
    {
        policy.RequireAuthenticatedUser();

        policy.RequireClaim(
            "scope",
            "Vshop.CartApi");
    });
});

// ===========================
// APP
// ===========================

var app = builder.Build();

// ===========================
// HTTP REQUEST PIPELINE
// ===========================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS
app.UseCors("CorsPolicy");

// Primeiro autentica o usuário
app.UseAuthentication();

// Depois verifica as permissões
app.UseAuthorization();

app.MapControllers();

app.Run();
