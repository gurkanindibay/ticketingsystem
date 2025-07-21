using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using System.Reflection;
using System.Text;
using TicketingSystem.Authentication.Data;
using TicketingSystem.Shared.Models;
using TicketingSystem.Authentication.Services;

var builder = WebApplication.CreateBuilder(args);

// Database configuration - supports both in-memory and PostgreSQL
var useInMemoryDatabase = builder.Configuration.GetSection("DatabaseSettings")["UseInMemoryDatabase"];

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    if (string.Equals(useInMemoryDatabase, "true", StringComparison.OrdinalIgnoreCase))
    {
        options.UseInMemoryDatabase("TicketingSystemDb");
    }
    else
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), 
            b => b.MigrationsAssembly("TicketingSystem.Authentication"));
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
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders();

// JWT configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
var key = Encoding.ASCII.GetBytes(secretKey);

// Enhanced HTTPS and Security Configuration
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });
}

// Security Headers Configuration
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = 1024 * 1024; // 1MB
    options.MultipartBodyLengthLimit = 1024 * 1024; // 1MB
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment(); // Enable in production
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
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true,
        RequireSignedTokens = true
    };
    
    // Enhanced JWT events for better security logging
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT authentication failed: {Exception}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var userId = context.Principal?.Identity?.Name ?? "Unknown";
            logger.LogInformation("JWT token validated for user: {UserId}", userId);
            return Task.CompletedTask;
        }
    };
});

// Register application services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthValidationService, AuthValidationService>();

// Enhanced Authorization with role-based and claim-based policies
builder.Services.AddAuthorization(options =>
{
    // Role-based policies
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin")
              .RequireAuthenticatedUser());

    options.AddPolicy("ManagerOrAdmin", policy =>
        policy.RequireRole("Manager", "Admin")
              .RequireAuthenticatedUser());

    options.AddPolicy("AuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("User", "Manager", "Admin"));

    // Claim-based policies for fine-grained access control
    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
                  context.User.IsInRole("Admin") ||
                  (context.User.IsInRole("Manager") && 
                   context.User.HasClaim("Permission", "UserManagement"))));

    // Time-based access policy (example for admin operations during business hours)
    options.AddPolicy("BusinessHoursAdmin", policy =>
        policy.RequireRole("Admin")
              .RequireAssertion(context =>
              {
                  var now = DateTime.UtcNow;
                  var businessStart = TimeSpan.FromHours(8);  // 8 AM UTC
                  var businessEnd = TimeSpan.FromHours(18);   // 6 PM UTC
                  var currentTime = now.TimeOfDay;
                  return currentTime >= businessStart && currentTime <= businessEnd;
              }));

    // Security policy requiring fresh authentication for sensitive operations
    options.AddPolicy("RequireFreshLogin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
              {
                  var authTime = context.User.FindFirst("auth_time")?.Value;
                  if (DateTime.TryParse(authTime, out var authDateTime))
                  {
                      return DateTime.UtcNow.Subtract(authDateTime).TotalMinutes <= 30; // 30 minutes
                  }
                  return false;
              }));
});

// Enhanced Rate Limiting with user-based and IP-based policies
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

    // Login rate limiting: 10 per 15 minutes per IP with user lockout
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

    // Refresh token rate limiting: 20 per 5 minutes per authenticated user
    options.AddPolicy("refresh", httpContext =>
    {
        var userId = httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = int.Parse(builder.Configuration["RateLimiting:Refresh:MaxAttempts"] ?? "20"),
                Window = TimeSpan.FromMinutes(int.Parse(builder.Configuration["RateLimiting:Refresh:WindowMinutes"] ?? "5")),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Admin operations rate limiting: stricter limits for sensitive operations
    options.AddPolicy("admin", httpContext =>
    {
        var userId = httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 50, // 50 admin operations per hour
                Window = TimeSpan.FromHours(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Global fallback with sliding window for better distribution
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new SlidingWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100, // 100 requests per minute as global limit
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6 // 10-second segments for smoother rate limiting
            }));

    // Enhanced rejection response with security headers
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = context.HttpContext.Request.Headers.UserAgent.ToString();
        var endpoint = context.HttpContext.Request.Path;
        
        logger.LogWarning("Rate limit exceeded - IP: {ClientIp}, UserAgent: {UserAgent}, Endpoint: {Endpoint}", 
            clientIp, userAgent, endpoint);

        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.ContentType = "application/json";
        
        // Add security headers
        context.HttpContext.Response.Headers.Append("Retry-After", "60");
        context.HttpContext.Response.Headers.Append("X-RateLimit-Policy", "exceeded");
        
        var response = System.Text.Json.JsonSerializer.Serialize(new
        {
            success = false,
            message = "Too many requests. Please try again later.",
            errorCode = "RATE_LIMIT_EXCEEDED",
            retryAfter = 60
        });
        
        await context.HttpContext.Response.WriteAsync(response, cancellationToken);
    };
});

// Add services to the container.
builder.Services.AddControllers();

// Enhanced Health Checks with dependencies
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuthDbContext>("database")
    .AddCheck("jwt-configuration", () =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"];
        return !string.IsNullOrEmpty(secretKey) && secretKey.Length >= 32
            ? HealthCheckResult.Healthy("JWT configuration is valid")
            : HealthCheckResult.Unhealthy("JWT SecretKey is invalid or too short");
    })
    .AddCheck("memory", () =>
    {
        var gc = GC.GetTotalMemory(false);
        return gc < 1024 * 1024 * 1024 // 1GB threshold
            ? HealthCheckResult.Healthy($"Memory usage: {gc / 1024 / 1024} MB")
            : HealthCheckResult.Degraded($"High memory usage: {gc / 1024 / 1024} MB");
    });

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
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    
    // Ensure database is created
    context.Database.EnsureCreated();
    
    // Seed roles
    await SeedRolesAsync(roleManager);
}

// Configure the HTTP request pipeline with enhanced security
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
    // Production security configurations
    app.UseHsts(); // Add HSTS headers
    app.UseHttpsRedirection();
    
    // Add security headers middleware
    app.Use(async (context, next) =>
    {
        // Security headers for production
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none';");
        
        // Remove server information
        context.Response.Headers.Remove("Server");
        
        await next();
    });
}

// Middleware pipeline order is critical for security
app.UseAuthentication();
app.UseRateLimiter(); // Rate limiting before authorization
app.UseAuthorization();

// Request logging middleware for security monitoring
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    await next();
    
    stopwatch.Stop();
    
    // Log suspicious activities
    if (context.Response.StatusCode == 401 || context.Response.StatusCode == 403 || context.Response.StatusCode == 429)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();
        
        logger.LogWarning("Security event - Status: {StatusCode}, IP: {ClientIp}, Path: {Path}, UserAgent: {UserAgent}, Duration: {Duration}ms",
            context.Response.StatusCode, clientIp, context.Request.Path, userAgent, stopwatch.ElapsedMilliseconds);
    }
});

app.MapControllers();

// Enhanced health checks endpoint with detailed responses
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// Minimal health check for load balancers
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

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
