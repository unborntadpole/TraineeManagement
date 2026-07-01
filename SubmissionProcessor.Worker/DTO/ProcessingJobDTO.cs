namespace SubmissionProcessor.Worker.DTO;
using System.ComponentModel.DataAnnotations;
using SubmissionProcessor.Worker.Models;

public class ProcessingJobDTO
{
    [Required]
    public string CorrelationId { get; set; }
    [Required]
    public string Status { get; set; } = null!;
    [Required]
    public int Attempts { get; set; }
    public string? ErrorSummary { get; set; }
    public DateTime Started { get; set; }
    public DateTime Finished { get; set; }

    public ProcessingJobDTO(ProcessingJob job)
    {
        CorrelationId = job.CorrelationId;
        Status = job.Status;
        Attempts = job.Attempts;
        ErrorSummary = job.ErrorSummary;
        Started = job.Started;
        Finished = job.Finished;
    }

    public ProcessingJobDTO() {}
}