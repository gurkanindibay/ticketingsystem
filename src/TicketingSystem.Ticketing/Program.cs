using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using StackExchange.Redis;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using System.Reflection;
using TicketingSystem.Ticketing.Services;
using TicketingSystem.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

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
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
            "Host=localhost;Database=ticketingdb;Username=ticketinguser;Password=ticketingpass123");
    }
});

// Configure Redis settings
builder.Services.Configure<TicketingSystem.Shared.Configuration.RedisSettings>(
    builder.Configuration.GetSection("RedisSettings"));

// Configure RabbitMQ settings
builder.Services.Configure<TicketingSystem.Shared.Configuration.RabbitMQSettings>(
    builder.Configuration.GetSection("RabbitMQSettings"));

// Add Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis") ?? 
                       builder.Configuration["RedisSettings:ConnectionString"] ?? 
                       "localhost:6379";
    return ConnectionMultiplexer.Connect(configuration);
});

// Add RedLock for distributed locking
builder.Services.AddSingleton<IDistributedLockFactory>(provider =>
{
    var redis = provider.GetRequiredService<IConnectionMultiplexer>();
    var multiplexers = new List<RedLockMultiplexer>
    {
        new RedLockMultiplexer(redis)
    };
    return RedLockFactory.Create(multiplexers);
});

// Register application services
builder.Services.AddScoped<IPaymentService, MockPaymentService>();
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddScoped<IRabbitMQService, RabbitMQService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IEventService, EventService>();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ticketing System - Ticketing & Events API",
        Version = "v1",
        Description = "Consolidated microservice for the Ticketing System - handles ticket purchases, cancellations, user ticket management, and event operations",
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ticketing API v1");
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

// Add authentication and authorization middleware here when implemented
// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
