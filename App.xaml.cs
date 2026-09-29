using System;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;

namespace MessengerApp;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private const string AppGuid = "MessengerPro_DotNet8_App_2026_UniqueGuid";

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            _singleInstanceMutex = new Mutex(true, AppGuid, out bool isNewInstance);
            if (!isNewInstance)
            {
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow();
            mainWindow.Show();
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
        ToastNotificationManagerCompat.Uninstall();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
