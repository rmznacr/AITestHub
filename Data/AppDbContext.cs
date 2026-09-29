using AITestHub.Entities;
using AITestHub.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AITestHub.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TestCase> TestCases => Set<TestCase>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TestExecutionResult> TestExecutionResults => Set<TestExecutionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");

            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(p => p.Description)
                .HasMaxLength(2000);

            entity.Property(p => p.CreatedAt)
                .HasDefaultValueSql("now() at time zone 'utc'");
        });

        modelBuilder.Entity<TestCase>(entity =>
        {
            entity.ToTable("test_cases");

            entity.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Description)
                .HasMaxLength(2000);

            entity.Property(t => t.HttpMethod)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(t => t.Endpoint)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(t => t.RequestBody);

            entity.Property(t => t.ExpectedStatusCode);

            entity.Property(t => t.ExpectedResult)
                .HasMaxLength(2000);

            entity.Property(t => t.Preconditions)
                .HasMaxLength(2000);

            entity.Property(t => t.Steps)
                .IsRequired()
                .HasMaxLength(5000);

            entity.Property(t => t.Priority);

            entity.Property(t => t.TestType);

            entity.Property(t => t.Status);

            entity.Property(t => t.ExecutionNotes)
                .HasMaxLength(3000);

            entity.Property(t => t.LastExecutedAt);

            entity.Property(t => t.CreatedAt)
                .HasDefaultValueSql("now() at time zone 'utc'");

            entity.Property(t => t.ProjectId);

            entity.HasIndex(t => t.ProjectId);

            entity.HasOne(t => t.Project)
                .WithMany(p => p.TestCases)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestExecutionResult>(entity =>
        {
            entity.ToTable("test_execution_results");

            entity.Property(r => r.RequestMethod)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(r => r.RequestUrl)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(r => r.RequestBody)
                .HasMaxLength(10000);

            entity.Property(r => r.ResponseBody)
                .HasMaxLength(110_000);

            entity.Property(r => r.ErrorMessage)
                .HasMaxLength(2000);

            entity.Property(r => r.ExecutedAt);

            entity.Property(r => r.DurationMs);

            entity.HasIndex(r => r.TestCaseId);

            entity.HasOne(r => r.TestCase)
                .WithMany(t => t.ExecutionResults)
                .HasForeignKey(r => r.TestCaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
