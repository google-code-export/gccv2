using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Collections;
using System.Windows.Forms;
using System.Data;
using System.IO;
using System.Reflection;
using GpsUtils;

/* From WIKI:
 1 international knot = 
 1 nautical mile per hour (exactly), 
 1.852 kilometres per hour (exactly),[5] 

 1 mile  = 1.609344 kilometers
*/

namespace GpsCycleComputer
{
    public class Form1 : System.Windows.Forms.Form
    {
        Gps gps = new Gps();
        GpsPosition position = null;
        UtmUtil utmUtil = new UtmUtil();

        FileStream fstream;
        BinaryWriter writer;

        // custom buttons
        PictureSelectorButton buttonMain = new PictureSelectorButton();
        PictureSelectorButton buttonGraph = new PictureSelectorButton();
        PictureSelectorButton buttonOptions = new PictureSelectorButton();
        PictureSelectorButton buttonAbout = new PictureSelectorButton();

        PictureButton buttonPicLoad = new PictureButton();
        PictureButton buttonPicSaveKML = new PictureButton();
        PictureButton buttonPicSaveGPX = new PictureButton();
        PictureButton buttonPicBkOff = new PictureButton();

        PictureSelectorButton buttonStart = new PictureSelectorButton();
        PictureSelectorButton buttonStop = new PictureSelectorButton();

        // buttons for my own FileOpen dialog
        PictureButton buttonDialogOpen = new PictureButton();
        PictureButton buttonDialogCancel = new PictureButton();

        Bitmap AboutTabImage;

        Color bk;
        Color fo;

        /* Note units for all internal vars: 
         * time : sec  
         * distance (as x/y to start point or total) : metres
         * height : metres
         * speed : km/h
         * 
         * These is converted to required units for display
         * in UpdateDisplay function
        */

        // Starting point
        DateTime StartTimeUtc;
        DateTime StartTime;
        double StartLat = 0.0;
        double StartLong = 0.0;

        // need to shift origin, to be able to save X/Y as short int in metres
        double OriginShiftX = 0.0;
        double OriginShiftY = 0.0;

        // Interval to switch GPS off (0 means always on)
        int[] PollGpsTimeSec = new int[6] { 0, 5, 20, 60, 180, 600 };
        DateTime LastPointUtc;
        int GpsSearchCount = 0;
        int FirstSampleValidCount = 0;

        // flag to indicate the the data was accepted by GetGpsData and save into file
        bool GpsDataOk = false;

        // to disable timer tick functions, if Stop is pressed, etc
        bool LockGpsTick = false;

        // to indicate that it was stopped on low
        bool StoppedOnLow = false;

        // to indicate that flush needs to be called
        bool FlushStream = false;

        // to save battery status (every 3 minutes)
        DateTime LastBatterySave;

        // total samples counter
        int Counter = 0;

        // average and max speed, distance. OldX/Y/T are coordinates/time of prev point.
        double MaxSpeed = 0.0;
        double Distance = 0.0;
        double OldX = 0.0, OldY = 0.0;
        int OldT = 0;

        // Current time, X/Y relative to starting point, abs height Z and current speed
        int CurrentTimeSec = 0;
        int CurrentStoppageTimeSec = 0;
        double CurrentX = 0.0, CurrentY = 0.0, CurrentZ = 0.0;
        double CurrentSpeed = 0.0;
        string CurrentFileName = "";

        // data used for plotting and saving to KML, in metres relative to starting point
        // also height Z and time T to save into GPX
        // decimated, if runs over PlotDataSize
        const int PlotDataSize = 4096;
        int Decimation = 1;
        int[] PlotX = new int[PlotDataSize];
        int[] PlotY = new int[PlotDataSize];
        Int16[] PlotZ = new Int16[PlotDataSize];
        UInt16[] PlotT = new UInt16[PlotDataSize];

        // to disable auto-save of controls on option page during startup
        bool DoNotSaveSettingsFlag = true;

        // temp file name in the root folder (main memory)
        string TempFileName = "\\tmp.gcc";

        // form components
        private Panel tabOptions;
        private Panel tabMain;
        private Panel tabGraph;
        private Panel tabAbout;
        private Panel tabOpenFile;
        private Timer timerGps;
        private ComboBox comboGpsPoll;
        private Label label1;
        private ComboBox comboUnits;
        private Timer timerIdleReset;
        private Label labelStartTime;
        private Label labelStartPos;
        private CheckBox checkStopOnLow;
        private Label labelRunTime;
        private Label labelSpeedUnit;
        private Label labelSpeed;
        private Label labelPosUnit;
        private Label labelPos;
        private Label labelRevision;
        private Label labelStatus;
        private Label labelDistUnit;
        private Label labelDist;
        private Timer timerStartDelay;
        private CheckBox checkExStopTime;
        private Label labelExStops;
        private Label labelStatusB;
        private Label labelCurrentTime;
        private Label labelFileName;
        private Label labelStartB;
        private Label labelStatusGps;
        private Label labelGpsLed;
        private ListBox listBoxFiles;
        private Label label2;

        // c-tor. Create classes used, init some components
        public Form1()
        {
            // Required for Windows Form Designer support
            InitializeComponent();

            // set defaults (shall load from file later)
            comboGpsPoll.SelectedIndex = 0;
            comboUnits.SelectedIndex = 0;


            string Revision = "$Revision: 1.29 $";
            Revision = Revision.Replace("Revision: 1", " 2");
            Revision = Revision.Replace("$", "");
            Revision = Revision.Trim();
            labelRevision.Text = "version " + Revision;

            ClearDisplay();

            ApplyCustomBackground();

            CreateCustomControls();

//            ScaleToQVGA();
        }

        private void ApplyCustomBackground()
        {
            bk = LoadBkColor();
            fo = LoadForeColor();

            // this one is not editable
            tabAbout.BackColor = Color.FromArgb(51, 51, 51);
            labelRevision.BackColor = Color.FromArgb(51, 51, 51);
            labelRevision.ForeColor = Color.FromArgb(255, 255, 255);

            tabOptions.BackColor = bk;
            tabMain.BackColor = bk;
            tabGraph.BackColor = bk;
            tabOpenFile.BackColor = bk;
            comboGpsPoll.BackColor = bk;  comboGpsPoll.ForeColor = fo;
            label1.BackColor = bk; label1.ForeColor = fo;
            comboUnits.BackColor = bk; comboUnits.ForeColor = fo;
            labelStartTime.BackColor = bk; labelStartTime.ForeColor = fo;
            labelStartPos.BackColor = bk; labelStartPos.ForeColor = fo;
            checkStopOnLow.BackColor = bk; checkStopOnLow.ForeColor = fo;
            labelRunTime.BackColor = bk; labelRunTime.ForeColor = fo;
            labelSpeedUnit.BackColor = bk; labelSpeedUnit.ForeColor = fo;
            labelSpeed.BackColor = bk; labelSpeed.ForeColor = fo;
            labelPosUnit.BackColor = bk; labelPosUnit.ForeColor = fo;
            labelPos.BackColor = bk; labelPos.ForeColor = fo;
            labelStatus.BackColor = bk; labelStatus.ForeColor = fo;
            labelDistUnit.BackColor = bk; labelDistUnit.ForeColor = fo;
            labelDist.BackColor = bk; labelDist.ForeColor = fo;
            checkExStopTime.BackColor = bk; checkExStopTime.ForeColor = fo;
            labelExStops.BackColor = bk; labelExStops.ForeColor = fo;
            labelStatusB.BackColor = bk; labelStatusB.ForeColor = fo;
            labelCurrentTime.BackColor = bk; labelCurrentTime.ForeColor = fo;
            labelFileName.BackColor = bk; labelFileName.ForeColor = fo;
            labelStartB.BackColor = bk; labelStartB.ForeColor = fo;
            labelStatusGps.BackColor = bk; labelStatusGps.ForeColor = fo;
            labelGpsLed.BackColor = bk; labelGpsLed.ForeColor = fo;
            label2.BackColor = bk; label2.ForeColor = fo;
            listBoxFiles.BackColor = bk; listBoxFiles.ForeColor = fo;
            this.BackColor = bk;
        }

        private Bitmap LoadBitmap(string name)
        {
            string CurrentDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            string file_name = name;
            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { file_name = CurrentDirectory + "\\" + file_name; }

            return File.Exists(file_name) ? new Bitmap(file_name) : new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("GpsCycleComputer.img." + name));

            // not exists, load internal one
        }

        private Color LoadBkColor()
        {
            string CurrentDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            string file_name = "bk_color.jpg";
            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { file_name = CurrentDirectory + "\\" + file_name; }

            if (File.Exists(file_name))
            {
                Bitmap bmp = new Bitmap(file_name);

                return bmp.GetPixel(0, 0);
            }

            // not exists, load internal one
            return Color.FromArgb(51, 51, 51);
        }

        private Color LoadForeColor()
        {
            string CurrentDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            string file_name = "fore_color.jpg";
            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { file_name = CurrentDirectory + "\\" + file_name; }

            if (File.Exists(file_name))
            {
                Bitmap bmp = new Bitmap(file_name);

                return bmp.GetPixel(0, 0);
            }

            // not exists, load internal one
            return Color.FromArgb(255, 255, 255);
        }

        private void CreateCustomControls()
        {
            // Create custom buttons ----------------------------
            Assembly asm = Assembly.GetExecutingAssembly();


            // bottom menu --------------
            buttonMain.Parent = this;
            buttonMain.Bounds = new Rectangle(0, 506, 119, 81);
            buttonMain.BackgroundImage = LoadBitmap("btm_main_normal.jpg");
            buttonMain.PressedImage = LoadBitmap("btm_main_pressed.jpg");
            buttonMain.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            buttonMain.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUp);
            buttonMain.pressed = true;

