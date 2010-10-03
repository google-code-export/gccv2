using System;
using System.Drawing;
using System.Windows.Forms;

namespace GpsCycleComputer
{
    public class MenuPage : Control
    {
        public Form1 form1ref;
        public struct MButton
        {
            public Bitmap icon;
            public Bitmap icon_p;
            public String text;
            public bool enabled;
            //constructor
            public MButton(String bm, String txt, bool en)
            {
                icon = Form1.LoadBitmap(bm);
                icon_p = Form1.LoadBitmap(bm.Replace(".", "_p."));
                text = txt;
                enabled = en;
            }
            public MButton(String bm, String txt)
            {
                icon = Form1.LoadBitmap(bm);
                icon_p = Form1.LoadBitmap(bm.Replace(".", "_p."));
                text = txt;
                enabled = true;
            }
        }

        public MButton mBPause = new MButton("pause.jpg", "Pause", false);
        public MButton mBPauseMode = new MButton("pause_mode.jpg", "Paused", false);
        public MButton mBGpsOn = new MButton("gps_on.jpg", "GPS is on", true);
        public MButton mBGpsOff = new MButton("gps_off.jpg", "GPS is off", true);
        public MButton mBmPageUp = new MButton("up.jpg", "", true);
        public MButton mBmPageDown = new MButton("down.jpg", "", true);

        public MButton[] mBAr = new MButton[] {                 //menu buttons; keep in sync with BFkt!
                                                  new MButton ("main.jpg", "Main"),
                                                  new MButton ("map.jpg", "Map"),
                                                  new MButton ("options.jpg", "Options"),

                                                  new MButton ("graph.jpg", "Altitude"),
                                                  new MButton ("graph.jpg", "Speed"),
                                                  new MButton ("checkpoint.jpg", "CheckPoint", false),

                                                  new MButton ("edit.jpg", "File"),
                                                  new MButton ("edit.jpg", "Track2Follow"),
                                                  new MButton ("cancel.jpg", "ClearTrack"),

                                                  new MButton ("exit.jpg", "Exit"),
                                                  new MButton ("continue.jpg", "Continue", false),
                                                  new MButton ("lap.jpg", "Lap"),

                                                  new MButton ("recall.jpg", "Recall 1"),
                                                  new MButton ("recall.jpg", "Recall 2"),
                                                  new MButton ("recall.jpg", "Recall 3"),

                                                  new MButton ("bklight.jpg", "BKLight"),
                                                  new MButton ("checkpoint.jpg", "Input LatLon"),
                                                  new MButton ("help.jpg", "Help"),
                                                  //end of menu page

                                                  new MButton ("start.jpg", "Start"),
                                                  new MButton ("stop.jpg", "Stop", false),
                                                  new MButton ("pause.jpg", "Pause", false),
                                                  new MButton ("gps_off.jpg", "GPS is off"),

                                                  new MButton ("left.jpg", ""),
                                                  new MButton ("right.jpg", ""),
                                                  new MButton ("zoom_in.jpg", ""),
                                                  new MButton ("zoom_out.jpg", ""),

                                                  //dialog buttons
                                                  new MButton ("open.jpg", ""),
                                                  new MButton ("cancel.jpg", ""),
                                                  new MButton ("up.jpg", ""),
                                                  new MButton ("down.jpg", ""),
                                                  new MButton ("left.jpg", ""),
                                                  new MButton ("right.jpg", ""),
                                                  new MButton ("kml.jpg", ""),
                                                  new MButton ("gpx.jpg", ""),

                                                  new MButton ("menu.jpg", "Menu"),
                                                  new MButton ("up.jpg", ""),
                                                  new MButton ("down.jpg", ""),
                                                  new MButton ("blank.jpg", "")
                                                                       
                                              };
        public enum BFkt        //menu button functions (mnemonic index for mBAr; keep in sync)
        {
            main,
            map,
            options,

            graph_alt,
            graph_speed,
            checkpoint,


            load_gcc,
            load_2follow,
            load_2clear,

            exit,
            continu,            //'continue' is reserved for c instruction 
            lap,

            recall1,
            recall2,
            recall3,

            backlight_off,
            inputLatLon,
            help,
            endOfMenuPage = help,

            //separate buttons on bottom
            start,
            stop,
            pause,
            gps_toggle,

            options_prev,
            options_next,
            map_zoomIn,
            map_zoomOut,

            dialog_open,
            dialog_cancel,
            dialog_up,
            dialog_down,
            dialog_prevFileType,
            dialog_nextFileType,
            dialog_saveKml,
            dialog_saveGpx,

            mPage,
            mPageUp,
            mPageDown,

            nothing
        }


        int bWidth =160, bHeigh = 80, GridHeigh = 120;
        int so = 0;     //scroll offset
        int lastDisplayed = 0;
        public int i_p = -1;   //index of pressed icon
        Rectangle iv_rect;
        int mouseDownY = -1;      //mouse down y position in pixels
        int mouseDrag = 0;      //dragging in pixels

