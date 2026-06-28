namespace KittyClaw.Web.Services;

public sealed class TeamChatState
{
    private string _currentProjectSlug = "";

    public string CurrentProjectSlug
    {
        get => _currentProjectSlug;
        set
        {
            if (_currentProjectSlug != value)
            {
                _currentProjectSlug = value;
                OnProjectChanged?.Invoke();
            }
        }
    }

    public event Action? OnProjectChanged;
}
