using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using VShop.OrderApi.Context;
using VShop.OrderApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var authority = builder.Configuration["IdentityServer:Authority"]
    ?? throw new InvalidOperationException("IdentityServer authority not found.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<VShop.OrderApi.Services.OrderPreparationService>();
builder.Services.AddScoped<VShop.OrderApi.Services.CheckoutSagaService>();
builder.Services.AddScoped<VShop.OrderApi.Services.SagaRemoteClient>();
builder.Services.AddHostedService<VShop.OrderApi.Services.CheckoutSagaWorker>();
builder.Services.AddHttpClient("CartApi", client => client.BaseAddress = new Uri(
    (builder.Configuration["ServicesUri:CartApi"] ?? throw new InvalidOperationException("ServicesUri:CartApi não configurado.")).TrimEnd('/') + "/"));
builder.Services.AddHttpClient("SagaIdentity", client => { client.BaseAddress = new Uri(authority.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(10); });
builder.Services.AddHttpClient("SagaProduct", client => { client.BaseAddress = new Uri((builder.Configuration["ServicesUri:ProductApi"] ?? throw new InvalidOperationException("ServicesUri:ProductApi não configurado.")).TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(10); });
builder.Services.AddHttpClient("SagaDiscount", client => { client.BaseAddress = new Uri((builder.Configuration["ServicesUri:DiscountApi"] ?? throw new InvalidOperationException("ServicesUri:DiscountApi não configurado.")).TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(10); });
builder.Services.AddScoped<VShop.OrderApi.Services.IPaymentService, VShop.OrderApi.Services.SimulatedPaymentService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VShop.OrderApi",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("ApiScope", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("scope", "Vshop.OrderApi")));

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
