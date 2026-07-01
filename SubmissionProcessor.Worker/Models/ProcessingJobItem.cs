namespace SubmissionProcessor.Worker.Models;
using System.ComponentModel.DataAnnotations;
using SubmissionProcessor.Worker.DTO;
public class ProcessingJob
{
    [Key]
    public string CorrelationId { get; set; }
    public string Status { get; set; } = null!;
    public int Attempts { get; set; }
    public string? ErrorSummary { get; set; }
    public DateTime Started { get; set; } = DateTime.UtcNow;
    public DateTime Finished { get; set; }

    public ProcessingJob(ProcessingJobDTO job)
    {
        CorrelationId = job.CorrelationId;
        Status = job.Status;
        Attempts = job.Attempts;
        ErrorSummary = job.ErrorSummary;
        Started = job.Started;
        Finished = job.Finished;
    }

    public ProcessingJob(string correlationId)
    {
        CorrelationId = correlationId;
        Status = "Queued";
        Attempts = 0;
    }
}