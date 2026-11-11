using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NeeView
{
    /// <summary>
    /// EPUB 아카이버: 본문 순서(spine)의 XHTML 문서 하나를 한 쪽으로 본다.
    /// 책 내용은 임시 폴더에 풀어 두고, 뷰어(WebView2)가 이미지와 CSS 를 상대 경로로 읽을 수 있게 한다.
    /// </summary>
    public class EpubArchive : Archive
    {
        public EpubArchive(string path, ArchiveEntry? source, ArchiveHint archiveHint) : base(path, source, archiveHint) { }

        public override string ToString() => "EPUB";

        public override bool IsSupported() => true;

        protected override Task<List<ArchiveEntry>> GetEntriesInnerAsync(bool decrypt, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var rootDirectory = ExtractToWorkDirectory(token);
            var documents = ReadSpineDocuments(rootDirectory);

            var entries = new List<ArchiveEntry>();
            int id = 0;
            foreach (var document in documents)
            {
                token.ThrowIfCancellationRequested();
                var filePath = System.IO.Path.Combine(rootDirectory, document.Replace('/', System.IO.Path.DirectorySeparatorChar));
                var info = new FileInfo(filePath);
                entries.Add(new EpubArchiveEntry(this, filePath, rootDirectory)
                {
                    IsValid = true,
                    Id = id++,
                    RawEntryName = document,
                    Length = info.Length,
                    CreationTime = info.GetSafeCreationTime(),
                    LastWriteTime = info.GetSafeLastWriteTime(),
                });
            }

            return Task.FromResult(entries);
        }

        protected override Task<Stream> OpenStreamInnerAsync(ArchiveEntry entry, bool decrypt, CancellationToken token)
        {
            Debug.Assert(entry.Archive == this);
            return Task.FromResult<Stream>(new FileStream(entry.EntityPath ?? Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        }

        protected override Task ExtractToFileInnerAsync(ArchiveEntry entry, string exportFileName, bool isOverwrite, CancellationToken token)
            => FileIO.CopyFileAsync(entry.EntityPath ?? Path, exportFileName, isOverwrite, true, token);

        /// <summary>
        /// EPUB 을 임시 폴더에 푼다. 같은 파일이 이미 풀려 있으면 그대로 쓴다.
        /// </summary>
        private string ExtractToWorkDirectory(CancellationToken token)
        {
            var info = new FileInfo(Path);
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}")))[..24];
            var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WaterView", "epub", key));
            var marker = System.IO.Path.Combine(directory, ".complete");
            if (File.Exists(marker))
            {
                return directory;
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
            Directory.CreateDirectory(directory);

            using (var zip = System.IO.Compression.ZipFile.OpenRead(Path))
            {
                foreach (var zipEntry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(zipEntry.Name)) continue; // ディレクトリ

                    var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, zipEntry.FullName.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                    if (!target.StartsWith(directory + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue; // パス遡行を防ぐ

                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                    System.IO.Compression.ZipFileExtensions.ExtractToFile(zipEntry, target, true);
                }
            }

            File.WriteAllText(marker, "");
            return directory;
        }

        /// <summary>
        /// container.xml → OPF → spine の順に本文の位置を読む。戻り値は展開先からの相対パス(/区切り)。
        /// </summary>
        private static List<string> ReadSpineDocuments(string rootDirectory)
        {
            var containerPath = System.IO.Path.Combine(rootDirectory, "META-INF", "container.xml");
            var container = XDocument.Load(containerPath);
            XNamespace containerNs = "urn:oasis:names:tc:opendocument:xmlns:container";
            var opfRelative = container.Descendants(containerNs + "rootfile").Select(e => (string?)e.Attribute("full-path")).FirstOrDefault(p => !string.IsNullOrEmpty(p))
                ?? throw new InvalidDataException("EPUB の container.xml に rootfile がありません");

            var opf = XDocument.Load(System.IO.Path.Combine(rootDirectory, opfRelative.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            XNamespace opfNs = "http://www.idpf.org/2007/opf";

            var manifest = new Dictionary<string, (string Href, string MediaType)>();
            foreach (var item in opf.Descendants(opfNs + "item"))
            {
                var id = (string?)item.Attribute("id");
                var href = (string?)item.Attribute("href");
                if (id is null || href is null) continue;
                manifest.TryAdd(id, (href, (string?)item.Attribute("media-type") ?? ""));
            }

            // OPF 파일이 있는 폴더 기준으로 href 를 풀어 준다 (../ 와 %XX 처리)
            var opfFolder = System.IO.Path.GetDirectoryName(opfRelative.Replace('\\', '/')) ?? "";
            var baseUri = new Uri("http://epub.local/" + (opfFolder.Length > 0 ? opfFolder.Trim('/') + "/" : ""));
            string Resolve(string href)
            {
                var pathPart = href.Split('#')[0];
                var uri = new Uri(baseUri, pathPart);
                return Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            }

            var spine = opf.Descendants(opfNs + "itemref")
                .Select(e => (string?)e.Attribute("idref"))
                .Where(id => id is not null && manifest.ContainsKey(id))
                .Select(id => manifest[id!])
                .Select(item => Resolve(item.Href))
                .Where(path => File.Exists(System.IO.Path.Combine(rootDirectory, path.Replace('/', System.IO.Path.DirectorySeparatorChar))))
                .ToList();

            if (spine.Count > 0)
            {
                return spine;
            }

            // spine が空のときは、XHTML として登録された項目を並べる
            return manifest.Values
                .Where(item => item.MediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase) || item.MediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                .Select(item => Resolve(item.Href))
                .Where(path => File.Exists(System.IO.Path.Combine(rootDirectory, path.Replace('/', System.IO.Path.DirectorySeparatorChar))))
                .ToList();
        }
    }

    public class EpubArchiveEntry : ArchiveEntry
    {
        public EpubArchiveEntry(Archive archive, string filePath, string rootDirectory) : base(archive)
        {
            FilePath = filePath;
            RootDirectory = rootDirectory;
        }

        /// <summary>展開済みの本文ファイルの場所</summary>
        public string FilePath { get; }

        /// <summary>展開先のフォルダ(画像や CSS がこの下にある)</summary>
        public string RootDirectory { get; }

        public override string? EntityPath => FilePath;
    }
}
