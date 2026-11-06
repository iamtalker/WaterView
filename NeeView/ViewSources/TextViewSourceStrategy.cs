using NeeView.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NeeView
{
    public class TextViewSourceStrategy : IViewSourceStrategy
    {
        public Task<DataSource> LoadCoreAsync(PageDataSource data, Size size, CancellationToken token)
            => Task.FromResult(new DataSource(new TextViewData(((TextPageData)data.Data!).Text), 0, null));
    }

    public sealed class TextViewData
    {
        public TextViewData(string text) => Text = text;
        public string Text { get; }
    }
}
