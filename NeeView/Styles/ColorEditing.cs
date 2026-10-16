using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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
    /// A color chooser with a large saturation/brightness square, a hue bar, a hex field and old/new swatches.
    /// </summary>
    public class ColorPickerDialog : Window
    {
        private const double FieldWidth = 240;
        private const double FieldHeight = 190;
        private const double HueHeight = 18;

        private readonly Rectangle _hueLayer = new() { Width = FieldWidth, Height = FieldHeight };
        private readonly Canvas _fieldCanvas = new() { Width = FieldWidth, Height = FieldHeight, Background = Brushes.Transparent, Cursor = Cursors.Cross, ClipToBounds = true };
        private readonly Ellipse _fieldMarker = new() { Width = 14, Height = 14, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
        private readonly Ellipse _fieldMarkerOuter = new() { Width = 16, Height = 16, Stroke = Brushes.Black, StrokeThickness = 1, IsHitTestVisible = false };
        private readonly Canvas _hueCanvas = new() { Width = FieldWidth, Height = HueHeight, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        private readonly Rectangle _hueMarker = new() { Width = 4, Height = HueHeight + 4, Stroke = Brushes.White, StrokeThickness = 1.5, Fill = Brushes.Black, IsHitTestVisible = false };
        private readonly TextBox _hex = new() { Width = 90 };
        private readonly Border _newSwatch = new() { Width = 56, Height = 32, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
        private readonly Border _oldSwatch = new() { Width = 56, Height = 32, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };

        private double _hue;        // 0..360
        private double _saturation; // 0..1
        private double _value;      // 0..1
        private bool _isUpdating;
        private bool _isDraggingField;
        private bool _isDraggingHue;

        private ColorPickerDialog(Window? owner, Color initial, string title)
        {
            Title = title;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Owner = owner;
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Panel.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            // saturation (left->right: white->hue) and brightness (top->bottom: transparent->black) layers
            var fieldGrid = new Grid { Width = FieldWidth, Height = FieldHeight };
            fieldGrid.Children.Add(_hueLayer);
            fieldGrid.Children.Add(new Rectangle
            {
                Width = FieldWidth,
                Height = FieldHeight,
                Fill = new LinearGradientBrush(Colors.White, Colors.Transparent, new Point(0, 0), new Point(1, 0)),
            });
            fieldGrid.Children.Add(new Rectangle
            {
                Width = FieldWidth,
                Height = FieldHeight,
                Fill = new LinearGradientBrush(Colors.Transparent, Colors.Black, new Point(0, 0), new Point(0, 1)),
            });
            _fieldCanvas.Children.Add(_fieldMarkerOuter);
            _fieldCanvas.Children.Add(_fieldMarker);
            fieldGrid.Children.Add(_fieldCanvas);
            var fieldBorder = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = fieldGrid, HorizontalAlignment = HorizontalAlignment.Left };

            // hue bar
            var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            for (var i = 0; i <= 6; i++)
            {
                rainbow.GradientStops.Add(new GradientStop(FromHsv(i * 60.0, 1, 1), i / 6.0));
            }
            var hueGrid = new Grid { Width = FieldWidth, Height = HueHeight, Margin = new Thickness(0, 10, 0, 0) };
            hueGrid.Children.Add(new Rectangle { Width = FieldWidth, Height = HueHeight, Fill = rainbow });
            _hueCanvas.Children.Add(_hueMarker);
            hueGrid.Children.Add(_hueCanvas);
            var hueBorder = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = hueGrid, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
            hueGrid.Margin = new Thickness(0);

            // swatches and hex
            var swatches = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var oldPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            oldPanel.Children.Add(new TextBlock { Text = "이전", FontSize = 11, Opacity = 0.7 });
            oldPanel.Children.Add(_oldSwatch);
            var newPanel = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
            newPanel.Children.Add(new TextBlock { Text = "현재", FontSize = 11, Opacity = 0.7 });
            newPanel.Children.Add(_newSwatch);
            var hexPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            hexPanel.Children.Add(new TextBlock { Text = "색 코드", FontSize = 11, Opacity = 0.7 });
            hexPanel.Children.Add(_hex);
            swatches.Children.Add(oldPanel);
            swatches.Children.Add(newPanel);
            swatches.Children.Add(hexPanel);

            var padding = new Thickness(14, 3, 14, 3);
            var ok = new Button { Content = "확인", MinWidth = 70, Padding = padding, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "취소", MinWidth = 70, Padding = padding, IsCancel = true };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
            root.Children.Add(fieldBorder);
            root.Children.Add(hueBorder);
            root.Children.Add(swatches);
            root.Children.Add(buttons);
            Content = root;

            // field: drag to choose saturation / brightness
            _fieldCanvas.MouseLeftButtonDown += (s, e) => { _isDraggingField = true; _fieldCanvas.CaptureMouse(); UpdateFromField(e.GetPosition(_fieldCanvas)); };
            _fieldCanvas.MouseMove += (s, e) => { if (_isDraggingField) UpdateFromField(e.GetPosition(_fieldCanvas)); };
            _fieldCanvas.MouseLeftButtonUp += (s, e) => { _isDraggingField = false; _fieldCanvas.ReleaseMouseCapture(); };

            // hue bar: drag to choose hue
            _hueCanvas.MouseLeftButtonDown += (s, e) => { _isDraggingHue = true; _hueCanvas.CaptureMouse(); UpdateFromHue(e.GetPosition(_hueCanvas)); };
            _hueCanvas.MouseMove += (s, e) => { if (_isDraggingHue) UpdateFromHue(e.GetPosition(_hueCanvas)); };
            _hueCanvas.MouseLeftButtonUp += (s, e) => { _isDraggingHue = false; _hueCanvas.ReleaseMouseCapture(); };

            _hex.TextChanged += (s, e) =>
            {
                if (_isUpdating) return;
                if (ColorEditing.TryParse(_hex.Text, out var parsed))
                {
                    SetFromColor(parsed, updateHex: false);
                }
            };
            ok.Click += (s, e) => DialogResult = true;

            _oldSwatch.Background = new SolidColorBrush(Color.FromRgb(initial.R, initial.G, initial.B));
            SetFromColor(initial, updateHex: true);
        }

        public Color SelectedColor { get; private set; }

        /// <summary>Shows the chooser. Returns null when canceled.</summary>
        public static Color? Pick(Window? owner, Color initial, string title = "색 선택")
        {
            var dialog = new ColorPickerDialog(owner, initial, title);
            return dialog.ShowDialog() == true ? dialog.SelectedColor : null;
        }

        private void UpdateFromField(Point point)
        {
            _saturation = Math.Clamp(point.X / FieldWidth, 0, 1);
            _value = 1.0 - Math.Clamp(point.Y / FieldHeight, 0, 1);
            Apply(updateHex: true);
        }

        private void UpdateFromHue(Point point)
        {
            _hue = Math.Clamp(point.X / FieldWidth, 0, 1) * 360.0;
            Apply(updateHex: true);
        }

        private void SetFromColor(Color color, bool updateHex)
        {
            ToHsv(color, out _hue, out _saturation, out _value);
            Apply(updateHex);
        }

        private void Apply(bool updateHex)
        {
            _isUpdating = true;
            try
            {
                var color = FromHsv(_hue, _saturation, _value);
                SelectedColor = color;
                _newSwatch.Background = new SolidColorBrush(color);
                _hueLayer.Fill = new SolidColorBrush(FromHsv(_hue, 1, 1));

                var x = _saturation * FieldWidth;
                var y = (1.0 - _value) * FieldHeight;
                Canvas.SetLeft(_fieldMarker, x - _fieldMarker.Width / 2);
                Canvas.SetTop(_fieldMarker, y - _fieldMarker.Height / 2);
                Canvas.SetLeft(_fieldMarkerOuter, x - _fieldMarkerOuter.Width / 2);
                Canvas.SetTop(_fieldMarkerOuter, y - _fieldMarkerOuter.Height / 2);

                Canvas.SetLeft(_hueMarker, _hue / 360.0 * FieldWidth - _hueMarker.Width / 2);
                Canvas.SetTop(_hueMarker, -2);

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

        private static Color FromHsv(double hue, double saturation, double value)
        {
            hue = ((hue % 360.0) + 360.0) % 360.0;
            var c = value * saturation;
            var x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
            var m = value - c;

            double r, g, b;
            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }

        private static void ToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            value = max;
            saturation = max <= 0 ? 0 : delta / max;

            if (delta <= 0) hue = 0;
            else if (max == r) hue = 60 * (((g - b) / delta) % 6);
            else if (max == g) hue = 60 * (((b - r) / delta) + 2);
            else hue = 60 * (((r - g) / delta) + 4);

            if (hue < 0) hue += 360;
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
