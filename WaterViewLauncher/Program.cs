using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WaterViewLauncher
{
    /// <summary>
    /// Tiny launcher. The real application lives in the "app" folder next to this exe.
    /// If the .NET 10 Desktop Runtime is missing, it offers to download and install it first.
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
