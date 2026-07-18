using TrovaBackend.Data;
using TrovaBackend.Middleware;
using TrovaBackend.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Database (Supabase Postgres)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// In-memory cache
builder.Services.AddMemoryCache();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<TrovaBackend.Services.IEmailService, TrovaBackend.Services.EmailService>();
builder.Services.AddScoped<TrovaBackend.Services.CompanyDetails.ICompanyDetailsService, TrovaBackend.Services.CompanyDetails.CompanyDetailsService>();
builder.Services.Configure<TrovaBackend.Services.CompanyDetails.CompanyClassificationOptions>(
    builder.Configuration.GetSection("CompanyClassification"));

// Bank connection — MockJofsDataProvider stands in for the real JOFS
// sandbox client. Swap this one registration to go live later.
builder.Services.AddScoped<TrovaBackend.Services.BankConnection.IJofsDataProvider, TrovaBackend.Services.BankConnection.MockJofsDataProvider>();
builder.Services.AddScoped<TrovaBackend.Services.BankConnection.IBankConnectionService, TrovaBackend.Services.BankConnection.BankConnectionService>();

// Capability score
builder.Services.AddScoped<TrovaBackend.Services.CapabilityScore.ICapabilityScoreService, TrovaBackend.Services.CapabilityScore.CapabilityScoreService>();
builder.Services.Configure<TrovaBackend.Services.CapabilityScore.ScoringOptions>(
    builder.Configuration.GetSection("ScoringConfig"));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

// CORS - wide open for now since frontend origin isn't known yet.
// Tighten this once the frontend domain is finalized.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Trova API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
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

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
