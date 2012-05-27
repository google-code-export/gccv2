using System.Drawing;
using System.Windows.Forms;

namespace GpsCycleComputer
{
    public class PictureButton : Control
    {
        Bitmap backgroundImage, pressedImage;
        bool pressed = false;
        public int align;

        // Property for the background image to be drawn behind the button text.
        public Bitmap BackgroundImage
        {
            get { return this.backgroundImage; }
            set { this.backgroundImage = value; }
        }

        // Property for the background image to be drawn behind the button text when
        // the button is pressed.
        public Bitmap PressedImage
        {
            get { return this.pressedImage; }
            set { this.pressedImage = value; }
        }

        // When the mouse button is pressed, set the "pressed" flag to true 
        // and invalidate the form to cause a repaint.  The .NET Compact Framework 
        // sets the mouse capture automatically.
        protected override void OnMouseDown(MouseEventArgs e)
        {
            this.pressed = true;
            this.Invalidate();
            base.OnMouseDown(e);
        }

        // When the mouse is released, reset the "pressed" flag 
        // and invalidate to redraw the button in the unpressed state.
        protected override void OnMouseUp(MouseEventArgs e)
        {
            this.pressed = false;
            this.Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {}      //to avoid flicker (textonly button is alway drawn over a clear background)

        // Override the OnPaint method to draw the background image and the text.
        protected override void OnPaint(PaintEventArgs e)
        {
            // check if an image is assigned. If not, just fill background
            if (this.backgroundImage != null)
            {
                Rectangle src_rec = new Rectangle(0, 0, backgroundImage.Width, backgroundImage.Height);
                Rectangle dest_rec = new Rectangle(0, 0, this.Width, this.Height);

                if (this.pressed && this.pressedImage != null)
                    e.Graphics.DrawImage(this.pressedImage, dest_rec, src_rec, GraphicsUnit.Pixel);
                else
                    e.Graphics.DrawImage(this.backgroundImage, dest_rec, src_rec, GraphicsUnit.Pixel);
            }
            else
            { 
                if (this.pressed) e.Graphics.Clear(this.ForeColor);
                else              e.Graphics.Clear(this.BackColor);     
            }

            // Draw the text if there is any.
            if (this.Text.Length > 0)
            {
                SizeF size = e.Graphics.MeasureString(this.Text, this.Font);

                int text_x = 1;
                if (align == 2) text_x = (int)((this.ClientSize.Width - size.Width) / 2);
                else if (align == 3) text_x = (int) (this.ClientSize.Width - size.Width - 1);

                // Center the text inside the client area of the PictureButton.
                if (this.pressed)
                {
                    e.Graphics.DrawString(this.Text,
                                          this.Font,
                                          new SolidBrush(this.BackColor),
                                          text_x, (this.ClientSize.Height - size.Height) / 2);
                }
                else
                {
                    e.Graphics.DrawString(this.Text,
                                          this.Font,
                                          new SolidBrush(this.ForeColor),
                                          text_x, (this.ClientSize.Height - size.Height) / 2);
                }
            }

            base.OnPaint(e);
        }
    }
}