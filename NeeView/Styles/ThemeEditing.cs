using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NeeView
{
    /// <summary>
    /// Generic list editor window: add / edit / delete (and optionally reset) items.
    /// </summary>
    public class ItemListEditor<T> : Window where T : class
    {
        private readonly ListBox _list = new();
        private readonly Func<IReadOnlyList<T>> _load;
        private readonly Func<T, UIElement> _view;
        private readonly Func<Window, T?, T?> _edit;
        private readonly Action<T?, T> _commit;
        private readonly Action<T> _remove;

        public ItemListEditor(Window? owner, string title, string hint, Func<IReadOnlyList<T>> load, Func<T, UIElement> view,
            Func<Window, T?, T?> edit, Action<T?, T> commit, Action<T> remove, Action? reset = null)
        {
            _load = load;
            _view = view;
            _edit = edit;
            _commit = commit;
            _remove = remove;

            Title = title;
            Width = 420;
            Height = 400;
            MinWidth = 340;
            MinHeight = 300;
            ShowInTaskbar = false;
            Owner = owner;
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Panel.Background");
            SetResourceReference(ForegroundProperty, "Window.Foreground");

            var padding = new Thickness(14, 3, 14, 3);
            var add = new Button { Content = "추가", MinWidth = 70, Padding = padding, Margin = new Thickness(0, 0, 8, 0) };
            var edit2 = new Button { Content = "편집", MinWidth = 70, Padding = padding, Margin = new Thickness(0, 0, 8, 0) };
            var delete = new Button { Content = "삭제", MinWidth = 70, Padding = padding, Margin = new Thickness(0, 0, 8, 0) };
            var close = new Button { Content = "닫기", MinWidth = 70, Padding = padding, IsDefault = true, IsCancel = true };

            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(add);
            left.Children.Add(edit2);
            left.Children.Add(delete);
            if (reset is not null)
            {
                var resetButton = new Button { Content = "기본값", MinWidth = 80, Padding = padding, Margin = new Thickness(0, 0, 8, 0) };
                resetButton.Click += (s, e) =>
                {
                    if (MessageBox.Show(this, "기본값으로 되돌릴까요?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        reset();
                        Changed = true;
                        Refresh();
                    }
                };
                left.Children.Add(resetButton);
            }

            var bottom = new DockPanel { Margin = new Thickness(10, 6, 10, 10), LastChildFill = false };
            DockPanel.SetDock(close, Dock.Right);
            bottom.Children.Add(close);
            DockPanel.SetDock(left, Dock.Left);
            bottom.Children.Add(left);

            var hintText = new TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 10, 12, 4), Opacity = 0.75 };

            _list.Margin = new Thickness(10, 4, 10, 6);
            _list.HorizontalContentAlignment = HorizontalAlignment.Stretch;

            var root = new DockPanel();
            DockPanel.SetDock(hintText, Dock.Top);
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(hintText);
            root.Children.Add(bottom);
            root.Children.Add(_list);
            Content = root;

            add.Click += (s, e) => EditItem(null);
            edit2.Click += (s, e) => EditItem(Selected);
            delete.Click += (s, e) => DeleteItem();
            close.Click += (s, e) => Close();
            _list.MouseDoubleClick += (s, e) => { if (Selected is not null) EditItem(Selected); };

            Refresh();
        }

        /// <summary>True when anything was added, edited or deleted.</summary>
        public bool Changed { get; private set; }

        private T? Selected => (_list.SelectedItem as ListBoxItem)?.Tag as T;

        private void Refresh(T? select = null)
        {
            var items = _load();
            _list.Items.Clear();
            foreach (var item in items)
            {
                _list.Items.Add(new ListBoxItem { Content = _view(item), Tag = item, Padding = new Thickness(6, 4, 6, 4) });
            }
            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = select is null ? 0 : Math.Max(0, items.ToList().FindIndex(e => Equals(e, select)));
            }
        }

        private void EditItem(T? current)
        {
            var result = _edit(this, current);
            if (result is null) return;

            _commit(current, result);
            Changed = true;
            Refresh(result);
        }

        private void DeleteItem()
        {
            var item = Selected;
            if (item is null) return;
            if (MessageBox.Show(this, "선택한 항목을 삭제할까요?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            _remove(item);
            Changed = true;
            Refresh();
        }
    }

    // ----- viewer background colors ------------------------------------------------------------

    public sealed record BackgroundPreset(string Name, Color Color);

    /// <summary>
    /// The list of viewer background colors shown in the menu / settings. Editable (add / edit / delete).
    /// </summary>
    public static class BackgroundPresetList
    {
        private static string FilePath => Path.Combine(Environment.LocalApplicationDataPath, "BackgroundColors.txt");

        public static IReadOnlyList<BackgroundPreset> Defaults { get; } = new[]
        {
            new BackgroundPreset("어두운 회색", Color.FromRgb(0x20, 0x20, 0x20)),
            new BackgroundPreset("검정", Color.FromRgb(0x00, 0x00, 0x00)),
            new BackgroundPreset("회색", Color.FromRgb(0x80, 0x80, 0x80)),
            new BackgroundPreset("밝은 회색", Color.FromRgb(0xD3, 0xD3, 0xD3)),
            new BackgroundPreset("흰색", Color.FromRgb(0xFF, 0xFF, 0xFF)),
            new BackgroundPreset("베이지", Color.FromRgb(0xF2, 0xE8, 0xD0)),
            new BackgroundPreset("남색", Color.FromRgb(0x1B, 0x25, 0x3F)),
            new BackgroundPreset("녹색", Color.FromRgb(0x1F, 0x3B, 0x2E)),
        };

        public static List<BackgroundPreset> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var list = new List<BackgroundPreset>();
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        var index = line.LastIndexOf('|');
                        if (index <= 0) continue;
                        if (ColorEditing.TryParse(line[(index + 1)..], out var color))
                        {
                            list.Add(new BackgroundPreset(line[..index].Trim(), color));
                        }
                    }
                    if (list.Count > 0) return list;
                }
            }
            catch
            {
                // use defaults
            }
            return Defaults.ToList();
        }

        public static void Save(IEnumerable<BackgroundPreset> presets)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllLines(FilePath, presets.Select(e => $"{e.Name.Replace('|', ' ')}|{ColorEditing.ToHex(e.Color)}"));
            }
            catch
            {
                // a failure to save is not fatal
            }
        }

        /// <summary>Applies the color to the viewer background.</summary>
        public static void Apply(Color color)
        {
            // NOTE: the canvas watches this very BrushSource instance, so it must be edited in place (not replaced).
            var custom = Config.Current.Background.CustomBackground;
            custom.Type = BrushType.SolidColor;
            custom.Color = color;
            Config.Current.Background.BackgroundType = BackgroundType.Custom;
        }

        /// <summary>Shows the editor. Returns true when the list changed.</summary>
        public static bool ShowEditor(Window? owner)
        {
            var items = Load();
            var editor = new ItemListEditor<BackgroundPreset>(
                owner,
                "배경 색 편집",
                "보기 배경 색 목록을 추가, 편집, 삭제할 수 있습니다.",
                () => items,
                item =>
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal };
                    panel.Children.Add(ColorEditing.CreateSwatch(item.Color, 20));
                    panel.Children.Add(new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center });
                    panel.Children.Add(new TextBlock { Text = "   " + ColorEditing.ToHex(item.Color), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 });
                    return panel;
                },
                (window, current) => ShowItemDialog(window, current),
                (old, @new) =>
                {
                    var index = old is null ? -1 : items.IndexOf(old);
                    if (index >= 0) items[index] = @new; else items.Add(@new);
                    Save(items);
                },
                item =>
                {
                    items.Remove(item);
                    Save(items);
                },
                () =>
                {
                    items.Clear();
                    items.AddRange(Defaults);
                    Save(items);
                });

            editor.ShowDialog();
            return editor.Changed;
        }

        private static BackgroundPreset? ShowItemDialog(Window owner, BackgroundPreset? current)
        {
            var name = new TextBox { Text = current?.Name ?? "새 색", MinWidth = 160 };
            var color = new ColorInput(current?.Color ?? Color.FromRgb(0x30, 0x30, 0x30));

            return ShowFieldsDialog(owner, current is null ? "배경 색 추가" : "배경 색 편집",
                new[] { ("이름", (UIElement)name), ("색", color) },
                () =>
                {
                    var text = name.Text.Trim();
                    if (text.Length == 0)
                    {
                        MessageBox.Show(owner, "이름을 입력해 주세요.", "배경 색", MessageBoxButton.OK, MessageBoxImage.Information);
                        return null;
                    }
                    return new BackgroundPreset(text, color.Color);
                });
        }

        /// <summary>A small form dialog: label + control rows with OK / Cancel.</summary>
        public static TResult? ShowFieldsDialog<TResult>(Window owner, string title, IEnumerable<(string Label, UIElement Control)> rows, Func<TResult?> collect) where TResult : class
        {
            var grid = new Grid { Margin = new Thickness(16, 12, 16, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var row = 0;
            foreach (var (label, control) in rows)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 8, 6) };
                Grid.SetRow(text, row);
                grid.Children.Add(text);
                if (control is FrameworkElement fe) fe.Margin = new Thickness(0, 6, 0, 6);
                Grid.SetRow(control, row);
                Grid.SetColumn(control, 1);
                grid.Children.Add(control);
                row++;
            }

            var padding = new Thickness(14, 3, 14, 3);
            var ok = new Button { Content = "확인", MinWidth = 70, Padding = padding, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "취소", MinWidth = 70, Padding = padding, IsCancel = true };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 4, 16, 12) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(grid);

            var window = new Window
            {
                Title = title,
                Width = 420,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = root,
            };
            window.SetResourceReference(Window.BackgroundProperty, "Panel.Background");
            window.SetResourceReference(Window.ForegroundProperty, "Window.Foreground");

            TResult? result = null;
            ok.Click += (s, e) =>
            {
                result = collect();
                if (result is not null) window.DialogResult = true;
            };

            return window.ShowDialog() == true ? result : null;
        }
    }

    // ----- custom UI themes --------------------------------------------------------------------

    public sealed record CustomTheme(string Name, bool IsLight, Color Window, Color Text, Color Panel, Color Accent);

    /// <summary>
    /// Custom UI themes stored as NeeView theme files in the custom theme folder
    /// (BasedOn + Colors). Window color, text color, panel color and accent color can be edited.
    /// </summary>
    public static class CustomThemeList
    {
        private static readonly Color _darkWindow = Color.FromRgb(0x00, 0x00, 0x00);
        private static readonly Color _darkText = Color.FromRgb(0xF0, 0xF0, 0xF0);
        private static readonly Color _darkPanel = Color.FromRgb(0x20, 0x20, 0x20);
        private static readonly Color _lightWindow = Color.FromRgb(0xFF, 0xFF, 0xFF);
        private static readonly Color _lightText = Color.FromRgb(0x20, 0x20, 0x20);
        private static readonly Color _lightPanel = Color.FromRgb(0xF3, 0xF3, 0xF3);
        private static readonly Color _accent = Color.FromRgb(0x11, 0x88, 0xDD);

        private static string Folder
        {
            get
            {
                var folder = Config.Current.Theme.CustomThemeFolder;
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        private static string FilePathOf(string name) => Path.Combine(Folder, name + ".json");

        public static List<CustomTheme> Load()
        {
            var list = new List<CustomTheme>();
            try
            {
                foreach (var file in new DirectoryInfo(Folder).GetFiles("*.json").OrderBy(e => e.Name))
                {
                    var theme = Read(file.FullName);
                    if (theme is not null) list.Add(theme);
                }
            }
            catch
            {
                // ignore unreadable folders
            }
            return list;
        }

        public static (Color Window, Color Text, Color Panel, Color Accent) DefaultsOf(bool isLight)
        {
            return isLight
                ? (_lightWindow, _lightText, _lightPanel, _accent)
                : (_darkWindow, _darkText, _darkPanel, _accent);
        }

        private static CustomTheme? Read(string path)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var root = doc.RootElement;
                var basedOn = root.TryGetProperty("BasedOn", out var b) ? b.GetString() ?? "" : "";
                var isLight = basedOn.Contains("Light", StringComparison.OrdinalIgnoreCase);
                var d = DefaultsOf(isLight);

                Color Get(JsonElement colors, string key, Color fallback)
                {
                    return colors.ValueKind == JsonValueKind.Object
                        && colors.TryGetProperty(key, out var v)
                        && v.ValueKind == JsonValueKind.String
                        && ColorEditing.TryParse(v.GetString(), out var c)
                            ? Color.FromRgb(c.R, c.G, c.B)
                            : fallback;
                }

                root.TryGetProperty("Colors", out var colorsElement);
                return new CustomTheme(
                    Path.GetFileNameWithoutExtension(path),
                    isLight,
                    Get(colorsElement, "Window.Background", d.Window),
                    Get(colorsElement, "Window.Foreground", d.Text),
                    Get(colorsElement, "Panel.Background", d.Panel),
                    Get(colorsElement, "Control.Accent", d.Accent));
            }
            catch
            {
                return null;
            }
        }

        public static void Save(CustomTheme theme)
        {
            var colors = new Dictionary<string, string>
            {
                ["Window.Background"] = ColorEditing.ToThemeHex(theme.Window),
                ["Window.Foreground"] = ColorEditing.ToThemeHex(theme.Text),
                ["Panel.Background"] = ColorEditing.ToThemeHex(theme.Panel),
                ["Menu.Background"] = ColorEditing.ToThemeHex(theme.Panel),
                ["SideBar.Background"] = ColorEditing.ToThemeHex(theme.Panel),
                ["Control.Accent"] = ColorEditing.ToThemeHex(theme.Accent),
            };
            var data = new Dictionary<string, object>
            {
                ["Format"] = "NeeView.Theme/1.0.0",
                ["BasedOn"] = theme.IsLight ? "themes://LightTheme.json" : "themes://DarkTheme.json",
                ["Colors"] = colors,
            };
            File.WriteAllText(FilePathOf(theme.Name), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static void Delete(CustomTheme theme)
        {
            var path = FilePathOf(theme.Name);
            if (File.Exists(path)) File.Delete(path);

            var current = Config.Current.Theme.ThemeType;
            if (current.Type == ThemeType.Custom && string.Equals(current.FileName, theme.Name + ".json", StringComparison.OrdinalIgnoreCase))
            {
                Config.Current.Theme.ThemeType = new ThemeSource(ThemeType.Dark);
            }
        }

        public static void Rename(string oldName, string newName)
        {
            var oldPath = FilePathOf(oldName);
            if (File.Exists(oldPath)) File.Delete(oldPath);

            var current = Config.Current.Theme.ThemeType;
            if (current.Type == ThemeType.Custom && string.Equals(current.FileName, oldName + ".json", StringComparison.OrdinalIgnoreCase))
            {
                Config.Current.Theme.ThemeType = new ThemeSource(ThemeType.Custom, newName + ".json");
            }
        }

        /// <summary>Selects the custom theme and reloads its colors.</summary>
        public static void Apply(CustomTheme theme)
        {
            Config.Current.Theme.ThemeType = new ThemeSource(ThemeType.Custom, theme.Name + ".json");
            ThemeManager.Current.RefreshThemeColor();
        }

        /// <summary>Re-reads the active theme (used after the active custom theme was edited).</summary>
        public static void RefreshActive()
        {
            ThemeManager.Current.RefreshThemeColor();
        }

        /// <summary>Shows the editor. Returns true when the list changed.</summary>
        public static bool ShowEditor(Window? owner)
        {
            var editor = new ItemListEditor<CustomTheme>(
                owner,
                "테마 편집",
                "직접 만든 테마의 창 색, 글자 색, 패널 색, 강조 색을 추가, 편집, 삭제할 수 있습니다. 목록에서 선택해 편집하면 바로 적용됩니다.",
                () => Load(),
                item =>
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal };
                    panel.Children.Add(ColorEditing.CreateSwatch(item.Window, 20));
                    panel.Children.Add(ColorEditing.CreateSwatch(item.Text, 20));
                    panel.Children.Add(ColorEditing.CreateSwatch(item.Accent, 20));
                    panel.Children.Add(new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center });
                    return panel;
                },
                (window, current) => ShowItemDialog(window, current),
                (old, @new) =>
                {
                    if (old is not null && !string.Equals(old.Name, @new.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        Rename(old.Name, @new.Name);
                    }
                    Save(@new);
                    var active = Config.Current.Theme.ThemeType;
                    if (active.Type == ThemeType.Custom && string.Equals(active.FileName, @new.Name + ".json", StringComparison.OrdinalIgnoreCase))
                    {
                        RefreshActive();
                    }
                    else if (old is null)
                    {
                        Apply(@new);
                    }
                },
                item => Delete(item));

            editor.ShowDialog();
            return editor.Changed;
        }

        private static CustomTheme? ShowItemDialog(Window owner, CustomTheme? current)
        {
            var d = DefaultsOf(current?.IsLight ?? false);

            var name = new TextBox { Text = current?.Name ?? "내 테마", MinWidth = 160 };
            var baseTheme = new ComboBox();
            baseTheme.Items.Add("어두운 테마 기반");
            baseTheme.Items.Add("밝은 테마 기반");
            baseTheme.SelectedIndex = (current?.IsLight ?? false) ? 1 : 0;

            var window = new ColorInput(current?.Window ?? d.Window);
            var text = new ColorInput(current?.Text ?? d.Text);
            var panel = new ColorInput(current?.Panel ?? d.Panel);
            var accent = new ColorInput(current?.Accent ?? d.Accent);

            if (current is null)
            {
                baseTheme.SelectionChanged += (s, e) =>
                {
                    var defaults = DefaultsOf(baseTheme.SelectedIndex == 1);
                    window.Color = defaults.Window;
                    text.Color = defaults.Text;
                    panel.Color = defaults.Panel;
                    accent.Color = defaults.Accent;
                };
            }

            var invalid = Path.GetInvalidFileNameChars();
            return BackgroundPresetList.ShowFieldsDialog(owner, current is null ? "테마 추가" : "테마 편집",
                new[]
                {
                    ("이름", (UIElement)name),
                    ("기본 테마", baseTheme),
                    ("창 색", window),
                    ("글자 색", text),
                    ("패널/메뉴 색", panel),
                    ("강조 색", accent),
                },
                () =>
                {
                    var n = name.Text.Trim();
                    if (n.Length == 0 || n.IndexOfAny(invalid) >= 0)
                    {
                        MessageBox.Show(owner, "이름을 입력해 주세요. (\\ / : * ? \" < > | 는 사용할 수 없습니다.)", "테마", MessageBoxButton.OK, MessageBoxImage.Information);
                        return null;
                    }

                    var renamed = current is null || !string.Equals(current.Name, n, StringComparison.OrdinalIgnoreCase);
                    if (renamed && File.Exists(FilePathOf(n)))
                    {
                        MessageBox.Show(owner, "같은 이름의 테마가 이미 있습니다.", "테마", MessageBoxButton.OK, MessageBoxImage.Information);
                        return null;
                    }

                    return new CustomTheme(n, baseTheme.SelectedIndex == 1, window.Color, text.Color, panel.Color, accent.Color);
                });
        }
    }
}
