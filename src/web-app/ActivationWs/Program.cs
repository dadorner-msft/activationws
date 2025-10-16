using ActivationWs.Data;
using ActivationWs.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Enhanced logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Bind options with validation
builder.Services.Configure<ActivationServiceOptions>(
    builder.Configuration.GetSection("ActivationService"));

// Validate options on startup
builder.Services.AddOptions<ActivationServiceOptions>()
    .Bind(builder.Configuration.GetSection("ActivationService"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Typed HttpClient with proxy support and retry policy
builder.Services.AddHttpClient<ActivationService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ActivationWs/1.0");
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var opts = sp.GetRequiredService<IOptions<ActivationServiceOptions>>().Value;
    var handler = new HttpClientHandler();

    if (opts.Proxy?.UseProxy == true && !string.IsNullOrWhiteSpace(opts.Proxy.Address)) {
        var proxy = new WebProxy(opts.Proxy.Address!, opts.Proxy.BypassOnLocal);

        if (opts.Proxy.UseDefaultCredentials) {
            proxy.UseDefaultCredentials = true;
        } else if (!string.IsNullOrWhiteSpace(opts.Proxy.Username)) {
            proxy.Credentials = new NetworkCredential(
                opts.Proxy.Username,
                opts.Proxy.Password ?? string.Empty,
                opts.Proxy.Domain
            );
        }

        handler.Proxy = proxy;
        handler.UseProxy = true;
    } else {
        handler.UseProxy = false;
    }

    return handler;
})
.AddStandardResilienceHandler(); // Add retry and circuit breaker policies

// Services
builder.Services.AddScoped<ActivationProcessor>();

// Database with connection string validation
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=ActivationWs.db";

builder.Services.AddDbContext<ActivationDbContext>(options =>
{
    options.UseSqlite(connectionString);
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRazorPages();

var app = builder.Build();

// Apply pending migrations with error handling
try
{
    using (var scope = app.Services.CreateScope()) {
        var db = scope.ServiceProvider.GetRequiredService<ActivationDbContext>();
        await db.Database.MigrateAsync();
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while migrating the database");
    throw;
}

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error");
    app.UseHsts();
} else {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

await app.RunAsync();
