namespace SubmissionProcessor.Worker;

using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TraineeManagementApi.db;
using TraineeManagementApi.Services;
using TraineeManagementApi.DTO;
using System;
using System.IO;
using System.Security.Cryptography;

public class TaskConsumerWorker : BackgroundService
{
    private readonly ILogger<TaskConsumerWorker> _logger;
    private IConnection _connection;
    private IChannel? _channel;
    private IServiceProvider _serviceProvider;
    private readonly string _queueName = "submission-processing";

    public TaskConsumerWorker(ILogger<TaskConsumerWorker> logger, IConnection connection, IServiceProvider serviceProvider)
    {
        _connection = connection;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    private static async Task<string> GetChecksum(Stream stream)
    {
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", String.Empty).ToLower();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var submissionFileRepository = scope.ServiceProvider.GetRequiredService<SubmissionFileRepository>();
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            // var processingJobsRepository = scope.ServiceProvider.GetRequiredService<ProcessingJobsRepository>();

            var body = ea.Body.ToArray();

            var message = Encoding.UTF8.GetString(body);

            try
            {
                _logger.LogInformation("Processing item: {Message}", message);
                var content = JsonSerializer.Deserialize<SubmissionProcessingRequested>(message);
                await ValidateCheckSum(content, submissionFileRepository, fileStorageService, stoppingToken);

                await SaveStatusToDatabaseAsync(message, stoppingToken);

                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                _logger.LogInformation("Successfully processed and persisted state. Message Acked.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure occurred during processing chain.");
                await HandleFailureStrategyAsync(_channel, ea, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ValidateCheckSum(
        SubmissionProcessingRequested data, 
        SubmissionFileRepository submissionFileRepository,
        IFileStorageService fileStorageService, 
        CancellationToken ct
    )
    {
        try
        {
            var fileMetadata = await submissionFileRepository.GetByIdAsync(data.FileId);
            var res = await fileStorageService.OpenReadAsync(fileMetadata.GeneratedStorageName);
            if (!res.IsSuccess)
            {
                _logger.LogWarning($"Error in getting file: {res.Error}");
            }
            var file = res.Value;
            string checksum = await GetChecksum(file);
        }
        catch(Exception e)
        {
            _logger.LogWarning($"Failed to connect to database: {e}");
        }
    }

    private async Task SaveStatusToDatabaseAsync(string data, CancellationToken ct)
    {
        await Task.Delay(100, ct);
    }

    private async Task HandleFailureStrategyAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        // Option A: Immediate requeue to retry instantly (if transient error)
        // await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);

        // Option B: Dead-letter / reject without requeueing (if using a Dead Letter Exchange retry pipeline)
        await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        _logger.LogWarning("Message Nacked without requeue to prevent poison loops.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}

