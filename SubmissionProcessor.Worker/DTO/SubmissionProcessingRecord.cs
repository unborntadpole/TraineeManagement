namespace SubmissionProcessor.Worker.DTO;

public record SubmissionProcessingRequested(
    Guid MessageId,
    Guid CorrelationId,
    long SubmissionId,
    long FileId,
    DateTimeOffset RequestedAt,
    string ContractVersion
);