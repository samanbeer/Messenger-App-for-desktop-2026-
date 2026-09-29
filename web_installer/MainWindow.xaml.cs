using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace MessengeR.WebInstaller;

public partial class MainWindow : Window
{
    private const string GitHubRepo = "samanbeer/Messenger-App-for-desktop-2026-";

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        BtnInstall.IsEnabled = false;
        ChkDesktop.IsEnabled = false;
        ChkStartMenu.IsEnabled = false;

        try
        {
            StatusText.Text = "Zjišťuji nejnovější verzi na GitHubu...";
            DownloadProgress.IsIndeterminate = true;

            string downloadUrl = "";
            string tagName = "latest";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MessengeR-WebInstaller");
            client.Timeout = TimeSpan.FromSeconds(15);

            try
            {
                string apiUrl = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    tagName = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "latest" : "latest";

                    if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                                if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = $"https://github.com/{GitHubRepo}/releases/latest/download/MessengeR_Setup.exe";
            }

            StatusText.Text = $"Stahuji instalátor MessengeR ({tagName})...";
            DownloadProgress.IsIndeterminate = false;

            string tempSetup = Path.Combine(Path.GetTempPath(), "MessengeR_Setup_Downloaded.exe");

            using var getRes = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!getRes.IsSuccessStatusCode)
            {
                throw new Exception($"GitHub vrátil kód {getRes.StatusCode}. Ujistěte se, že je na GitHubu publikován Release s instalačním souborem MessengeR_Setup.exe.");
            }

            long? totalBytes = getRes.Content.Headers.ContentLength;
            using var stream = await getRes.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempSetup, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long bytesReadTotal = 0;
            int bytesRead;

            var sw = Stopwatch.StartNew();

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                bytesReadTotal += bytesRead;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    double pct = (double)bytesReadTotal / totalBytes.Value * 100.0;
                    double mbRead = bytesReadTotal / (1024.0 * 1024.0);
                    double mbTotal = totalBytes.Value / (1024.0 * 1024.0);
                    double speed = mbRead / (sw.Elapsed.TotalSeconds + 0.001);

                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgress.Value = pct;
                        SpeedText.Text = $"{mbRead:F1} MB / {mbTotal:F1} MB ({speed:F1} MB/s)";
                    });
                }
            }

            fileStream.Close();

            StatusText.Text = "Spouštím instalaci...";
            DownloadProgress.IsIndeterminate = true;
            SpeedText.Text = "";

            // Prepare tasks arguments
            var tasks = new System.Collections.Generic.List<string>();
            if (ChkDesktop.IsChecked == true) tasks.Add("desktopicon");
            if (ChkStartMenu.IsChecked == true) tasks.Add("startmenuicon");

            string taskArg = tasks.Count > 0 ? $"/TASKS=\"{string.Join(",", tasks)}\"" : "";
            string fullArgs = $"/SILENT {taskArg} /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS".Trim();

            Process.Start(new ProcessStartInfo
            {
                FileName = tempSetup,
                Arguments = fullArgs,
                UseShellExecute = true
            });

            // Brief delay then close installer
            await Task.Delay(1000);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Chyba při instalaci.";
            SpeedText.Text = "";
            DownloadProgress.Value = 0;
            DownloadProgress.IsIndeterminate = false;
            BtnInstall.IsEnabled = true;
            ChkDesktop.IsEnabled = true;
            ChkStartMenu.IsEnabled = true;

            MessageBox.Show(
                $"Nepodařilo se stáhnout nebo spustit instalátor:\n\n{ex.Message}\n\nPokud na GitHubu zatím není vytvořen Release, vytvořte jej prosím v repozitáři na GitHubu.",
                "Chyba instalace",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}
