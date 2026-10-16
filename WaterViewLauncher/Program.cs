using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WaterViewLauncher
{
    /// <summary>
    /// Tiny launcher. The real application lives in the "app" folder next to this exe.
    /// - If the .NET 10 Desktop Runtime is missing, it offers to download and install it first.
    /// - "--update zip installDir pid": applies a downloaded update (used by the app's self-update).
    /// </summary>
    internal static class Program
    {
        private const int RequiredMajor = 10;
        private const string RuntimeUrl = "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe";
        private const string RuntimePage = "https://dotnet.microsoft.com/download/dotnet/10.0";
        private const string Caption = "물뷰";

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();

            if (args.Length >= 4 && args[0] == "--update")
            {
                return RunUpdate(args[1], args[2], args[3]);
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var target = Path.Combine(baseDir, "app", "WaterView.exe");
            if (!File.Exists(target))
            {
                MessageBox.Show("app\\WaterView.exe 를 찾을 수 없습니다.\n압축을 푼 폴더 구조를 그대로 유지해 주세요.", Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            if (!HasDesktopRuntime())
            {
                if (!OfferRuntimeInstall())
                {
                    return 1;
                }
            }

            return StartApp(target, args);
        }

        private static int StartApp(string target, string[] args)
        {
            try
            {
                var info = new ProcessStartInfo(target)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(target)!,
                    Arguments = string.Join(" ", args.Select(Quote)),
                };
                Process.Start(info);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static string Quote(string arg)
        {
            // standard Windows command line quoting
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return arg;
            var escaped = arg.Replace("\\\"", "\\\\\"").Replace("\"", "\\\"");
            if (escaped.EndsWith("\\")) escaped += "\\";
            return "\"" + escaped + "\"";
        }

        // ---- self-update ----

        // the launcher's own (water drop) icon, so the small dialogs match the app in the taskbar
        private static System.Drawing.Icon AppIcon()
        {
            try { return System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return null; }
        }

        /// <summary>
        /// Waits for the app to exit, swaps the "app" folder (keeping its Profile folder) and the launcher,
        /// then starts the new version. On failure the previous version is restored.
        /// </summary>
        private static int RunUpdate(string zipPath, string installDir, string pidText)
        {
            using (var form = new Form())
            using (var label = new Label())
            {
                form.Text = Caption;
                form.Icon = AppIcon();
                form.ClientSize = new System.Drawing.Size(340, 64);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.ControlBox = false;
                form.StartPosition = FormStartPosition.CenterScreen;
                label.Text = "물뷰를 업데이트하는 중입니다...";
                label.SetBounds(16, 22, 308, 22);
                form.Controls.Add(label);
                form.Show();
                Application.DoEvents();

                string? error = null;
                try
                {
                    ApplyUpdate(zipPath, installDir, pidText, () => Application.DoEvents());
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                form.Close();

                if (error != null)
                {
                    MessageBox.Show("업데이트에 실패해 이전 버전을 그대로 사용합니다.\n\n" + error + "\n\n(자세한 기록: " + LogPath + ")", Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            // start whatever is installed now (the new version, or the previous one after a failure)
            var launcher = Path.Combine(installDir, "WaterView.exe");
            try
            {
                if (File.Exists(launcher)) Process.Start(new ProcessStartInfo(launcher) { UseShellExecute = false, WorkingDirectory = installDir });
            }
            catch
            {
                // nothing more to do
            }
            return 0;
        }

        private static string LogPath => Path.Combine(Path.GetTempPath(), "WaterViewUpdate", "update.log");

        private static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
            }
            catch
            {
                // logging must never break the update
            }
        }

        private static void ApplyUpdate(string zipPath, string installDir, string pidText, Action pump)
        {
            Log("update start: zip=" + zipPath + " install=" + installDir + " pid=" + pidText);
            installDir = Path.GetFullPath(installDir).TrimEnd('\\');
            var appDir = Path.Combine(installDir, "app");
            var staging = Path.Combine(installDir, ".update-new");
            var backup = Path.Combine(installDir, ".update-old");

            // 1. wait for the running app to exit
            if (int.TryParse(pidText, out var pid))
            {
                try
                {
                    using (var process = Process.GetProcessById(pid))
                    {
                        var waited = 0;
                        while (!process.HasExited && waited < 60000)
                        {
                            Thread.Sleep(200);
                            waited += 200;
                            pump();
                        }
                        if (!process.HasExited) throw new IOException("물뷰가 종료되지 않아 업데이트를 중단했습니다.");
                    }
                }
                catch (ArgumentException)
                {
                    // already gone
                }
            }

            if (!File.Exists(zipPath)) throw new FileNotFoundException("내려받은 파일을 찾을 수 없습니다.", zipPath);

            // 2. extract to a staging folder (never straight over the installed files)
            DeleteDirectory(staging);
            DeleteDirectory(backup);
            Directory.CreateDirectory(staging);
            var stagingFull = Path.GetFullPath(staging).TrimEnd('\\') + "\\";

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry

                    var destination = Path.GetFullPath(Path.Combine(staging, entry.FullName.Replace('/', '\\')));
                    if (!destination.StartsWith(stagingFull, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("올바르지 않은 파일 경로가 들어 있어 중단했습니다: " + entry.FullName);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination, true);
                    pump();
                }
            }

            if (!File.Exists(Path.Combine(staging, "app", "WaterView.exe")) || !File.Exists(Path.Combine(staging, "WaterView.exe")))
            {
                DeleteDirectory(staging);
                throw new IOException("내려받은 파일의 구성이 올바르지 않습니다.");
            }

            Log("extracted to staging");

            // 3. keep the user's data (settings, history, thumbnails): move Profile into the new app folder
            var oldProfile = Path.Combine(appDir, "Profile");
            var newProfile = Path.Combine(staging, "app", "Profile");
            DeleteDirectory(newProfile);

            // 4. swap folders; roll back on any failure
            var movedProfile = false;
            var movedApp = false;
            try
            {
                if (Directory.Exists(oldProfile))
                {
                    Log("moving Profile into the new app folder");
                    MoveWithRetry(oldProfile, newProfile);
                    movedProfile = true;
                }
                Log("renaming app -> .update-old");
                MoveWithRetry(appDir, backup);
                movedApp = true;
                Log("moving new app into place");
                MoveWithRetry(Path.Combine(staging, "app"), appDir);

                Log("replacing the launcher");
                CopyWithRetry(Path.Combine(staging, "WaterView.exe"), Path.Combine(installDir, "WaterView.exe"));
                Log("swap done");
            }
            catch (Exception ex)
            {
                Log("FAILED: " + ex);
                try
                {
                    if (movedApp)
                    {
                        if (Directory.Exists(appDir)) MoveWithRetry(appDir, Path.Combine(installDir, ".update-failed"));
                        MoveWithRetry(backup, appDir);
                    }
                    if (movedProfile && Directory.Exists(newProfile) && !Directory.Exists(oldProfile))
                    {
                        MoveWithRetry(newProfile, oldProfile);
                    }
                }
                catch
                {
                    // best effort
                }
                throw;
            }

            // 5. clean up
            DeleteDirectory(backup);
            DeleteDirectory(Path.Combine(installDir, ".update-failed"));
            DeleteDirectory(staging);
        }

        /// <summary>
        /// Right after the app exits, Windows (or a virus scanner) may still hold files for a moment,
        /// which makes a folder rename fail with "access denied". Retry for a few seconds.
        /// </summary>
        private static void MoveWithRetry(string source, string destination)
        {
            for (var i = 0; ; i++)
            {
                try
                {
                    Directory.Move(source, destination);
                    return;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && i < 20)
                {
                    Log("move retry " + (i + 1) + ": " + source + " -> " + destination + " : " + ex.Message);
                    Thread.Sleep(500);
                }
            }
        }

        private static void CopyWithRetry(string source, string destination)
        {
            for (var i = 0; ; i++)
            {
                try
                {
                    File.Copy(source, destination, true);
                    return;
                }
                catch (IOException) when (i < 10)
                {
                    Thread.Sleep(300);
                }
                catch (UnauthorizedAccessException) when (i < 10)
                {
                    Thread.Sleep(300);
                }
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            for (var i = 0; ; i++)
            {
                try
                {
                    Directory.Delete(path, true);
                    return;
                }
                catch (Exception) when (i < 5)
                {
                    Thread.Sleep(300);
                }
                catch
                {
                    return; // leftovers are removed by the next update
                }
            }
        }

        // ---- runtime detection ----

        private static bool HasDesktopRuntime()
        {
            // test hook: simulate a PC without the runtime
            if (Environment.GetEnvironmentVariable("WATERVIEW_FORCE_NO_RUNTIME") == "1") return false;

            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"))
                {
                    if (key != null && key.GetValueNames().Any(IsRequiredVersion)) return true;
                }
            }
            catch
            {
                // fall through to the folder check
            }

            foreach (var root in new[]
            {
                Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
            })
            {
                if (string.IsNullOrEmpty(root)) continue;
                try
                {
                    var dir = Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App");
                    if (Directory.Exists(dir) && Directory.GetDirectories(dir).Any(d => IsRequiredVersion(Path.GetFileName(d)))) return true;
                }
                catch
                {
                    // ignore
                }
            }

            return false;
        }

        private static bool IsRequiredVersion(string name)
        {
            var dot = name.IndexOf('.');
            return dot > 0 && int.TryParse(name.Substring(0, dot), out var major) && major >= RequiredMajor;
        }

        // ---- runtime install ----

        private static bool OfferRuntimeInstall()
        {
            var answer = MessageBox.Show(
                "물뷰를 실행하려면 .NET 10 데스크톱 런타임이 필요합니다.\n\n" +
                "지금 Microsoft 에서 내려받아 설치할까요?\n" +
                "(약 57MB, 설치 중 관리자 권한 확인 창이 한 번 나타납니다.)",
                Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                MessageBox.Show("직접 설치하려면 아래 페이지에서 \".NET Desktop Runtime 10\" (x64) 을 설치해 주세요.\n" + RuntimePage, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            var installer = Path.Combine(Path.GetTempPath(), "WaterView-dotnet-runtime", "windowsdesktop-runtime-win-x64.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(installer)!);

            if (!Download(RuntimeUrl, installer)) return false;

            try
            {
                var info = new ProcessStartInfo(installer, "/install /passive /norestart")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };
                using (var process = Process.Start(info))
                {
                    process?.WaitForExit();
                    var code = process?.ExitCode ?? -1;
                    if (code != 0 && code != 3010)
                    {
                        MessageBox.Show("설치가 완료되지 않았습니다. (코드 " + code + ")\n직접 설치해 주세요.\n" + RuntimePage, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                MessageBox.Show("관리자 권한 확인이 취소되어 설치하지 않았습니다.", Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("설치를 시작하지 못했습니다.\n" + ex.Message + "\n직접 설치해 주세요.\n" + RuntimePage, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!HasDesktopRuntime())
            {
                MessageBox.Show("설치 후에도 런타임을 찾지 못했습니다. 컴퓨터를 다시 시작한 뒤 실행해 보세요.", Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private static bool Download(string url, string path)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (var form = new Form())
            using (var label = new Label())
            using (var bar = new ProgressBar())
            using (var client = new WebClient())
            {
                form.Text = Caption;
                form.Icon = AppIcon();
                form.ClientSize = new System.Drawing.Size(360, 78);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ControlBox = false;
                form.StartPosition = FormStartPosition.CenterScreen;

                label.Text = ".NET 런타임을 내려받는 중... 0%";
                label.SetBounds(14, 12, 332, 20);
                bar.SetBounds(14, 38, 332, 22);
                bar.Minimum = 0;
                bar.Maximum = 100;
                form.Controls.Add(label);
                form.Controls.Add(bar);

                Exception? error = null;
                var done = false;
                client.DownloadProgressChanged += (s, e) =>
                {
                    bar.Value = Math.Min(100, e.ProgressPercentage);
                    label.Text = ".NET 런타임을 내려받는 중... " + e.ProgressPercentage + "%";
                };
                client.DownloadFileCompleted += (s, e) =>
                {
                    error = e.Error;
                    done = !e.Cancelled && e.Error == null;
                    form.Close();
                };

                form.Shown += (s, e) => client.DownloadFileAsync(new Uri(url), path);
                form.ShowDialog();

                if (!done)
                {
                    MessageBox.Show("내려받지 못했습니다." + (error != null ? "\n" + error.Message : "") + "\n직접 설치해 주세요.\n" + RuntimePage, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                return true;
            }
        }
    }
}
