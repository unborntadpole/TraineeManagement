namespace SubmissionProcessor.Worker.db;

using Microsoft.EntityFrameworkCore;
using SubmissionProcessor.Worker.Models;

using System;
using System.Globalization;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        
    }
    public DbSet<SubmissionFile> SubmissionFiles { get; set; }
    public DbSet<ProcessingJob> ProcessingJobs { get; set; }

}

