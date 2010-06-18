using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;
using System.IO;
using GpsUtils;

namespace GpsCycleWin32
{
    public partial class FormWin32 : Form
    {
        UtmUtil utmUtil = new UtmUtil();
        MapUtil mapUtil = new MapUtil();

        NoBackgroundPanel NoBkPanel = new NoBackgroundPanel();

        // data used for plotting and saving to KML/GPX
        // decimated, max size is PlotDataSize

        // total samples counter
        int Counter = 0;

        const int PlotDataSize = 4096;
        int Decimation = 1;
        float[] PlotLat = new float[PlotDataSize];
        float[] PlotLong = new float[PlotDataSize];
        Int16[] PlotZ = new Int16[PlotDataSize];
        UInt16[] PlotT = new UInt16[PlotDataSize];
        UInt16[] PlotS = new UInt16[PlotDataSize];

        // data for plotting 2nd line (track to follow)
        float[] Plot2ndLat = new float[PlotDataSize];
        float[] Plot2ndLong = new float[PlotDataSize];
        UInt16[] Plot2ndT = new UInt16[PlotDataSize];
        int Counter2nd = 0;

        public FormWin32()
        {
            InitializeComponent();

            comboUnits.SelectedIndex = 0;
            comboBoxKmlOptColor.SelectedIndex = 0;
            comboBoxKmlOptWidth.SelectedIndex = 0;
            comboBoxLine2OptColor.SelectedIndex = 3;
            comboBoxLine2OptWidth.SelectedIndex = 0;
            checkPlotLine2AsDots.Checked = false;
            comboMultiMaps.SelectedIndex = 0;

            // No Background Panel for flicker-free paint
            NoBkPanel.Parent = tabPage2;
            NoBkPanel.Location = new System.Drawing.Point(0, 0);
            NoBkPanel.Name = "NoBkPanel";
            NoBkPanel.Size = new System.Drawing.Size(tabPage2.Width, tabPage2.Height);
            NoBkPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.tabGraph_Paint);
            NoBkPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.tabGraph_MouseMove);
            NoBkPanel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.tabGraph_MouseUp);
            NoBkPanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tabGraph_MouseDown);

            // load maps and file
            EventArgs e = EventArgs.Empty;
            buttonLoadMaps_Click(buttonLoadMaps, e);
            buttonLoadKml2_Click(buttonLoadMaps, e);
            tabControl1.SelectedTab = tabPage2;
        }

        // paint graph ------------------------------------------------------
        // To have nice flicker-free picture movement, we paint first into a bitmap which is larger
        // than the screen, then just paint the bitmap into the screen with a correct shift.
        // We need to paint on "no background panel", which has blank OnPaintBackground, to avoid flicker
        // The bitmap is updated as screen shift is complete (i.e. on mouse up).

        private int MousePosX = 0;
        private int MousePosY = 0;
        private bool MouseMoving = false;

        Bitmap BackBuffer = null;           // the bitmap we draw into
        Graphics BackBufferGraphics = null;

        void PrepareBackBuffer()
        {
            if ((BackBuffer == null)
                || (BackBuffer.Width != NoBkPanel.Width)
                || (BackBuffer.Height != NoBkPanel.Height))
            {
                if (BackBuffer != null)
                { BackBuffer.Dispose(); BackBuffer = null; }
                if (BackBufferGraphics != null)
                { BackBufferGraphics.Dispose(); BackBufferGraphics = null; }

                BackBuffer = new Bitmap(NoBkPanel.Width, NoBkPanel.Height, PixelFormat.Format16bppRgb565);
                BackBufferGraphics = Graphics.FromImage(BackBuffer);
            }
        }

        private double GetUnitsConversionCff()
        {
            // conversion from metres into km or miles for plot
            double c = 0.001;
            if (comboUnits.SelectedIndex == 0) { c = 0.001 / 1.609344; }      // miles
            else if (comboUnits.SelectedIndex == 1) { c = 0.001; }            // km
            else if (comboUnits.SelectedIndex == 2) { c = 0.001 / 1.852; }    // naut miles
            else if (comboUnits.SelectedIndex == 3) { c = 0.001 / 1.609344; } // miles
            else if (comboUnits.SelectedIndex == 4) { c = 0.001; }            // km
            else if (comboUnits.SelectedIndex == 5) { c = 0.001 / 1.609344; } // miles
            return c;
        }
        private Color GetLineColor(ComboBox cmb)
        {
            if (cmb.SelectedIndex == 0) { return Color.Blue; }
            else if (cmb.SelectedIndex == 1) { return Color.Red; }
            else if (cmb.SelectedIndex == 2) { return Color.Lime; }
            else if (cmb.SelectedIndex == 3) { return Color.Yellow; }
            else if (cmb.SelectedIndex == 4) { return Color.White; }
            else if (cmb.SelectedIndex == 5) { return Color.Black; }
            else if (cmb.SelectedIndex == 6) { return Color.LightGray; }
            return Color.Blue;
        }
        private int GetLineWidth(ComboBox cmb)
        {
            return ((cmb.SelectedIndex + 1) * 2);
        }
        private string GetUnitsName()
        {
            if (comboUnits.SelectedIndex == 0) { return " miles"; }
            else if (comboUnits.SelectedIndex == 1) { return " km"; }
            else if (comboUnits.SelectedIndex == 2) { return " naut miles"; }
            else if (comboUnits.SelectedIndex == 3) { return " miles"; }
            else if (comboUnits.SelectedIndex == 4) { return " km"; }
            else if (comboUnits.SelectedIndex == 5) { return " miles"; }
            return " miles";
        }

        private void ComputeMapPosition()
        {
            // MAP scale was here!!! mapUtil.Maps[i].scale


            // reset move/zoom vars
            mapUtil.ZoomValue = 1.0;
            mapUtil.ScreenShiftX = 0;
            mapUtil.ScreenShiftY = 0;
            MousePosX = 0;
            MousePosY = 0;
            MouseMoving = false;
        }

        private void tabGraph_Paint(object sender, PaintEventArgs e)
        {
            PrepareBackBuffer();

                // plotting in Long (as X) / Lat (as Y) coordinates
                mapUtil.DrawMaps(e.Graphics, BackBuffer, BackBufferGraphics, MouseMoving,
                                 checkIsRunning.Checked, comboMultiMaps.SelectedIndex, GetUnitsConversionCff(), GetUnitsName(),
                                 PlotLong, PlotLat, Counter / Decimation, GetLineColor(comboBoxKmlOptColor), GetLineWidth(comboBoxKmlOptWidth),
                                 checkPlotTrackAsDots.Checked,
                                 Plot2ndLong, Plot2ndLat, Counter2nd, GetLineColor(comboBoxLine2OptColor), GetLineWidth(comboBoxLine2OptWidth),
                                 checkPlotLine2AsDots.Checked);
        }
        private void tabGraph_MouseDown(object sender, MouseEventArgs e)
        {
            MouseMoving = false;
            MousePosX = e.X;
            MousePosY = e.Y;
            mapUtil.ScreenShiftSaveX = mapUtil.ScreenShiftX;
            mapUtil.ScreenShiftSaveY = mapUtil.ScreenShiftY;
        }
        private void tabGraph_MouseUp(object sender, MouseEventArgs e)
        {
            MouseMoving = false;
            mapUtil.ScreenShiftSaveX = 0;
            mapUtil.ScreenShiftSaveY = 0;
            NoBkPanel.Invalidate();
        }
        private void tabGraph_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.None)
            {
                mapUtil.ScreenShiftX = mapUtil.ScreenShiftSaveX + (e.X - MousePosX);
                mapUtil.ScreenShiftY = mapUtil.ScreenShiftSaveY + (e.Y - MousePosY);
                MouseMoving = true;
                NoBkPanel.Invalidate();
            }
            else { MouseMoving = false; }
        }
        private void buttonZoomIn_Click(object sender, EventArgs e)
        {
            mapUtil.ZoomIn();
            NoBkPanel.Invalidate();
        }
        private void buttonZoomOut_Click(object sender, EventArgs e)
        {
            mapUtil.ZoomOut();
            NoBkPanel.Invalidate();
        }

        

        // end paint graph ------------------------------------------------------

        private void buttonLoadMaps_Click(object sender, EventArgs e)
        {
            mapUtil.LoadMaps("C:\\temp\\badminton_files");
            mapUtil.OsmTilesWebDownload = 0;
//            mapUtil.LoadMaps("C:\\temp\\phone\\maps");
          

            ComputeMapPosition();
        }
        private void buttonLoadKml2_Click(object sender, EventArgs e)
        {
            string kml_file = "C:\\temp\\badminton_files\\chip_test1.kml";

            if (ReadFileUtil.LoadKml(kml_file, PlotDataSize, ref Plot2ndLat, ref Plot2ndLong, ref Plot2ndT, out Counter2nd))  // loaded OK
            {
          //      MessageBox.Show("Loaded " + Counter2nd.ToString() + " points for the track to follow", Path.GetFileName(kml_file),
          //                  MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

                // AAZ debug
                if (Counter2nd != 0)
                {
                    Counter = 1;
                    PlotLat[0] = Plot2ndLat[0];
                    PlotLong[0] = Plot2ndLong[0];
                    ComputeMapPosition();
                }
            }
            else
            {
                // show message box and stay on file open tab
                MessageBox.Show("Error reading file or it does not have track data", "Error loading .kml file",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
          //  mapUtil.DownloadFileTest();
        }

    }
    public class NoBackgroundPanel : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }
    }

}