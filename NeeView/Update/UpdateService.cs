using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NeeView
{
    public sealed record ReleaseInfo(Version Version, string Tag, string Notes, string AssetUrl, long Size, string? Sha256, string PageUrl);

    /// <summary>
    /// Checks GitHub for a newer release, downloads it and hands over to the launcher
    /// (WaterView.exe next to the "app" folder), which swaps the files while the app is closed.
    /// </summary>
    public static class UpdateService
    {
        public const string Repo = "iamtalker/WaterView";
        public const string AssetName = "WaterView.zip";

        // for tests only: point the check at another server (e.g. http://localhost:8765/latest)
        private const string TestApiEnv = "WATERVIEW_UPDATE_API";

        private static readonly string[] _allowedHosts = { "github.com", "api.github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com" };

        private static string ApiUrl => System.Environment.GetEnvironmentVariable(TestApiEnv) is { Length: > 0 } test
            ? test
            : $"https://api.github.com/repos/{Repo}/releases/latest";

        private static bool IsTest => System.Environment.GetEnvironmentVariable(TestApiEnv) is { Length: > 0 };

        // ---- settings (a small json file in the user data folder) ----

        public sealed class UpdateSettings
        {
            public bool AutoCheck { get; set; } = true;
            public DateTime LastCheckUtc { get; set; }
            public string SkippedVersion { get; set; } = "";
        }

        private static string SettingsPath => Path.Combine(Environment.LocalApplicationDataPath, "update.json");

        public static UpdateSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    return JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(SettingsPath)) ?? new UpdateSettings();
                }
            }
            catch
            {
                // use defaults
            }
            return new UpdateSettings();
        }

        public static void SaveSettings(UpdateSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // not fatal
            }
        }

        // ---- check ----

        private static Version CurrentVersion
        {
            get
            {
                var v = Environment.AssemblyVersion;
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"WaterView/{CurrentVersion}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        /// <summary>Returns the newer release, or null when this version is the latest.</summary>
        public static async Task<ReleaseInfo?> CheckAsync(CancellationToken token = default)
        {
            using var client = CreateClient();
            using var response = await client.GetAsync(ApiUrl, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
            latest = new Version(latest.Major, Math.Max(0, latest.Minor), Math.Max(0, latest.Build));
            if (latest <= CurrentVersion) return null;

            if (!root.TryGetProperty("assets", out var assets)) return null;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != AssetName) continue;

                var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                var size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                string? sha = null;
                if (asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String)
                {
                    var digest = d.GetString() ?? "";
                    if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) sha = digest[7..];
                }

                var notes = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";
                var page = root.TryGetProperty("html_url", out var h) ? (h.GetString() ?? "") : $"https://github.com/{Repo}/releases";
                return new ReleaseInfo(latest, tag, SummarizeNotes(notes), url, size, sha, page);
            }

            return null;
        }

        private static string SummarizeNotes(string body)
        {
            var lines = body.Replace("\r", "").Split('\n')
                .Select(CleanNoteLine)
                .Where(l => l.Length > 0)
                .Take(6);
            return string.Join("\n", lines);
        }

        // Drops markdown marks (headings, bullets, bold, code, links) and shortens a line to fit the prompt.
        private static string CleanNoteLine(string line)
        {
            var s = line.Trim().TrimStart('#', '-', '*', '>', ' ');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\[([^\]]*)\]\([^)]*\)", "$1");
            s = s.Replace("**", "").Replace("__", "").Replace("`", "").Trim();
            if (s.Length == 0) return "";
            return s.Length > 70 ? s[..70] + "…" : s;
        }

        // ---- startup / manual entry points ----

        /// <summary>Checks once a day in the background and asks the user when a newer version exists.</summary>
        public static async Task CheckOnStartupAsync(Window owner)
        {
            try
            {
                var settings = LoadSettings();
                if (!settings.AutoCheck) return;
                if (!Environment.IsZipLikePackage && !IsTest) return;
                if (DateTime.UtcNow - settings.LastCheckUtc < TimeSpan.FromHours(20)) return;

                var release = await CheckAsync().ConfigureAwait(true);

                settings.LastCheckUtc = DateTime.UtcNow;
                SaveSettings(settings);

                if (release is null || release.Version.ToString(3) == settings.SkippedVersion) return;
                await PromptAsync(owner, release, settings).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // offline or rate limited: try again the next time
                Debug.WriteLine("update check failed: " + ex.Message);
            }
        }

        /// <summary>Menu / settings button: always reports the result.</summary>
        public static async Task CheckNowAsync(Window? owner)
        {
            try
            {
                var settings = LoadSettings();
                var release = await CheckAsync().ConfigureAwait(true);
                settings.LastCheckUtc = DateTime.UtcNow;
                SaveSettings(settings);

                if (release is null)
                {
                    MessageBox.Show(owner, $"현재 최신 버전입니다. (v{CurrentVersion})", "물뷰 업데이트", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                await PromptAsync(owner, release, settings, allowSkip: false).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "업데이트를 확인하지 못했습니다.\n인터넷 연결을 확인해 주세요.\n\n" + ex.Message, "물뷰 업데이트", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static async Task PromptAsync(Window? owner, ReleaseInfo release, UpdateSettings settings, bool allowSkip = true)
        {
            var buttons = allowSkip ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo;
            var text = $"물뷰 {release.Version.ToString(3)} 버전이 나왔습니다. (현재 {CurrentVersion})\n\n" +
                       (release.Notes.Length > 0 ? release.Notes + "\n\n" : "") +
                       "지금 업데이트할까요?\n" +
                       (allowSkip ? "  예: 지금 업데이트   아니요: 나중에   취소: 이 버전은 건너뛰기" : "  예: 지금 업데이트   아니요: 나중에");

            var answer = MessageBox.Show(owner, text, "물뷰 업데이트", buttons, MessageBoxImage.Information);
            if (answer == MessageBoxResult.Cancel)
            {
                settings.SkippedVersion = release.Version.ToString(3);
                SaveSettings(settings);
                return;
            }
            if (answer != MessageBoxResult.Yes) return;

            await DownloadAndApplyAsync(owner, release).ConfigureAwait(true);
        }

        // ---- download + hand over ----

        private static async Task DownloadAndApplyAsync(Window? owner, ReleaseInfo release)
        {
            var appDir = Environment.AssemblyFolder.TrimEnd('\\', '/');
            var installDir = Directory.GetParent(appDir)?.FullName;
            var launcher = installDir is null ? null : Path.Combine(installDir, "WaterView.exe");

            if (installDir is null || launcher is null || !File.Exists(launcher) || !string.Equals(Path.GetFileName(appDir), "app", StringComparison.OrdinalIgnoreCase))
            {
                if (MessageBox.Show(owner, "이 위치에서는 자동 업데이트를 쓸 수 없습니다.\n새 버전을 내려받는 페이지를 열까요?", "물뷰 업데이트", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    OpenPage(release.PageUrl);
                }
                return;
            }

            if (!CanWrite(installDir))
            {
                if (MessageBox.Show(owner, $"이 폴더에는 쓸 수 없어 자동 업데이트를 할 수 없습니다.\n({installDir})\n새 버전을 내려받는 페이지를 열까요?", "물뷰 업데이트", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    OpenPage(release.PageUrl);
                }
                return;
            }

            if (!IsTest && !IsAllowedUrl(release.AssetUrl))
            {
                MessageBox.Show(owner, "내려받을 주소가 올바르지 않아 업데이트를 중단했습니다.", "물뷰 업데이트", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var workDir = Path.Combine(Path.GetTempPath(), "WaterViewUpdate");
            string zipPath;
            try
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
                Directory.CreateDirectory(workDir);
                zipPath = Path.Combine(workDir, AssetName);

                var ok = await DownloadWithProgressAsync(owner, release, zipPath).ConfigureAwait(true);
                if (!ok) return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "내려받지 못했습니다.\n\n" + ex.Message, "물뷰 업데이트", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // integrity check (when GitHub provides a digest)
            if (release.Sha256 is not null)
            {
                string actual;
                using (var stream = File.OpenRead(zipPath))
                {
                    actual = Convert.ToHexString(await SHA256.HashDataAsync(stream).ConfigureAwait(true));
                }
                if (!string.Equals(actual, release.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(owner, "내려받은 파일이 올바르지 않아 업데이트를 중단했습니다. (체크섬 불일치)", "물뷰 업데이트", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                // run the updater from a copy so that the real launcher file can be replaced
                var updater = Path.Combine(workDir, "WaterViewUpdater.exe");
                File.Copy(launcher, updater, true);

                var info = new ProcessStartInfo(updater) { UseShellExecute = false, WorkingDirectory = workDir };
                info.ArgumentList.Add("--update");
                info.ArgumentList.Add(zipPath);
                info.ArgumentList.Add(installDir);
                info.ArgumentList.Add(System.Environment.ProcessId.ToString());
                Process.Start(info);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "업데이트를 시작하지 못했습니다.\n\n" + ex.Message, "물뷰 업데이트", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // the updater waits for this process to exit, then swaps the files and starts the new version
            Application.Current.Shutdown();
        }

        private static bool IsAllowedUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && _allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
                && (uri.AbsolutePath.StartsWith($"/{Repo}/", StringComparison.OrdinalIgnoreCase) || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase));
        }

        private static bool CanWrite(string dir)
        {
            try
            {
                var probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void OpenPage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // ignore
            }
        }

        private static async Task<bool> DownloadWithProgressAsync(Window? owner, ReleaseInfo release, string path)
        {
            var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 20, Margin = new Thickness(0, 8, 0, 8) };
            var label = new TextBlock { Text = "새 버전을 내려받는 중... 0%" };
            var cancel = new Button { Content = "취소", Padding = new Thickness(16, 3, 16, 3), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true };
            var panel = new StackPanel { Margin = new Thickness(18, 16, 18, 14) };
            panel.Children.Add(label);
            panel.Children.Add(bar);
            panel.Children.Add(cancel);

            var window = new Window
            {
                Title = "물뷰 업데이트",
                Width = 380,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Owner = owner,
                WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Content = panel,
            };
            window.SetResourceReference(Window.BackgroundProperty, "Panel.Background");
            window.SetResourceReference(Window.ForegroundProperty, "Window.Foreground");

            using var cts = new CancellationTokenSource();
            cancel.Click += (s, e) => cts.Cancel();
            window.Show();

            try
            {
                using var client = CreateClient();
                client.Timeout = Timeout.InfiniteTimeSpan;
                using var response = await client.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? release.Size;

                await using var source = await response.Content.ReadAsStreamAsync(cts.Token);
                await using var target = File.Create(path);
                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await source.ReadAsync(buffer, cts.Token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, n), cts.Token);
                    read += n;
                    if (total > 0)
                    {
                        var percent = (int)(read * 100 / total);
                        bar.Value = percent;
                        label.Text = $"새 버전을 내려받는 중... {percent}%  ({read / 1048576.0:0.0} / {total / 1048576.0:0.0} MB)";
                    }
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                window.Close();
            }
        }
    }
}
