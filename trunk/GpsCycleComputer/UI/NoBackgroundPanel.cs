using System.Windows.Forms;

namespace GpsCycleComputer
{
    public class NoBackgroundPanel : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            //do not paint background
        }
    }
}