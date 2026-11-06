using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NeeView
{
    public class TextArchive : Archive
    {
        public TextArchive(string path, ArchiveEntry? source, ArchiveHint archiveHint) : base(path, source, archiveHint) { }
        public override string ToString() => "TXT";
        protected override Task<List<ArchiveEntry>> GetEntriesInnerAsync(bool decrypt, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var info = new FileInfo(Path);
            var entry = new TextArchiveEntry(this)
            {
                IsValid = true, Id = 0, RawEntryName = LoosePath.GetFileName(EntryName),
                Length = info.Length, CreationTime = info.GetSafeCreationTime(), LastWriteTime = info.GetSafeLastWriteTime(),
            };
            return Task.FromResult(new List<ArchiveEntry> { entry });
        }
        public override bool IsSupported() => true;
        protected override Task<Stream> OpenStreamInnerAsync(ArchiveEntry entry, bool decrypt, CancellationToken token)
        {
            Debug.Assert(entry.Archive == this);
            return Task.FromResult<Stream>(new FileStream(entry.EntityPath ?? Path, FileMode.Open, FileAccess.Read));
        }
        protected override Task ExtractToFileInnerAsync(ArchiveEntry entry, string exportFileName, bool isOverwrite, CancellationToken token)
            => FileIO.CopyFileAsync(entry.EntityPath ?? Path, exportFileName, isOverwrite, true, token);
    }

    public class TextArchiveEntry : ArchiveEntry
    {
        public TextArchiveEntry(Archive archive) : base(archive) { }
        public override string EntryFullName => Archive.SystemPath;
        public override string SystemPath => Archive.SystemPath;
        public override string? EntityPath => Archive.Path;
        public override string Ident => Archive.Ident;
    }
}
