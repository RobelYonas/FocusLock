using System.Windows;
using System.Windows.Threading;

namespace FocusLock.App.Services;

/// <summary>Runs work on the UI thread. Core raises its events on background threads.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class WpfDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    public void Post(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }
}
