using NeeView.Properties;

namespace NeeView
{
    public class HelpSupportedFormatsCommand : CommandElement
    {
        public HelpSupportedFormatsCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Other");
            this.IsShowMessage = false;
        }

        public override void Execute(object? sender, CommandContext e)
        {
            SupportedFormatsManual.OpenSupportedFormatsManual();
        }
    }
}
