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
    private readonly ConnectionFactory _connectionFactory; 
    private IConnection? _connection;
    private IChannel? _channel;
    private IServiceProvider _serviceProvider;
    private readonly string _queueName = "submission-processing";

    public TaskConsumerWorker(ILogger<TaskConsumerWorker> logger, ConnectionFactory connectionFactory, IServiceProvider serviceProvider)
    {
        _connectionFactory = connectionFactory;
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
        await Task.Yield(); 
        _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken: stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(exchange: "submission.exchange", type: ExchangeType.Topic, durable: true);

        await _channel.QueueDeclareAsync(
            queue: "submission-processing", 
            durable: true,
            exclusive: false, 
            autoDelete: false, 
            arguments: null);

        await _channel.QueueBindAsync(
            queue: "submission-processing", 
            exchange: "submission.exchange", 
            routingKey: "submission.requested");
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var submissionFileRepository = scope.ServiceProvider.GetRequiredService<SubmissionFileRepository>();
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var processingJobsRepository = scope.ServiceProvider.GetRequiredService<ProcessingJobsRepository>();

            var body = ea.Body.ToArray();

            var message = Encoding.UTF8.GetString(body);

            try
            {
                
                var content = JsonSerializer.Deserialize<SubmissionProcessingRequested>(message);
                string correlationId = content.CorrelationId.ToString();
                var job = await processingJobsRepository.GetByIdAsync(correlationId);
                if (job == null)
                {
                    await processingJobsRepository.PostByIdAsync(correlationId);
                    job = await processingJobsRepository.GetByIdAsync(correlationId);
                }
                await ProcessingStatusAndIncrementAsync(correlationId, processingJobsRepository, stoppingToken);
                _logger.LogInformation("Processing item: {Message}", message);

                await ValidateCheckSum(content, submissionFileRepository, fileStorageService, stoppingToken);

                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                await processingJobsRepository.SetStatusById(correlationId, "Completed");
                _logger.LogInformation("Successfully processed and persisted state. Message Acked.");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure occurred during processing chain.");
                string correlationId = JsonSerializer.Deserialize<SubmissionProcessingRequested>(message)
                    .CorrelationId.ToString();
                await processingJobsRepository.SetStatusById(correlationId, "Failed");
                await HandleFailureStrategyAsync(_channel, ea, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
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
            if (checksum == fileMetadata.Checksum)
            {
                _logger.LogInformation($"Checksum for file with id {fileMetadata.Id} is correctly stored.");
            }
        }
        catch(Exception e)
        {
            _logger.LogWarning($"Failed to connect to database: {e}");
        }
    }


    private async Task ProcessingStatusAndIncrementAsync(string correlationId, ProcessingJobsRepository repo, CancellationToken ct)
    {
        await repo.SetStatusById(correlationId, "Processing");
        await repo.IncrementAttemptById(correlationId);
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

