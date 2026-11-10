namespace FocusLock.Core.Abstractions;

/// <summary>Calls back at a fixed interval until the returned handle is disposed.</summary>
public interface ITickTimer
{
    IDisposable Start(TimeSpan interval, Action onTick);
}
