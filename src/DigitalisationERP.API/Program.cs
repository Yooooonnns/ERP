using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using DigitalisationERP.Application.Interfaces;
using DigitalisationERP.Application.Services;
using DigitalisationERP.Infrastructure.Data;
using DigitalisationERP.Infrastructure.Services;
using DigitalisationERP.API.Hubs;
using DigitalisationERP.API.Services;
using ProductionTokenService = DigitalisationERP.Infrastructure.Services.TokenService;
using ProductionITokenService = DigitalisationERP.Infrastructure.Services.ITokenService;
using DigitalisationERP.API;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Add SignalR for real-time communication
builder.Services.AddSignalR();

// Configure Database (PostgreSQL)
var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options
        .UseNpgsql(defaultConnectionString)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
);

// Configure JWT Authentication
var jwtSecret = builder.Configuration["JWT:Secret"] ?? "your-super-secret-key-at-least-32-characters-long!";
var jwtIssuer = builder.Configuration["JWT:Issuer"] ?? "DigitalisationERP";
var jwtAudience = builder.Configuration["JWT:Audience"] ?? "DigitalisationERP";

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
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Register MediatR and application services from Application layer
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DigitalisationERP.Application.Services.SensorSimulationService).Assembly));

// Register core application services (without Identity layer)
builder.Services.AddScoped<ProductionITokenService, ProductionTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISeedDataService, SeedDataService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Register simulation services
builder.Services.AddSingleton<SensorSimulationService>();
builder.Services.AddSingleton<ProductionSimulationService>();
builder.Services.AddSingleton<MaintenanceAlertManager>();
builder.Services.AddSingleton<MaintenanceHealthScoreCalculationService>();
builder.Services.AddSingleton<RealTimeSimulationIntegrator>();

// Register background streaming service
// TEMPORARILY DISABLED FOR DEBUGGING
//builder.Services.AddSingleton<RealtimeStreamingService>();
//builder.Services.AddHostedService(provider => provider.GetRequiredService<RealtimeStreamingService>());

// Add CORS for future frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

// app.UseHttpsRedirection(); // Disabled for HTTP-only development
app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map SignalR hubs for real-time communication
app.MapHub<RealtimeSimulationHub>("/hubs/realtime-simulation");

// Health check endpoint
app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow })
    .WithName("HealthCheck");

// Initialize database and seed test account on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        // Apply EF Core migrations (creates/updates schema)
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        // Log but don't crash on startup
        Console.WriteLine($"Migration warning: {ex.Message}");
    }

    try
    {
        // Ensure schema for newer modules (EnsureCreated does not evolve schema).
        DigitalisationERP.API.Services.SqliteSchemaBootstrapper.EnsureStockDiagramSchema(dbContext);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Schema bootstrap warning: {ex.Message}");
    }

    try
    {
        // Centralized seeding (roles/authorizations + optionally configured users)
        var seedService = scope.ServiceProvider.GetRequiredService<ISeedDataService>();
        seedService.SeedAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        // Log but don't crash on startup
        Console.WriteLine($"Seeding warning: {ex.Message}");
    }
}

try
{
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL ERROR: {ex}");
    Console.WriteLine($"Inner exception: {ex.InnerException}");
    throw;
}
