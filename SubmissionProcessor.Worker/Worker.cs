namespace SubmissionProcessor.Worker;

using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SubmissionProcessor.Worker.db;
using SubmissionProcessor.Worker.Services;
using SubmissionProcessor.Worker.DTO;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Net;

public class TaskConsumerWorker : BackgroundService
{
    private readonly ILogger<TaskConsumerWorker> _logger;
    private readonly ConnectionFactory _connectionFactory; 
    private IConnection? _connection;
    private IChannel? _channel;
    private IServiceProvider _serviceProvider;
    private static string queue = SubmissionProcessor.Worker.Constants.QueueConstants.SubmissionQueueName;
    private static string exchangeAndRoutingKey = SubmissionProcessor.Worker.Constants.QueueConstants.SubmissionExchangeAndRoutingKey;
    private static int MaxRetryAttempts = SubmissionProcessor.Worker.Constants.QueueConstants.SubmissionMaxRetryAttempts;
    private HttpClient _client;
    

    public TaskConsumerWorker(
        ILogger<TaskConsumerWorker> logger, 
        ConnectionFactory connectionFactory, 
        IServiceProvider serviceProvider,
        HttpClient client
        )
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _client = client;
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

        string dlxName = $"{exchangeAndRoutingKey}.dlx";
        string dlqName = $"{queue}-dead-letter";
        string deadLetterRoutingKey = $"{exchangeAndRoutingKey}.failed";

        string routingKey = $"{exchangeAndRoutingKey}.requested";
        string exchange = $"{exchangeAndRoutingKey}.exchange";

        await _channel.ExchangeDeclareAsync(exchange: dlxName, type: ExchangeType.Direct, durable: true);
        await _channel.QueueDeclareAsync(queue: dlqName, durable: true, exclusive: false, autoDelete: false);
        await _channel.QueueBindAsync(queue: dlqName, exchange: dlxName, routingKey: deadLetterRoutingKey);

        await _channel.ExchangeDeclareAsync(exchange: exchange, type: ExchangeType.Topic, durable: true);
        var queueArguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", dlxName },
            { "x-dead-letter-routing-key", deadLetterRoutingKey }
        };
        await _channel.QueueDeclareAsync(
            queue: queue, 
            durable: true,
            exclusive: false, 
            autoDelete: false, 
            arguments: null);

        await _channel.QueueBindAsync(
            queue: queue, 
            exchange: exchange, 
            routingKey: routingKey);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var submissionFileRepository = scope.ServiceProvider.GetRequiredService<SubmissionFileRepository>();
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var processingJobsRepository = scope.ServiceProvider.GetRequiredService<ProcessingJobsRepository>();

            var body = ea.Body.ToArray();

            var message = Encoding.UTF8.GetString(body);
            string correlationId = "unknown";
            try
            {
                
                var content = JsonSerializer.Deserialize<SubmissionProcessingRequested>(message);
                correlationId = content.CorrelationId.ToString();
                var job = await processingJobsRepository.GetByIdAsync(correlationId);
                if (job == null)
                {
                    _logger.LogWarning($"Processing job with correlation id {correlationId} not found. Adding new job to the database.");
                    await processingJobsRepository.PostByIdAsync(correlationId);
                    job = await processingJobsRepository.GetByIdAsync(correlationId);
                }
                else if (job.Status == "Completed")
                {
                    _logger.LogWarning($"Processing job with correlation id {correlationId} has been completed. Moving to next message.");
                    return;
                }
                else if (job.Status == "Processing" && job.Started < DateTime.UtcNow.AddSeconds(10))
                {
                    _logger.LogWarning($"Processing job with correlation id {correlationId} was recently added to processing. Waiting for other processes to work on it. Skipping");
                    return;
                }
                if (job.Attempts >= MaxRetryAttempts)
                {
                    throw new OperationCanceledException($"Message has exhausted maximum allowed {MaxRetryAttempts} retry attempts.");
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
                if (correlationId == "Unknown")
                {
                    try { correlationId = JsonSerializer.Deserialize<SubmissionProcessingRequested>(message)?.CorrelationId.ToString() ?? "Unknown"; } 
                    catch { }
                }

                bool isTransient = IsErrorTransient(ex);
                
                _logger.LogError(ex, "Failure occurred during processing. Error Type: {ErrorClassification}. Context Target: {CorrelationId}", 
                    isTransient ? "Transient" : "Permanent", correlationId);

                if (correlationId != "Unknown")
                {
                    string finalStatus = isTransient ? "Processing" : "Failed";
                    await processingJobsRepository.SetStatusById(correlationId, finalStatus);
                }

                await HandleFailureStrategyAsync(_channel, ea, isTransient, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }


    private async Task ValidateCheckSum(
        SubmissionProcessingRequested data,
        SubmissionFileRepository submissionFileRepository,
        IFileStorageService fileStorageService,
        // HttpClient _client,
        CancellationToken ct
    )
    {
        try
        {
            var fileMetadata = await submissionFileRepository.GetByIdAsync(data.FileId);
            if (fileMetadata == null)
            {
                _logger.LogWarning($"No file found with id: {data.FileId}");
                throw new FileNotFoundException($"Metadata registration for file ID {data.FileId} cannot be found in database.");
            }
            var res = await fileStorageService.OpenReadAsync(fileMetadata.GeneratedStorageName);
            if (!res.IsSuccess)
            {
                _logger.LogWarning($"Error in getting file: {res.Error}");
                throw new HttpRequestException($"Storage engine failure accessing data payload: {res.Error}");
            }
            var file = res.Value;
            string checksum = await GetChecksum(file);
            if (checksum != fileMetadata.Checksum)
            {
                throw new InvalidDataException($"Integrity mismatch detected. Extracted Checksum: '{checksum}' did not equal target value '{fileMetadata.Checksum}'.");
            }
            _logger.LogInformation($"Checksum for file with id {fileMetadata.Id} is correctly stored.");

            using var response = await _client.GetAsync("api/trainee", ct);
            if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogInformation("Data received successfully: {content}", content);
                }
            else
            {
                _logger.LogWarning("Received non-success status code: {StatusCode}", response.StatusCode);
            }
        }
        catch(Exception e)
        {
            _logger.LogWarning($"Failed to connect to database: {e}");
        }
    }

    private bool IsErrorTransient(Exception ex)
    {
        return ex switch
        {
            TimeoutException => true,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.ServiceUnavailable || httpEx.StatusCode == HttpStatusCode.GatewayTimeout => true,
            
            System.Data.Common.DbException dbEx when dbEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,

            InvalidDataException => false,
            FileNotFoundException => false,
            OperationCanceledException => false,
            _ => false
        };
    }

    private async Task ProcessingStatusAndIncrementAsync(string correlationId, ProcessingJobsRepository repo, CancellationToken ct)
    {
        try
        {
            await repo.SetStatusById(correlationId, "Processing");
            await repo.IncrementAttemptById(correlationId);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Error in getting file: {e}");
            throw new HttpRequestException($"Storage engine failure accessing data payload: {e.Message}");
        }
    }


    private async Task HandleFailureStrategyAsync(
        IChannel channel, 
        BasicDeliverEventArgs ea, 
        bool isTransient, 
        CancellationToken ct)
    {
        if (isTransient)
        {
            _logger.LogWarning("Transient error caught. Returning message back to working stream.");
            await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
        }
        else
        {
            _logger.LogError("Permanent or exhausted failure encountered. Committing tracking to DLQ.");
            await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        }
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}

