using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace FocusLock.App.Views;

public partial class ToastWindow : Window
{
    public ToastWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Reposition();
    }

    public event EventHandler? Clicked;

    public void SetMessage(string title, string body)
    {
        TitleText.Text = title;
        BodyText.Text = body;
        AutomationProperties.SetName(this, $"{title}. {body}");
        Reposition();
    }

    private void Reposition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 12;
        Top = workArea.Bottom - ActualHeight - 12;
    }

    private void OnClick(object sender, MouseButtonEventArgs e) => Clicked?.Invoke(this, EventArgs.Empty);
}
