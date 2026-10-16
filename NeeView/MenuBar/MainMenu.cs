using CommunityToolkit.Mvvm.ComponentModel;
using NeeLaboratory.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace NeeView
{
    public class MainMenu : ObservableObject
    {
        static MainMenu() => Current = new MainMenu();
        public static MainMenu Current { get; }


        private readonly MenuTree _mainMenuSource;
        private Menu? _menu;

#if DEBUG
        private readonly DebugMenu _debugMenu = new();
#endif


        private MainMenu()
        {
            _mainMenuSource = MenuTree.CreateMainMenuDefault();

            Config.Current.MenuBar.SubscribePropertyChanged(nameof(MenuBarConfig.IsHamburgerMenu),
                (s, e) => Update());

            RoutedCommandTable.Current.Changed +=
                (s, e) => AppDispatcher.Invoke(() => UpdateInputGestureText());

            PageSlider.Current.SliderDirectionChanged +=
                (s, e) => AppDispatcher.Invoke(() => UpdateInputGestureText());

            Config.Current.Command.SubscribePropertyChanged(nameof(CommandConfig.IsReversePageMove),
                (s, e) => AppDispatcher.Invoke(() => UpdateInputGestureText()));

            Update();
        }


        public MenuTree MenuSource => _mainMenuSource;

        public Menu? Menu
        {
            get { return _menu; }
            set { SetProperty(ref _menu, value); }
        }


        public void Update()
        {
            this.Menu = CreateMainMenu(Config.Current.MenuBar.IsHamburgerMenu);
            this.Menu.UpdateInputGestureText();
        }

        public void UpdateInputGestureText()
        {
            Menu?.UpdateInputGestureText();
        }

        private Menu CreateMainMenu(bool isHamburgerMenu)
        {
            var items = _mainMenuSource.CreateMenuItems(true);
#if DEBUG
            items.Add(_debugMenu.CreateDevMenuItem());
#endif

            ReplaceSlideShowItem(items);
            InsertThemeItem(items);

            // Honeyview-like toolbar: icon + label for the top level items
            foreach (var topItem in items.OfType<MenuItem>())
            {
                AddToolbarIcon(topItem);
            }

            var menu = new Menu();
            if (isHamburgerMenu)
            {
                var image = new Image();
                image.Width = 18;
                image.Height = 18;
                image.Source = CreateMenuIcon();

                var topMenu = new MenuItem();
                topMenu.Header = image;
                foreach (var item in items)
                {
                    topMenu.Items.Add(item);
                }
                menu.Items.Add(topMenu);
            }
            else
            {
                menu.Margin = new Thickness(0, 0, 40, 0);
                foreach (var item in items)
                {
                    menu.Items.Add(item);
                }
            }

            return menu;
        }

        /// <summary>
        /// Replaces the slideshow command item with a dropdown: start/stop + interval choices (seconds).
        /// </summary>
        private static void ReplaceSlideShowItem(List<object> items)
        {
            var index = items.FindIndex(e => e is MenuItem m && m.Header is string s && s.Contains("슬라이드"));
            if (index < 0) return;

            var original = (MenuItem)items[index];
            var group = new MenuItem { Header = "슬라이드쇼" };

            var toggle = new MenuItem { Header = "시작 / 정지", InputGestureText = original.InputGestureText };
            toggle.Click += (s, e) => SlideShow.Current.SetPlaying(!SlideShow.Current.IsPlaying);
            group.Items.Add(toggle);
            group.Items.Add(new Separator());

            var intervals = SlideShowIntervalList.Load();
            var intervalItems = new List<(MenuItem Item, double Value)>();
            foreach (var seconds in intervals)
            {
                var value = seconds;
                var interval = new MenuItem { Header = $"{value:0.#}초", IsCheckable = false };
                interval.Click += (s, e) =>
                {
                    Config.Current.SlideShow.SlideShowInterval = value;
                    SlideShow.Current.SetPlaying(true);
                };
                intervalItems.Add((interval, value));
                group.Items.Add(interval);
            }

            group.Items.Add(new Separator());
            var edit = new MenuItem { Header = "편집..." };
            edit.Click += (s, e) =>
            {
                if (SlideShowIntervalList.ShowEditor(Application.Current.MainWindow))
                {
                    Current.Update();
                }
            };
            group.Items.Add(edit);

            group.SubmenuOpened += (s, e) =>
            {
                foreach (var (item, value) in intervalItems)
                {
                    item.IsChecked = Math.Abs(Config.Current.SlideShow.SlideShowInterval - value) < 0.05;
                }
            };

            items[index] = group;
        }

        /// <summary>
        /// Inserts a quick "theme" dropdown (UI theme + viewer background color) before the last item.
        /// </summary>
        private static void InsertThemeItem(List<object> items)
        {
            var group = new MenuItem { Header = "테마" };

            var themes = new (string Label, ThemeType Type)[]
            {
                ("어두운 테마", ThemeType.Dark),
                ("밝은 테마", ThemeType.Light),
                ("어두운 흑백", ThemeType.DarkMonochrome),
                ("밝은 흑백", ThemeType.LightMonochrome),
                ("고대비", ThemeType.HighContrast),
                ("시스템 설정 따르기", ThemeType.System),
            };
            var themeItems = new List<(MenuItem Item, ThemeType Type)>();
            foreach (var (label, type) in themes)
            {
                var themeType = type;
                var item = new MenuItem { Header = label };
                item.Click += (s, e) => Config.Current.Theme.ThemeType = new ThemeSource(themeType);
                themeItems.Add((item, themeType));
                group.Items.Add(item);
            }

            group.Items.Add(new Separator());

            var background = new MenuItem { Header = "보기 배경 색" };
            var colors = new (string Label, byte R, byte G, byte B)[]
            {
                ("어두운 회색", 0x20, 0x20, 0x20),
                ("검정", 0x00, 0x00, 0x00),
                ("회색", 0x80, 0x80, 0x80),
                ("밝은 회색", 0xD3, 0xD3, 0xD3),
                ("흰색", 0xFF, 0xFF, 0xFF),
                ("베이지", 0xF2, 0xE8, 0xD0),
                ("남색", 0x1B, 0x25, 0x3F),
                ("녹색", 0x1F, 0x3B, 0x2E),
            };
            var colorItems = new List<(MenuItem Item, System.Windows.Media.Color Color)>();
            foreach (var (label, r, g, b) in colors)
            {
                var color = System.Windows.Media.Color.FromRgb(r, g, b);
                var item = new MenuItem { Header = label };
                item.Click += (s, e) =>
                {
                    // NOTE: the canvas watches this very BrushSource instance, so it must be edited in place (not replaced).
                    var custom = Config.Current.Background.CustomBackground;
                    custom.Type = BrushType.SolidColor;
                    custom.Color = color;
                    Config.Current.Background.BackgroundType = BackgroundType.Custom;
                };
                colorItems.Add((item, color));
                background.Items.Add(item);
            }
            background.Items.Add(new Separator());
            var checker = new MenuItem { Header = "체크무늬" };
            checker.Click += (s, e) => Config.Current.Background.BackgroundType = BackgroundType.Check;
            background.Items.Add(checker);
            var auto = new MenuItem { Header = "이미지에 맞춤(자동)" };
            auto.Click += (s, e) => Config.Current.Background.BackgroundType = BackgroundType.Auto;
            background.Items.Add(auto);
            group.Items.Add(background);

            group.Items.Add(new Separator());
            var more = new MenuItem { Header = "더 많은 색 설정..." };
            more.Click += (s, e) => ThemeManager.OpenCustomThemeFolder();
            group.Items.Add(more);

            group.SubmenuOpened += (s, e) =>
            {
                var current = Config.Current.Theme.ThemeType.Type;
                foreach (var (item, type) in themeItems)
                {
                    item.IsChecked = current == type;
                }
            };
            background.SubmenuOpened += (s, e) =>
            {
                var isCustom = Config.Current.Background.BackgroundType == BackgroundType.Custom;
                var currentColor = Config.Current.Background.CustomBackground.Color;
                foreach (var (item, color) in colorItems)
                {
                    item.IsChecked = isCustom && color == currentColor;
                }
                checker.IsChecked = Config.Current.Background.BackgroundType == BackgroundType.Check;
                auto.IsChecked = Config.Current.Background.BackgroundType == BackgroundType.Auto;
            };

            // keep the "메뉴" group last
            items.Insert(Math.Max(0, items.Count - 1), group);
        }

        private static void AddToolbarIcon(MenuItem item)
        {
            if (item.Header is not string text) return;

            var key = text switch
            {
                _ when text.Contains("정보") => "g_info_24px",
                _ when text.Contains("보기 모드") => "g_crop_original_24px",
                _ when text.Contains("슬라이드") => "g_slideshow_24px",
                _ when text.Contains("책갈피") => "g_bookmark_24px",
                _ when text.Contains("편집") => "g_content_copy_24px",
                _ when text.Contains("위에") => "g_push_pin_24px",
                _ when text == "테마" => "g_colorize_24px",
                _ when text.Contains("전체 화면") || text.Contains("전체화면") => "g_desktop_windows_24px",
                _ when text == "메뉴" => "g_menu_24px",
                _ => null,
            };
            if (key is null || App.Current.Resources[key] is not Geometry geometry) return;

            var icon = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Width = 15,
                Height = 15,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            BindingOperations.SetBinding(icon, System.Windows.Shapes.Shape.FillProperty,
                new Binding(nameof(Control.Foreground)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(MenuItem), 1) });

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(icon);
            var label = text.Contains("정보") ? "EXIF" : text;
            panel.Children.Add(new AccessText { Text = label, VerticalAlignment = VerticalAlignment.Center });
            if (item.Items.Count > 0)
            {
                panel.Children.Add(new TextBlock { Text = "▾", Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            item.Header = panel;
        }

        /// <summary>
        /// メニューアイコン作成
        /// </summary>
        /// <remarks>
        /// アイコンの色は親のForegroundを参照する
        /// </remarks>
        private static DrawingImage CreateMenuIcon()
        {
            var drawing = new GeometryDrawing();
            drawing.Geometry = App.Current.Resources["g_menu_24px"] as Geometry;
            BindingOperations.SetBinding(drawing, GeometryDrawing.BrushProperty, new Binding(nameof(Control.Foreground)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) });
            var drawingImage = new DrawingImage(drawing);
            return drawingImage;
        }

    }
}
