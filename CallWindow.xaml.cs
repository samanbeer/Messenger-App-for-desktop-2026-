using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace MessengerApp;

public partial class CallWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public CallWindow()
    {
        InitializeComponent();
        Loaded += CallWindow_Loaded;
    }

    private void CallWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
    }

    public async Task InitializeWithEnvironmentAsync(CoreWebView2Environment env)
    {
        await ChildWebView.EnsureCoreWebView2Async(env);
        ChildWebView.CoreWebView2.PermissionRequested += (s, args) =>
        {
            if (args.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                args.PermissionKind == CoreWebView2PermissionKind.Camera)
            {
                args.State = CoreWebView2PermissionState.Allow;
            }
        };
    }
}
