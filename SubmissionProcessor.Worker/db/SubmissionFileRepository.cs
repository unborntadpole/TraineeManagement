namespace SubmissionProcessor.Worker.db;

using SubmissionProcessor.Worker.Models;

public class SubmissionFileRepository
{
    private readonly ApplicationDbContext _context;

    public SubmissionFileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SubmissionFile submissionFile)
    {
        await _context.SubmissionFiles.AddAsync(submissionFile);
        await _context.SaveChangesAsync();

        // return submissionFile.Id;

    }

    public async Task<SubmissionFile?> GetByIdAsync(long id)
    {
        return await _context.SubmissionFiles.FindAsync(id);
    }

    public async Task DeleteAsync(SubmissionFile file)
    {
        _context.SubmissionFiles.Remove(file);
        await _context.SaveChangesAsync();
    }

}
