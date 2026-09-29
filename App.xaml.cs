using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;

namespace MessengerApp;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
    private const int ASFW_ANY = -1;

    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _wakeUpEvent;
    private static RegisteredWaitHandle? _waitHandleRegistration;
    private const string AppGuid = "MessengerPro_DotNet8_App_2026_UniqueGuid";
    private const string WakeUpEventName = "MessengerPro_DotNet8_App_2026_WakeUp";
    private static MainWindow? _mainWindow;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            _singleInstanceMutex = new Mutex(true, AppGuid, out bool isNewInstance);
            if (!isNewInstance)
            {
                // Another instance is already running; wake it up and bring to foreground
                try
                {
                    AllowSetForegroundWindow(ASFW_ANY);
                    if (EventWaitHandle.TryOpenExisting(WakeUpEventName, out var wakeEvent))
                    {
                        wakeEvent.Set();
                        wakeEvent.Dispose();
                    }
                }
                catch { }

                Shutdown();
                return;
            }

            // Register background listener for secondary activations (e.g. pinned taskbar / shortcut clicks)
            try
            {
                _wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeUpEventName);
                _waitHandleRegistration = ThreadPool.RegisterWaitForSingleObject(
                    _wakeUpEvent,
                    (state, timedOut) =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            _mainWindow?.ShowWindow();
                        });
                    },
                    null,
                    Timeout.Infinite,
                    false
                );
            }
            catch { }

            _mainWindow = new MainWindow();
            _mainWindow.Show();
        }
        catch (Exception ex)
        {
            string errPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MessengerPro",
                "startup_error.log"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(errPath)!);
            File.WriteAllText(errPath, ex.ToString());
            System.Windows.MessageBox.Show(ex.ToString(), "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _waitHandleRegistration?.Unregister(null);
            _wakeUpEvent?.Dispose();
        }
        catch { }

        ToastNotificationManagerCompat.Uninstall();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
