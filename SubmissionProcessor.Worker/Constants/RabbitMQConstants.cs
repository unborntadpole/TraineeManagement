namespace SubmissionProcessor.Worker.Constants;

public class QueueConstants
{
    public static readonly string SubmissionQueueName = "submission-processing";
    public static readonly string SubmissionExchangeAndRoutingKey = "submission";
    public static readonly int SubmissionMaxRetryAttempts = 3;
}