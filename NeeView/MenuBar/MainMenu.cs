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

            var intervals = new[] { 1.0, 2.0, 3.0, 5.0, 7.0, 10.0, 15.0, 20.0, 30.0 };
            var intervalItems = new List<(MenuItem Item, double Value)>();
            foreach (var seconds in intervals)
            {
                var value = seconds;
                var interval = new MenuItem { Header = $"{value:0}초", IsCheckable = false };
                interval.Click += (s, e) =>
                {
                    Config.Current.SlideShow.SlideShowInterval = value;
                    SlideShow.Current.SetPlaying(true);
                };
                intervalItems.Add((interval, value));
                group.Items.Add(interval);
            }

            group.SubmenuOpened += (s, e) =>
            {
                foreach (var (item, value) in intervalItems)
                {
                    item.IsChecked = Math.Abs(Config.Current.SlideShow.SlideShowInterval - value) < 0.05;
                }
            };

            items[index] = group;
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
