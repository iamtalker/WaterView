using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WaterViewLauncher;

/// <summary>
/// Tiny launcher. The real application lives in the "app" folder next to this exe,
/// so that only this exe is visible at the top level of the distribution folder.
/// </summary>
internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private static int Main(string[] args)
    {
        var target = Path.Combine(AppContext.BaseDirectory, "app", "WaterView.exe");
        if (!File.Exists(target))
        {
            MessageBoxW(IntPtr.Zero, "app\\WaterView.exe 를 찾을 수 없습니다.\n압축을 푼 폴더 구조를 그대로 유지해 주세요.", "물뷰", 0x10);
            return 1;
        }

        var info = new ProcessStartInfo(target)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(target)!,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        try
        {
            Process.Start(info);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero, ex.Message, "물뷰", 0x10);
            return 1;
        }
    }
}
