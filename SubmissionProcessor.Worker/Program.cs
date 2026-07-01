using SubmissionProcessor.Worker;
using SubmissionProcessor.HttpHandler;
using RabbitMQ.Client;
using SubmissionProcessor.Worker.db;
using Microsoft.EntityFrameworkCore;
using SubmissionProcessor.Worker.Services;
using System.Net;
using Polly;

var builder = Host.CreateApplicationBuilder(args);

var rabbitMQSettings = builder.Configuration.GetSection("ConnectionStrings");
var uriString = rabbitMQSettings["RabbitMqURI"] ?? throw new InvalidOperationException("RabbitMQ URI is missing.");
// builder.Services.AddSingleton<IConnection>(serviceProvider =>
// {
//     var factory = new ConnectionFactory
//     {
//         Uri = new Uri(uriString)
//     };
//     return factory.CreateConnectionAsync().GetAwaiter().GetResult();
// });
builder.Services.AddSingleton(new ConnectionFactory
{
    Uri = new Uri(uriString)
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
?? throw new InvalidOperationException("Connections String: 'Default connection string not found'");

builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseMySQL(
        connectionString
    ));

builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<SubmissionFileRepository>();
builder.Services.AddScoped<ProcessingJobsRepository>();
builder.Services.AddHttpClient<TaskConsumerWorker>((serviceProvider, client) =>
    {
        var baseUri = builder.Configuration["ConnectionStrings:HttpClientAPI"] ?? throw new InvalidOperationException("CRITICAL: ConnectionStrings:HttpClientAPI is missing from your appsettings configuration.");
        client.BaseAddress = new Uri(baseUri);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new SocketsHttpHandler()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15)
        };
    })
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
    .AddHttpMessageHandler(() => new HttpStatusCodeFallbackHandler())
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 5;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
    
        options.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(response =>
            response.StatusCode == HttpStatusCode.RequestTimeout ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable ||
            response.StatusCode == HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode >= 500
            );
    
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    }
);

builder.Services.AddHostedService(sp => sp.GetRequiredService<TaskConsumerWorker>());

var host = builder.Build();
host.Run();
