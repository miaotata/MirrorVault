using System.Threading.Tasks;
using Avalonia.Controls;

namespace BackupApp.UI.Views;

public partial class ConfirmView : UserControl
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> WaitForResultAsync() => _tcs.Task;

    public ConfirmView(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        BtnConfirm.Click += (_, _) => _tcs.TrySetResult(true);
        BtnCancel.Click += (_, _) => _tcs.TrySetResult(false);
    }
}
