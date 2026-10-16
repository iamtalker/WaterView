using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NeeView
{
    /// <summary>
    /// Small helpers for the color editors.
    /// </summary>
    public static class ColorEditing
    {
        public static bool TryParse(string? text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var s = text.Trim();
            if (!s.StartsWith('#')) s = "#" + s;
            if (s.Length != 7 && s.Length != 9) return false;

            try
            {
                color = (Color)ColorConverter.ConvertFromString(s);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string ToHex(Color color)
        {
            return color.A == 0xFF
                ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
                : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>#FFRRGGBB form used in theme files.</summary>
        public static string ToThemeHex(Color color)
        {
            return $"#FF{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public static Border CreateSwatch(Color color, double size = 18)
        {
            return new Border
            {
                Width = size,
                Height = size,
                Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
    }

    /// <summary>
    /// A color chooser with RGB sliders, a hex field and a preview swatch.
    /// </summary>
    public class ColorPickerDialog : Window
    {
        private readonly Slider _red = new() { Minimum = 0, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true };
        private readonly Slider _green = new() { Minimum = 0, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true };
        private readonly Slider _blue = new() { Minimum = 0, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true };
        private readonly TextBox _hex = new() { Width = 90 };
        private readonly Border _swatch = new() { Width = 60, Height = 40, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
        private bool _isUpdating;

        private ColorPickerDialog(Window? owner, Color initial, string title)
        {
            Title = title;
            Width = 340;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Owner = owner;
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Panel.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            var grid = new Grid { Margin = new Thickness(16, 12, 16, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void AddSlider(int row, string label, Slider slider)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(text, row);
                grid.Children.Add(text);
                slider.Margin = new Thickness(0, 6, 0, 6);
                Grid.SetRow(slider, row);
                Grid.SetColumn(slider, 1);
                grid.Children.Add(slider);
                slider.ValueChanged += (s, e) => OnSliderChanged();
            }
            AddSlider(0, "R", _red);
            AddSlider(1, "G", _green);
            AddSlider(2, "B", _blue);

            var hexPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 4, 16, 4) };
            hexPanel.Children.Add(_swatch);
            hexPanel.Children.Add(new TextBlock { Text = "  색 코드  ", VerticalAlignment = VerticalAlignment.Center });
            hexPanel.Children.Add(_hex);

            var ok = new Button { Content = "확인", Width = 70, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "취소", Width = 70, IsCancel = true };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 4, 16, 12) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Bottom);
            DockPanel.SetDock(hexPanel, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(hexPanel);
            root.Children.Add(grid);
            Content = root;

            _hex.TextChanged += (s, e) =>
            {
                if (_isUpdating) return;
                if (ColorEditing.TryParse(_hex.Text, out var parsed))
                {
                    SetColor(parsed, updateHex: false);
                }
            };
            ok.Click += (s, e) => DialogResult = true;

            SetColor(initial, updateHex: true);
        }

        public Color SelectedColor { get; private set; }

        /// <summary>Shows the chooser. Returns null when canceled.</summary>
        public static Color? Pick(Window? owner, Color initial, string title = "색 선택")
        {
            var dialog = new ColorPickerDialog(owner, initial, title);
            return dialog.ShowDialog() == true ? dialog.SelectedColor : null;
        }

        private void OnSliderChanged()
        {
            if (_isUpdating) return;
            SetColor(Color.FromRgb((byte)_red.Value, (byte)_green.Value, (byte)_blue.Value), updateHex: true, updateSliders: false);
        }

        private void SetColor(Color color, bool updateHex, bool updateSliders = true)
        {
            _isUpdating = true;
            try
            {
                color = Color.FromRgb(color.R, color.G, color.B);
                SelectedColor = color;
                _swatch.Background = new SolidColorBrush(color);
                if (updateSliders)
                {
                    _red.Value = color.R;
                    _green.Value = color.G;
                    _blue.Value = color.B;
                }
                if (updateHex)
                {
                    _hex.Text = ColorEditing.ToHex(color);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    /// <summary>
    /// Inline color field: a clickable swatch and a hex text box.
    /// </summary>
    public class ColorInput : StackPanel
    {
        private readonly Border _swatch;
        private readonly TextBox _hex = new() { Width = 90, VerticalContentAlignment = VerticalAlignment.Center };
        private Color _color;

        public ColorInput(Color initial)
        {
            Orientation = Orientation.Horizontal;
            _swatch = ColorEditing.CreateSwatch(initial, 24);
            _swatch.Cursor = Cursors.Hand;
            _swatch.Margin = new Thickness(0, 0, 8, 0);
            _swatch.MouseLeftButtonDown += (s, e) =>
            {
                var picked = ColorPickerDialog.Pick(Window.GetWindow(this), _color);
                if (picked is { } c) Color = c;
            };

            var pick = new Button { Content = "선택...", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 1, 8, 1) };
            pick.Click += (s, e) =>
            {
                var picked = ColorPickerDialog.Pick(Window.GetWindow(this), _color);
                if (picked is { } c) Color = c;
            };

            Children.Add(_swatch);
            Children.Add(_hex);
            Children.Add(pick);

            _hex.TextChanged += (s, e) =>
            {
                if (ColorEditing.TryParse(_hex.Text, out var parsed) && parsed != _color)
                {
                    _color = Color.FromRgb(parsed.R, parsed.G, parsed.B);
                    _swatch.Background = new SolidColorBrush(_color);
                }
            };

            Color = initial;
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = Color.FromRgb(value.R, value.G, value.B);
                _swatch.Background = new SolidColorBrush(_color);
                var hex = ColorEditing.ToHex(_color);
                if (_hex.Text != hex) _hex.Text = hex;
            }
        }
    }
}