            buttonGraph.Parent = this;
            buttonGraph.Bounds = new Rectangle(119, 506, 120, 81);
            buttonGraph.BackgroundImage = LoadBitmap("btm_graph_normal.jpg");
            buttonGraph.PressedImage = LoadBitmap("btm_graph_pressed.jpg");
            buttonGraph.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            buttonGraph.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUp);
            buttonGraph.pressed = false;

            buttonOptions.Parent = this;
            buttonOptions.Bounds = new Rectangle(239, 506, 121, 81);
            buttonOptions.BackgroundImage = LoadBitmap("btm_options_normal.jpg");
            buttonOptions.PressedImage = LoadBitmap("btm_options_pressed.jpg");
            buttonOptions.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            buttonOptions.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUp);
            buttonOptions.pressed = false;

            buttonAbout.Parent = this;
            buttonAbout.Bounds = new Rectangle(360, 506, 120, 81);
            buttonAbout.BackgroundImage = LoadBitmap("btm_about_normal.jpg");
            buttonAbout.PressedImage = LoadBitmap("btm_about_pressed.jpg");
            buttonAbout.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            buttonAbout.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUp);
            buttonAbout.pressed = false;

            // save buttons --------------
            buttonPicLoad.Parent = this.tabOptions;
            buttonPicLoad.Bounds = new Rectangle(51, 400, 124, 98);
            buttonPicLoad.BackgroundImage = LoadBitmap("open_gcc_normal.jpg");
            buttonPicLoad.PressedImage = LoadBitmap("open_gcc_pressed.jpg");
            buttonPicLoad.Click += new System.EventHandler(this.buttonLoad_Click);

            buttonPicSaveKML.Parent = this.tabOptions;
            buttonPicSaveKML.Bounds = new Rectangle(175, 400, 124, 98);
            buttonPicSaveKML.BackgroundImage = LoadBitmap("save_kml_normal.jpg");
            buttonPicSaveKML.PressedImage = LoadBitmap("save_kml_pressed.jpg");
            buttonPicSaveKML.Click += new System.EventHandler(this.buttonSaveKML_Click);

            buttonPicSaveGPX.Parent = this.tabOptions;
            buttonPicSaveGPX.Bounds = new Rectangle(299, 400, 124, 98);
            buttonPicSaveGPX.BackgroundImage = LoadBitmap("save_gpx_normal.jpg");
            buttonPicSaveGPX.PressedImage = LoadBitmap("save_gpx_pressed.jpg");
            buttonPicSaveGPX.Click += new System.EventHandler(this.buttonSaveGPX_Click);

            // bk off buttons --------------
            buttonPicBkOff.Parent = this.tabMain;
            buttonPicBkOff.Bounds = new Rectangle(327, 32, 155, 77);
            buttonPicBkOff.BackgroundImage = LoadBitmap("bklight_normal.jpg");
            buttonPicBkOff.PressedImage = LoadBitmap("bklight_pressed.jpg");
            buttonPicBkOff.Click += new System.EventHandler(this.buttonBklitOff_Click); ;


            // Start Stop buttons --------------

            buttonStart.Parent = this.tabMain;
            buttonStart.Bounds = new Rectangle(3, 32, 155, 77);
            buttonStart.BackgroundImage = LoadBitmap("start_enable.jpg");
            buttonStart.PressedImage = LoadBitmap("start_disable.jpg");
            buttonStart.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDownS);
            buttonStart.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUpS);
            buttonStart.pressed = false;

            buttonStop.Parent = this.tabMain;
            buttonStop.Bounds = new Rectangle(166, 32, 155, 77);
            buttonStop.BackgroundImage = LoadBitmap("stop_enable.jpg");
            buttonStop.PressedImage = LoadBitmap("stop_disable.jpg");
            buttonStop.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDownS);
            buttonStop.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUpS);
            buttonStop.pressed = true;

            buttonStop.Enabled = false;
            buttonStart.Enabled = true;

            // buttons on FileDialog tab --------------

            buttonDialogOpen.Parent = this.tabOpenFile;
            buttonDialogOpen.Bounds = new Rectangle(55, 420, 155, 77);
            buttonDialogOpen.BackgroundImage = LoadBitmap("dlg_open_normal.jpg");
            buttonDialogOpen.PressedImage = LoadBitmap("dlg_open_pressed.jpg");
            buttonDialogOpen.Click += new System.EventHandler(this.buttonDialogOpen_Click);

            buttonDialogCancel.Parent = this.tabOpenFile;
            buttonDialogCancel.Bounds = new Rectangle(270, 420, 155, 77);
            buttonDialogCancel.BackgroundImage = LoadBitmap("dlg_cancel_normal.jpg");
            buttonDialogCancel.PressedImage = LoadBitmap("dlg_cancel_pressed.jpg");
            buttonDialogCancel.Click += new System.EventHandler(this.buttonDialogCancel_Click);

            // about tab image
            AboutTabImage = new Bitmap(asm.GetManifestResourceStream("GpsCycleComputer.img.about.jpg"));

            tabMain.BringToFront();
        }

        private void ScaleControl(Control c)
        {
            c.Top /= 2;
            c.Left /= 2;
            c.Width /= 2;
            c.Height /= 2;
        }

        private void ScaleToQVGA()
        {
            ScaleControl((Control)buttonMain);
            ScaleControl((Control)buttonGraph);
            ScaleControl((Control)buttonOptions);
            ScaleControl((Control)buttonAbout);
            ScaleControl((Control)buttonPicLoad);
            ScaleControl((Control)buttonPicSaveKML);
            ScaleControl((Control)buttonPicSaveGPX);
            ScaleControl((Control)buttonPicBkOff);
            ScaleControl((Control)buttonStart);
            ScaleControl((Control)buttonStop);
            ScaleControl((Control)buttonDialogOpen);
            ScaleControl((Control)buttonDialogCancel);
            ScaleControl((Control)tabOptions);
            ScaleControl((Control)tabMain);
            ScaleControl((Control)tabGraph);
            ScaleControl((Control)tabAbout);
            ScaleControl((Control)tabOpenFile);
            ScaleControl((Control)comboGpsPoll);
            ScaleControl((Control)label1);
            ScaleControl((Control)comboUnits);
            ScaleControl((Control)labelStartTime);
            ScaleControl((Control)labelStartPos);
            ScaleControl((Control)checkStopOnLow);
            ScaleControl((Control)labelRunTime);
            ScaleControl((Control)labelSpeedUnit);
            ScaleControl((Control)labelSpeed);
            ScaleControl((Control)labelPosUnit);
            ScaleControl((Control)labelPos);
            ScaleControl((Control)labelRevision);
            ScaleControl((Control)labelStatus);
            ScaleControl((Control)labelDistUnit);
            ScaleControl((Control)labelDist);
            ScaleControl((Control)checkExStopTime);
            ScaleControl((Control)labelExStops);
            ScaleControl((Control)labelStatusB);
            ScaleControl((Control)labelCurrentTime);
            ScaleControl((Control)labelFileName);
            ScaleControl((Control)labelStartB);
            ScaleControl((Control)labelStatusGps);
            ScaleControl((Control)labelGpsLed);
            ScaleControl((Control)listBoxFiles);
            ScaleControl((Control)label2);
        }

        // Clean up any resources being used.
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
          System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
          this.labelExStops = new System.Windows.Forms.Label();
          this.tabMain = new System.Windows.Forms.Panel();
          this.labelGpsLed = new System.Windows.Forms.Label();
          this.labelStatusGps = new System.Windows.Forms.Label();
          this.labelStatus = new System.Windows.Forms.Label();
          this.labelPosUnit = new System.Windows.Forms.Label();
          this.labelPos = new System.Windows.Forms.Label();
          this.labelStartB = new System.Windows.Forms.Label();
          this.labelStatusB = new System.Windows.Forms.Label();
          this.labelCurrentTime = new System.Windows.Forms.Label();
          this.labelDistUnit = new System.Windows.Forms.Label();
          this.labelDist = new System.Windows.Forms.Label();
          this.labelSpeedUnit = new System.Windows.Forms.Label();
          this.labelSpeed = new System.Windows.Forms.Label();
          this.labelRunTime = new System.Windows.Forms.Label();
          this.labelStartPos = new System.Windows.Forms.Label();
          this.labelStartTime = new System.Windows.Forms.Label();
          this.tabAbout = new System.Windows.Forms.Panel();
          this.labelRevision = new System.Windows.Forms.Label();
          this.tabOpenFile = new System.Windows.Forms.Panel();
          this.listBoxFiles = new System.Windows.Forms.ListBox();
          this.tabGraph = new System.Windows.Forms.Panel();
          this.tabOptions = new System.Windows.Forms.Panel();
          this.labelFileName = new System.Windows.Forms.Label();
          this.checkExStopTime = new System.Windows.Forms.CheckBox();
          this.checkStopOnLow = new System.Windows.Forms.CheckBox();
          this.comboUnits = new System.Windows.Forms.ComboBox();
          this.label2 = new System.Windows.Forms.Label();
          this.comboGpsPoll = new System.Windows.Forms.ComboBox();
          this.label1 = new System.Windows.Forms.Label();
          this.timerGps = new System.Windows.Forms.Timer();
          this.timerIdleReset = new System.Windows.Forms.Timer();
          this.timerStartDelay = new System.Windows.Forms.Timer();
          this.tabMain.SuspendLayout();
          this.tabAbout.SuspendLayout();
          this.tabOpenFile.SuspendLayout();
          this.tabOptions.SuspendLayout();
          this.SuspendLayout();
          // 
          // labelExStops
          // 
          this.labelExStops.Location = new System.Drawing.Point(183, 181);
          this.labelExStops.Name = "labelExStops";
          this.labelExStops.Size = new System.Drawing.Size(166, 35);
          this.labelExStops.Text = "incl stop";
          // 
          // tabMain
          // 
          this.tabMain.Controls.Add(this.labelGpsLed);
          this.tabMain.Controls.Add(this.labelStatusGps);
          this.tabMain.Controls.Add(this.labelStatus);
          this.tabMain.Controls.Add(this.labelPosUnit);
          this.tabMain.Controls.Add(this.labelPos);
          this.tabMain.Controls.Add(this.labelStartB);
          this.tabMain.Controls.Add(this.labelStatusB);
          this.tabMain.Controls.Add(this.labelCurrentTime);
          this.tabMain.Controls.Add(this.labelExStops);
          this.tabMain.Controls.Add(this.labelDistUnit);
          this.tabMain.Controls.Add(this.labelDist);
          this.tabMain.Controls.Add(this.labelSpeedUnit);
          this.tabMain.Controls.Add(this.labelSpeed);
          this.tabMain.Controls.Add(this.labelRunTime);
          this.tabMain.Controls.Add(this.labelStartPos);
          this.tabMain.Controls.Add(this.labelStartTime);
          this.tabMain.Location = new System.Drawing.Point(0, 0);
          this.tabMain.Name = "tabMain";
          this.tabMain.Size = new System.Drawing.Size(480, 507);
          // 
          // labelGpsLed
          // 
          this.labelGpsLed.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
          this.labelGpsLed.Location = new System.Drawing.Point(460, 1);
          this.labelGpsLed.Name = "labelGpsLed";
          this.labelGpsLed.Size = new System.Drawing.Size(21, 28);
          this.labelGpsLed.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelStatusGps
          // 
          this.labelStatusGps.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
          this.labelStatusGps.Location = new System.Drawing.Point(239, 0);
          this.labelStatusGps.Name = "labelStatusGps";
          this.labelStatusGps.Size = new System.Drawing.Size(216, 28);
          this.labelStatusGps.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelStatus
          // 
          this.labelStatus.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
          this.labelStatus.Location = new System.Drawing.Point(3, 0);
          this.labelStatus.Name = "labelStatus";
          this.labelStatus.Size = new System.Drawing.Size(256, 28);
          this.labelStatus.Text = "gps off";
          // 
          // labelPosUnit
          // 
          this.labelPosUnit.Location = new System.Drawing.Point(208, 363);
          this.labelPosUnit.Name = "labelPosUnit";
          this.labelPosUnit.Size = new System.Drawing.Size(269, 31);
          this.labelPosUnit.Text = "x / y (km)   z (m)";
          this.labelPosUnit.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelPos
          // 
          this.labelPos.Font = new System.Drawing.Font("Tahoma", 16F, System.Drawing.FontStyle.Regular);
          this.labelPos.Location = new System.Drawing.Point(126, 316);
          this.labelPos.Name = "labelPos";
          this.labelPos.Size = new System.Drawing.Size(351, 67);
          this.labelPos.Text = "000.00 000.00";
          this.labelPos.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelStartB
          // 
          this.labelStartB.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
          this.labelStartB.Location = new System.Drawing.Point(292, 443);
          this.labelStartB.Name = "labelStartB";
          this.labelStartB.Size = new System.Drawing.Size(185, 28);
          this.labelStartB.Text = "battery";
          this.labelStartB.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelStatusB
          // 
          this.labelStatusB.Location = new System.Drawing.Point(292, 405);
          this.labelStatusB.Name = "labelStatusB";
          this.labelStatusB.Size = new System.Drawing.Size(185, 28);
          this.labelStatusB.Text = "battery";
          this.labelStatusB.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelCurrentTime
          // 
          this.labelCurrentTime.Location = new System.Drawing.Point(3, 405);
          this.labelCurrentTime.Name = "labelCurrentTime";
          this.labelCurrentTime.Size = new System.Drawing.Size(346, 28);
          this.labelCurrentTime.Text = "time";
          // 
          // labelDistUnit
          // 
          this.labelDistUnit.Location = new System.Drawing.Point(3, 181);
          this.labelDistUnit.Name = "labelDistUnit";
          this.labelDistUnit.Size = new System.Drawing.Size(143, 35);
          this.labelDistUnit.Text = "km";
          // 
          // labelDist
          // 
          this.labelDist.Font = new System.Drawing.Font("Tahoma", 20F, System.Drawing.FontStyle.Regular);
          this.labelDist.Location = new System.Drawing.Point(3, 125);
          this.labelDist.Name = "labelDist";
          this.labelDist.Size = new System.Drawing.Size(193, 75);
          this.labelDist.Text = "000.0";
          // 
          // labelSpeedUnit
          // 
          this.labelSpeedUnit.Location = new System.Drawing.Point(3, 273);
          this.labelSpeedUnit.Name = "labelSpeedUnit";
          this.labelSpeedUnit.Size = new System.Drawing.Size(474, 31);
          this.labelSpeedUnit.Text = "current / average / max (km/h)";
          this.labelSpeedUnit.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelSpeed
          // 
          this.labelSpeed.Font = new System.Drawing.Font("Tahoma", 18F, System.Drawing.FontStyle.Regular);
          this.labelSpeed.Location = new System.Drawing.Point(3, 216);
          this.labelSpeed.Name = "labelSpeed";
          this.labelSpeed.Size = new System.Drawing.Size(474, 72);
          this.labelSpeed.Text = "000.0 / 000.0 / 000.0";
          this.labelSpeed.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelRunTime
          // 
          this.labelRunTime.Font = new System.Drawing.Font("Tahoma", 24F, System.Drawing.FontStyle.Regular);
          this.labelRunTime.Location = new System.Drawing.Point(126, 114);
          this.labelRunTime.Name = "labelRunTime";
          this.labelRunTime.Size = new System.Drawing.Size(351, 75);
          this.labelRunTime.Text = "00:00:00";
          this.labelRunTime.TextAlign = System.Drawing.ContentAlignment.TopRight;
          // 
          // labelStartPos
          // 
          this.labelStartPos.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
          this.labelStartPos.Location = new System.Drawing.Point(3, 471);
          this.labelStartPos.Name = "labelStartPos";
          this.labelStartPos.Size = new System.Drawing.Size(474, 28);
          this.labelStartPos.Text = "start position";
          // 
          // labelStartTime
          // 
          this.labelStartTime.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
          this.labelStartTime.Location = new System.Drawing.Point(3, 443);
          this.labelStartTime.Name = "labelStartTime";
          this.labelStartTime.Size = new System.Drawing.Size(358, 28);
          this.labelStartTime.Text = "start time";
          // 
          // tabAbout
          // 
          this.tabAbout.Controls.Add(this.labelRevision);
          this.tabAbout.Location = new System.Drawing.Point(0, 0);
          this.tabAbout.Name = "tabAbout";
          this.tabAbout.Size = new System.Drawing.Size(480, 507);
          this.tabAbout.Paint += new System.Windows.Forms.PaintEventHandler(this.tabAbout_Paint);
          // 
          // labelRevision
          // 
          this.labelRevision.Location = new System.Drawing.Point(0, 445);
          this.labelRevision.Name = "labelRevision";
          this.labelRevision.Size = new System.Drawing.Size(480, 31);
          this.labelRevision.Text = "version";
          this.labelRevision.TextAlign = System.Drawing.ContentAlignment.TopCenter;
          // 
          // tabOpenFile
          // 
          this.tabOpenFile.Controls.Add(this.listBoxFiles);
          this.tabOpenFile.Location = new System.Drawing.Point(0, 0);
          this.tabOpenFile.Name = "tabOpenFile";
          this.tabOpenFile.Size = new System.Drawing.Size(480, 507);
          // 
          // listBoxFiles
          // 
          this.listBoxFiles.Items.Add("1");
          this.listBoxFiles.Items.Add("2");
          this.listBoxFiles.Items.Add("3");
          this.listBoxFiles.Items.Add("4");
          this.listBoxFiles.Location = new System.Drawing.Point(0, 0);
          this.listBoxFiles.Name = "listBoxFiles";
          this.listBoxFiles.Size = new System.Drawing.Size(478, 437);
          this.listBoxFiles.TabIndex = 0;
          // 
          // tabGraph
          // 
          this.tabGraph.Location = new System.Drawing.Point(0, 0);
          this.tabGraph.Name = "tabGraph";
          this.tabGraph.Size = new System.Drawing.Size(480, 507);
          this.tabGraph.Paint += new System.Windows.Forms.PaintEventHandler(this.tabPage3_Paint);
          // 
          // tabOptions
          // 
          this.tabOptions.Controls.Add(this.labelFileName);
          this.tabOptions.Controls.Add(this.checkExStopTime);
          this.tabOptions.Controls.Add(this.checkStopOnLow);
          this.tabOptions.Controls.Add(this.comboUnits);
          this.tabOptions.Controls.Add(this.label2);
          this.tabOptions.Controls.Add(this.comboGpsPoll);
          this.tabOptions.Controls.Add(this.label1);
          this.tabOptions.Location = new System.Drawing.Point(0, 0);
          this.tabOptions.Name = "tabOptions";
          this.tabOptions.Size = new System.Drawing.Size(480, 507);
          // 
          // labelFileName
          // 
          this.labelFileName.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
          this.labelFileName.Location = new System.Drawing.Point(3, 329);
          this.labelFileName.Name = "labelFileName";
          this.labelFileName.Size = new System.Drawing.Size(474, 28);
          this.labelFileName.Text = ".";
          // 
          // checkExStopTime
          // 
          this.checkExStopTime.Checked = true;
          this.checkExStopTime.CheckState = System.Windows.Forms.CheckState.Checked;
          this.checkExStopTime.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular);
          this.checkExStopTime.Location = new System.Drawing.Point(1, 156);
          this.checkExStopTime.Name = "checkExStopTime";
          this.checkExStopTime.Size = new System.Drawing.Size(477, 40);
          this.checkExStopTime.TabIndex = 29;
          this.checkExStopTime.Text = "Exclude stop time";
          this.checkExStopTime.Click += new System.EventHandler(this.checkExStopTime_Click);
          // 
          // checkStopOnLow
          // 
          this.checkStopOnLow.Checked = true;
          this.checkStopOnLow.CheckState = System.Windows.Forms.CheckState.Checked;
          this.checkStopOnLow.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular);
          this.checkStopOnLow.Location = new System.Drawing.Point(1, 231);
          this.checkStopOnLow.Name = "checkStopOnLow";
          this.checkStopOnLow.Size = new System.Drawing.Size(477, 40);
          this.checkStopOnLow.TabIndex = 16;
          this.checkStopOnLow.Text = "Stop GPS if battery <20%";
          this.checkStopOnLow.Click += new System.EventHandler(this.comboGpsPoll_SelectedIndexChanged);
          // 
          // comboUnits
          // 
          this.comboUnits.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Regular);
          this.comboUnits.Items.Add("miles / mph");
          this.comboUnits.Items.Add("km / kmh");
          this.comboUnits.Items.Add("naut miles / knots");
          this.comboUnits.Location = new System.Drawing.Point(98, 65);
          this.comboUnits.Name = "comboUnits";
          this.comboUnits.Size = new System.Drawing.Size(379, 51);
          this.comboUnits.TabIndex = 4;
          this.comboUnits.SelectedIndexChanged += new System.EventHandler(this.checkExStopTime_Click);
          // 
          // label2
          // 
          this.label2.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Regular);
          this.label2.Location = new System.Drawing.Point(3, 68);
          this.label2.Name = "label2";
          this.label2.Size = new System.Drawing.Size(219, 51);
          this.label2.Text = "Units:";
          // 
          // comboGpsPoll
          // 
          this.comboGpsPoll.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Regular);
          this.comboGpsPoll.Items.Add("always on");
          this.comboGpsPoll.Items.Add("run every 5 sec");
          this.comboGpsPoll.Items.Add("run every 20 sec");
          this.comboGpsPoll.Items.Add("run every 1 min");
          this.comboGpsPoll.Items.Add("run every 3 min ");
          this.comboGpsPoll.Items.Add("run every 10 min");
          this.comboGpsPoll.Location = new System.Drawing.Point(192, 2);
          this.comboGpsPoll.Name = "comboGpsPoll";
          this.comboGpsPoll.Size = new System.Drawing.Size(285, 51);
          this.comboGpsPoll.TabIndex = 1;
          this.comboGpsPoll.SelectedIndexChanged += new System.EventHandler(this.comboGpsPoll_SelectedIndexChanged);
          // 
          // label1
          // 
          this.label1.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Regular);
          this.label1.Location = new System.Drawing.Point(3, 6);
          this.label1.Name = "label1";
          this.label1.Size = new System.Drawing.Size(219, 51);
          this.label1.Text = "GPS activity:";
          // 
          // timerGps
          // 
          this.timerGps.Interval = 2000;
          this.timerGps.Tick += new System.EventHandler(this.timerGps_Tick);
          // 
          // timerIdleReset
          // 
          this.timerIdleReset.Interval = 15000;
          this.timerIdleReset.Tick += new System.EventHandler(this.timerIdleReset_Tick);
          // 
          // timerStartDelay
          // 
          this.timerStartDelay.Interval = 2000;
          this.timerStartDelay.Tick += new System.EventHandler(this.timerStartDelay_Tick);
          // 
          // Form1
          // 
          this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
          this.ClientSize = new System.Drawing.Size(480, 588);
          this.Controls.Add(this.tabOptions);
          this.Controls.Add(this.tabMain);
          this.Controls.Add(this.tabGraph);
          this.Controls.Add(this.tabOpenFile);
          this.Controls.Add(this.tabAbout);
          this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
          this.Location = new System.Drawing.Point(0, 52);
          this.Name = "Form1";
          this.Text = "GPS Cycle Computer";
          this.Load += new System.EventHandler(this.Form1_Load);
          this.Closed += new System.EventHandler(this.Form1_Closed);
          this.Closing += new System.ComponentModel.CancelEventHandler(this.Form1_Closing);
          this.tabMain.ResumeLayout(false);
          this.tabAbout.ResumeLayout(false);
          this.tabOpenFile.ResumeLayout(false);
          this.tabOptions.ResumeLayout(false);
          this.ResumeLayout(false);

        }
        #endregion

        // The main entry point for the application.
        static void Main()
        {
            Application.Run(new Form1());
        }

        // Create GPS event handlers on form load
        private void Form1_Load(object sender, System.EventArgs e)
        {
            // load settings -----------------
            string CurrentDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            string file_name = "GpsCycleComputer.dat";
            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { file_name = CurrentDirectory + "\\" + file_name; }

            try
            {
                FileStream fs = new FileStream(file_name, FileMode.OpenOrCreate);
                BinaryReader wr = new BinaryReader(fs);

                if (wr.PeekChar() != -1) { comboGpsPoll.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { comboUnits.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { wr.ReadInt32(); }  // not used anymore
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkExStopTime.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkStopOnLow.Checked = (i == 1); }

                wr.Close();
                fs.Close();
            }
            catch (Exception /*e*/) { }

            UpdateUnitLabels();

            // now allow to save setting on combo change
            DoNotSaveSettingsFlag = false;
        }

        // close GPS and files on form close
        private void Form1_Closed(object sender, System.EventArgs e)
        {
            LockGpsTick = true;
            timerGps.Enabled = false;

            if (gps.Opened)
            { gps.Close(); }

            // Stop button enabled - indivate that we need to close streams
            if (buttonStop.Enabled)
            {
                try{
                writer.Close();
                fstream.Close();

                // copy file into "permanent place"
                File.Copy(TempFileName, GenerateFileName(), true);
                } catch (Exception /*e*/) { }
            }
        }

        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (buttonStop.Enabled) // means applicaion is still running
            {
                if (MessageBox.Show("If you exit, all data will be lost. Do you want to exit?", "GPS is logging!",
                   MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.No)
                {
                    // Cancel the Closing event from closing the form.
                    e.Cancel = true;
                }
            }
        }


        private void SaveSettings()
        {
            // save settings -----------------
            string CurrentDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            string file_name = "GpsCycleComputer.dat";
            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { file_name = CurrentDirectory + "\\" + file_name; }

            try {
            FileStream fs = new FileStream(file_name, FileMode.Create);
            BinaryWriter wr = new BinaryWriter(fs);

            wr.Write((int)comboGpsPoll.SelectedIndex);
            wr.Write((int)comboUnits.SelectedIndex);
            wr.Write((int)0); // not used anymore
            wr.Write((int)(checkExStopTime.Checked ? 1 : 0));
            wr.Write((int)(checkStopOnLow.Checked ? 1 : 0));

            wr.Close();
            fs.Close();
            } catch (Exception /*e*/) { }
        }

        // main logging function to receive date from GPS
        private void GetGpsData()
        {
            GpsDataOk = false;

            if (gps.Opened)
            {
                position = gps.GetPosition();

                UpdateGpsSearchFlags();

                if (position != null)
                {
                    bool time_check_passed = false;

                    if (position.LatitudeValid && position.LongitudeValid
                        && position.TimeValid && position.SpeedValid)
                    {
                        // if blank - set to curently returned time
                        if (LastPointUtc == DateTime.MinValue)
                        {
                            LastPointUtc = position.Time;
                        }
                        else if ((LastPointUtc != DateTime.MinValue) && (LastPointUtc < position.Time))
                        {
                            // OK, time is increasing- need more checks for first sample
                            if (Counter == 0)
                            {
                                // wait first few samples to get a "better grip" !
                                if (FirstSampleValidCount > 3) { time_check_passed = true; }

                                FirstSampleValidCount++;
                            }
                            else
                            {
                                time_check_passed = true;
                            }

                            // save last time
                            LastPointUtc = position.Time;
                        }
                    }

                    // passed the time check
                    if (time_check_passed)
                    {
                        // save and write starting position
                        if (Counter == 0)
                        {
                            StartLat = position.Latitude;
                            StartLong = position.Longitude;
                            StartTime = DateTime.Now;
                            StartTimeUtc = DateTime.UtcNow;

                            try
                            {
                                WriteStartDateTime();
                                writer.Write((double)position.Latitude);
                                writer.Write((double)position.Longitude);
                            }
                            catch (Exception /*e*/) { }

                            utmUtil.setReferencePoint(position.Latitude, position.Longitude);

                            // update display
                            labelStartPos.Text = "lat/long: " +
                                position.Latitude.ToString("0.000000") +
                                " / " + position.Longitude.ToString("0.000000");

                            labelStartTime.Text = "start: " + StartTime.ToString();
                            labelStartB.Text = BatteryString(Utils.GetBatteryStatus());

                            WriteOptionsInfo();
                        }
                        // write battery info every 3 min
                        WriteBatteryInfo();

                        WriteRecord(position);

                        Counter++;
                        GpsDataOk = true;
                        GpsSearchCount = 0;
                    }
                }
            }
            // if not opened
            else
            {
                labelStatusGps.Text = "no data available";
            }

            // indication that value was not set ...
            if (!GpsDataOk)
            {
                if (!StoppedOnLow) { labelStatus.Text = "gps searching ... " + GpsSearchCount; }
                GpsSearchCount++;
                labelGpsLed.BackColor = Color.Red;
            }
            else
            {
                if (!StoppedOnLow) { labelStatus.Text = "gps on"; }
                labelGpsLed.BackColor = Color.Green;
            }
        }

        // Write record. Position must be valid
        private void WriteRecord(GpsPosition pos)
        {
            double x;
            double y;
            utmUtil.getXY(pos.Latitude, pos.Longitude, out x, out y);

            // compute distance
            double deltax = x - OldX;
            double deltay = y - OldY;
            Distance += Math.Sqrt(deltax * deltax + deltay * deltay);
            OldX = x; OldY = y;

            int plot_data_x = (int)x;
            int plot_data_y = (int)y;

            // shift to origin
            x -= OriginShiftX;
            y -= OriginShiftY;

            // check if an origin update is required
            while ((Math.Abs(x) > 30000.0) || (Math.Abs(y) > 30000.0))
            {
                Int16 deltaX = 0;
                if (x > 30000.0) { x -= 30000.0; deltaX = 30000; }
                else if (x < -30000.0) { x += 30000.0; deltaX = -30000; }

                Int16 deltaY = 0;
                if (y > 30000.0) { y -= 30000.0; deltaY = 30000; }
                else if (y < -30000.0) { y += 30000.0; deltaY = -30000; }

                // Yes, need an origin shift record
                if ((deltaX != 0) || (deltaY != 0))
                {
                    OriginShiftX += deltaX;
                    OriginShiftY += deltaY;

                    try {
                    writer.Write((Int16)deltaX);
                    writer.Write((Int16)deltaY);
                    writer.Write((Int16)0);         // this is origin update (0)
                    writer.Write((UInt16)0xFFFF);   // status record (0xFFFF/0xFFFF)
                    writer.Write((UInt16)0xFFFF);
                    } catch (Exception /*e*/) { }
                }
            }

            // proceed with "normal" record
            try {
            Int16 x_int = (Int16)x; writer.Write((Int16)x_int);
            Int16 y_int = (Int16)y; writer.Write((Int16)y_int);

            Int16 z_int = 0;
            if (position.EllipsoidAltitudeValid)
            { z_int = (Int16)(pos.EllipsoidAltitude); }
            writer.Write((Int16)z_int);

            // speed in in kmh*10 (as UInt16) - converted from knots (see top of this file)
            UInt16 v_int = (UInt16)(pos.Speed * 1.852 * 10.0); writer.Write((UInt16)v_int);

            TimeSpan run_time = DateTime.UtcNow - StartTimeUtc;

            // Safety check 1: make sure elapsed time is not negative
            double double_total_sec = run_time.TotalSeconds;
            if (double_total_sec < 0.0) double_total_sec = 0.0;

            // Safety check 2: make sure new time is increasing
            if ((int)double_total_sec < OldT) { OldT = (int)double_total_sec; }

            // OK, write
            UInt16 t_int = (UInt16)double_total_sec;
            writer.Write((UInt16)t_int);

            // compute Stoppage time
            if (v_int == 0) { CurrentStoppageTimeSec += t_int - OldT; }
            OldT = t_int;

            // Update max speed (in kmh)
            if (v_int / 10.0 > MaxSpeed) { MaxSpeed = v_int / 10.0; }

            // update current vars
            CurrentTimeSec = t_int;
            CurrentX = OriginShiftX + x_int;
            CurrentY = OriginShiftY + y_int;
            CurrentZ = z_int;
            CurrentSpeed = v_int / 10.0;

            // store data
            AddPlotData(plot_data_x, plot_data_y, z_int, t_int);

            if (FlushStream)
            {
                fstream.Flush();
                FlushStream = false;
            }

            } catch (Exception /*e*/) { }

            UpdateDisplay();
        }

        private void AddPlotData(int x, int y, Int16 z, UInt16 t)
        {
            // check if we need to increase decimation level
            if(Counter >= PlotDataSize*Decimation)
            {
                for(int i = 0; i < PlotDataSize/2; i++)
                {
                    PlotX[i] = PlotX[i*2];
                    PlotY[i] = PlotY[i*2];
                    PlotZ[i] = PlotZ[i*2];
                    PlotT[i] = PlotT[i*2];
                }
                Decimation *= 2;
            }

            PlotX[Counter/Decimation] = x;
            PlotY[Counter/Decimation] = y;
            PlotZ[Counter/Decimation] = z;
            PlotT[Counter/Decimation] = t;
        }

        // Write starting date/time to the new file
        private void WriteStartDateTime()
        {
            Byte x;
            try {
                x = (Byte)(StartTime.Year - 2000); writer.Write((Byte)x);
                x = (Byte)StartTime.Month; writer.Write((Byte)x);
                x = (Byte)StartTime.Day; writer.Write((Byte)x);
                x = (Byte)StartTime.Hour; writer.Write((Byte)x);
                x = (Byte)StartTime.Minute; writer.Write((Byte)x);
                x = (Byte)StartTime.Second; writer.Write((Byte)x);
            }  catch (Exception /*e*/) { }
        }

        // write battery info
        private string BatteryString(int x)
        {
            if (x == -1) { return "battery AC"; }
            else if (x < 0) { return "battery ??%"; }
            else { return ("battery " + x.ToString() + "%"); }
        }
        private void WriteBatteryInfo()
        {
            if (Counter != 0)
            {
                TimeSpan maxAge = new TimeSpan(0, 3, 0); // 3 min
                if ((LastBatterySave + maxAge) >= DateTime.UtcNow)
                { return; }
            }

            LastBatterySave = DateTime.UtcNow;

            Int16 x = (Int16)Utils.GetBatteryStatus();

            labelStatusB.Text = BatteryString(x);

            try {
            writer.Write((Int16)x);
            writer.Write((Int16)0);
            writer.Write((Int16)1);         // this is battery status record (1)
            writer.Write((UInt16)0xFFFF);   // status record (0xFFFF/0xFFFF)
            writer.Write((UInt16)0xFFFF);
            } catch (Exception /*e*/) { }

            // terminate if low power
            if (x > 0)
            {
                if (checkStopOnLow.Checked && (x < 20))
                {
                    LockGpsTick = true;
                    timerGps.Enabled = false;
                    timerIdleReset.Enabled = false;
                    if (gps.Opened) { gps.Close(); }
                    StoppedOnLow = true;
                    labelStatus.Text = "Stopped on low power";
                }
            }
        }

        private void WriteOptionsInfo()
        {
            try
            {
                writer.Write((Int16)PollGpsTimeSec[comboGpsPoll.SelectedIndex]);
                writer.Write((Int16)1);
                writer.Write((Int16)2);         // this is options record
                writer.Write((UInt16)0xFFFF);   // status record (0xFFFF/0xFFFF)
                writer.Write((UInt16)0xFFFF);
            }
            catch (Exception /*e*/) { }
        }

        // generate a new file name using StartTime
        private string GenerateFileName()
        {
            string str = "";
            string file_name = "";

            // file name is constructed as year,month,day, hour, min, sec, all as 2-digit values
            str = (StartTime.Year - 2000).ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str;

            str = StartTime.Month.ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str;

            str = StartTime.Day.ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str;

            str = StartTime.Hour.ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str;

            str = StartTime.Minute.ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str + ".gcc";

            string CurrentDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { file_name = CurrentDirectory + "\\" + file_name; }

            CurrentFileName = file_name;

            return file_name;
        }

        // New Trace: open file, log start time, etc
        private void StartNewTrace()
        {
            StartTime = DateTime.Now;
            StartTimeUtc = DateTime.UtcNow;
            LastBatterySave = StartTimeUtc;

            // create writer and write header
            try {
            fstream = new FileStream(TempFileName, FileMode.Create);
            writer = new BinaryWriter(fstream);

            writer.Write((char)'G'); writer.Write((char)'C'); writer.Write((char)'C'); writer.Write((Byte)1);
            } catch (Exception /*e*/) { }

            OriginShiftX = 0.0;
            OriginShiftY = 0.0;

            MaxSpeed = 0.0;
            Distance = 0.0;
            CurrentStoppageTimeSec = 0;
            OldX = 0.0;
            OldY = 0.0;
            OldT = 0;

            Counter = 0;
            Decimation = 1;
            FirstSampleValidCount = 0;

            LastPointUtc = DateTime.MinValue;
        }

        // Start/stop GPS
        private void buttonStart_Click()
        {
            buttonStop.Enabled = true;
            buttonStart.Enabled = false;

            comboGpsPoll.Enabled = false;
            comboUnits.Enabled = false;
            checkExStopTime.Enabled = false;
            checkStopOnLow.Enabled = false;
            buttonPicLoad.Enabled = false;
            buttonPicSaveKML.Enabled = false;
            buttonPicSaveGPX.Enabled = false;

            LockGpsTick = false;
            StoppedOnLow = false;

            GpsSearchCount = 0;
            labelStatus.Text = "gps off";
            labelStatus.ForeColor = fo;
            labelStatus.BackColor = bk;
            ClearDisplay();

            StartNewTrace();

            if (!gps.Opened)
            { gps.Open(); }

            timerGps.Interval = 1000;
            timerGps.Enabled = true;

            timerIdleReset.Enabled = true;
        }
        private void buttonStop_Click()
        {
            LockGpsTick = true;
            timerGps.Enabled = false;
            timerIdleReset.Enabled = false;
            buttonStop.Enabled = false;

            if (gps.Opened) { gps.Close(); }

            try {
            writer.Close();
            fstream.Close();
            } catch (Exception /*e*/) { }
            comboGpsPoll.Enabled = true;
            GpsSearchCount = 0;
            labelStatus.Text = "gps off";
            labelGpsLed.BackColor = bk;

            timerStartDelay.Enabled = true;
            Cursor.Current = Cursors.WaitCursor;
        }
        private void timerStartDelay_Tick(object sender, EventArgs e)
        {
            timerStartDelay.Enabled = false;

            buttonStart.Enabled = true;

            comboGpsPoll.Enabled = true;
            comboUnits.Enabled = true;
            checkExStopTime.Enabled = true;
            checkStopOnLow.Enabled = true;
            buttonPicLoad.Enabled = true;
            buttonPicSaveKML.Enabled = true;
            buttonPicSaveGPX.Enabled = true;

            // copy file into "permanent place", only if any records
            if (Counter > 0)
                { File.Copy(TempFileName, GenerateFileName(), true); }

            Cursor.Current = Cursors.Default;
        }

        // Switch Off backlight, set flag to flash stream
        private void buttonBklitOff_Click(object sender, EventArgs e)
        {
            Utils.SwitchBacklight();
            // AAZ DEBUG - do not flush at all (seems makes no difference (set to true if want to try)
            FlushStream = false;
        }

        // read file
        private void buttonLoad_Click(object sender, EventArgs e)
        {
            listBoxFiles.Items.Clear();

            string CurrentDirectory =
                  Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            string[] files = Directory.GetFiles(CurrentDirectory, "*.gcc");
            Array.Sort(files);

            foreach (string s in files)
            {
                listBoxFiles.Items.Add(Path.GetFileName(s));
            }

            tabOpenFile.BringToFront();

            if(listBoxFiles.Items.Count == 0) 
            {
                buttonDialogOpen.Enabled = false;
                return;
            }

            listBoxFiles.SelectedIndex = 0;
            buttonDialogOpen.Enabled = true;
        }

        private void LoadGcc(string filename)
        {
                // reset vars for computation
                Counter = 0;
                Decimation = 1;
                MaxSpeed = 0.0; Distance = 0.0; CurrentStoppageTimeSec = 0;
                OldX = 0.0; OldY = 0.0; OldT = 0;
                OriginShiftX = 0.0; OriginShiftY = 0.0;

                int gps_poll_sec = 0;

                CurrentFileName = filename;

                ClearDisplay();

                // preset label text for errors
                labelStatusGps.Text = "";
                labelStatus.Text = "Reading errors or blank file";
                labelFileName.Text = "Reading errors or blank file";

                Cursor.Current = Cursors.WaitCursor;

                do
                {
                    try
                    {
                        FileStream fs = new FileStream(filename, FileMode.Open);
                        BinaryReader rd = new BinaryReader(fs);

                        // load header "GCC1" (1 is version)
                        if (rd.ReadChar() != 'G') break; if (rd.ReadChar() != 'C') break;
                        if (rd.ReadChar() != 'C') break; if (rd.ReadChar() != 1) break;

                        // read time as 6 bytes: year, month...
                        int t1 = (int)rd.ReadByte(); t1 += 2000;
                        int t2 = (int)rd.ReadByte(); int t3 = (int)rd.ReadByte();
                        int t4 = (int)rd.ReadByte(); int t5 = (int)rd.ReadByte(); 
                        int t6 = (int)rd.ReadByte();
                        StartTime = new DateTime(t1, t2, t3, t4, t5, t6);
                        labelStartTime.Text = "start: " + StartTime.ToString();

                        // read lat/long
                        StartLat = rd.ReadDouble(); StartLong = rd.ReadDouble();
                        labelStartPos.Text = "lat/long: " + StartLat.ToString("0.000000") +
                                    " / " + StartLong.ToString("0.000000");

                        bool is_battery_printed = false;

                        Int16 x_int = 0; Int16 y_int = 0; Int16 z_int = 0; UInt16 v_int = 0; UInt16 t_int = 0;

                        while (rd.PeekChar() != -1)
                        {
                            // get 5 short ints
                            x_int = rd.ReadInt16();
                            y_int = rd.ReadInt16();
                            z_int = rd.ReadInt16();
                            v_int = rd.ReadUInt16();
                            t_int = rd.ReadUInt16();

                            // check if this is a special record
                            // battery: z_int = 1
                            if ((v_int == 0xFFFF) && (t_int == 0xFFFF) && (z_int == 1)) 
                            {
                                if (is_battery_printed == false)
                                {
                                    labelStartB.Text = BatteryString(x_int);
                                    is_battery_printed = true;
                                }
                                labelStatusB.Text = BatteryString(x_int);
                            }
                            // origin shift: z_int = 0
                            else if ((v_int == 0xFFFF) && (t_int == 0xFFFF) && (z_int == 0))
                            {
                                OriginShiftX += x_int;
                                OriginShiftY += y_int;
                            }
                            // which GPS options were selected
                            else if ((v_int == 0xFFFF) && (t_int == 0xFFFF) && (z_int == 2))
                            {
                                gps_poll_sec = x_int;
                            }
                            // "normal" record
                            else
                            {
                                // compute distance
                                double real_x = OriginShiftX + x_int;
                                double real_y = OriginShiftY + y_int;

                                double deltax = real_x - OldX;
                                double deltay = real_y - OldY;
                                Distance += Math.Sqrt(deltax * deltax + deltay * deltay);
                                OldX = real_x; OldY = real_y;

                                // compute Stoppage time
                                if (v_int == 0) { CurrentStoppageTimeSec += t_int - OldT; }
                                OldT = t_int;

                                // update max speed
                                if (v_int / 10.0 > MaxSpeed) { MaxSpeed = v_int / 10.0; }

                                // store data in plot array (use for KML as well)
                                AddPlotData((int)real_x, (int)real_y, z_int, t_int);
                                Counter++;
                            }
                        }

                        // update current vars and print
                        CurrentTimeSec = t_int;
                        CurrentX = OriginShiftX + x_int;
                        CurrentY = OriginShiftY + y_int;
                        CurrentZ = z_int;
                        CurrentSpeed = v_int / 10.0;

                        UpdateDisplay();

                        rd.Close();
                        fs.Close();

                        // if all is file, set label to the file name + status
                        string options = (gps_poll_sec != 0 ? 
                                          ", gps run every " + gps_poll_sec.ToString() + " sec" : 
                                          ", gps always active");

                        labelStatus.Text = Path.GetFileName(filename);
                        labelFileName.Text = "Loaded " + Path.GetFileName(filename) + options;
                    }
                    catch (Exception /*e*/) { }
                }  while (false);
                Cursor.Current = Cursors.Default;

        }

        // update data as travel distance, time, etc. 
        // Some vars like Distance and MaxSpeed are global, so not passed as args
        private string PrintDist(double x)
        {
            if (x > 100.0) { return x.ToString("0.#"); }
            return x.ToString("0.0#");
        }
        private string PrintSpeed(double x)
        {
            if (x > 100.0) { return x.ToString("0."); }
            return x.ToString("0.0");
        }
        private void ClearDisplay()
        {
            labelDist.Text = "0.0";
            labelRunTime.Text = "0:00:00";
            labelSpeed.Text = "0.0 / 0.0 / 0.0";
            labelPos.Text = "0.0 0.0  0";
            labelStartTime.Text = "";
            labelStartPos.Text = "";
            labelCurrentTime.Text = "";
            labelStatusB.Text = "";
            labelStartB.Text = "";
            labelFileName.Text = "";
        }
        private void UpdateDisplay()
        {
            double ceff = 1.0;
            if( comboUnits.SelectedIndex == 0 ) { ceff = 1.0 / 1.609344; }   // miles
            else if( comboUnits.SelectedIndex == 1 ) { ceff = 1.0; }         // km
            else if( comboUnits.SelectedIndex == 2 ) { ceff = 1.0 / 1.852; } // naut miles
            
            int time_to_use = (checkExStopTime.Checked ? CurrentTimeSec - CurrentStoppageTimeSec : CurrentTimeSec);

            TimeSpan ts = new TimeSpan(0, 0, time_to_use);
            labelRunTime.Text = ts.ToString();

            TimeSpan ts_all = new TimeSpan(0, 0, CurrentTimeSec);
            labelCurrentTime.Text = "last sample : " + (StartTime + ts_all).ToString("T");

            labelDist.Text = PrintDist(Distance * 0.001 * ceff);
            labelPos.Text = PrintDist(CurrentX * ceff / 1000.0) + " " +
                            PrintDist(CurrentY * ceff / 1000.0) + "    " + CurrentZ.ToString();

            double averageSpeed = (time_to_use == 0 ? CurrentSpeed : Distance * 0.001 / (time_to_use / 3600.0));
            labelSpeed.Text = PrintSpeed(CurrentSpeed * ceff) + " / " +
                              PrintSpeed(averageSpeed * ceff) + " / " +
                              PrintSpeed(MaxSpeed * ceff);

        }
        private void UpdateUnitLabels()
        {
            if (comboUnits.SelectedIndex == 0)
            {
                labelDistUnit.Text = "miles";
                labelPosUnit.Text = "x / y (miles)   z (m)";
                labelSpeedUnit.Text = "current / average / max (mph)";
            }
            else if (comboUnits.SelectedIndex == 1)
            {
                labelDistUnit.Text = "km";
                labelPosUnit.Text = "x / y (km)   z (m)";
                labelSpeedUnit.Text = "current / average / max (km/h)";
            }
            else if (comboUnits.SelectedIndex == 2)
            {
                labelDistUnit.Text = "naut miles";
                labelPosUnit.Text = "x / y (naut miles)   z (m)";
                labelSpeedUnit.Text = "current / average / max (knots)";
            }

            if (checkExStopTime.Checked) { labelExStops.Text = "ex stop"; }
            else { labelExStops.Text = "inc stop"; }
        }

        private void UpdateGpsSearchFlags()
        {
            if(position == null)
                { labelStatusGps.Text = "no data available"; return; }

            string str = "";
            if (position.SatellitesInViewCountValid)
            { str += "S" + position.SatellitesInViewCount.ToString() + " Snr" + position.GetMaxSNR().ToString(); }
            else
            { str += "S0 Snr-"; }

            if (position.TimeValid)
            {
                TimeSpan age = DateTime.UtcNow - position.Time;
                int total_sec = (int)age.TotalSeconds;
                if (total_sec > 99) total_sec = 99;
                str += " T" + total_sec.ToString();
            }
            else
            { str += " T-"; }

            if (position.HorizontalDilutionOfPrecisionValid)
            {
                double x = position.HorizontalDilutionOfPrecision;
                if (x > 50) { x = 50; }
                str += " Dh" + x.ToString("#0");
            }
            else
            { str += " Dh-"; }

            labelStatusGps.Text = str;
        }

        // reset Idle Timer (to stop phone switching off)
        private void timerIdleReset_Tick(object sender, EventArgs e)
        {
            SystemIdleTimerReset();
        }

        // start/stop GPS
        private void timerGps_Tick(object sender, EventArgs e)
        {
            if (LockGpsTick) { return; }

            // set a lock for this function (just in case it got stack in GPS calls)
            LockGpsTick = true;

            // permament GPS operation
            if (comboGpsPoll.SelectedIndex == 0)
                { GetGpsData(); LockGpsTick = false; return; }

            // start/stop GPS operation
            if (gps.Opened)
            {
                GetGpsData();

                // if data was not valid, need to wait before disabling GPS
                if (GpsDataOk == false) { LockGpsTick = false; return; }

                // set "long" interval
                timerGps.Interval = 1000 * PollGpsTimeSec[comboGpsPoll.SelectedIndex];

                gps.Close();
                if (!StoppedOnLow) { labelStatus.Text = "gps off for " + PollGpsTimeSec[comboGpsPoll.SelectedIndex] + " sec"; }

                LastPointUtc = DateTime.MinValue;
            }
            else
            {
                gps.Open();
                if (!StoppedOnLow) { labelStatus.Text = "gps opening ..."; }

                // set "short" interval
                timerGps.Interval = 1000;
            }

            LockGpsTick = false;
        }

        #region PInvokes to coredll.dll
        [DllImport("coredll.dll")]
        static extern void SystemIdleTimerReset();
        #endregion


        private string replaceCommas(double x)
        {
            string output = x.ToString("0.######");
            output = output.Replace(",", ".");
            return output;
        }

        private void buttonSaveKML_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                string kml_file = Path.GetFileNameWithoutExtension(CurrentFileName) + ".kml";

                string CurrentDirectory = Path.GetDirectoryName(CurrentFileName);
                if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
                if (CurrentDirectory != "")
                { kml_file = CurrentDirectory + "\\" + kml_file; }

                FileStream fs = new FileStream(kml_file, FileMode.Create);
                StreamWriter wr = new StreamWriter(fs);

                // write KML header
                wr.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                wr.WriteLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
                wr.WriteLine("  <Placemark>");
                wr.WriteLine("    <name>" + StartTime.ToString() + "</name>");

                wr.WriteLine("    <Style id=\"yellowLineGreenPoly\">");
                wr.WriteLine("      <LineStyle>");
                wr.WriteLine("        <color>7f00ffff</color>");
                wr.WriteLine("        <width>4</width>");
                wr.WriteLine("      </LineStyle>");
                wr.WriteLine("      <PolyStyle>");
                wr.WriteLine("        <color>7f00ff00</color>");
                wr.WriteLine("      </PolyStyle>");
                wr.WriteLine("    </Style>");

                wr.WriteLine("      <description>");

                // write description for this trip
                wr.WriteLine(labelDist.Text + " " + labelDistUnit.Text + " " + labelRunTime.Text + " " + labelExStops.Text);
                wr.WriteLine(labelSpeed.Text);
                wr.WriteLine(labelSpeedUnit.Text);
                wr.WriteLine(labelStartB.Text + " ... " + labelStatusB.Text);

                wr.WriteLine("	</description>");
                wr.WriteLine("      <styleUrl>#yellowLineGreenPoly</styleUrl>");

                wr.WriteLine("	    <LookAt>");
                wr.WriteLine("			<longitude>" + replaceCommas(StartLong) + "</longitude>");
                wr.WriteLine("			<latitude>" + replaceCommas(StartLat) + "</latitude>");
                wr.WriteLine("			<altitude>0</altitude>");
                wr.WriteLine("			<range>3000</range>");
                wr.WriteLine("			<tilt>0</tilt>");
                wr.WriteLine("			<heading>0</heading>");
                wr.WriteLine("		</LookAt>");

                wr.WriteLine("      <LineString>");
                wr.WriteLine("        <coordinates>");

                // convert x/y into lat/long and write into KML
                utmUtil.setReferencePoint(StartLat, StartLong);
                // here write coordinates
                for (int i = 0; i < Counter/Decimation; i++)
                {
                    double out_lat;
                    double out_long;
                    utmUtil.getLatLong((double)PlotX[i], (double)PlotY[i], out out_lat, out out_long);
                    wr.WriteLine(replaceCommas(out_long) + "," + replaceCommas(out_lat));
                }

                // write end of the KML file
                wr.WriteLine("        </coordinates>");
                wr.WriteLine("      </LineString>");
                wr.WriteLine("    </Placemark>");
                wr.WriteLine("</kml>");
                wr.Close();
                fs.Close();

            } catch (Exception /*e*/) { }
            Cursor.Current = Cursors.Default;
            labelFileName.Text = ">>> " + Path.GetFileName(CurrentFileName) + " saved to .kml";
        }
        private void buttonSaveGPX_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                string gpx_file = Path.GetFileNameWithoutExtension(CurrentFileName) + ".gpx";

                string CurrentDirectory = Path.GetDirectoryName(CurrentFileName);
                if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
                if (CurrentDirectory != "")
                { gpx_file = CurrentDirectory + "\\" + gpx_file; }

                FileStream fs = new FileStream(gpx_file, FileMode.Create);
                StreamWriter wr = new StreamWriter(fs);

                // write GPX header
                wr.WriteLine("<?xml version=\"1.0\"?>");
                wr.WriteLine("<gpx");
                wr.WriteLine("version=\"1.0\"");
                wr.WriteLine(" creator=\"GPSCycleComputer\"");
                wr.WriteLine(" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
                wr.WriteLine(" xmlns=\"http://www.topografix.com/GPX/1/0\"");
                wr.WriteLine(" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/0 http://www.topografix.com/GPX/1/0/gpx.xsd\">");
                wr.WriteLine("");
                wr.WriteLine("<trk>");
                wr.WriteLine("<name>" + StartTime.ToString() + "</name>");

                wr.WriteLine("<desc><![CDATA[" + labelDist.Text + " " + labelDistUnit.Text + " " + labelRunTime.Text + " " + labelExStops.Text
                               + " " + labelSpeed.Text
                               + " " + labelSpeedUnit.Text
                               + " " + labelStartB.Text + " ... " + labelStatusB.Text
                               + "]]></desc>");

                wr.WriteLine("<trkseg>");

                // convert x/y into lat/long and write into KML
                utmUtil.setReferencePoint(StartLat, StartLong);
                // here write coordinates
                for (int i = 0; i < Counter/Decimation; i++)
                {
                    double out_lat;
                    double out_long;
                    utmUtil.getLatLong((double)PlotX[i], (double)PlotY[i], out out_lat, out out_long);
                    wr.WriteLine("<trkpt lat=\"" + replaceCommas(out_lat) +
                                 "\" lon=\"" + replaceCommas(out_long) + "\">");
                    wr.WriteLine("<ele>" + PlotZ[i].ToString() + "</ele>");

                    TimeSpan run_time = new TimeSpan(0, 0, PlotT[i]);
                    string run_time_str = (StartTime + run_time).ToString("u");
                    run_time_str = run_time_str.Replace(" ", "T");
                    wr.WriteLine("<time>" + run_time_str + "</time>");

                    wr.WriteLine("</trkpt>");
                }
                // write end of the GPX file
                wr.WriteLine("</trkseg>");
                wr.WriteLine("</trk>");
                wr.WriteLine("</gpx>");
                wr.Close();
                fs.Close();

            }
            catch (Exception /*e*/) { }
            Cursor.Current = Cursors.Default;
            labelFileName.Text = ">>> " + Path.GetFileName(CurrentFileName) + " saved to .gpx";
        }

        private void comboGpsPoll_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DoNotSaveSettingsFlag) return;
            SaveSettings();
        }

        private void checkExStopTime_Click(object sender, EventArgs e)
        {
            UpdateUnitLabels();

            if (DoNotSaveSettingsFlag) return;
            SaveSettings();
            UpdateDisplay();
        }

        // paint graph ------------------------------------------------------
        private int ScreenX = 100;     // this is screen size in pixels (updated as required)
        private int ScreenY = 100;
        private int ScreenMargin = 5;  // Margin in pixels from the screen edge
        private double Data2Screen = 1.0;  // Coefficient to convert from data to screen values
        private double DataXmin = 1.0E9, DataXmax = -1.0E9, DataYmin = 1.0E9, DataYmax = -1.0E9;

        private void DrawPoint(Graphics g, Pen p, double x, double y, int size)
        {
            int x_point = ScreenMargin + (int)((x - DataXmin) * Data2Screen);
            int y_point = ScreenMargin - (int)((y - DataYmax) * Data2Screen);

            int delta = size / 2;
            for (int i = -delta; i <= delta; i++)
            {
                g.DrawLine(p, x_point - delta, y_point + i, x_point + delta, y_point + i);
            }
        }
        private void DrawGridLineX(Graphics g, Pen p, double x)
        {
            int x_point = ScreenMargin + (int)((x - DataXmin) * Data2Screen);
            if ((x_point >= 0) && (x_point <= ScreenX - 1))
            { g.DrawLine(p, x_point, 0, x_point, ScreenY - 1); }
        }
        private void DrawGridLineY(Graphics g, Pen p, double y)
        {
            int y_point = ScreenMargin - (int)((y - DataYmax) * Data2Screen);
            if ((y_point >= 0) && (y_point <= ScreenY - 1))
            { g.DrawLine(p, 0, y_point, ScreenX - 1, y_point); }
        }
        private void DrawTickLabel(Graphics g, Pen p, double tick_dist)
        {
            // locate the place for the label
            for (int i = -100; i < 100; i++)
            {
                double x1 = tick_dist * i;
                double x2 = tick_dist * (i + 1);
                int x_point1 = ScreenMargin + (int)((x1 - DataXmin) * Data2Screen);
                int x_point2 = ScreenMargin + (int)((x2 - DataXmin) * Data2Screen);

                // here is the first point withing the screen - print
                if ((x_point1 >= 0) && (x_point1 <= ScreenX - 1))
                {
                    int y_point = ScreenY - ScreenMargin;
                    g.DrawLine(p, x_point1, y_point + 1, x_point2, y_point + 1);
                    g.DrawLine(p, x_point1, y_point + 2, x_point2, y_point + 2);
                    g.DrawLine(p, x_point1, y_point, x_point2, y_point);
                    g.DrawLine(p, x_point1, y_point - 1, x_point2, y_point - 1);
                    g.DrawLine(p, x_point1, y_point - 2, x_point2, y_point - 2);

                    g.DrawLine(p, x_point1, y_point - 5, x_point1, y_point + 5);
                    g.DrawLine(p, x_point2, y_point - 5, x_point2, y_point + 5);

                    // draw text: Create font and brush.
                    Font drawFont = new Font("Arial", 8, FontStyle.Regular);
                    SolidBrush drawBrush = new SolidBrush(Color.FromArgb(0x00000000));

                    string text = tick_dist.ToString();
                    if (comboUnits.SelectedIndex == 0) { text += " miles"; }
                    else if (comboUnits.SelectedIndex == 1) { text += " km"; }
                    else if (comboUnits.SelectedIndex == 2) { text += " naut miles"; }

                    g.DrawString(text, drawFont, drawBrush, x_point1 + 2, y_point - 30);
                    return;
                }
            }
        }
        private double TickMark(double x, int nx)
        {
            double num, mult, mant;
            int inum;

            if (nx == 0) nx = 1;
            num = Math.Log10(Math.Abs((double)(x / nx)));
            inum = (int) Math.Floor(num);
            mult = Math.Pow(10.0, inum);
            mant = Math.Pow(10.0, num - inum);
            if (mant > 7.5) mant = 10.0;
            else if (mant > 3.5) mant = 5.0;
            else if (mant > 1.5) mant = 2.0;
            else mant = 1.0;
            return (mant * mult);
        }

        private void tabPage3_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(SystemColors.Window);

            if(Counter == 0) { return; }

            ScreenX = tabGraph.Width - ScreenMargin*2;
            ScreenY = tabGraph.Height - ScreenMargin * 2;

            // conversion from metres int km or miles for plot
            double c = 0.001;
            if (comboUnits.SelectedIndex == 0) { c = 0.001 / 1.609344; }   // miles
            else if (comboUnits.SelectedIndex == 1) { c = 0.001; }         // km
            else if (comboUnits.SelectedIndex == 2) { c = 0.001 / 1.852; } // naut miles

            // compute data limits
            DataXmin = 1.0E9; DataXmax = -1.0E9; DataYmin = 1.0E9; DataYmax = -1.0E9;
            for (int i = 0; i < Counter/Decimation; i++)
            {
                if (PlotX[i] * c < DataXmin) { DataXmin = PlotX[i] * c; }
                if (PlotX[i] * c > DataXmax) { DataXmax = PlotX[i] * c; }
                if (PlotY[i] * c < DataYmin) { DataYmin = PlotY[i] * c; }
                if (PlotY[i] * c > DataYmax) { DataYmax = PlotY[i] * c; }
            }

            // set plot scale, must be equal for both axis to plot map
            double xsize = DataXmax - DataXmin;
            double ysize = DataYmax - DataYmin;
            if(xsize == 0.0) { xsize = 1.0; } // check that size not 0
            if(ysize == 0.0) { ysize = 1.0; }
            double xscale = (double)ScreenX / xsize;
            double yscale = (double)ScreenY / ysize;
            Data2Screen = (yscale > xscale ? xscale : yscale);

            Pen pen = new Pen(Color.LightGray, 1);

            // draw tickmarks : target 3 ticks
            double tick_distance = (ScreenY < ScreenX ? TickMark(ScreenY / Data2Screen, 4) : TickMark(ScreenX / Data2Screen, 4));
            pen.Color = Color.LightGray;
            for (int i = -100; i < 100; i++)
            {
                DrawGridLineX(g, pen, i * tick_distance);
                DrawGridLineY(g, pen, i * tick_distance);
            }

            // draw data
            pen.Color = Color.Blue;

            // reduce further size of the points to max 512
            const int max_plot_size = 512;
            int decimation = 1 + (int)(Counter / Decimation / max_plot_size);

            for (int i = 1; i < Counter/Decimation - 1; i += decimation)
            { DrawPoint(g, pen, PlotX[i] * c, PlotY[i] * c, 5); }

            // draw start/stop point - green and red
            pen.Color = Color.Green;
            DrawPoint(g, pen, PlotX[0] * c, PlotY[0] * c, 9);
            pen.Color = Color.Red;
            DrawPoint(g, pen, PlotX[Counter/Decimation - 1] * c, PlotY[Counter/Decimation - 1] * c, 9);

            // draw tick label
            pen.Color = Color.Black;
            DrawTickLabel(g, pen, tick_distance);

        }

        private void tabAbout_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(AboutTabImage, 0, 0);
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if ((sender == buttonMain) && buttonMain.pressed) return;
            if ((sender == buttonGraph) && buttonGraph.pressed) return;
            if ((sender == buttonOptions) && buttonOptions.pressed) return;
            if ((sender == buttonAbout) && buttonAbout.pressed) return;

            if (sender == buttonMain)
            {
                buttonMain.pressed = true;
                buttonGraph.pressed = false;
                buttonOptions.pressed = false;
                buttonAbout.pressed = false;
            }
            else if (sender == buttonGraph)
            {
                buttonMain.pressed = false;
                buttonGraph.pressed = true;
                buttonOptions.pressed = false;
                buttonAbout.pressed = false;
            }
            else if (sender == buttonOptions)
            {
                buttonMain.pressed = false;
                buttonGraph.pressed = false;
                buttonOptions.pressed = true;
                buttonAbout.pressed = false;
            }
            else if (sender == buttonAbout)
            {
                buttonMain.pressed = false;
                buttonGraph.pressed = false;
                buttonOptions.pressed = false;
                buttonAbout.pressed = true;
            }
            buttonMain.Invalidate();
            buttonGraph.Invalidate();
            buttonOptions.Invalidate();
            buttonAbout.Invalidate();
        }
        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            if (sender == buttonMain) { tabMain.BringToFront(); }
            else if (sender == buttonGraph) { tabGraph.BringToFront(); }
            else if (sender == buttonOptions) { tabOptions.BringToFront(); }
            else if (sender == buttonAbout) { tabAbout.BringToFront(); }
        }

        private void Form1_MouseDownS(object sender, MouseEventArgs e)
        {
            if ((sender == buttonStart) && buttonStart.pressed) return;
            if ((sender == buttonStop) && buttonStop.pressed) return;

            if (sender == buttonStart)
            {
                buttonStart.pressed = true;
                buttonStop.pressed = false;
            }
            else if (sender == buttonStop)
            {
                buttonStart.pressed = false;
                buttonStop.pressed = true;
            }
            buttonStart.Invalidate();
            buttonStop.Invalidate();
        }
        private void Form1_MouseUpS(object sender, MouseEventArgs e)
        {
            if (sender == buttonStart) { buttonStart_Click(); }
            else if (sender == buttonStop) { buttonStop_Click(); }
        }

        // Open dialog buttons
        private void buttonDialogOpen_Click(object sender, EventArgs e)
        {
            string gcc_file = listBoxFiles.SelectedItem.ToString();

            string CurrentDirectory =
                  Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { gcc_file = CurrentDirectory + "\\" + gcc_file; }

            tabOpenFile.SendToBack();

            LoadGcc(gcc_file);
        }
        private void buttonDialogCancel_Click(object sender, EventArgs e)
        {
            tabOpenFile.SendToBack();
        }
    }

    // button-like control that has a background image.
    public class PictureButton : Control
    {
        Bitmap backgroundImage, pressedImage;
        bool pressed = false;

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

        // Override the OnPaint method to draw the background image and the text.
        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.pressed && this.pressedImage != null)
                e.Graphics.DrawImage(this.pressedImage, 0, 0);
            else
                e.Graphics.DrawImage(this.backgroundImage, 0, 0);

            // Optinonal black line
//            e.Graphics.DrawRectangle(new Pen(Color.Black), 0, 0,
//                this.ClientSize.Width - 1, this.ClientSize.Height - 1);

            base.OnPaint(e);
        }
    }

    // button-like control that has a background image.
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
            if (this.pressed && this.pressedImage != null)
                e.Graphics.DrawImage(this.pressedImage, 0, 0);
            else
                e.Graphics.DrawImage(this.backgroundImage, 0, 0);

            // Optinonal black line
//            e.Graphics.DrawRectangle(new Pen(Color.Black), 0, 0,
//                this.ClientSize.Width - 1, this.ClientSize.Height - 1);

            base.OnPaint(e);
        }
    }
}
