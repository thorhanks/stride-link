using StrideLink.Input;
using System.Text.Json.Serialization;

namespace StrideLink.Networking;

// Keep the browser/server wire format explicit. System.Text.Json's default
// reflection options are case-sensitive, while the browser sends camelCase.
public sealed record MotionMessage(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("active")] bool? Active);

public sealed class ConnectionHub : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(1600);
    private readonly object _gate = new();
    private readonly IMovementOutput _output;
    private readonly Timer _watchdog;
    private Guid? _activeConnection;
    private DateTimeOffset _lastSeen;

    public ConnectionHub(IMovementOutput output)
    {
        _output = output;
        _watchdog = new Timer(CheckWatchdog, null, 250, 250);
    }

    public Guid Register()
    {
        lock (_gate)
        {
            _output.SetMoving(false);
            _activeConnection = Guid.NewGuid();
            _lastSeen = DateTimeOffset.UtcNow;
            return _activeConnection.Value;
        }
    }

    public void Handle(Guid connectionId, MotionMessage message)
    {
        lock (_gate)
        {
            if (_activeConnection != connectionId)
                return;

            _lastSeen = DateTimeOffset.UtcNow;
            if (message.Type is "movement" or "heartbeat" && message.Active.HasValue)
                _output.SetMoving(message.Active.Value);
        }
    }

    public void Unregister(Guid connectionId)
    {
        lock (_gate)
        {
            if (_activeConnection != connectionId)
                return;

            _output.SetMoving(false);
            _activeConnection = null;
        }
    }

    public object GetStatus()
    {
        lock (_gate)
        {
            return new
            {
                connected = _activeConnection.HasValue,
                moving = _output.IsMoving,
                lastSeenUtc = _activeConnection.HasValue ? _lastSeen : (DateTimeOffset?)null
            };
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _output.SetMoving(false);
            _activeConnection = null;
        }
    }

    private void CheckWatchdog(object? _)
    {
        lock (_gate)
        {
            if (_activeConnection.HasValue && DateTimeOffset.UtcNow - _lastSeen > Timeout)
            {
                _output.SetMoving(false);
            }
        }
    }

    public void Dispose()
    {
        _watchdog.Dispose();
        Stop();
        _output.Dispose();
    }
}
