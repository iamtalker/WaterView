using NeeView.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NeeView
{
    public class EpubViewSourceStrategy : IViewSourceStrategy
    {
        public Task<DataSource> LoadCoreAsync(PageDataSource data, Size size, CancellationToken token)
        {
            var pageData = (EpubPageData)data.Data!;
            return Task.FromResult(new DataSource(new EpubViewData(pageData.FilePath, pageData.RootDirectory), 0, null));
        }
    }

    public sealed class EpubViewData
    {
        public EpubViewData(string filePath, string rootDirectory)
        {
            FilePath = filePath;
            RootDirectory = rootDirectory;
        }

        public string FilePath { get; }
        public string RootDirectory { get; }
    }
}
