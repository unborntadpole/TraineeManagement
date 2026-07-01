namespace SubmissionProcessor.Worker.Models;

public class Mentor
{
    public long Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Expertise { get; set; }
    public string Status { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<TaskAssignment> TaskAssignments { get; }

    public ICollection<Review> Reviews { get; }

}