        public MenuPage()
        {
            //icon = LoadBitmap("graph.jpg");
            //icon = new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("GpsSample.Graphics.graph.jpg"));
        }

        public bool DownPossible
        {
            get { return so <= (int)BFkt.endOfMenuPage - 3; }
        }
        public bool UpPossible
        {
            get { return so > 0; }
        }

        public void MenuDown()
        {
            if (DownPossible)
                so += 3;
            //Invalidate();
        }
        public void MenuUp()
        {
            if (so >= 3)
                so -= 3;
            else
                so = 0;     //for safety
            //Invalidate();
        }
        public int getButtonIndex(int X, int Y)
        {
            i_p = Y / GridHeigh * 3 + X / bWidth + so;
            iv_rect = new Rectangle(bWidth * ((i_p - so) % 3), GridHeigh * ((i_p - so) / 3), bWidth, GridHeigh);
            Invalidate(iv_rect);
            return i_p;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            mouseDownY = e.Y;
            int index = getButtonIndex(e.X, e.Y);
            if (index <= lastDisplayed)
                this.Tag = index;
            else
                this.Tag = null;

            base.OnMouseDown(e);
        }
        
        protected override void OnMouseUp(MouseEventArgs e)
        {
            mouseDrag = 0;
            mouseDownY = -1;    //disable move
            i_p = -1;
            Invalidate(iv_rect);
            if (form1ref.BufferDrawMode == Form1.BufferDrawModeMenu)
            {
                form1ref.showButton(form1ref.button2, UpPossible ? BFkt.mPageUp : BFkt.nothing);
                form1ref.showButton(form1ref.button3, DownPossible ? BFkt.mPageDown : BFkt.nothing);
            }
            base.OnMouseUp(e);
        }

        protected override void OnClick(EventArgs e)
        {
            mouseDrag = 0;
            base.OnClick(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (mouseDownY == -1) return;   //move without prior MouseDown (occures at longclick (context menu))
            if (e.Y > mouseDownY)
            {
                mouseDrag = e.Y - mouseDownY;
                if (mouseDrag > GridHeigh / 2 && UpPossible)
                {
                    MenuUp();
                    mouseDownY += GridHeigh;
                    mouseDrag -= GridHeigh;
                }
                if (!UpPossible)
                    mouseDrag = 0;
            }
            if (e.Y < mouseDownY)
            {
                mouseDrag = e.Y - mouseDownY;
                if (mouseDrag < -GridHeigh / 2 && DownPossible)
                {
                    MenuDown();
                    mouseDownY -= GridHeigh;
                    mouseDrag += GridHeigh;
                }
                if (!DownPossible)
                    mouseDrag = 0;
            }
            if (Math.Abs(e.Y - mouseDownY) > GridHeigh/8)
                this.Tag = null;
            Invalidate();
            base.OnMouseMove(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            bWidth = this.Width / 3;
            bHeigh = bWidth / 2;
            GridHeigh = bHeigh * 3 / 2;

            e.Graphics.Clear(this.BackColor);
            if (!Form1.isLandscape)
            {
                Rectangle cliprec = e.ClipRectangle;
                cliprec.Height = cliprec.Height * 31 / 32;      //create a black separator between mPage buttons and standard buttons on bottom
                e.Graphics.Clip = new System.Drawing.Region(cliprec);
            }

            if (Form1.gps.OpenedOrSuspended)
                mBAr[(int)BFkt.gps_toggle] = mBGpsOn;
            else
                mBAr[(int)BFkt.gps_toggle] = mBGpsOff;
            SolidBrush br = new SolidBrush(this.ForeColor);
            for (int i = so; i <= (int)BFkt.endOfMenuPage; i++)
            {
                int ix = (i - so) % 3; int iy = (i - so) / 3;
                if ((2 * iy + 1) * GridHeigh > 2 * Size.Height)            //show minimum half
                    break;                              // not enough room for a complete button
                Rectangle dest_rect = new Rectangle(bWidth * ix, GridHeigh * iy + mouseDrag, bWidth, bHeigh);
                Bitmap bm = (i == i_p) ? mBAr[i].icon_p : mBAr[i].icon;
                e.Graphics.DrawImage(bm, dest_rect, new Rectangle(0, 0, mBAr[i].icon.Width, mBAr[i].icon.Height), GraphicsUnit.Pixel);
                SizeF Tsize = e.Graphics.MeasureString(mBAr[i].text, this.Font);
                e.Graphics.DrawString(mBAr[i].text, this.Font, br, bWidth * ix + (bWidth - Tsize.Width) / 2, GridHeigh * iy + bHeigh + mouseDrag);
                lastDisplayed = i;
            }
            base.OnPaint(e);
        }
    }
}