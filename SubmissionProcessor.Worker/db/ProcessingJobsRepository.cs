using SubmissionProcessor.Worker.Models;
using SubmissionProcessor.Worker.DTO;


namespace SubmissionProcessor.Worker.db;

public class ProcessingJobsRepository
{
    private readonly ApplicationDbContext _context;
    public ProcessingJobsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProcessingJobDTO?> GetByIdAsync(string id)
    {
        var job = await _context.ProcessingJobs.FindAsync(id);
        if (job == null)
        {
            return null;
        }
        return new ProcessingJobDTO(job);
    }

    public async Task PostAsync(ProcessingJobDTO jobDTO)
    {
        ProcessingJob job = new ProcessingJob(jobDTO);
        await _context.ProcessingJobs.AddAsync(job);
        await _context.SaveChangesAsync();
    }

    public async Task PostByIdAsync(string corrId)
    {
        ProcessingJob job = new ProcessingJob(corrId);
        await _context.ProcessingJobs.AddAsync(job);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> SetStatusById(string id, string status)
    {
        var job = await _context.ProcessingJobs.FindAsync(id);
        if (job == null)
        {
            return false;
        }
        job.Status = status;
        if (status == "Completed")
        {
            job.Finished = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IncrementAttemptById(string id)
    {
        var job = await _context.ProcessingJobs.FindAsync(id);
        if (job == null)
        {
            return false;
        }
        job.Attempts++;
        await _context.SaveChangesAsync();
        return true;
    }

}