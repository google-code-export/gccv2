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
            public BFkt func;
            public Bitmap icon;
            public Bitmap icon_p;
            public String text;
            public bool enabled;
            //constructor
            public MButton(BFkt f, String bm, String txt, bool en)
            {
                func = f;
                icon = Form1.LoadBitmap(bm);
                icon_p = Form1.LoadBitmap(bm.Replace(".", "_p."));
                text = txt;
                enabled = en;
            }
            public MButton(BFkt f, String bm, String txt)
            {
                func = f;
                icon = Form1.LoadBitmap(bm);                        //don't know how to call constructor explicitly and subroutine isn't possible
                icon_p = Form1.LoadBitmap(bm.Replace(".", "_p."));
                text = txt;
                enabled = true;
            }
        }

        public MButton mBPause = new MButton(BFkt.pause, "pause.jpg", "Pause", false);
        public MButton mBPauseMode = new MButton(BFkt.pause, "pause_mode.jpg", "Paused", false);
        public MButton mBGpsOn = new MButton(BFkt.gps_toggle, "gps_on.jpg", "GPS is on", true);
        public MButton mBGpsOff = new MButton(BFkt.gps_toggle, "gps_off.jpg", "GPS is off", true);
        public MButton mBmPageUp = new MButton(BFkt.mPageUp, "up.jpg", "", true);
        public MButton mBmPageDown = new MButton(BFkt.mPageDown, "down.jpg", "", true);
        public MButton mBmPageBKLight = new MButton(BFkt.backlight_off, "bklight.jpg", "BKLight");
        public MButton mBmPageOptions = new MButton(BFkt.options, "options.jpg", "Options");

        public MButton[] mBAr = new MButton[] {                 //menu buttons; keep in sync with BFkt!
                                                  new MButton (BFkt.map, "map.jpg", "Map"),
                                                  new MButton (BFkt.options, "options.jpg", "Options"),
                                                  new MButton (BFkt.exit, "exit.jpg", "Exit"),

                                                  new MButton (BFkt.navigate, "navigate.jpg", "Navigate"),
                                                  new MButton (BFkt.inputLatLon, "checkpoint.jpg", "Input LatLon"),
                                                  new MButton (BFkt.waypoint, "waypoint.jpg", "Add Waypoint"),

                                                  new MButton (BFkt.load_gcc, "edit.jpg", "File/Export"),
                                                  new MButton (BFkt.load_2follow, "edit.jpg", "Track2Follow"),
                                                  new MButton (BFkt.lap, "lap.jpg", "Lap"),

                                                  new MButton (BFkt.clearTrack, "cancel.jpg", "ClearTrack"),
                                                  new MButton (BFkt.restore_clear2f, "restore.jpg", "Restore"),
                                                  new MButton (BFkt.continu, "continue.jpg", "Continue"),

                                                  new MButton (BFkt.graph_alt, "graph.jpg", "Altitude"),
                                                  new MButton (BFkt.graph_speed, "graph.jpg", "Speed"),
                                                  new MButton (BFkt.graph_heartRate, "graph.jpg", "Heart Rate", false),

                                                  new MButton (BFkt.backlight_off, "bklight.jpg", "BKLight"),
                                                  new MButton (BFkt.help, "help.jpg", "Help"),
                                                  new MButton (BFkt.about, "help.jpg", "About"),

                                                  new MButton (BFkt.recall1, "recall.jpg", "Recall 1"),
                                                  new MButton (BFkt.recall2, "recall.jpg", "Recall 2"),
                                                  new MButton (BFkt.recall3, "recall.jpg", "Recall 3"),
                                                  //end of menu page

                                                  new MButton (BFkt.main, "main.jpg", "Main"),
                                                  new MButton (BFkt.start, "start.jpg", "Start"),
                                                  new MButton (BFkt.stop, "stop.jpg", "Stop"),
                                                  new MButton (BFkt.pause, "pause.jpg", "Pause"),
                                                  new MButton (BFkt.gps_toggle, "gps_off.jpg", "GPS is off"),

                                                  new MButton (BFkt.options_prev, "left.jpg", ""),
                                                  new MButton (BFkt.options_next, "right.jpg", ""),
                                                  new MButton (BFkt.map_zoomIn, "zoom_in.jpg", ""),
                                                  new MButton (BFkt.map_zoomOut, "zoom_out.jpg", ""),

                                                  //dialog buttons
                                                  new MButton (BFkt.dialog_open, "open.jpg", ""),
                                                  new MButton (BFkt.dialog_cancel, "cancel.jpg", ""),
                                                  new MButton (BFkt.dialog_up, "up.jpg", ""),
                                                  new MButton (BFkt.dialog_down, "down.jpg", ""),
                                                  new MButton (BFkt.dialog_prevFileType, "left.jpg", ""),
                                                  new MButton (BFkt.dialog_nextFileType, "right.jpg", ""),
                                                  new MButton (BFkt.dialog_saveKml, "kml.jpg", ""),
                                                  new MButton (BFkt.dialog_saveGpx, "gpx.jpg", ""),

                                                  new MButton (BFkt.mPage, "menu.jpg", "Menu"),
                                                  new MButton (BFkt.mPageUp, "up.jpg", ""),
                                                  new MButton (BFkt.mPageDown, "down.jpg", ""),

                                                  new MButton (BFkt.waypoint2, "waypoint.jpg", ""),

                                                  new MButton (BFkt.nothing, "blank.jpg", "")
                                                                       
                                              };
        public enum BFkt        //menu button functions
        {
            map,
            options,
            exit,

            navigate,
            inputLatLon,
            waypoint,

            load_gcc,
            load_2follow,
            lap,

            clearTrack,
            restore_clear2f,
            continu,            //'continue' is reserved for c instruction
            
            graph_alt,
            graph_speed,
            graph_heartRate,

            backlight_off,
            help,
            about,

            recall1,
            recall2,
            recall3,

            endOfMenuPage = recall3,

            //separate buttons on bottom
            main,
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

            waypoint2,

            nothing,
        }


        int bWidth =160, bHeigh = 80, GridHeigh = 120;
        int so = 0;     //scroll offset (3 = one line)
        int lastDisplayed = 0;
        public int i_p = -1;   //index of pressed icon
        Rectangle iv_rect;
        int mouseDownY = -1;      //mouse down y position in pixels
        int mouseDrag = 0;      //dragging in pixels
        public BFkt lastSelectedBFkt;  // Remember the last selected button, required for Recall buttons (renaming/save)
        //Color disableColor;

        public MenuPage()
        {
            //icon = LoadBitmap("graph.jpg");
            //icon = new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("GpsSample.Graphics.graph.jpg"));
            //Color color1, color2;
            //color1 = mBmPageUp.icon.GetPixel(0, mBmPageUp.icon.Height / 4);
            //color2 = mBmPageUp.icon.GetPixel(0, mBmPageUp.icon.Height * 3 / 4);
            //disableColor = Color.FromArgb(Math.Max(0, Math.Min(2 * color2.R - color1.R, 255)), Math.Max(0, Math.Min(2 * color2.G - color1.G, 255)), Math.Max(0, Math.Min(2 * color2.B - color1.B, 255)));
            //disableColor = mBmPageUp.icon.GetPixel(0, 0);
        }

        public bool buttonIsClear2F = false;
        public void ChangeRestore2Clear()
        {
            if (buttonIsClear2F) return;
            mBAr[(int)BFkt.restore_clear2f].icon = mBAr[(int)BFkt.clearTrack].icon;
            mBAr[(int)BFkt.restore_clear2f].icon_p = mBAr[(int)BFkt.clearTrack].icon_p;
            mBAr[(int)BFkt.restore_clear2f].text = "Clear2Follow";
            buttonIsClear2F = true;
        }

        public void ShowMenu()
        {
            i_p = -1;
            if (form1ref.checkChangeMenuOptBKL.Checked)
            {
                mBAr[(int)BFkt.options] = mBmPageBKLight;
                mBAr[(int)BFkt.backlight_off] = mBmPageOptions;
            }
            else
            {
                mBAr[(int)BFkt.options] = mBmPageOptions;
                mBAr[(int)BFkt.backlight_off] = mBmPageBKLight;
            }
            form1ref.BufferDrawMode = Form1.BufferDrawModeMenu;
            
            BringToFront();
            Invalidate();
            form1ref.showButton(form1ref.button1, MenuPage.BFkt.main);
            if (UpPossible)
                form1ref.showButton(form1ref.button2, MenuPage.BFkt.mPageUp);
            else
                form1ref.showButton(form1ref.button2, MenuPage.BFkt.nothing);
            if (DownPossible)
                form1ref.showButton(form1ref.button3, MenuPage.BFkt.mPageDown);
            else
                form1ref.showButton(form1ref.button3, MenuPage.BFkt.nothing);
        }

        public bool DownPossible
        {
            get { return so <= (int)BFkt.endOfMenuPage - (Size.Height / GridHeigh) * 3; }
        }
        public bool UpPossible
        {
            get { return so > 0; }
        }

        public void ResetScroll()
        {
            so = 0;
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

        public void deselectButton()
        {
            i_p = -1;
            Invalidate();   //invalidate all to overwrite 'lines' from dialog
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            mouseDownY = e.Y;
            int index = getButtonIndex(e.X, e.Y);
            if (index <= lastDisplayed && mBAr[index].enabled)
                this.Tag = mBAr[index].func;
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
                if (!UpPossible && mouseDrag > 0)
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
                if (!DownPossible && mouseDrag < 0)
                    mouseDrag = 0;
            }
            if (Math.Abs(e.Y - mouseDownY) > GridHeigh/8)
                this.Tag = null;
            Invalidate();
            base.OnMouseMove(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {}
        protected override void OnPaint(PaintEventArgs e)
        {
            //determine enabled state
            mBAr[(int)BFkt.clearTrack].enabled = (form1ref.TSummary.StartTime != DateTime.MinValue || form1ref.WayPointsT.Count > 0) ? true : false;
            mBAr[(int)BFkt.continu].enabled = (form1ref.PlotCount == 0) ? false : true;
            mBAr[(int)BFkt.waypoint].enabled = (form1ref.state == Form1.State.logging || form1ref.state == Form1.State.paused) ? true : false;
            mBAr[(int)BFkt.navigate].enabled = (form1ref.Plot2ndCount > 0) ? true : false;
            mBAr[(int)BFkt.graph_alt].enabled = (form1ref.Plot2ndCount + form1ref.PlotCount == 0) ? false : true;
            mBAr[(int)BFkt.graph_speed].enabled = (form1ref.PlotCount == 0) ? false : true;
            //mBAr[(int)BFkt.graph_heartRate].enabled =      is set from AddPlotData and cleared from ClearHR
            if (buttonIsClear2F)
                mBAr[(int)BFkt.restore_clear2f].enabled = (form1ref.Plot2ndCount + form1ref.WayPointsT2F.Count == 0) ? false : true;

            bWidth = this.Width / 3;
            bHeigh = bWidth / 2;
            GridHeigh = bHeigh * 3 / 2;

            //mBAr[(int)BFkt.gps_toggle] = Form1.gps.OpenedOrSuspended ? mBGpsOn : mBGpsOff;
            //mBAr[(int)BFkt.clearTrack].text = (form1ref.Plot2ndCount + form1ref.WayPointsT2F.Count == 0) ? "Clear Track" : "Clear2Follow";
            SolidBrush br = new SolidBrush(this.ForeColor);
            //Pen pen = new Pen(disableColor, bWidth / 40);
            Pen pen = new Pen(Color.FromArgb(0x555555), 1);
            Font f = new Font("Arial", 9 * form1ref.df, FontStyle.Regular);
            //int dx = bWidth / 8;
            //int dy = bHeigh / 8;
            Bitmap temp = new Bitmap(this.Width, this.Height);
            Graphics graphics = Graphics.FromImage(temp);
            graphics.Clear(this.BackColor);
            if (!Form1.isLandscape)
            {
                Rectangle cliprec = e.ClipRectangle;
                cliprec.Height = cliprec.Height * 63 / 64;      //create a black separator between mPage buttons and standard buttons on bottom
                graphics.Clip = new System.Drawing.Region(cliprec);
            }
            for (int i = so; i <= (int)BFkt.endOfMenuPage; i++)
            {
                int ix = (i - so) % 3; int iy = (i - so) / 3;
                if ((2 * iy + 1) * GridHeigh > 2 * (Size.Height - mouseDrag))       //show minimum half
                    break;                                                          //not enough room for a complete button
                Rectangle dest_rect = new Rectangle(bWidth * ix, GridHeigh * iy + mouseDrag, bWidth, bHeigh);
                Bitmap bm = (i == i_p && mBAr[i].enabled) ? mBAr[i].icon_p : mBAr[i].icon;
                graphics.DrawImage(bm, dest_rect, new Rectangle(0, 0, mBAr[i].icon.Width, mBAr[i].icon.Height), GraphicsUnit.Pixel);
                if (mBAr[i].enabled) br.Color = ForeColor; else br.Color = Color.FromArgb(0x606060);
                SizeF Tsize = graphics.MeasureString(mBAr[i].text, f);
                graphics.DrawString(mBAr[i].text, f, br, bWidth * ix + (bWidth - Tsize.Width) / 2, GridHeigh * iy + bHeigh + mouseDrag);
                if (!mBAr[i].enabled)
                {
                    //graphics.DrawLine(pen, bWidth * ix + dx, GridHeigh * iy + dy + mouseDrag, bWidth * (ix + 1) - dx, GridHeigh * iy + bHeigh - dy + mouseDrag);
                    //graphics.DrawLine(pen, bWidth * (ix + 1) - dx, GridHeigh * iy + dy + mouseDrag, bWidth * ix + dx, GridHeigh * iy + bHeigh - dy + mouseDrag);
                    for (int g = 0; g < bHeigh; g += 2)
                        graphics.DrawLine(pen, bWidth * ix, GridHeigh * iy + mouseDrag + g, bWidth * (ix + 1), GridHeigh * iy + mouseDrag + g);
                }
                lastDisplayed = i;
            }

            e.Graphics.DrawImage(temp, 0, 0);
            temp.Dispose();
            base.OnPaint(e);
        }
    }
}