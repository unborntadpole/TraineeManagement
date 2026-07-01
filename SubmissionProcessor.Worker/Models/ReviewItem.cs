namespace SubmissionProcessor.Worker.Models;

public class Review
{
    public long Id { get; set; }
    public string ReviewStatus { get; set; }
    public string Feedback { get; set; }
    public int Score { get; set; }
    public DateTime ReviewedDate { get; set; }

    public long MentorId { get; set; }
    public Mentor Mentor { get; set; }

    public long SubmissionId { get; set; }
    public Submission Submission { get; set; }


}