using System;
using System.Threading;
using System.Windows;

namespace NeeView
{
    public class TextViewContentStrategy : IDisposable, IViewContentStrategy
    {
        private readonly ViewContent _viewContent;
        private TextPageControl? _pageControl;
        public TextViewContentStrategy(ViewContent viewContent) => _viewContent = viewContent;
        public void OnSourceChanged() => _viewContent.RequestLoadViewSource(CancellationToken.None);
        public FrameworkElement CreateLoadedContent(object data) => _pageControl ??= new TextPageControl((TextViewData)data);
        public void Dispose() { }
    }
}
