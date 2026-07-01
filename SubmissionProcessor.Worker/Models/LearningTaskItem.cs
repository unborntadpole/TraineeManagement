
namespace SubmissionProcessor.Worker.Models;

public class LearningTask
{
    public long Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string ExpectedTechStack { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<TaskAssignment> TaskAssignments { get; }


}