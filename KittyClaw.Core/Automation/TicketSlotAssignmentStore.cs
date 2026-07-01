using System.Text.Json;

namespace KittyClaw.Core.Automation;

/// <summary>
/// Stores ticket-to-slot assignments globally (not project-specific).
/// This separates control plane concerns from project-specific ticket data.
/// 
/// Storage: {dataDir}/roster/assignments.json
/// </summary>
public class TicketSlotAssignmentStore
{
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private Dictionary<int, TicketSlotAssignment> _assignments = new();

    public TicketSlotAssignmentStore(string dataDir)
    {
        var rosterDir = Path.Combine(dataDir, "roster");
        Directory.CreateDirectory(rosterDir);
        _storePath = Path.Combine(rosterDir, "assignments.json");
    }

    public IReadOnlyDictionary<int, TicketSlotAssignment> Assignments => _assignments;

    public void Load()
    {
        if (!File.Exists(_storePath)) return;
        
        try
        {
            var json = File.ReadAllText(_storePath);
            var list = JsonSerializer.Deserialize<List<TicketSlotAssignment>>(json, _jsonOptions) ?? new();
            _assignments = list.ToDictionary(a => a.TicketId);
        }
        catch { /* ignore corrupt file */ }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_assignments.Values.ToList(), _jsonOptions);
        File.WriteAllText(_storePath, json);
    }

    public TicketSlotAssignment? Get(int ticketId) => _assignments.GetValueOrDefault(ticketId);

    public void Upsert(TicketSlotAssignment assignment)
    {
        _assignments[assignment.TicketId] = assignment;
        Save();
    }

    public void Remove(int ticketId)
    {
        _assignments.Remove(ticketId);
        Save();
    }

    public string? GetSlotId(int ticketId) => _assignments.GetValueOrDefault(ticketId)?.AssignedSlotId;
    public string? GetOverrideModel(int ticketId) => _assignments.GetValueOrDefault(ticketId)?.OverrideModelProfileId;
    public bool IsLocked(int ticketId) => _assignments.GetValueOrDefault(ticketId)?.LockExecutor ?? false;
}
