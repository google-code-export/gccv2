using System.Drawing;
using System.Windows.Forms;

namespace GpsCycleComputer
{
    public class PictureSelectorButton : Control
    {
        Bitmap backgroundImage, pressedImage;

        public bool pressed = false;

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

        // Override the OnPaint method to draw the background image and the text.
        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle src_rec = new Rectangle(0, 0, backgroundImage.Width, backgroundImage.Height);
            Rectangle dest_rec = new Rectangle(0, 0, this.Width, this.Height);

            if (this.pressed && this.pressedImage != null)
                e.Graphics.DrawImage(this.pressedImage, dest_rec, src_rec, GraphicsUnit.Pixel);
            else
                e.Graphics.DrawImage(this.backgroundImage, dest_rec, src_rec, GraphicsUnit.Pixel);

            base.OnPaint(e);
        }
    }
}