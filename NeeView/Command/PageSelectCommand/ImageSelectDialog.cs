using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NeeView
{
    /// <summary>
    /// Honeyview-like "image select" dialog: preview, file list (name / date / size), sort button.
    /// </summary>
    public class ImageSelectDialog : Window
    {
        private enum SortKey { Name, Date, Size }

        private sealed record Row(Page Page, string Name, DateTime Date, long Size);

        private readonly List<Row> _rows;
        private readonly ListView _list = new();
        private readonly Image _preview = new();
        private readonly Button _sortButton = new();
        private readonly CheckBox _noThumbnail = new() { Content = new TextBlock { Text = "썸네일 이미지 보지 않기", Foreground = Brushes.Black }, VerticalAlignment = VerticalAlignment.Center };
        private SortKey _sortKey = SortKey.Name;
        private CancellationTokenSource? _previewCancellation;

        public ImageSelectDialog(IReadOnlyList<Page> pages, int currentIndex)
        {
            _rows = pages
                .Where(e => e.PageType != PageType.Empty)
                .Select(e => new Row(e, e.EntryLastName, e.LastWriteTime, e.Length))
                .ToList();

            Title = "이미지 선택";
            Width = 490;
            Height = 430;
            MinWidth = 380;
            MinHeight = 300;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var previewBorder = new Border { Background = Brushes.Black, Height = 68, Margin = new Thickness(8, 8, 8, 6), Child = _preview };
            _preview.Stretch = Stretch.Uniform;
            RenderOptions.SetBitmapScalingMode(_preview, BitmapScalingMode.HighQuality);

            // light colors like Honeyview, independent of the app theme
            var light = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
            Background = light;
            Foreground = Brushes.Black;

            var headerStyle = new Style(typeof(GridViewColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));

            var grid = new GridView { ColumnHeaderContainerStyle = headerStyle };
            grid.Columns.Add(new GridViewColumn { Header = "사진 목록", Width = 270, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(Row.Name)) });
            grid.Columns.Add(new GridViewColumn { Header = "수정 날짜", Width = 120, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(Row.Date)) { StringFormat = "yyyy-MM-dd HH:mm" } });
            grid.Columns.Add(new GridViewColumn { Header = "크기", Width = 70, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(Row.Size)) { StringFormat = "N0" } });
            _list.View = grid;
            _list.Background = Brushes.White;
            _list.Foreground = Brushes.Black;
            _list.BorderBrush = Brushes.Gray;
            _list.Margin = new Thickness(8, 0, 8, 6);
            _list.SelectionMode = SelectionMode.Single;
            _list.SelectionChanged += (s, e) => UpdatePreview();
            _list.MouseDoubleClick += (s, e) => { if (_list.SelectedItem != null) Accept(); };

            _sortButton.Content = new TextBlock { Text = SortLabel(), Foreground = Brushes.Black };
            _sortButton.Width = 150;
            _sortButton.Height = 26;
            _sortButton.Style = null;
            _sortButton.Background = Brushes.White;
            _sortButton.Foreground = Brushes.Black;
            _noThumbnail.Foreground = Brushes.Black;
            _sortButton.Click += (s, e) => { _sortKey = (SortKey)(((int)_sortKey + 1) % 3); Resort(); };

            var okButton = new Button { Content = new TextBlock { Text = "확인", Foreground = Brushes.Black }, Width = 74, Height = 26, IsDefault = true, Background = Brushes.White, Foreground = Brushes.Black };
            okButton.Style = null;
            okButton.Click += (s, e) => Accept();

            _noThumbnail.Style = null;
            _noThumbnail.Margin = new Thickness(12, 0, 0, 0);
            _noThumbnail.Checked += (s, e) => UpdatePreview();
            _noThumbnail.Unchecked += (s, e) => UpdatePreview();

            var bottom = new DockPanel { Margin = new Thickness(8, 0, 8, 8), LastChildFill = false };
            DockPanel.SetDock(okButton, Dock.Right);
            bottom.Children.Add(okButton);
            DockPanel.SetDock(_sortButton, Dock.Left);
            bottom.Children.Add(_sortButton);
            DockPanel.SetDock(_noThumbnail, Dock.Left);
            bottom.Children.Add(_noThumbnail);

            var root = new DockPanel();
            DockPanel.SetDock(previewBorder, Dock.Top);
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(previewBorder);
            root.Children.Add(bottom);
            root.Children.Add(_list);
            Content = root;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && !e.IsRepeat)
                {
                    Accept();
                    e.Handled = true;
                }
                else if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
                {
                    MoveSelection(e.Key);
                    e.Handled = true;
                }
            };
            Loaded += (s, e) => FocusSelected();
            Closed += (s, e) => _previewCancellation?.Cancel();

            var current = pages.Count > 0 && currentIndex >= 0 && currentIndex < pages.Count ? pages[currentIndex] : null;
            Resort(current);
        }

        /// <summary>
        /// Selected page (valid when DialogResult is true)
        /// </summary>
        public Page? SelectedPage => (_list.SelectedItem as Row)?.Page;

        private string SortLabel() => _sortKey switch
        {
            SortKey.Date => "정렬방식: 날짜",
            SortKey.Size => "정렬방식: 크기",
            _ => "정렬방식: 파일명",
        };

        private void Resort(Page? keep = null)
        {
            keep ??= SelectedPage;

            IEnumerable<Row> sorted = _sortKey switch
            {
                SortKey.Date => _rows.OrderBy(e => e.Date),
                SortKey.Size => _rows.OrderBy(e => e.Size),
                _ => _rows.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase),
            };

            _sortButton.Content = new TextBlock { Text = SortLabel(), Foreground = Brushes.Black };
            _list.ItemsSource = sorted.ToList();

            var target = _list.Items.OfType<Row>().FirstOrDefault(e => e.Page == keep) ?? _list.Items.OfType<Row>().FirstOrDefault();
            if (target != null)
            {
                _list.SelectedItem = target;
                _list.ScrollIntoView(target);
            }
        }

        private void MoveSelection(Key key)
        {
            var count = _list.Items.Count;
            if (count == 0) return;

            var index = _list.SelectedIndex < 0 ? 0 : _list.SelectedIndex;
            var pageSize = 10;
            index = key switch
            {
                Key.Up => index - 1,
                Key.Down => index + 1,
                Key.PageUp => index - pageSize,
                Key.PageDown => index + pageSize,
                Key.Home => 0,
                _ => count - 1,
            };
            index = Math.Clamp(index, 0, count - 1);

            _list.SelectedIndex = index;
            _list.ScrollIntoView(_list.SelectedItem);
            FocusSelected();
        }

        private void FocusSelected()
        {
            if (_list.SelectedItem is null) return;
            _list.ScrollIntoView(_list.SelectedItem);
            (_list.ItemContainerGenerator.ContainerFromItem(_list.SelectedItem) as ListViewItem)?.Focus();
            if (!_list.IsKeyboardFocusWithin) _list.Focus();
        }

        private void Accept()
        {
            if (SelectedPage is null) return;
            DialogResult = true;
        }

        private async void UpdatePreview()
        {
            _previewCancellation?.Cancel();
            _preview.Source = null;

            if (_noThumbnail.IsChecked == true || SelectedPage is not { } page) return;

            var cancellation = new CancellationTokenSource();
            _previewCancellation = cancellation;
            try
            {
                using var stream = await page.ArchiveEntry.OpenEntryAsync(false, cancellation.Token);
                var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellation.Token);
                memory.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelHeight = 136;
                bitmap.StreamSource = memory;
                bitmap.EndInit();
                bitmap.Freeze();

                if (!cancellation.IsCancellationRequested)
                {
                    _preview.Source = bitmap;
                }
            }
            catch
            {
                // preview is optional
            }
        }
    }
}
