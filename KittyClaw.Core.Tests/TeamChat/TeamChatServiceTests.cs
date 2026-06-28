using KittyClaw.Core.TeamChat;
using Xunit;

namespace KittyClaw.Core.Tests.TeamChat;

public class TeamChatServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TeamChatService _service;

    public TeamChatServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"teamchat_test_{Guid.NewGuid():n}");
        Directory.CreateDirectory(_testDir);
        _service = new TeamChatService(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task PostMessage_StoresMessage()
    {
        var req = new PostTeamChatMessageRequest(
            Body: "Hello team",
            AuthorId: "owner",
            AuthorType: "human",
            MessageType: "message",
            TargetType: "team"
        );

        var msg = await _service.PostMessageAsync("test-project", req);

        Assert.NotNull(msg);
        Assert.Equal("Hello team", msg.Body);
        Assert.Equal("owner", msg.AuthorId);
        Assert.Equal("human", msg.AuthorType);
        Assert.Equal("team", msg.TargetType);
    }

    [Fact]
    public async Task ListMessages_ReturnsPostedMessages()
    {
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Message 1", AuthorId: "owner"));
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Message 2", AuthorId: "owner"));

        var messages = await _service.ListMessagesAsync("test-project", new TeamChatQuery());

        Assert.Equal(2, messages.Count);
        Assert.Equal("Message 2", messages[0].Body); // Most recent first
        Assert.Equal("Message 1", messages[1].Body);
    }

    [Fact]
    public async Task ResolveMessage_MarksAsResolved()
    {
        var msg = await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Question", AuthorId: "agent", MessageType: "question"));

        await _service.ResolveMessageAsync("test-project", msg.Id, "owner");

        var resolved = await _service.GetMessageAsync("test-project", msg.Id);
        Assert.NotNull(resolved);
        Assert.Equal("resolved", resolved.DeliveryStatus);
        Assert.Equal("owner", resolved.ResolvedBy);
    }

    [Fact]
    public async Task GetInbox_ReturnsOpenAndQuestionMessages()
    {
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Open msg", AuthorId: "owner"));
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Question", AuthorId: "agent", MessageType: "question"));
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Resolved", AuthorId: "system"));

        // Resolve the third message
        var resolved = await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Already resolved", AuthorId: "agent"));
        await _service.ResolveMessageAsync("test-project", resolved.Id, "owner");

        var inbox = await _service.GetInboxAsync("test-project");

        // Should include: Open msg (open), Question (question), Resolved (open) - but not "Already resolved" (resolved)
        Assert.Equal(3, inbox.Count);
    }

    [Fact]
    public async Task AddSystemEvent_StoresSystemMessage()
    {
        var req = new SystemEventRequest(
            Body: "Run started for KC-42",
            MessageType: "status",
            TicketId: 42
        );

        var msg = await _service.AddSystemEventAsync("test-project", req);

        Assert.Equal("system", msg.AuthorType);
        Assert.Equal("Run started for KC-42", msg.Body);
        Assert.Equal(42, msg.TicketId);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCorrectCount()
    {
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Agent msg", AuthorId: "builder", AuthorType: "agent"));
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Human msg", AuthorId: "owner", AuthorType: "human"));
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "System msg", AuthorId: "system", AuthorType: "system"));

        var count = await _service.GetUnreadCountAsync("test-project");

        Assert.Equal(2, count); // Agent and system messages count as unread (not human)
    }

    [Fact]
    public async Task ListMessages_FiltersByTicketId()
    {
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Msg for ticket 1", AuthorId: "owner", TicketId: 1));
        await _service.PostMessageAsync("test-project", new PostTeamChatMessageRequest(
            Body: "Msg for ticket 2", AuthorId: "owner", TicketId: 2));

        var messages = await _service.ListMessagesAsync("test-project", new TeamChatQuery(TicketId: 1));

        Assert.Single(messages);
        Assert.Equal("Msg for ticket 1", messages[0].Body);
    }
}
