using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NeeView
{
    /// <summary>
    /// A small settings window with the commonly used options.
    /// Everything else stays in the detailed settings window ("상세 설정...").
    /// </summary>
    public class QuickSettingsDialog : Window
    {
        private static QuickSettingsDialog? _current;

        private bool _isUpdating;

        private readonly ComboBox _theme = new();
        private readonly ComboBox _background = new();
        private readonly ComboBox _readOrder = new();
        private readonly ComboBox _pageMode = new();
        private readonly ComboBox _interval = new();
        private readonly CheckBox _menuBar = new() { Content = "상단 메뉴 막대를 항상 표시" };
        private readonly CheckBox _explorer = new() { Content = "탐색기 우클릭 메뉴에 \"물뷰로 열기\" 등록" };
        private readonly TextBox _accessKey = new() { Width = 40, MaxLength = 1, HorizontalAlignment = HorizontalAlignment.Left };

        private static readonly (string Label, ThemeType Type)[] _themes =
        {
            ("어두운 테마", ThemeType.Dark),
            ("밝은 테마", ThemeType.Light),
            ("어두운 흑백", ThemeType.DarkMonochrome),
            ("밝은 흑백", ThemeType.LightMonochrome),
            ("고대비", ThemeType.HighContrast),
            ("시스템 설정 따르기", ThemeType.System),
        };

        private static readonly (string Label, Color Color)[] _colors =
        {
            ("어두운 회색", Color.FromRgb(0x20, 0x20, 0x20)),
            ("검정", Color.FromRgb(0x00, 0x00, 0x00)),
            ("회색", Color.FromRgb(0x80, 0x80, 0x80)),
            ("밝은 회색", Color.FromRgb(0xD3, 0xD3, 0xD3)),
            ("흰색", Color.FromRgb(0xFF, 0xFF, 0xFF)),
            ("베이지", Color.FromRgb(0xF2, 0xE8, 0xD0)),
            ("남색", Color.FromRgb(0x1B, 0x25, 0x3F)),
            ("녹색", Color.FromRgb(0x1F, 0x3B, 0x2E)),
        };

        private QuickSettingsDialog()
        {
            Title = "설정";
            Width = 460;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            SetResourceReference(BackgroundProperty, "Panel.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");
            Owner = Application.Current.MainWindow;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid { Margin = new Thickness(16, 12, 16, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            void AddRow(string label, UIElement control)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 8, 6) };
                Grid.SetRow(text, row);
                Grid.SetColumn(text, 0);
                grid.Children.Add(text);
                if (control is FrameworkElement fe) fe.Margin = new Thickness(0, 6, 0, 6);
                Grid.SetRow(control, row);
                Grid.SetColumn(control, 1);
                grid.Children.Add(control);
                row++;
            }
            void AddWide(UIElement control)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                if (control is FrameworkElement fe) fe.Margin = new Thickness(0, 6, 0, 6);
                Grid.SetRow(control, row);
                Grid.SetColumn(control, 0);
                Grid.SetColumnSpan(control, 2);
                grid.Children.Add(control);
                row++;
            }

            AddRow("테마", _theme);
            AddRow("보기 배경 색", _background);
            AddRow("읽는 방향", _readOrder);
            AddRow("페이지 보기", _pageMode);

            var intervalPanel = new DockPanel();
            var editButton = new Button { Content = "편집...", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0) };
            DockPanel.SetDock(editButton, Dock.Right);
            intervalPanel.Children.Add(editButton);
            intervalPanel.Children.Add(_interval);
            AddRow("슬라이드쇼 간격", intervalPanel);

            AddWide(_menuBar);
            AddWide(_explorer);

            var keyPanel = new StackPanel { Orientation = Orientation.Horizontal };
            keyPanel.Children.Add(_accessKey);
            keyPanel.Children.Add(new TextBlock { Text = "  (우클릭 메뉴에서 이 키로 선택)", VerticalAlignment = VerticalAlignment.Center });
            AddRow("우클릭 메뉴 단축키", keyPanel);

            var detail = new Button { Content = "상세 설정...", Padding = new Thickness(14, 4, 14, 4), Margin = new Thickness(0, 0, 8, 0) };
            var close = new Button { Content = "닫기", Padding = new Thickness(14, 4, 14, 4), IsDefault = true, IsCancel = true };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 4, 16, 14) };
            buttons.Children.Add(detail);
            buttons.Children.Add(close);

            var hint = new TextBlock
            {
                Text = "자주 쓰는 항목만 모았습니다. 나머지 모든 설정은 '상세 설정...'에서 바꿀 수 있습니다.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16, 8, 16, 0),
                Opacity = 0.7,
            };

            var root = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Bottom);
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(hint);
            root.Children.Add(buttons);
            root.Children.Add(grid);
            Content = root;

            // items
            foreach (var t in _themes) _theme.Items.Add(t.Label);
            foreach (var c in _colors) _background.Items.Add(c.Label);
            _background.Items.Add("체크무늬");
            _background.Items.Add("이미지에 맞춤(자동)");
            _readOrder.Items.Add("왼쪽 → 오른쪽");
            _readOrder.Items.Add("오른쪽 → 왼쪽 (일본 만화)");
            _pageMode.Items.Add("한 쪽씩");
            _pageMode.Items.Add("두 쪽씩 (펼침)");

            // handlers
            _theme.SelectionChanged += (s, e) =>
            {
                if (_isUpdating || _theme.SelectedIndex < 0) return;
                Config.Current.Theme.ThemeType = new ThemeSource(_themes[_theme.SelectedIndex].Type);
            };
            _background.SelectionChanged += (s, e) =>
            {
                if (_isUpdating || _background.SelectedIndex < 0) return;
                var index = _background.SelectedIndex;
                if (index < _colors.Length)
                {
                    // NOTE: the canvas watches this very BrushSource instance, so it must be edited in place (not replaced).
                    var custom = Config.Current.Background.CustomBackground;
                    custom.Type = BrushType.SolidColor;
                    custom.Color = _colors[index].Color;
                    Config.Current.Background.BackgroundType = BackgroundType.Custom;
                }
                else if (index == _colors.Length)
                {
                    Config.Current.Background.BackgroundType = BackgroundType.Check;
                }
                else
                {
                    Config.Current.Background.BackgroundType = BackgroundType.Auto;
                }
            };
            _readOrder.SelectionChanged += (s, e) =>
            {
                if (_isUpdating || _readOrder.SelectedIndex < 0) return;
                var order = _readOrder.SelectedIndex == 0 ? PageReadOrder.LeftToRight : PageReadOrder.RightToLeft;
                Config.Current.BookSettingDefault.BookReadOrder = order;
                BookSettings.Current.SetBookReadOrder(order);
            };
            _pageMode.SelectionChanged += (s, e) =>
            {
                if (_isUpdating || _pageMode.SelectedIndex < 0) return;
                var mode = _pageMode.SelectedIndex == 0 ? PageMode.SinglePage : PageMode.WidePage;
                Config.Current.BookSettingDefault.PageMode = mode;
                BookSettings.Current.SetPageMode(mode);
            };
            _interval.SelectionChanged += (s, e) =>
            {
                if (_isUpdating || _interval.SelectedItem is not string text) return;
                if (double.TryParse(text.TrimEnd('초'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    Config.Current.SlideShow.SlideShowInterval = seconds;
                }
            };
            editButton.Click += (s, e) =>
            {
                if (SlideShowIntervalList.ShowEditor(this))
                {
                    MainMenu.Current.Update();
                    Refresh();
                }
            };
            _menuBar.Click += (s, e) =>
            {
                if (_isUpdating) return;
                Config.Current.MenuBar.IsHideMenu = _menuBar.IsChecked != true;
            };
            _explorer.Click += (s, e) =>
            {
                if (_isUpdating) return;
                ExplorerContextMenu.Current.IsEnabled = _explorer.IsChecked == true;
                Refresh();
            };
            _accessKey.TextChanged += (s, e) =>
            {
                if (_isUpdating) return;
                var text = _accessKey.Text.Trim();
                if (text.Length == 1 && char.IsLetterOrDigit(text[0]))
                {
                    ExplorerContextMenu.Current.AccessKey = text;
                }
            };
            detail.Click += (s, e) => MainWindowModel.Current.OpenSettingWindow();
            close.Click += (s, e) => Close();

            Closed += (s, e) => _current = null;

            Refresh();
        }

        /// <summary>
        /// Shows the dialog, or activates the one that is already open.
        /// </summary>
        public static void ShowOrActivate()
        {
            if (_current is not null)
            {
                if (_current.WindowState == WindowState.Minimized) _current.WindowState = WindowState.Normal;
                _current.Activate();
                return;
            }

            _current = new QuickSettingsDialog();
            _current.Show();
        }

        private void Refresh()
        {
            _isUpdating = true;
            try
            {
                _theme.SelectedIndex = Array.FindIndex(_themes, e => e.Type == Config.Current.Theme.ThemeType.Type);

                var backgroundType = Config.Current.Background.BackgroundType;
                if (backgroundType == BackgroundType.Custom)
                {
                    var color = Config.Current.Background.CustomBackground.Color;
                    _background.SelectedIndex = Array.FindIndex(_colors, e => e.Color == color);
                }
                else if (backgroundType == BackgroundType.Check)
                {
                    _background.SelectedIndex = _colors.Length;
                }
                else if (backgroundType == BackgroundType.Auto)
                {
                    _background.SelectedIndex = _colors.Length + 1;
                }
                else
                {
                    _background.SelectedIndex = backgroundType == BackgroundType.White ? 4 : 1;
                }

                _readOrder.SelectedIndex = BookSettings.Current.BookReadOrder == PageReadOrder.LeftToRight ? 0 : 1;
                _pageMode.SelectedIndex = BookSettings.Current.PageMode == PageMode.WidePage ? 1 : 0;

                _interval.Items.Clear();
                foreach (var seconds in SlideShowIntervalList.Load())
                {
                    _interval.Items.Add($"{seconds:0.#}초");
                }
                var current = $"{Config.Current.SlideShow.SlideShowInterval:0.#}초";
                if (!_interval.Items.Contains(current)) _interval.Items.Add(current);
                _interval.SelectedItem = current;

                _menuBar.IsChecked = !Config.Current.MenuBar.IsHideMenu;
                _explorer.IsChecked = ExplorerContextMenu.Current.IsEnabled;
                _accessKey.Text = ExplorerContextMenu.Current.AccessKey;
                _accessKey.IsEnabled = _explorer.IsChecked == true;
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}
