using KittyClaw.Core.Services;
using Xunit;

namespace KittyClaw.Core.Tests.Automation;

public class DecompositionServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly ProjectService _projects;
    private readonly TicketService _tickets;
    private readonly TreeService _treeService;
    private readonly DecompositionService _decomposition;

    public DecompositionServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"beaverboard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _projects = new ProjectService(_testDir);
        _tickets = new TicketService(_projects, new MemberService(_projects));
        _treeService = new TreeService(_projects);
        _decomposition = new DecompositionService(_treeService, _tickets, _projects);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task CanDecompose_TicketNotFound_ReturnsFalse()
    {
        var (can, reason) = await _decomposition.CanDecomposeAsync("test-project", 999);
        Assert.False(can);
        Assert.Contains("not found", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanDecompose_DoneTicket_ReturnsFalse()
    {
        await _projects.CreateProjectAsync("test-project");
        await _tickets.CreateTicketAsync("test-project", "Test", "", "owner", "Done");

        var (can, reason) = await _decomposition.CanDecomposeAsync("test-project", 1);
        Assert.False(can);
        Assert.Contains("completed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewDecomposition_GeneratesChildren()
    {
        await _projects.CreateProjectAsync("test-project");
        var ticket = await _tickets.CreateTicketAsync("test-project", "Implement Health Center", "", "owner", "Backlog");

        var request = new DecompositionRequest
        {
            Children = new List<DecompositionChild>
            {
                new() { Title = "Event model" },
                new() { Title = "SQLite store" },
                new() { Title = "UI page" }
            }
        };

        var preview = await _decomposition.PreviewDecompositionAsync("test-project", ticket!.Id, request);

        Assert.True(preview.IsValid);
        Assert.Equal(3, preview.Children.Count);
        Assert.Equal("Implement Health Center", preview.ParentTitle);
    }

    [Fact]
    public async Task PreviewDecomposition_DepthWarning()
    {
        await _projects.CreateProjectAsync("test-project");
        var ticket = await _tickets.CreateTicketAsync("test-project", "Deep task", "", "owner", "Backlog");

        var request = new DecompositionRequest
        {
            Children = new List<DecompositionChild>
            {
                new() { Title = "Only child" }
            }
        };

        var preview = await _decomposition.PreviewDecompositionAsync("test-project", ticket!.Id, request);

        // Should have warning about small decomposition
        Assert.Contains(preview.Warnings, w => w.Contains("small", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetLeafTasks_NoChildren_ReturnsTicket()
    {
        await _projects.CreateProjectAsync("test-project");
        var ticket = await _tickets.CreateTicketAsync("test-project", "Leaf ticket", "", "owner", "Backlog");

        var leaves = await _decomposition.GetLeafTasksAsync("test-project", ticket!.Id);

        Assert.NotEmpty(leaves);
        Assert.Contains(leaves, l => l.Title == "Leaf ticket");
    }
}
