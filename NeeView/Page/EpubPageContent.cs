using System;
using System.Threading;
using System.Threading.Tasks;

namespace NeeView
{
    public class EpubPageContent : PageContent
    {
        public EpubPageContent(ArchiveEntry archiveEntry, BookMemoryService? bookMemoryService) : base(archiveEntry, bookMemoryService) { }

        protected override Task<PictureInfo?> LoadPictureInfoCoreAsync(CancellationToken token)
            => Task.FromResult<PictureInfo?>(new PictureInfo(new System.Windows.Size(1000, 700)));

        protected override Task<PageSource> LoadSourceAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (ArchiveEntry is not EpubArchiveEntry entry)
            {
                throw new InvalidOperationException("EPUB 문서가 아닙니다.");
            }

            var data = new EpubPageData(entry.FilePath, entry.RootDirectory);
            return Task.FromResult(new PageSource(data, null, new PictureInfo(new System.Windows.Size(1000, 700))));
        }
    }

    public sealed class EpubPageData
    {
        public EpubPageData(string filePath, string rootDirectory)
        {
            FilePath = filePath;
            RootDirectory = rootDirectory;
        }

        public string FilePath { get; }
        public string RootDirectory { get; }
    }
}
