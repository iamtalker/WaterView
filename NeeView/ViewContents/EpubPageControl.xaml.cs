using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NeeView
{
    /// <summary>
    /// EPUB 의 XHTML 문서 한 쪽을 WebView2 로 보여준다.
    /// 책 폴더를 가상 호스트에 묶어 두어 이미지와 CSS 가 상대 경로로 로드되게 한다.
    /// </summary>
    public partial class EpubPageControl : UserControl, IDisposable
    {
        private const string HostName = "epub.waterview.local";
        private static Task<CoreWebView2Environment>? _environmentTask;

        private Task? _initializeTask;
        private string? _pendingFilePath;
        private string? _pendingRoot;
        private string? _mappedRoot;
        private bool _isDisposed;

        public EpubPageControl()
        {
            InitializeComponent();
        }

        public void Show(string filePath, string rootDirectory)
        {
            _pendingFilePath = filePath;
            _pendingRoot = rootDirectory;
            _ = ShowPendingAsync();
        }

        private async Task ShowPendingAsync()
        {
            if (_isDisposed) return;

            try
            {
                await EnsureInitializedAsync();
                if (_isDisposed || _pendingFilePath is null || _pendingRoot is null) return;

                var core = WebView.CoreWebView2;
                if (_mappedRoot != _pendingRoot)
                {
                    core.SetVirtualHostNameToFolderMapping(HostName, _pendingRoot, CoreWebView2HostResourceAccessKind.Allow);
                    _mappedRoot = _pendingRoot;
                }

                core.Navigate(BuildUrl(_pendingFilePath, _pendingRoot));
            }
            catch (Exception ex)
            {
                ShowMessage($"EPUB 을 표시하지 못했습니다. WebView2 런타임이 설치되어 있는지 확인해 주세요.\n{ex.Message}");
            }
        }

        private Task EnsureInitializedAsync()
        {
            _initializeTask ??= InitializeCoreAsync();
            return _initializeTask;
        }

        private async Task InitializeCoreAsync()
        {
            var userDataFolder = Path.Combine(Path.GetTempPath(), "WaterView", "WebView2");
            _environmentTask ??= CoreWebView2Environment.CreateAsync(null, userDataFolder);
            var environment = await _environmentTask;

            await WebView.EnsureCoreWebView2Async(environment);
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // 모든 문서가 열릴 때 읽기용 스타일을 덧씌운다 (책 CSS 보다 우선)
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(ReadingStyleScript);
        }

        /// <summary>
        /// 읽기 기본값: 본문 폭을 제한하고, 책의 고정 픽셀 크기와 양끝맞춤을 풀고, 종이색 배경을 쓴다.
        /// 세로쓰기 책을 해치지 않도록 writing-mode 가 세로인 경우는 건드리지 않는다.
        /// </summary>
        private const string ReadingStyleCss = @"
html, body { background: #fbf8f1 !important; color: #222 !important; }
body {
  font-size: 18px !important; line-height: 1.85 !important;
  max-width: 760px !important; width: auto !important; height: auto !important;
  margin: 0 auto !important; padding: 2.5em 2.5em 4em !important;
  text-align: left !important; overflow-wrap: break-word;
}
p { text-indent: 1em !important; margin: 0 0 0.5em !important; }
img, svg, video { max-width: 100% !important; height: auto !important; width: auto !important; }
h1, h2, h3 { line-height: 1.4 !important; }
";

        private static string ReadingStyleScript => $@"
(function () {{
  var css = {System.Text.Json.JsonSerializer.Serialize(ReadingStyleCss)};
  document.addEventListener('DOMContentLoaded', function () {{
    if (document.documentElement.style.writingMode || getComputedStyle(document.body).writingMode.indexOf('vertical') === 0) return;
    var style = document.createElement('style');
    style.textContent = css;
    document.head.appendChild(style);
  }});
}})();";

        private static string BuildUrl(string filePath, string rootDirectory)
        {
            var relative = Path.GetRelativePath(rootDirectory, filePath).Replace('\\', '/');
            var escaped = string.Join("/", relative.Split('/').Select(Uri.EscapeDataString));
            return $"https://{HostName}/{escaped}";
        }

        private void ShowMessage(string message)
        {
            MessageText.Text = message;
            MessageText.Visibility = Visibility.Visible;
            WebView.Visibility = Visibility.Collapsed;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            WebView.Dispose();
        }
    }
}
