using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT;

namespace TaskbarLyriz.App;

internal static class Program
{
    private const string MainInstanceKey = "TaskbarLyriz.Main";

    [STAThread]
    public static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        if (RedirectToMainInstance())
        {
            return;
        }

        Application.Start(callbackParameters =>
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(dispatcherQueue));
            new App();
        });
    }

    private static bool RedirectToMainInstance()
    {
        var mainInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);
        if (mainInstance.IsCurrent)
        {
            return false;
        }

        var activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        Task.Run(async () => await mainInstance.RedirectActivationToAsync(activationArguments))
            .GetAwaiter()
            .GetResult();
        return true;
    }
}
