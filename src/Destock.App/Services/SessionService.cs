using System.Text.Json;
using Destock.App.Models;

namespace Destock.App.Services;

public sealed class SessionService
{
    private const string UserIdKey = "session_user_id";
    private const string UserJsonKey = "session_user_json";
    private const string LastUsernameKey = "last_username";

    private readonly DestockClient _client;

    public SessionService(DestockClient client)
    {
        _client = client;
    }

    public UserDto? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public bool IsAdmin => CurrentUser?.IsAdmin == true;

    public string LastUsername => Preferences.Default.Get(LastUsernameKey, "admin");

    public event EventHandler? UserChanged;

    public void SignIn(UserDto user)
    {
        CurrentUser = user;
        Preferences.Default.Set(UserIdKey, user.Id);
        Preferences.Default.Set(UserJsonKey, JsonSerializer.Serialize(user));
        Preferences.Default.Set(LastUsernameKey, user.Username);
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> TryRestoreAsync()
    {
        var id = Preferences.Default.Get(UserIdKey, 0L);
        if (id <= 0)
        {
            return false;
        }

        try
        {
            var user = await _client.CallAsync<UserDto>("auth.me", new { UserId = id });
            CurrentUser = user;
            Preferences.Default.Set(UserJsonKey, JsonSerializer.Serialize(user));
            UserChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (DestockException)
        {
            Clear();
            return false;
        }
    }

    public void Clear()
    {
        CurrentUser = null;
        Preferences.Default.Remove(UserIdKey);
        Preferences.Default.Remove(UserJsonKey);
        UserChanged?.Invoke(this, EventArgs.Empty);
    }
}
