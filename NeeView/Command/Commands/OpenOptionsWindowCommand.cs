using NeeView.Properties;

namespace NeeView
{
    public class OpenOptionsWindowCommand : CommandElement
    {
        public OpenOptionsWindowCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Other");
            this.IsShowMessage = false;
        }

        public override void Execute(object? sender, CommandContext e)
        {
            // The simple settings window; the full NeeView settings are opened from its "상세 설정..." button.
            QuickSettingsDialog.ShowOrActivate();
        }
    }
}
