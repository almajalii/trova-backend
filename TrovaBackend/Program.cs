using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TrovaBackend.Data;
using TrovaBackend.Middleware;
using TrovaBackend.Services;
using TrovaBackend.Services.Auth;
using TrovaBackend.Services.Projects;

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
builder.Services.AddScoped<ICompanyDetailsService, CompanyDetailsService>(); builder.Services.Configure<TrovaBackend.Services.CompanyDetails.CompanyClassificationOptions>(
builder.Configuration.GetSection("CompanyClassification"));
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<TrovaBackend.Services.Bids.IBidService, TrovaBackend.Services.Bids.BidService>();
builder.Services.AddScoped<TrovaBackend.Services.Guarantees.IGuaranteeService, TrovaBackend.Services.Guarantees.GuaranteeService>();
builder.Services.Configure<TrovaBackend.Services.Guarantees.GuaranteeStorageOptions>(
    builder.Configuration.GetSection("Storage"));
builder.Services.AddScoped<TrovaBackend.Services.ReviewWork.IReviewWorkService, TrovaBackend.Services.ReviewWork.ReviewWorkService>();
builder.Services.AddScoped<TrovaBackend.Services.RepostProject.IRepostProjectService, TrovaBackend.Services.RepostProject.RepostProjectService>();
builder.Services.AddScoped<TrovaBackend.Services.LeaveReview.ILeaveReviewService, TrovaBackend.Services.LeaveReview.LeaveReviewService>();
builder.Services.AddScoped<TrovaBackend.Services.CompanyProfile.ICompanyProfileService, TrovaBackend.Services.CompanyProfile.CompanyProfileService>();
builder.Services.AddScoped<TrovaBackend.Services.Admin.IAdminService, TrovaBackend.Services.Admin.AdminService>();
builder.Services.AddScoped<TrovaBackend.Services.Notifications.INotificationService, TrovaBackend.Services.Notifications.NotificationService>();
// Bank connection — driven by Jofs:UseMock in appsettings. Flip that flag
// (plus BaseUrl/AuthorizationHeader/BankCustomerIds) once sandbox creds
// are filled in — everything else in the codebase stays the same either way.
builder.Services.Configure<TrovaBackend.Services.BankConnection.JofsApiOptions>(
    builder.Configuration.GetSection("Jofs"));

var jofsOptions = builder.Configuration.GetSection("Jofs").Get<TrovaBackend.Services.BankConnection.JofsApiOptions>()
    ?? new TrovaBackend.Services.BankConnection.JofsApiOptions();

if (jofsOptions.UseMock)
{
    builder.Services.AddScoped<TrovaBackend.Services.BankConnection.IJofsDataProvider, TrovaBackend.Services.BankConnection.MockJofsDataProvider>();
}
else
{
    // No shared BaseAddress here — RealJofsDataProvider builds a full
    // absolute URL per call since Accounts/Transactions/Loans each live
    // on their own gateway path (see JofsApiOptions).
    builder.Services.AddHttpClient<TrovaBackend.Services.BankConnection.IJofsDataProvider, TrovaBackend.Services.BankConnection.RealJofsDataProvider>();
}
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
builder.Services.AddControllers(options =>
{
    // Blocks non-approved users from every endpoint except the ones that
    // have to work pre-approval â€” see ApprovalGateFilter for the full list.
    options.Filters.Add<TrovaBackend.Middleware.ApprovalGateFilter>();
});

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
