using NeeView.Properties;

namespace NeeView
{
    /// <summary>
    /// 물뷰 소개: GitHub 의 소개글을 브라우저로 연다.
    /// </summary>
    public class HelpIntroCommand : CommandElement
    {
        private const string IntroUrl = "https://github.com/iamtalker/WaterView/blob/master/docs/waterview/intro.ko.md";

        public HelpIntroCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Other");
            this.IsShowMessage = false;
        }

        public override void Execute(object? sender, CommandContext e)
        {
            ExternalProcess.Start(IntroUrl);
        }
    }
}
