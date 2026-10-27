using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using VShop.DiscountApi.Context;
using VShop.DiscountApi.DTOs.Mappings;
using VShop.DiscountApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "VShop.DiscountApi", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Digite Bearer, espaço e depois o token JWT.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não foi configurada.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<VShop.DiscountApi.Services.CouponReservationService>();
builder.Services.AddHostedService<VShop.DiscountApi.Services.CouponExpirationWorker>();
builder.Services.AddHttpClient("CartApi", client => client.BaseAddress = new Uri(
    (builder.Configuration["ServicesUri:CartApi"] ?? throw new InvalidOperationException("ServicesUri:CartApi não foi configurado.")).TrimEnd('/') + "/"));

var identityServer = builder.Configuration["ServicesUri:IdentityServer"]
    ?? throw new InvalidOperationException("ServicesUri:IdentityServer não foi configurado.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = identityServer;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiScope", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "Vshop.DiscountApi"));
    options.AddPolicy("OrderSaga", policy => policy.RequireAuthenticatedUser()
        .RequireClaim("client_id", "Vshop.OrderSaga").RequireClaim("scope", "Vshop.OrderSaga.Coupon"));
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
