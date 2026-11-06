using System.Threading;
using System.Threading.Tasks;

namespace NeeView
{
    public class TextPageContent : PageContent
    {
        public TextPageContent(ArchiveEntry archiveEntry, BookMemoryService? bookMemoryService) : base(archiveEntry, bookMemoryService) { }
        protected override Task<PictureInfo?> LoadPictureInfoCoreAsync(CancellationToken token)
            => Task.FromResult<PictureInfo?>(new PictureInfo(new System.Windows.Size(1000, 700)));
        protected override async Task<PageSource> LoadSourceAsync(CancellationToken token)
        {
            var proxy = await ArchiveEntry.GetFileProxyAsync(false, token);
            var text = await TextDocumentReader.ReadFileAsync(proxy.Path, token);
            return new PageSource(new TextPageData(text), null, new PictureInfo(new System.Windows.Size(1000, 700)));
        }
    }

    public sealed class TextPageData
    {
        public TextPageData(string text) => Text = text;
        public string Text { get; }
    }
}
