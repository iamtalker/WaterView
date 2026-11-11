using Microsoft.Web.WebView2.Core;
using NeeView.ComponentModel;
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
        private bool _isBusy = true; // 장을 여는 중(또는 아직 열지 않음)에는 쪽 넘김 키를 무시한다
        // 장이 넘어갈 때 화면 컨트롤이 새로 만들어질 수 있으므로, 이 표시는 인스턴스가 아니라 전체에서 하나만 쓴다
        private static bool _scrollToEndOnLoad;

        public EpubPageControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 지금 화면에 떠 있는 EPUB 컨트롤. 쪽 넘김 명령이 장 안 스크롤에 쓴다.
        /// </summary>
        public static EpubPageControl? Current { get; private set; }

        /// <summary>
        /// 쪽 넘김 명령을 EPUB 에 맞게 처리한다. 장 안에서 한 화면씩 스크롤하고,
        /// 장 끝(또는 시작)에 닿으면 onBoundary 로 다음(이전) 장으로 넘어간다.
        /// EPUB 이 화면에 없으면 false 를 돌려주고 아무것도 하지 않는다.
        /// </summary>
        public static bool TryStep(LinkedListDirection direction, Action onBoundary)
        {
            var epub = Current;
            if (epub is null || epub._isDisposed || !epub.IsVisible) return false;
            // 장을 여는 중에 키가 들어오면 장을 한 번 더 넘기지 않도록 무시한다 (빠른 연속 입력 시 장이 건너뛰던 문제)
            if (epub._isBusy) return true;
            _ = epub.StepAsync(direction, onBoundary);
            return true;
        }

        private async Task StepAsync(LinkedListDirection direction, Action onBoundary)
        {
            int sign = direction == LinkedListDirection.Next ? 1 : -1;
            try
            {
                var script = "(function(d){"
                    + "var se=document.scrollingElement||document.documentElement;"
                    + "var before=se.scrollTop; var max=se.scrollHeight-se.clientHeight;"
                    + "if (d>0 && before>=max-2) return false;"
                    + "if (d<0 && before<=2) return false;"
                    + "se.scrollTop = before + d*Math.max(window.innerHeight*0.9, 80);"
                    + "return true; })(" + sign + ");";
                var result = await WebView.ExecuteScriptAsync(script);
                if (result == "true")
                {
                    // 사용자가 직접 움직였으므로 "끝에서 시작" 요청은 끝났다
                    _scrollToEndOnLoad = false;
                    return;
                }
            }
            catch
            {
                // 스크립트 실행에 실패하면 장 단위 이동으로 대신한다
            }

            if (sign < 0) _scrollToEndOnLoad = true; // 앞 장으로 갈 때는 그 장의 끝에서 시작
            onBoundary();
        }

        public void Show(string filePath, string rootDirectory)
        {
            Current = this;
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

                // 앞 장으로 갈 때는 주소에 #end 를 붙여 문서가 맨 아래에서 시작하게 한다 (의도가 주소에 남아 두 번 열려도 유지됨)
                var url = BuildUrl(_pendingFilePath, _pendingRoot) + (_scrollToEndOnLoad ? "#end" : "");
                core.Navigate(url);
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

            WebView.CoreWebView2.NavigationStarting += (s, e) => _isBusy = true;
            WebView.CoreWebView2.NavigationCompleted += (s, e) => _isBusy = false;
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
p { margin: 0 0 0.5em !important; }
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
  // 앞 장으로 갈 때(#end): 이미지까지 모두 로드된 뒤 맨 아래로 내린다
  window.addEventListener('load', function () {{
    if (location.hash !== '#end') return;
    var se = document.scrollingElement || document.documentElement;
    se.scrollTop = se.scrollHeight;
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
            if (Current == this) Current = null;
            WebView.Dispose();
        }
    }
}
