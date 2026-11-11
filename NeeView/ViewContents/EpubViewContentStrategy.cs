using System;
using System.Threading;
using System.Windows;

namespace NeeView
{
    public class EpubViewContentStrategy : IDisposable, IViewContentStrategy
    {
        private readonly ViewContent _viewContent;
        private EpubPageControl? _pageControl;

        public EpubViewContentStrategy(ViewContent viewContent) => _viewContent = viewContent;

        public void OnSourceChanged() => _viewContent.RequestLoadViewSource(CancellationToken.None);

        public FrameworkElement CreateLoadedContent(object data)
        {
            var viewData = (EpubViewData)data;
            _pageControl ??= new EpubPageControl();
            _pageControl.Show(viewData.FilePath, viewData.RootDirectory);
            return _pageControl;
        }

        public void Dispose()
        {
            _pageControl?.Dispose();
            _pageControl = null;
        }
    }
}
