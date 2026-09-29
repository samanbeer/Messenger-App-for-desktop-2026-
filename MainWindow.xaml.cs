using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MessengerApp;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private Icon? _normalIcon;
    private Icon? _alertIcon;
    private bool _isExiting;
    private bool _notificationsMuted;
    private bool _firstMinimizeShown;
    private string _currentTitle = "MessengeR";
    private bool _hideFullBanner = true;
    private CoreWebView2Environment? _environment;

    private const string StartupRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "MessengeR";

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowSettings();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
        SizeChanged += (s, e) => SaveWindowSettings();
        LocationChanged += (s, e) => SaveWindowSettings();

        // Register toast click activation
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(ShowWindow);
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyDarkMode();
        SetupTrayIcon();

        // Check if launched with --minimized flag (e.g. on Windows startup)
        string[] args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-minimized", StringComparison.OrdinalIgnoreCase))
            {
                Hide();
                break;
            }
        }

        SetupMemoryTimer();
        await InitializeWebViewAsync();
    }

    private void ApplyDarkMode()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
        catch { }
    }

    private void SetupTrayIcon()
    {
        try
        {
            var iconSize = System.Windows.Forms.SystemInformation.SmallIconSize;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
            {
                _normalIcon = new Icon(iconPath, iconSize);
            }
            else
            {
                var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/icon.ico"))?.Stream;
                if (iconStream != null)
                {
                    _normalIcon = new Icon(iconStream, iconSize);
                }
                else
                {
                    _normalIcon = System.Drawing.SystemIcons.Application;
                }
            }
            _alertIcon = CreateAlertIcon(_normalIcon);
        }
        catch
        {
            _normalIcon = System.Drawing.SystemIcons.Application;
            _alertIcon = _normalIcon;
        }

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        var openItem = new System.Windows.Forms.ToolStripMenuItem("Otevřít MessengeR", null, (s, e) => ShowWindow());
        openItem.Font = new Font(openItem.Font, System.Drawing.FontStyle.Bold);

        var reloadItem = new System.Windows.Forms.ToolStripMenuItem("Obnovit stránku (F5)", null, (s, e) =>
        {
            Dispatcher.Invoke(() => WebViewControl.CoreWebView2?.Reload());
        });

        var navMenu = new System.Windows.Forms.ToolStripMenuItem("Přepnout adresu");
        var navFb = new System.Windows.Forms.ToolStripMenuItem("Facebook Messages (Výchozí)", null, (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                ShowWindow();
                WebViewControl.CoreWebView2?.Navigate("https://www.facebook.com/messages/");
            });
        });
        var navMessenger = new System.Windows.Forms.ToolStripMenuItem("Messenger.com", null, (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                ShowWindow();
                WebViewControl.CoreWebView2?.Navigate("https://www.messenger.com/");
            });
        });
        navMenu.DropDownItems.Add(navFb);
        navMenu.DropDownItems.Add(navMessenger);

        var muteItem = new System.Windows.Forms.ToolStripMenuItem("Ztlumit oznámení");
        muteItem.CheckOnClick = true;
        muteItem.CheckedChanged += (s, e) =>
        {
            _notificationsMuted = muteItem.Checked;
            muteItem.Text = _notificationsMuted ? "Oznámení jsou ztlumena" : "Ztlumit oznámení";
        };

        var startupItem = new System.Windows.Forms.ToolStripMenuItem("Spouštět při startu Windows");
        startupItem.CheckOnClick = true;
        startupItem.Checked = IsRunOnStartupEnabled();
        startupItem.CheckedChanged += (s, e) => SetRunOnStartup(startupItem.Checked);

        var hideBannerItem = new System.Windows.Forms.ToolStripMenuItem("Skrýt horní lištu Facebooku");
        hideBannerItem.CheckOnClick = true;
        hideBannerItem.Checked = _hideFullBanner;
        hideBannerItem.CheckedChanged += (s, e) =>
        {
            _hideFullBanner = hideBannerItem.Checked;
            if (WebViewControl?.CoreWebView2 != null)
            {
                Dispatcher.Invoke(async () => await ApplyCleanUiAsync(WebViewControl.CoreWebView2));
            }
        };

        var devToolsItem = new System.Windows.Forms.ToolStripMenuItem("Vývojářské nástroje (F12)", null, (s, e) =>
        {
            Dispatcher.Invoke(() => WebViewControl.CoreWebView2?.OpenDevToolsWindow());
        });

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Ukončit", null, (s, e) => ExitApp());

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(reloadItem);
        contextMenu.Items.Add(navMenu);
        contextMenu.Items.Add(hideBannerItem);
        contextMenu.Items.Add(devToolsItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add(muteItem);
        contextMenu.Items.Add(startupItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _normalIcon,
            Visible = true,
            Text = "MessengeR",
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ToggleWindow();
            }
        };

        _notifyIcon.DoubleClick += (s, e) => ShowWindow();
    }

    private Icon CreateAlertIcon(Icon baseIcon)
    {
        try
        {
            using var bmp = baseIcon.ToBitmap();
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int dotSize = (int)(bmp.Width * 0.35);
                int x = bmp.Width - dotSize - 1;
                int y = 1;

                // Red circle with white border
                using var whiteBrush = new SolidBrush(Color.White);
                using var redBrush = new SolidBrush(Color.FromArgb(250, 62, 62));

                g.FillEllipse(whiteBrush, x - 1, y - 1, dotSize + 2, dotSize + 2);
                g.FillEllipse(redBrush, x, y, dotSize, dotSize);
            }
            IntPtr hIcon = bmp.GetHicon();
            return System.Drawing.Icon.FromHandle(hIcon);
        }
        catch
        {
            return baseIcon;
        }
    }

    private async System.Threading.Tasks.Task InitializeWebViewAsync()
    {
        try
        {
            LoadingBar.Visibility = Visibility.Visible;

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MessengerPro",
                "UserData"
            );
            Directory.CreateDirectory(userDataFolder);

            var options = new CoreWebView2EnvironmentOptions(
                "--renderer-process-limit=1 " +
                "--js-flags=\"--max_old_space_size=128 --optimize_for_size\" " +
                "--disable-background-networking " +
                "--disable-component-update " +
                "--disable-features=Translate,MediaRouter,OptimizationHints,BackForwardCache " +
                "--enable-features=VaapiVideoDecoder,AudioServiceOutOfProcess " +
                "--disk-cache-size=104857600"
            );

            _environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            await WebViewControl.EnsureCoreWebView2Async(_environment);

            var core = WebViewControl.CoreWebView2;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsBuiltInErrorPageEnabled = true;

            // Set clean Chrome User-Agent so Facebook/Meta doesn't reject embedded WebView2
            core.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

            // Automatically grant camera, mic, and notifications permissions for Messenger
            core.PermissionRequested += Core_PermissionRequested;

            // Handle popups / calls / external links
            core.NewWindowRequested += Core_NewWindowRequested;

            // Block third-party tracking / heavy ad scripts (never block facebook/meta scripts needed for login)
            SetupAdBlocker(core);

            // Hook JavaScript Notification API and title changes
            await InjectNativeNotificationHooksAsync(core);

            core.WebMessageReceived += Core_WebMessageReceived;
            core.DocumentTitleChanged += Core_DocumentTitleChanged;

            core.NavigationCompleted += async (s, e) =>
            {
                LoadingBar.Visibility = Visibility.Collapsed;
                try
                {
                    WebViewControl.ZoomFactor = 0.9;
                }
                catch { }

                if (core != null)
                {
                    await ApplyCleanUiAsync(core);
                }
            };

            // Navigate to Facebook Messages (Primary)
            core.Navigate("https://www.facebook.com/messages/");
        }
        catch (Exception ex)
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            System.Windows.MessageBox.Show(
                $"Chyba při inicializaci WebView2:\n{ex.Message}",
                "Chyba",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void SetupAdBlocker(CoreWebView2 core)
    {
        string[] blockedDomains =
        {
            "*doubleclick.net*",
            "*google-analytics.com*",
            "*googletagmanager.com*"
        };

        foreach (var pattern in blockedDomains)
        {
            core.AddWebResourceRequestedFilter(pattern, CoreWebView2WebResourceContext.All);
        }

        core.WebResourceRequested += (s, args) =>
        {
            args.Response = _environment!.CreateWebResourceResponse(null, 403, "Blocked", "");
        };
    }

    private async System.Threading.Tasks.Task InjectNativeNotificationHooksAsync(CoreWebView2 core)
    {
        // Script that overrides window.Notification to relay to C# host via postMessage
        string script = @"
        (function() {
            if (window.__messengerNativeInjected) return;
            window.__messengerNativeInjected = true;

            // 1. Hook Notification API
            if (typeof Notification !== 'undefined') {
                Notification.requestPermission = function(cb) {
                    if (cb) cb('granted');
                    return Promise.resolve('granted');
                };
                try {
                    Object.defineProperty(Notification, 'permission', {
                        get: function() { return 'granted'; }
                    });
                } catch(e) {}

                window.Notification = function(title, options) {
                    options = options || {};
                    try {
                        window.chrome.webview.postMessage({
                            type: 'notification',
                            title: title || 'MessengeR',
                            body: options.body || '',
                            icon: options.icon || ''
                        });
                    } catch(err) {}

                    return {
                        title: title,
                        body: options.body,
                        close: function() {},
                        addEventListener: function() {},
                        removeEventListener: function() {},
                        dispatchEvent: function() { return true; }
                    };
                };
                window.Notification.permission = 'granted';
                window.Notification.requestPermission = Notification.requestPermission;
            }

            // 2. Observe Document Title changes
            var lastTitle = document.title;
            var sendTitle = function() {
                if (document.title !== lastTitle) {
                    lastTitle = document.title;
                    try {
                        window.chrome.webview.postMessage({
                            type: 'title_change',
                            title: document.title
                        });
                    } catch(e) {}
                }
            };
            setInterval(sendTitle, 1000);

            // 3. Cleaner for Facebook Top Navigation and Messenger Layout
            window.__hideFullFacebookBanner = true;

            function getCleanCss(hideFull) {
                if (hideFull) {
                    return `
                        :root, html, body, div, [id^=""mount_0_0""] {
                            --header-height: 0px !important;
                        }
                        .xxzkxad {
                            top: 0px !important;
                        }
                        .xat3117 {
                            min-height: 100vh !important;
                            height: 100vh !important;
                        }
                        div.x9f619.x1s65kcs.x1o0tod.x16xn7b0.xixxii4.x13vifvy,
                        div[role=""navigation""][aria-label=""Facebook""],
                        div[aria-label=""Facebook""][role=""navigation""],
                        div[aria-label=""Ovládací prvky a nastavení účtu""],
                        ul.xuk3077 {
                            display: none !important;
                            height: 0px !important;
                            max-height: 0px !important;
                            min-height: 0px !important;
                            margin: 0px !important;
                            padding: 0px !important;
                            overflow: hidden !important;
                            visibility: hidden !important;
                            pointer-events: none !important;
                        }
                    `;
                } else {
                    return `
                        div[role=""navigation""][aria-label=""Facebook""],
                        div[aria-label=""Facebook""][role=""navigation""],
                        ul.xuk3077 {
                            display: none !important;
                            height: 0px !important;
                            margin: 0px !important;
                            padding: 0px !important;
                            overflow: hidden !important;
                            visibility: hidden !important;
                        }
                    `;
                }
            }

            window.__setHideFullBanner = function(hideFull) {
                window.__hideFullFacebookBanner = hideFull;
                applyMessengerCleanUi();
            };

            function applyMessengerCleanUi() {
                try {
                    var target = document.head || document.documentElement || document.body;
                    if (target) {
                        var existing = document.getElementById('__messenger_clean_ui');
                        if (!existing) {
                            existing = document.createElement('style');
                            existing.id = '__messenger_clean_ui';
                            existing.type = 'text/css';
                            target.appendChild(existing);
                        }
                        var expectedCss = getCleanCss(window.__hideFullFacebookBanner);
                        if (existing.textContent !== expectedCss) {
                            existing.textContent = expectedCss;
                        }
                    }

                    if (window.__hideFullFacebookBanner) {
                        // Dynamically hide the fixed top Facebook header container
                        var logoOrSearch = document.querySelector('a[aria-label=""Facebook""][href=""/""], input[aria-label*=""Facebook"" i], input[type=""search""]');
                        if (logoOrSearch) {
                            var curr = logoOrSearch;
                            while (curr && curr !== document.body) {
                                var cs = window.getComputedStyle(curr);
                                if (cs.position === 'fixed' || cs.position === 'sticky') {
                                    curr.style.setProperty('display', 'none', 'important');
                                    curr.style.setProperty('height', '0px', 'important');
                                    curr.style.setProperty('min-height', '0px', 'important');
                                    curr.style.setProperty('max-height', '0px', 'important');
                                    curr.style.setProperty('visibility', 'hidden', 'important');
                                    curr.style.setProperty('overflow', 'hidden', 'important');
                                    break;
                                }
                                curr = curr.parentElement;
                            }
                        }

                        // Reset messenger layout top offset and expand container to full height
                        document.querySelectorAll('.xxzkxad').forEach(function(el) {
                            el.style.setProperty('top', '0px', 'important');
                        });
                        document.querySelectorAll('.xat3117').forEach(function(el) {
                            el.style.setProperty('min-height', '100vh', 'important');
                            el.style.setProperty('height', '100vh', 'important');
                        });
                    }

                    document.querySelectorAll('ul.xuk3077').forEach(function(el) {
                        el.style.setProperty('display', 'none', 'important');
                    });
                } catch(e) {}
            }

            try {
                applyMessengerCleanUi();
                document.addEventListener('DOMContentLoaded', applyMessengerCleanUi);
                window.addEventListener('load', applyMessengerCleanUi);

                var observer = new MutationObserver(function() {
                    applyMessengerCleanUi();
                });
                observer.observe(document, { childList: true, subtree: true });

                // Also run periodically to handle dynamic React conversation switches
                setInterval(applyMessengerCleanUi, 500);
            } catch(e) {}
        })();";

        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    private void Core_PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (e.PermissionKind == CoreWebView2PermissionKind.Microphone ||
            e.PermissionKind == CoreWebView2PermissionKind.Camera ||
            e.PermissionKind == CoreWebView2PermissionKind.Notifications)
        {
            e.State = CoreWebView2PermissionState.Allow;
        }
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        string uri = e.Uri;
        if (string.IsNullOrEmpty(uri)) return;

        bool isMetaUri = uri.Contains("messenger.com", StringComparison.OrdinalIgnoreCase) ||
                         uri.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) ||
                         uri.Contains("fb.com", StringComparison.OrdinalIgnoreCase) ||
                         uri.Contains("meta.com", StringComparison.OrdinalIgnoreCase) ||
                         uri.Contains("accountscenter", StringComparison.OrdinalIgnoreCase);

        if (isMetaUri)
        {
            // If it's a messenger audio/video call, open in dedicated CallWindow
            if (uri.Contains("call", StringComparison.OrdinalIgnoreCase) || uri.Contains("videocall", StringComparison.OrdinalIgnoreCase))
            {
                var callWin = new CallWindow();
                callWin.Owner = this;
                e.Handled = true;
                callWin.Show();
                _ = callWin.InitializeWithEnvironmentAsync(_environment!).ContinueWith(t =>
                {
                    Dispatcher.Invoke(() => callWin.ChildWebView.CoreWebView2.Navigate(uri));
                });
            }
            else
            {
                // Login popups, OAuth redirects, and internal Meta links stay inside the app
                e.Handled = false;
            }
        }
        else
        {
            // Non-Meta external links (e.g. shared articles, youtube) open in user's default browser
            try
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeProp))
            {
                string type = typeProp.GetString() ?? "";
                if (type == "notification")
                {
                    string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "MessengeR" : "MessengeR";
                    string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                    string icon = root.TryGetProperty("icon", out var ic) ? ic.GetString() ?? "" : "";

                    OnMessengerNotification(title, body, icon);
                }
                else if (type == "title_change")
                {
                    string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    UpdateTitleStatus(title);
                }
            }
        }
        catch { }
    }

    private void Core_DocumentTitleChanged(object? sender, object e)
    {
        if (WebViewControl.CoreWebView2 != null)
        {
            UpdateTitleStatus(WebViewControl.CoreWebView2.DocumentTitle);
        }
    }

    private void UpdateTitleStatus(string title)
    {
        _currentTitle = string.IsNullOrWhiteSpace(title) ? "MessengeR" : title;
        Title = _currentTitle;

        // Check for unread message indicator like (1), (2), etc.
        bool hasUnread = Regex.IsMatch(_currentTitle, @"\(\d+\)");

        if (_notifyIcon != null)
        {
            _notifyIcon.Icon = hasUnread ? _alertIcon : _normalIcon;
            _notifyIcon.Text = hasUnread ? $"MessengeR: {_currentTitle}" : "MessengeR";
            if (_notifyIcon.Text.Length > 63)
            {
                _notifyIcon.Text = _notifyIcon.Text.Substring(0, 60) + "...";
            }
        }
    }

    private void OnMessengerNotification(string title, string body, string iconUrl)
    {
        if (_notificationsMuted) return;

        // Don't show toast if window is active and in foreground
        if (IsVisible && IsActive && WindowState != WindowState.Minimized)
        {
            return;
        }

        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(body);

            if (!string.IsNullOrEmpty(iconUrl) && Uri.TryCreate(iconUrl, UriKind.Absolute, out var iconUri))
            {
                builder.AddAppLogoOverride(iconUri, ToastGenericAppLogoCrop.Circle);
            }

            builder.Show();
        }
        catch
        {
            // Fallback to tray balloon notification if toast fails
            _notifyIcon?.ShowBalloonTip(4000, title, body, System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
    private System.Windows.Threading.DispatcherTimer? _memoryTimer;

    private void SetupMemoryTimer()
    {
        _memoryTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _memoryTimer.Tick += (s, e) =>
        {
            if (!IsVisible || WindowState == WindowState.Minimized)
            {
                SetMemoryUsageLevel(CoreWebView2MemoryUsageTargetLevel.Low);
                TrimMemory();
            }
        };
        _memoryTimer.Start();
    }

    private void SetMemoryUsageLevel(CoreWebView2MemoryUsageTargetLevel level)
    {
        try
        {
            if (WebViewControl?.CoreWebView2 != null)
            {
                WebViewControl.CoreWebView2.MemoryUsageTargetLevel = level;
            }
        }
        catch { }
    }

    private void TrimMemory()
    {
        try
        {
            // 1. Trim WPF host process
            using (var currentProcess = Process.GetCurrentProcess())
            {
                EmptyWorkingSet(currentProcess.Handle);
            }

            // 2. Trim WebView2 browser process
            if (WebViewControl?.CoreWebView2 != null)
            {
                uint browserPid = WebViewControl.CoreWebView2.BrowserProcessId;
                if (browserPid > 0)
                {
                    TrimPid((int)browserPid);
                }
            }

            // 3. Trim all msedgewebview2 processes in current user session
            int currentSessionId = Process.GetCurrentProcess().SessionId;
            foreach (var p in Process.GetProcessesByName("msedgewebview2"))
            {
                try
                {
                    if (p.SessionId == currentSessionId)
                    {
                        TrimPid(p.Id);
                    }
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch { }
    }

    private static void TrimPid(int pid)
    {
        try
        {
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                EmptyWorkingSet(hProcess);
                CloseHandle(hProcess);
            }
        }
        catch { }
    }

    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();

        SetMemoryUsageLevel(CoreWebView2MemoryUsageTargetLevel.Normal);
    }

    public void HideWindow()
    {
        Hide();
        SetMemoryUsageLevel(CoreWebView2MemoryUsageTargetLevel.Low);
        TrimMemory();
    }

    public void ToggleWindow()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            HideWindow();
        }
        else
        {
            ShowWindow();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            HideWindow();

            if (!_firstMinimizeShown)
            {
                _firstMinimizeShown = true;
                _notifyIcon?.ShowBalloonTip(
                    3000,
                    "MessengeR",
                    "Aplikace běží na pozadí v oznamovací oblasti a šetří paměť RAM.",
                    System.Windows.Forms.ToolTipIcon.Info
                );
            }
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideWindow();
        }
    }

    public void ExitApp()
    {
        _isExiting = true;
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        System.Windows.Application.Current.Shutdown();
    }

    private static bool IsRunOnStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegKey, false);
            return key?.GetValue(AppRegistryName) != null;
        }
        catch { return false; }
    }

    private static void SetRunOnStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegKey, true);
            if (key == null) return;

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppRegistryName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(AppRegistryName, false);
            }
        }
        catch { }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F5)
        {
            WebViewControl.CoreWebView2?.Reload();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F12)
        {
            WebViewControl.CoreWebView2?.OpenDevToolsWindow();
            e.Handled = true;
        }
    }

    private async System.Threading.Tasks.Task ApplyCleanUiAsync(CoreWebView2 core)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                try { WebViewControl.ZoomFactor = 0.9; } catch { }
            });

            string boolVal = _hideFullBanner ? "true" : "false";
            string script = $@"
            (function() {{
                try {{
                    if (typeof window.__setHideFullBanner === 'function') {{
                        window.__setHideFullBanner({boolVal});
                    }}
                }} catch(e) {{}}
            }})();";
            await core.ExecuteScriptAsync(script);
        }
        catch { }
    }

    private void LoadWindowSettings()
    {
        try
        {
            string file = GetWindowSettingsPath();
            if (File.Exists(file))
            {
                string json = File.ReadAllText(file);
                var settings = JsonSerializer.Deserialize<WindowSettings>(json);
                if (settings != null)
                {
                    if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top) &&
                        settings.Left >= SystemParameters.VirtualScreenLeft - 20 &&
                        settings.Left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100 &&
                        settings.Top >= SystemParameters.VirtualScreenTop - 20 &&
                        settings.Top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual;
                        Left = settings.Left;
                        Top = settings.Top;
                    }

                    if (settings.Width >= MinWidth) Width = settings.Width;
                    if (settings.Height >= MinHeight) Height = settings.Height;

                    if (settings.IsMaximized)
                    {
                        WindowState = WindowState.Maximized;
                    }
                }
            }
        }
        catch { }
    }

    private void SaveWindowSettings()
    {
        try
        {
            if (WindowState == WindowState.Minimized) return;

            var settings = new WindowSettings
            {
                IsMaximized = WindowState == WindowState.Maximized,
                Left = WindowState == WindowState.Normal ? Left : (RestoreBounds.Left > 0 ? RestoreBounds.Left : Left),
                Top = WindowState == WindowState.Normal ? Top : (RestoreBounds.Top > 0 ? RestoreBounds.Top : Top),
                Width = WindowState == WindowState.Normal ? Width : (RestoreBounds.Width > 0 ? RestoreBounds.Width : Width),
                Height = WindowState == WindowState.Normal ? Height : (RestoreBounds.Height > 0 ? RestoreBounds.Height : Height)
            };

            string file = GetWindowSettingsPath();
            string? dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(file, JsonSerializer.Serialize(settings));
        }
        catch { }
    }

    private static string GetWindowSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MessengerPro",
            "window_settings.json"
        );
    }
}