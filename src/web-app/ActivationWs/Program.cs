using ActivationWs.Activation;
using ActivationWs.Api;
using ActivationWs.Storage;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ActivationServiceOptions>()
    .Bind(builder.Configuration.GetSection(ActivationServiceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ProxyOptions>()
    .Bind(builder.Configuration.GetSection(ProxyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ActivationStoreOptions>()
    .Bind(builder.Configuration.GetSection(ActivationStoreOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

// The store only holds a resolved connection string, so a single instance is shared across requests.
builder.Services.AddSingleton<IActivationStore, SqliteActivationStore>();
builder.Services.AddSingleton<KeyedAsyncLock>();
builder.Services.AddHostedService<ActivationStoreInitializer>();

builder.Services.AddScoped<IConfirmationIdService, ConfirmationIdService>();

// Requesting a Confirmation ID consumes an activation, so the client deliberately does not retry.
builder.Services.AddHttpClient<IActivationServiceClient, ActivationServiceClient>(static (serviceProvider, httpClient) => {
    ActivationServiceOptions options = serviceProvider.GetRequiredService<IOptions<ActivationServiceOptions>>().Value;
    httpClient.Timeout = options.Timeout;
    httpClient.DefaultRequestHeaders.ExpectContinue = false;
})
.ConfigurePrimaryHttpMessageHandler(static serviceProvider => {
    ProxyOptions proxyOptions = serviceProvider.GetRequiredService<IOptions<ProxyOptions>>().Value;
    var handler = new SocketsHttpHandler();

    // Without an explicit proxy the handler keeps its default behaviour and honours the system proxy.
    if (proxyOptions.CreateWebProxy() is { } proxy) {
        handler.Proxy = proxy;
        handler.UseProxy = true;
        serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ActivationWs.Activation.Proxy")
            .LogInformation("Outbound requests use the configured proxy {ProxyAddress}.", proxyOptions.Address);
    }

    return handler;
});

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = static context => {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    });

builder.Services.AddExceptionHandler<ActivationExceptionHandler>();
builder.Services.AddValidation();

builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("activation", limiter => {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();

app.MapActivationEndpoints().RequireRateLimiting("activation");

app.MapVersionEndpoint();

app.MapGet("/health", static async (IActivationStore store, CancellationToken cancellationToken) => {
    // The activation store is the one dependency whose outage changes behaviour: while it is down,
    // activation requests are rejected to avoid consuming duplicate activations. Reporting it here
    // lets the static pages show a banner and lets a load balancer probe the same signal.
    bool storeHealthy = await store.PingAsync(cancellationToken);

    return storeHealthy
        ? Results.Ok(new { status = "healthy", store = "available" })
        : Results.Json(
            new { status = "unhealthy", store = "unavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
})
   .ExcludeFromDescription();

app.Run();

/// <summary>Entry point marker so integration tests can reference the host.</summary>
public partial class Program;
