using KittyClaw.Core.Services;
using Xunit;

namespace KittyClaw.Core.Tests.Automation;

public class TreeServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly ProjectService _projects;
    private readonly TreeService _treeService;

    public TreeServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"beaverboard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _projects = new ProjectService(_testDir);
        _treeService = new TreeService(_projects);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task WouldCreateCycle_SelfReference_ReturnsTrue()
    {
        var result = await _treeService.WouldCreateCycleAsync("test-project", 1, 1);
        Assert.True(result);
    }

    [Fact]
    public async Task WouldCreateCycle_DirectParent_ReturnsTrue()
    {
        // Create parent and child
        await _projects.CreateProjectAsync("test-project");
        await using var db = _projects.GetProjectDb("test-project");
        await _treeService.EnsureHierarchyColumnsAsync("test-project");
        
        var parent = new KittyClaw.Core.Models.Ticket { Id = 10, Title = "Parent" };
        var child = new KittyClaw.Core.Models.Ticket { Id = 20, Title = "Child", ParentId = 10 };
        db.Tickets.AddRange(parent, child);
        await db.SaveChangesAsync();

        // Try to make parent a child of child (cycle)
        var result = await _treeService.WouldCreateCycleAsync("test-project", 10, 20);
        Assert.True(result);
    }

    [Fact]
    public async Task WouldCreateCycle_NoCycle_ReturnsFalse()
    {
        await _projects.CreateProjectAsync("test-project");
        await using var db = _projects.GetProjectDb("test-project");
        await _treeService.EnsureHierarchyColumnsAsync("test-project");
        
        var ticket1 = new KittyClaw.Core.Models.Ticket { Id = 10, Title = "Ticket 1" };
        var ticket2 = new KittyClaw.Core.Models.Ticket { Id = 20, Title = "Ticket 2" };
        db.Tickets.AddRange(ticket1, ticket2);
        await db.SaveChangesAsync();

        // Making ticket2 a child of ticket1 is fine
        var result = await _treeService.WouldCreateCycleAsync("test-project", 20, 10);
        Assert.False(result);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsDirectChildren()
    {
        await _projects.CreateProjectAsync("test-project");
        await using var db = _projects.GetProjectDb("test-project");
        await _treeService.EnsureHierarchyColumnsAsync("test-project");
        
        var parent = new KittyClaw.Core.Models.Ticket { Id = 10, Title = "Parent" };
        var child1 = new KittyClaw.Core.Models.Ticket { Id = 20, Title = "Child 1", ParentId = 10 };
        var child2 = new KittyClaw.Core.Models.Ticket { Id = 30, Title = "Child 2", ParentId = 10 };
        var grandchild = new KittyClaw.Core.Models.Ticket { Id = 40, Title = "Grandchild", ParentId = 20 };
        db.Tickets.AddRange(parent, child1, child2, grandchild);
        await db.SaveChangesAsync();

        var children = await _treeService.GetChildrenAsync("test-project", 10);

        Assert.Equal(2, children.Count);
        Assert.Contains(children, c => c.Id == 20);
        Assert.Contains(children, c => c.Id == 30);
    }

    [Fact]
    public async Task GetSubtreeAsync_ReturnsAllDescendants()
    {
        await _projects.CreateProjectAsync("test-project");
        await using var db = _projects.GetProjectDb("test-project");
        await _treeService.EnsureHierarchyColumnsAsync("test-project");
        
        var root = new KittyClaw.Core.Models.Ticket { Id = 10, Title = "Root", Path = "10" };
        var child = new KittyClaw.Core.Models.Ticket { Id = 20, Title = "Child", ParentId = 10, Path = "10/20" };
        var grandchild = new KittyClaw.Core.Models.Ticket { Id = 30, Title = "Grandchild", ParentId = 20, Path = "10/20/30" };
        db.Tickets.AddRange(root, child, grandchild);
        await db.SaveChangesAsync();

        var subtree = await _treeService.GetSubtreeAsync("test-project", 10);

        Assert.Equal(2, subtree.Count); // child + grandchild (not root)
        Assert.Contains(subtree, t => t.Id == 20);
        Assert.Contains(subtree, t => t.Id == 30);
    }

    [Fact]
    public async Task GetProgressAsync_CountsCorrectly()
    {
        await _projects.CreateProjectAsync("test-project");
        await using var db = _projects.GetProjectDb("test-project");
        await _treeService.EnsureHierarchyColumnsAsync("test-project");
        
        var parent = new KittyClaw.Core.Models.Ticket { Id = 10, Title = "Parent" };
        var done = new KittyClaw.Core.Models.Ticket { Id = 20, Title = "Done", ParentId = 10, Status = "Done" };
        var inProgress = new KittyClaw.Core.Models.Ticket { Id = 30, Title = "In Progress", ParentId = 10, Status = "InProgress" };
        var blocked = new KittyClaw.Core.Models.Ticket { Id = 40, Title = "Blocked", ParentId = 10, Status = "Blocked" };
        db.Tickets.AddRange(parent, done, inProgress, blocked);
        await db.SaveChangesAsync();

        var progress = await _treeService.GetProgressAsync("test-project", 10);

        Assert.Equal(3, progress.Total);
        Assert.Equal(1, progress.Done);
        Assert.Equal(1, progress.Running);
        Assert.Equal(1, progress.Blocked);
    }
}
