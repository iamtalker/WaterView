using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NeeView
{
    /// <summary>
    /// The list of slideshow intervals (seconds) shown in the toolbar dropdown.
    /// Stored as a plain text file in the user data folder, one value per line.
    /// </summary>
    public static class SlideShowIntervalList
    {
        private static readonly double[] _defaults = { 1, 2, 3, 5, 7, 10, 15, 20, 30 };

        private static string FilePath => Path.Combine(Environment.LocalApplicationDataPath, "SlideShowIntervals.txt");

        public static IReadOnlyList<double> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var values = Parse(File.ReadAllText(FilePath));
                    if (values.Count > 0) return values;
                }
            }
            catch
            {
                // fall back to defaults
            }
            return _defaults;
        }

        public static void Save(IEnumerable<double> values)
        {
            try
            {
                var text = string.Join(System.Environment.NewLine, values.Select(e => e.ToString("0.#", CultureInfo.InvariantCulture)));
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, text);
            }
            catch
            {
                // a failure to save is not fatal
            }
        }

        public static List<double> Parse(string text)
        {
            return text
                .Split(new[] { '\r', '\n', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().TrimEnd('초', 's', 'S'))
                .Select(e => double.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
                .Where(v => !double.IsNaN(v) && v >= 0.1 && v <= 3600)
                .Select(v => Math.Round(v, 1))
                .Distinct()
                .OrderBy(v => v)
                .ToList();
        }

        /// <summary>
        /// Shows the editor. Returns true when the list was changed.
        /// </summary>
        public static bool ShowEditor(Window? owner)
        {
            var box = new TextBox
            {
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = string.Join(System.Environment.NewLine, Load().Select(e => e.ToString("0.#", CultureInfo.InvariantCulture))),
                Margin = new Thickness(10, 6, 10, 6),
            };

            var hint = new TextBlock
            {
                Text = "한 줄에 한 값씩 초 단위로 입력하세요. (0.1 ~ 3600) 줄을 지우거나 추가하면 메뉴에 반영됩니다.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 10, 10, 0),
            };

            var reset = new Button { Content = "기본값", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
            var ok = new Button { Content = "확인", Width = 70, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "취소", Width = 70, IsCancel = true };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10, 0, 10, 10) };
            buttons.Children.Add(reset);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new DockPanel();
            DockPanel.SetDock(hint, Dock.Top);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(hint);
            root.Children.Add(buttons);
            root.Children.Add(box);

            var window = new Window
            {
                Title = "슬라이드쇼 간격 편집",
                Width = 340,
                Height = 360,
                Owner = owner,
                WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Content = root,
            };

            reset.Click += (s, e) => box.Text = string.Join(System.Environment.NewLine, _defaults.Select(v => v.ToString("0.#", CultureInfo.InvariantCulture)));
            ok.Click += (s, e) => window.DialogResult = true;

            if (window.ShowDialog() != true) return false;

            var values = Parse(box.Text);
            Save(values.Count > 0 ? values : _defaults);
            return true;
        }
    }
}
