using System.Drawing;
using System.Windows.Forms;

namespace GpsCycleComputer
{
    public class AlwaysFitLabel : Control
    {
        public void SetText(string s)
        {
            this.Text = s; 
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(this.BackColor);
            
            // Draw the text if there is any.
            if (this.Text.Length > 0)
            {
                // trim text so it fits
                string s = this.Text;
                SizeF size = e.Graphics.MeasureString(s, this.Font);

                // check if we need to add "..." and delete some chars in the middle
                if ((size.Width > this.Width) && (s.Length > 10))
                {
                    s = s.Insert(9, "...");
                    while (size.Width >= this.Width)
                    {
                        s = s.Remove(12, 1);
                        size = e.Graphics.MeasureString(s, this.Font);
                    }
                    this.Text = s;
                }

                e.Graphics.DrawString(this.Text, this.Font, new SolidBrush(this.ForeColor), 0, 0);
            }

            base.OnPaint(e);
        }
    }
}