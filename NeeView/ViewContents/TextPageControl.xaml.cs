using System.Windows.Controls;

namespace NeeView
{
    public partial class TextPageControl : UserControl
    {
        public TextPageControl(TextViewData source)
        {
            InitializeComponent();
            TextBox.Text = source.Text;
        }
    }
}
