using NeeView.Properties;

namespace NeeView
{
    /// <summary>
    /// 책 소개: 만든이의 책 소개 페이지를 브라우저로 연다.
    /// </summary>
    public class HelpBookCommand : CommandElement
    {
        public const string BookUrl = "https://bookk.co.kr/bookStore/6af3769e04f230844ed9303c";

        public HelpBookCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Other");
            this.IsShowMessage = false;
        }

        public override void Execute(object? sender, CommandContext e)
        {
            ExternalProcess.Start(BookUrl);
        }
    }
}
