using NeeView.Properties;
using System.IO;
using System.Text;

namespace NeeView
{
    /// <summary>
    /// 지원 형식 도움말 (브라우저로 표시)
    /// </summary>
    public static class SupportedFormatsManual
    {
        private static readonly string[] _sectionKeys = { "Image", "Archive", "Document", "Video", "Music" };

        public static void OpenSupportedFormatsManual()
        {
            Directory.CreateDirectory(Temporary.Current.TempSystemDirectory);
            string fileName = Path.Combine(Temporary.Current.TempSystemDirectory, "SupportedFormats.html");

            using (var writer = new StreamWriter(fileName, false))
            {
                writer.WriteLine(CreateSupportedFormatsHelp());
            }

            ExternalProcess.Start(fileName);
        }

        public static string CreateSupportedFormatsHelp()
        {
            var title = TextResources.GetString("HelpSupportedFormats.Title");

            var sb = new StringBuilder();
            sb.AppendLine(HtmlHelpUtility.CreateHeader(title));
            sb.AppendLine("<body>");
            sb.AppendLine($"<h1>{title}</h1>");
            sb.AppendLine($"<p>{TextResources.GetString("HelpSupportedFormats.Message")}</p>");
            sb.AppendLine("<table>");
            foreach (var key in _sectionKeys)
            {
                sb.AppendLine($"<tr><th class=\"nowrap\">{TextResources.GetString("HelpSupportedFormats." + key)}</th><td>{TextResources.GetString("HelpSupportedFormats." + key + ".Value")}</td></tr>");
            }
            sb.AppendLine("</table>");
            sb.AppendLine("</body>");
            sb.AppendLine(HtmlHelpUtility.CreateFooter());

            return sb.ToString();
        }
    }
}
