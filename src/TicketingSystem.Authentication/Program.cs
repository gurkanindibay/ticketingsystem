using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Reflection;
using System.Text;
using TicketingSystem.Shared.Data;
using TicketingSystem.Shared.Models;
using TicketingSystem.Authentication.Services;

var builder = WebApplication.CreateBuilder(args);

// Database configuration - supports both in-memory and PostgreSQL
var useInMemoryDatabase = builder.Configuration.GetSection("DatabaseSettings")["UseInMemoryDatabase"];

builder.Services.AddDbContext<TicketingDbContext>(options =>
{
    if (string.Equals(useInMemoryDatabase, "true", StringComparison.OrdinalIgnoreCase))
    {
        options.UseInMemoryDatabase("TicketingSystemDb");
    }
    else
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// Identity configuration
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;

    // Sign in settings
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<TicketingDbContext>()
.AddDefaultTokenProviders();

// JWT configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Register application services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthValidationService, AuthValidationService>();

// Built-in .NET Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Registration rate limiting: 5 per hour per IP
    options.AddPolicy("register", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = int.Parse(builder.Configuration["RateLimiting:Registration:MaxAttempts"] ?? "5"),
                Window = TimeSpan.FromMinutes(int.Parse(builder.Configuration["RateLimiting:Registration:WindowMinutes"] ?? "60")),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0 // No queuing
            }));

    // Login rate limiting: 10 per 15 minutes per IP
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = int.Parse(builder.Configuration["RateLimiting:Login:MaxAttempts"] ?? "10"),
                Window = TimeSpan.FromMinutes(int.Parse(builder.Configuration["RateLimiting:Login:WindowMinutes"] ?? "15")),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Refresh token rate limiting: 20 per 5 minutes per IP
    options.AddPolicy("refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = int.Parse(builder.Configuration["RateLimiting:Refresh:MaxAttempts"] ?? "20"),
                Window = TimeSpan.FromMinutes(int.Parse(builder.Configuration["RateLimiting:Refresh:WindowMinutes"] ?? "5")),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Global fallback - applies to endpoints without specific rate limiting
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100, // 100 requests per minute as global limit
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Rejection response
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        
        logger.LogWarning("Rate limit exceeded for IP: {ClientIp}, Endpoint: {Endpoint}", 
            clientIp, context.HttpContext.Request.Path);

        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.ContentType = "application/json";
        
        var response = System.Text.Json.JsonSerializer.Serialize(new
        {
            success = false,
            message = "Too many requests. Please try again later.",
            errorCode = "RATE_LIMIT_EXCEEDED"
        });
        
        await context.HttpContext.Response.WriteAsync(response, cancellationToken);
    };
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ticketing System - Authentication API",
        Version = "v1",
        Description = "Authentication microservice for the Ticketing System with ASP.NET Core Identity and JWT",
        Contact = new OpenApiContact
        {
            Name = "Ticketing System Team",
            Email = "support@ticketingsystem.com"
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add JWT Bearer Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
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
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    
    // Ensure database is created
    context.Database.EnsureCreated();
    
    // Seed roles
    await SeedRolesAsync(roleManager);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Authentication API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
    });
    
    // Disable HTTPS redirection in development for easier testing
    // app.UseHttpsRedirection();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseRateLimiter(); // Add rate limiting middleware
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
{
    string[] roles = { "Admin", "User", "Manager" };

    foreach (string role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
