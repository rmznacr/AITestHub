namespace AITestHub.Entities;

/// <summary>
/// Represents a target project that groups related test cases.
/// </summary>
public class Project
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
}
