using SubmissionProcessor.Worker;

using RabbitMQ.Client;
using TraineeManagementApi.db;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var rabbitMQSettings = builder.Configuration.GetSection("ConnectionStrings");
var uriString = rabbitMQSettings["RabbitMqURI"] ?? throw new InvalidOperationException("RabbitMQ URI is missing.");
builder.Services.AddSingleton<IConnection>(serviceProvider =>
{
    var factory = new ConnectionFactory
    {
        Uri = new Uri(uriString)
    };
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
?? throw new InvalidOperationException("Connections String: 'Default connection string not found'");

builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseMySQL(
        connectionString
    ));

builder.Services.AddHostedService<TaskConsumerWorker>();

var host = builder.Build();
host.Run();
