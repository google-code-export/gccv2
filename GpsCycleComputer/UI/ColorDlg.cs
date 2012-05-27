using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace GpsCycleComputer
{
    class ColorDlg : System.Windows.Forms.Form
    {
        Color CurrentCol;
        int r, g, b;
        int x, y;           //location of color stripe
        int sc_x, sc_y;     //scale (Layout made for 24 x 32)
        int inc;            //color increment
        Color[] col_ar;
        NumericUpDown n_r;
        NumericUpDown n_g;
        NumericUpDown n_b;
        Rectangle ColRect;

        public Color CurrentColor
        {
            get { return CurrentCol; }
            set
            {
                //CurrentCol = value;   is done with numeric_ValueChanged
                n_r.Value = value.R;
                n_g.Value = value.G;
                n_b.Value = value.B;
            }
        }

        public ColorDlg()
        {
            sc_x = this.Width / 24;
            sc_y = this.Height / 32;
            inc = 11 * 240 / this.Width;
            col_ar = new Color[this.Width];
            ColRect = new Rectangle(14 * sc_x, 7 * sc_y, 7 * sc_x, 10 * sc_y);

            Button b_ok = new Button();
            b_ok.Bounds = new Rectangle(2 * sc_x, 20 * sc_y, 8 * sc_x, 3 * sc_y);
            b_ok.Text = "OK";
            b_ok.DialogResult = DialogResult.OK;
            Button b_cancel = new Button();
            b_cancel.Bounds = new Rectangle(12 * sc_x, 20 * sc_y, 8 * sc_x, 3 * sc_y);
            b_cancel.Text = "Cancel";
            b_cancel.DialogResult = DialogResult.Cancel;
            n_r = new NumericUpDown();
            n_r.Bounds = new Rectangle(3 * sc_x, 7 * sc_y, 7 * sc_x, 3 * sc_y);
            //n_r.Width = 50 * sc_x;
            n_r.Maximum = 255;
            n_r.ValueChanged += new EventHandler(numeric_ValueChanged);
            n_g = new NumericUpDown();
            n_g.Bounds = new Rectangle(3 * sc_x, 11 * sc_y, 7 * sc_x, 3 * sc_y);
            //n_g.Width = 50;
            n_g.Maximum = 255;
            n_g.ValueChanged += new EventHandler(numeric_ValueChanged);
            n_b = new NumericUpDown();
            n_b.Bounds = new Rectangle(3 * sc_x, 15 * sc_y, 7 * sc_x, 3 * sc_y);
            //n_b.Width = 50;
            n_b.Maximum = 255;
            n_b.ValueChanged += new EventHandler(numeric_ValueChanged);

            this.Controls.Add(b_ok);
            this.Controls.Add(b_cancel);
            this.Controls.Add(n_r);
            this.Controls.Add(n_g);
            this.Controls.Add(n_b);

            this.Paint += new PaintEventHandler(f2_Paint);
            this.MouseDown += new MouseEventHandler(f2_MouseDown);
            Invalidate(ColRect);
        }

        void numeric_ValueChanged(object sender, EventArgs e)
        {
            CurrentCol = Color.FromArgb((int)n_r.Value, (int)n_g.Value, (int)n_b.Value);
            ((ColorDlg)((NumericUpDown)sender).Parent).Invalidate(ColRect);
        }

        void f2_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.X >= 1 * sc_x && e.X < x && e.Y >= 1 * sc_y && e.Y < 5 * sc_y)
            {
                n_r.Value = col_ar[e.X].R;
                n_g.Value = col_ar[e.X].G;
                n_b.Value = col_ar[e.X].B;
                //((ColorDlg)sender).Invalidate(ColRect);   done with ValueChanged
            }
        }
        
        void f2_Paint(object sender, PaintEventArgs e)
        {
            x = 1 * sc_x;
            y = 1 * sc_y;
            
            SolidBrush brush1 = new SolidBrush(CurrentCol);
            e.Graphics.FillRectangle(brush1, ColRect);
            brush1.Color = Color.Black;
            Font font1 = new Font("", 12, FontStyle.Regular);
            e.Graphics.DrawString("R:", font1, brush1, 1 * sc_x, 7 * sc_y);
            e.Graphics.DrawString("G:", font1, brush1, 1 * sc_x, 11 * sc_y);
            e.Graphics.DrawString("B:", font1, brush1, 1 * sc_x, 15 * sc_y);

            for (r = g = b = 255; r >= 0; r = g = b -= inc)
                drawCol(e);
            r = g = b = 0; drawCol(e);

            for (r = 0; r < 256; r += inc)
                drawCol(e);
            r = 255; drawCol(e);
            for (g = 0; g < 256; g += inc)
                drawCol(e);
            g = 255; drawCol(e);
            for (r = 255; r >= 0; r -= inc)
                drawCol(e);
            r = 0; drawCol(e);
            for (b = 0; b < 256; b += inc)
                drawCol(e);
            b = 255; drawCol(e);
            for (g = 255; g >= 0; g -= inc)
                drawCol(e);
            g = 0; drawCol(e);
            for (r = 0; r < 256; r += inc)
                drawCol(e);
            r = 255; drawCol(e);
            for (b = 255; b >= 0; b -= inc)
                drawCol(e);
            b = 0; drawCol(e);
        }
        void drawCol(PaintEventArgs e)
        {
            Color col = Color.FromArgb(r, g, b);
            Pen p = new Pen(col);
            e.Graphics.DrawLine(p, x, y, x, y + 4*sc_y);
            col_ar[x] = col;
            x++;
        }

    }
}
