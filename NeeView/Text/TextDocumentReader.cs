using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeeView
{
    /// <summary>
    /// Reads plain-text documents with BOM-based encoding detection.
    /// This class has no dependency on the WPF viewer and can be reused by other hosts.
    /// </summary>
    public static class TextDocumentReader
    {
        public static async Task<string> ReadFileAsync(string path, CancellationToken token = default)
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await ReadAsync(stream, token);
        }

        public static async Task<string> ReadAsync(Stream stream, CancellationToken token = default)
        {
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: true);

            return await reader.ReadToEndAsync(token);
        }
    }
}
