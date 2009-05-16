using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections;
using System.Windows.Forms;
using System.Data;
using System.IO;
using System.Text;
using System.Reflection;
using Microsoft.Win32;
using GpsUtils;
using LiveTracker;

/* From WIKI:
 1 international knot = 
 1 nautical mile per hour (exactly), 
 1.852 kilometres per hour (exactly),[5] 

 1 mile  = 1.609344 kilometers
 1 foot =.30480 of a meter 
  
 http://utrack.crempa.net/ - for GPX plots
*/

namespace GpsCycleComputer
{
    public class Form1 : System.Windows.Forms.Form
    {
        Gps gps = new Gps();
        GpsPosition position = null;
        UtmUtil utmUtil = new UtmUtil();
        MapUtil mapUtil = new MapUtil();

        FileStream fstream;
        BinaryWriter writer;

        // custom buttons
        PictureButton buttonMain = new PictureButton();
        PictureButton buttonGraph = new PictureButton();
        PictureButton buttonOptions = new PictureButton();

        PictureButton buttonPicLoad = new PictureButton();
        PictureButton buttonPicSaveKML = new PictureButton();
        PictureButton buttonPicSaveGPX = new PictureButton();
        PictureButton buttonPicBkOff = new PictureButton();
        PictureSelectorButton buttonPicPause = new PictureSelectorButton();

        // up/down/next/prev buttons
        PictureButton buttonUp = new PictureButton();
        PictureButton buttonDown = new PictureButton();
        PictureButton buttonNext = new PictureButton();
        PictureButton buttonPrev = new PictureButton();

        // buttons for the graph page
        PictureButton buttonZoomIn = new PictureButton();
        PictureButton buttonZoomOut = new PictureButton();

        PictureSelectorButton buttonStart = new PictureSelectorButton();
        PictureSelectorButton buttonStop = new PictureSelectorButton();

        // buttons for my own FileOpen dialog
        PictureButton buttonDialogOpen = new PictureButton();
        PictureButton buttonDialogCancel = new PictureButton();

        // button to set i/o location
        PictureButton buttonIoLocation = new PictureButton();
        PictureButton buttonMapsLocation = new PictureButton();
        AlwaysFitLabel labelFileName = new AlwaysFitLabel();

        // CrossingWays buttons on Options2 tab
        PictureButton buttonCWShowKeyboard = new PictureButton();
        PictureButton buttonCWVerify = new PictureButton();

        // button on EnterFileName tab
        PictureButton buttonEnterFileName = new PictureButton();

        // button to load track to follow
        PictureButton buttonLoadTrack2Follow = new PictureButton();
        PictureButton buttonLoad2Clear = new PictureButton();
        PictureButton buttonNextFileType = new PictureButton();
        PictureButton buttonPrevFileType = new PictureButton();

        // show help
        PictureButton buttonHelp = new PictureButton();

        // button to show/hide option view selector
        PictureButton buttonShowViewSelector = new PictureButton();

        NoBackgroundPanel NoBkPanel = new NoBackgroundPanel();

        Bitmap AboutTabImage;
        Bitmap BlankImage;
        Bitmap CWImage;

        Color bkColor;
        Color foColor;

        /* Note units for all internal vars: 
         * time : sec  
         * distance (as x/y to start point or total) : metres
         * height : metres
         * speed : km/h
         * 
         * These is converted to required units as shown on screen
        */

        // Starting point
        DateTime StartTimeUtc;
        DateTime StartTime;
        double StartLat = 0.0;
        double StartLong = 0.0;
        int StartBattery = -1;

        // need to shift origin, to be able to save X/Y as short int in metres
        double OriginShiftX = 0.0;
        double OriginShiftY = 0.0;

        // Interval to switch GPS off (0 means always on)
        int[] PollGpsTimeSec = new int[14] { 0, 5, 10, 20, 30, 60, 2*60, 3*60, 5*60, 10*60, 20*60, 30*60, 60*60, 120*60};
        DateTime LastPointUtc;
        int GpsSearchCount = 0;
        int FirstSampleValidCount = 0;

        // flag to indicate the the data was accepted by GetGpsData and save into file
        bool GpsDataOk = false;

        // to disable timer tick functions, if Stop is pressed, etc
        bool LockGpsTick = false;

        // to indicate that it was stopped on low
        bool StoppedOnLow = false;

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
        double CurrentLat = 0.0, CurrentLong = 0.0, CurrentZ = 0.0;
        double CurrentSpeed = 0.0;
        string CurrentFileName = "";
        string CurrentStatusString = "gps off";
        Color CurrentGpsLedColor = Color.Gray;
        int CurrentBattery = -1;

        // baud rates
        int[] BaudRates = new int[6] { 4800, 9600, 19200, 38400, 57600, 115200 };

        // get pass the command line arguments
        static string FirstArgument;

        // data used for plotting and saving to KML/GPX
        // decimated, max size is PlotDataSize
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

        // to disable auto-save of controls on option page during startup
        bool DoNotSaveSettingsFlag = true;

        // temp file name in the root folder (main memory)
        string TempFileName = "\\tmp.gcc";

        // vars to work with the custom folder open box
        // 1. flag to activate folder open mode or file open mode in the custom dialog
        bool FolderSetupMode = false;
        // index where list of the current directories starts in the listBoxFiles
        int CurrentSubDirIndex;
        // current directory for i/o files
        string IoFilesDirectory;
        // flag that we are setting maps folder
        bool MapsFolderSetupMode = false;
        // current directory for maps files
        string MapsFilesDirectory;
        // current directory for maps files
        int LastOsmMapDownloadIndex = 0;


        // vars to select which file type to open
        const int FileOpenMode_Gcc = 0;
        const int FileOpenMode_2ndGcc = 1;
        const int FileOpenMode_2ndKml = 2;
        const int FileOpenMode_2ndGpx = 3;
        int FileOpenMode = FileOpenMode_Gcc;
        int FileExtentionToOpen = FileOpenMode_2ndGcc;

        // to save registry setting for GPD0: in unnatenden mode, to be restored after stopping the program
        Int32 SaveGpdUnattendedValue = 4;

        // main screen drawing mode (main or maps)
        const int BufferDrawModeMain = 0;
        const int BufferDrawModeMaps = 1;
        int BufferDrawMode = BufferDrawModeMain;

        // main screen drawing vars (to set position)
        int[] MGridX = new int[3] { 0, 263, 346 };
        int[] MGridY = new int[6] { 0, 120, 184, 248, 364, 368 };
        int MGridDelta = 3;     // delta to have small gap between values and the border
        int MHeightDelta = 27;  // height of an item,  when we print a few values into a single cell

        // vars for landscape support - all we do is move button from bottom to side, storing their orig position
        bool isLandscape = false;
        bool LockResize = true;
        int LandscapeX1, LandscapeX2, LandscapeY1, LandscapeY2;

        // Hashed password for CrossingWays, as the text edit will display ***, so we cannot read it from there
        string CwHashPassword;
        DateTime LastLiveLogging;
        int[] LiveLoggingTimeMin = new int[7] { 0, 1, 5, 10, 20, 30, 60 };
        string CurrentLiveLoggingString = "";
        bool LockCwVerify = false;

        // var to show/hide options pages (resize for 16 pages max)
        int[] PagesToShow = new int[16];
        int NumPagesToShow = 0;
        int CurrentOptionsPage = 0;

        // form components
        private Panel tabBlank;
        private Panel tabBlank1;
        private Panel tabEnterFileName;
        private Panel tabOpenFile;
        private Timer timerGps;
        private ComboBox comboGpsPoll;
        private Label labelGpsActivity;
        private Label labelUnits;
        private ComboBox comboUnits;
        private Timer timerIdleReset;
        private CheckBox checkStopOnLow;
        private Label labelRevision;
        private Timer timerStartDelay;
        private CheckBox checkExStopTime;
        private ListBox listBoxFiles;
        private Label labelGeoID;
        private NumericUpDown numericGeoID;
        private CheckBox checkGpxRte;
        private CheckBox checkKmlAlt;
        private TextBox textBoxEnterFileName;
        private Label labelEnterFileName;
        private Microsoft.WindowsCE.Forms.InputPanel inputPanel;
        private CheckBox checkEditFileName;
        private CheckBox checkShowBkOff;
        private CheckBox checkRelativeAlt;
        private Label labelMultiMaps;
        private Label labelMapDownload;
        private ComboBox comboMultiMaps;
        private Label labelKmlOpt2;
        private Label labelKmlOpt1;
        private ComboBox comboBoxKmlOptColor;
        private Label labelGpsBaudRate;
        private CheckBox checkBoxUseGccDll;
        private ComboBox comboBoxUseGccDllRate;
        private ComboBox comboBoxUseGccDllCom;
        private ComboBox comboBoxKmlOptWidth;
        private Label labelCw2;
        private Label labelCw1;
        private Label labelCwInfo;
        private Label labelCwLogMode;
        private ComboBox comboBoxCwLogMode;
        private TextBox textBoxCw2;
        private TextBox textBoxCw1;
        private ComboBox comboBoxLine2OptWidth;
        private ComboBox comboBoxLine2OptColor;
        private Label labelLine2Opt1;
        private Label labelLine2Opt2;
        private CheckBox checkPlotTrackAsDots;
        private CheckBox checkPlotLine2AsDots;
        private CheckBox checkOptAbout;
        private CheckBox checkOptLiveLog;
        private CheckBox checkOptLaps;
        private CheckBox checkOptMaps;
        private Label labelOptText;
        private CheckBox checkOptGps;
        private CheckBox checkOptKmlGpx;
        private CheckBox checkOptMain;
        private NumericUpDown numericGpxTimeShift;
        private Label labelGpxTimeShift;
        private CheckBox checkMapsWhiteBk;
        private ComboBox comboLapOptions;
        private NumericUpDown numericLapOptionsT;
        private Label labelLapOptions1;
        private TextBox textLapOptions;
        private Label labelLapOptions2;
        private NumericUpDown numericLapOptionsD;
        private ComboBox comboMapDownload;
        private TabControl tabControl;
        private TabPage tabPageOptions;
        private TabPage tabPageGps;
        private TabPage tabPageMainScr;
        private TabPage tabPageMapScr;
        private TabPage tabPageKmlGpx;
        private TabPage tabPageLiveLog;
        private TabPage tabPageAbout;
        private TabPage tabPageLaps;
        private CheckBox checkUploadGpx;
        private Panel panelCwLogo;


        // c-tor. Create classes used, init some components
        public Form1()
        {
            // Required for Windows Form Designer support
            InitializeComponent();

            // set defaults (shall load from file later)
            comboGpsPoll.SelectedIndex = 0;
            comboUnits.SelectedIndex = 0;
            comboBoxKmlOptColor.SelectedIndex = 0;
            comboBoxKmlOptWidth.SelectedIndex = 1;
            comboBoxLine2OptColor.SelectedIndex = 6;
            comboBoxLine2OptWidth.SelectedIndex = 1;
            comboBoxUseGccDllRate.SelectedIndex = 0;
            comboBoxUseGccDllCom.SelectedIndex = 3;
            checkBoxUseGccDll.Checked = true;
            checkPlotLine2AsDots.Checked = true;
            comboMultiMaps.SelectedIndex = 0;
            comboMapDownload.SelectedIndex = 0;
            comboBoxCwLogMode.SelectedIndex = 0;
            comboLapOptions.SelectedIndex = 0;

            string Revision = Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "."
                            + Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString();
            
            labelRevision.Text = "programming/idea : AndyZap\ndesign : expo7\nspecial thanks to AngelGR\n\nversion " + Revision;

            ApplyCustomBackground();

            CreateCustomControls();

            ScaleToCurrentResolution();

            DoOrientationSwitch();
            LockResize = false;
        }

        private void ApplyCustomBackground()
        {
            bkColor = LoadBkColor();
            foColor = LoadForeColor();

            // this one is not editable
            tabPageAbout.BackColor = Color.FromArgb(34, 34, 34);
            labelRevision.BackColor = Color.FromArgb(34, 34, 34);
            labelRevision.ForeColor = Color.FromArgb(255, 255, 255);

            tabBlank.BackColor = bkColor;
            tabBlank1.BackColor = bkColor;
            tabOpenFile.BackColor = bkColor;
            comboGpsPoll.BackColor = bkColor;  comboGpsPoll.ForeColor = foColor;
            labelGpsActivity.BackColor = bkColor; labelGpsActivity.ForeColor = foColor;
            comboUnits.BackColor = bkColor; comboUnits.ForeColor = foColor;
            checkStopOnLow.BackColor = bkColor; checkStopOnLow.ForeColor = foColor;
            checkExStopTime.BackColor = bkColor; checkExStopTime.ForeColor = foColor;
            labelFileName.BackColor = bkColor; labelFileName.ForeColor = foColor;
            labelUnits.BackColor = bkColor; labelUnits.ForeColor = foColor;
            listBoxFiles.BackColor = bkColor; listBoxFiles.ForeColor = foColor;
            buttonIoLocation.BackColor = bkColor; buttonIoLocation.ForeColor = foColor;
            buttonMapsLocation.BackColor = bkColor; buttonMapsLocation.ForeColor = foColor;
            buttonUp.BackColor = bkColor; buttonUp.ForeColor = foColor;
            buttonDown.BackColor = bkColor; buttonDown.ForeColor = foColor;
            buttonPrev.BackColor = bkColor; buttonPrev.ForeColor = foColor;
            buttonNext.BackColor = bkColor; buttonNext.ForeColor = foColor;
            labelGeoID.BackColor = bkColor; labelGeoID.ForeColor = foColor;
            numericGeoID.BackColor = bkColor; numericGeoID.ForeColor = foColor;
            checkGpxRte.BackColor = bkColor; checkGpxRte.ForeColor = foColor;
            checkKmlAlt.BackColor = bkColor; checkKmlAlt.ForeColor = foColor;
            buttonShowViewSelector.BackColor = bkColor; buttonShowViewSelector.ForeColor = foColor;

            tabEnterFileName.BackColor = bkColor;
            textBoxEnterFileName.BackColor = bkColor; textBoxEnterFileName.ForeColor = foColor;
            labelEnterFileName.BackColor = bkColor; labelEnterFileName.ForeColor = foColor;
            buttonEnterFileName.BackColor = bkColor; buttonEnterFileName.ForeColor = foColor;
            checkEditFileName.BackColor = bkColor; checkEditFileName.ForeColor = foColor;
            checkShowBkOff.BackColor = bkColor; checkShowBkOff.ForeColor = foColor;
            checkRelativeAlt.BackColor = bkColor; checkRelativeAlt.ForeColor = foColor;
            labelMultiMaps.BackColor = bkColor; labelMultiMaps.ForeColor = foColor;
            labelMapDownload.BackColor = bkColor; labelMapDownload.ForeColor = foColor;  
            comboMultiMaps.BackColor = bkColor; comboMultiMaps.ForeColor = foColor;
            comboMapDownload.BackColor = bkColor; comboMapDownload.ForeColor = foColor;  

            labelKmlOpt2.BackColor = bkColor; labelKmlOpt2.ForeColor = foColor;
            labelKmlOpt1.BackColor = bkColor; labelKmlOpt1.ForeColor = foColor;
            comboBoxKmlOptColor.BackColor = bkColor; comboBoxKmlOptColor.ForeColor = foColor;
            comboBoxKmlOptWidth.BackColor = bkColor; comboBoxKmlOptWidth.ForeColor = foColor;

            labelGpsBaudRate.BackColor = bkColor; labelGpsBaudRate.ForeColor = foColor;
            checkBoxUseGccDll.BackColor = bkColor; checkBoxUseGccDll.ForeColor = foColor;
            comboBoxUseGccDllRate.BackColor = bkColor; comboBoxUseGccDllRate.ForeColor = foColor;
            comboBoxUseGccDllCom.BackColor = bkColor; comboBoxUseGccDllCom.ForeColor = foColor;

            buttonCWShowKeyboard.BackColor = bkColor; buttonCWShowKeyboard.ForeColor = foColor;
            buttonCWVerify.BackColor = bkColor;       buttonCWVerify.ForeColor = foColor;
            labelCw2.BackColor = bkColor;          labelCw2.ForeColor = foColor;
            labelCw1.BackColor = bkColor;          labelCw1.ForeColor = foColor;
            labelCwInfo.BackColor = bkColor;       labelCwInfo.ForeColor = foColor;
            labelCwLogMode.BackColor = bkColor;    labelCwLogMode.ForeColor = foColor;
            comboBoxCwLogMode.BackColor = bkColor; comboBoxCwLogMode.ForeColor = foColor;
            textBoxCw2.BackColor = bkColor;        textBoxCw2.ForeColor = foColor;
            textBoxCw1.BackColor = bkColor;        textBoxCw1.ForeColor = foColor;
            checkUploadGpx.BackColor = bkColor;    checkUploadGpx.ForeColor = foColor;
            panelCwLogo.BackColor = bkColor;

            comboBoxLine2OptWidth.BackColor = bkColor; comboBoxLine2OptWidth.ForeColor = foColor;
            comboBoxLine2OptColor.BackColor = bkColor; comboBoxLine2OptColor.ForeColor = foColor;
            labelLine2Opt1.BackColor = bkColor;        labelLine2Opt1.ForeColor = foColor;
            labelLine2Opt2.BackColor = bkColor;        labelLine2Opt2.ForeColor = foColor;

            buttonLoadTrack2Follow.BackColor = bkColor; buttonLoadTrack2Follow.ForeColor = foColor;
            buttonLoad2Clear.BackColor = bkColor; buttonLoad2Clear.ForeColor = foColor;
            buttonHelp.BackColor = bkColor; buttonHelp.ForeColor = foColor;

            checkPlotTrackAsDots.BackColor = bkColor; checkPlotTrackAsDots.ForeColor = foColor;
            checkPlotLine2AsDots.BackColor = bkColor; checkPlotLine2AsDots.ForeColor = foColor;

            labelOptText.BackColor = bkColor; labelOptText.ForeColor = foColor;
            checkOptAbout.BackColor = bkColor; checkOptAbout.ForeColor = foColor;
            checkOptLiveLog.BackColor = bkColor; checkOptLiveLog.ForeColor = foColor;
            checkOptLaps.BackColor = bkColor; checkOptLaps.ForeColor = foColor;
            checkOptMaps.BackColor = bkColor; checkOptMaps.ForeColor = foColor;
            checkOptGps.BackColor = bkColor; checkOptGps.ForeColor = foColor;
            checkOptKmlGpx.BackColor = bkColor; checkOptKmlGpx.ForeColor = foColor;
            checkOptMain.BackColor = bkColor; checkOptMain.ForeColor = foColor;

            numericGpxTimeShift.BackColor = bkColor; numericGpxTimeShift.ForeColor = foColor;
            labelGpxTimeShift.BackColor = bkColor; labelGpxTimeShift.ForeColor = foColor;
            checkMapsWhiteBk.BackColor = bkColor; checkMapsWhiteBk.ForeColor = foColor;

            comboLapOptions.BackColor = bkColor; comboLapOptions.ForeColor = foColor;
            numericLapOptionsT.BackColor = bkColor; numericLapOptionsT.ForeColor = foColor;
            numericLapOptionsD.BackColor = bkColor; numericLapOptionsD.ForeColor = foColor;
            labelLapOptions1.BackColor = bkColor; labelLapOptions1.ForeColor = foColor;
            labelLapOptions2.BackColor = bkColor; labelLapOptions2.ForeColor = foColor;
            textLapOptions.BackColor = bkColor; textLapOptions.ForeColor = foColor;

            tabPageOptions.BackColor = bkColor;
            tabPageGps.BackColor = bkColor;
            tabPageMainScr.BackColor = bkColor;
            tabPageMapScr.BackColor = bkColor;
            tabPageKmlGpx.BackColor = bkColor;
            tabPageLiveLog.BackColor = bkColor;
            tabPageAbout.BackColor = bkColor;
            tabPageLaps.BackColor = bkColor;

            // this is not component color - but an option to plot maps
            if (checkMapsWhiteBk.Checked)
                { mapUtil.Back_Color = Color.White; mapUtil.Fore_Color = Color.Black; }
            else
                { mapUtil.Back_Color = bkColor; mapUtil.Fore_Color = foColor; }

            this.BackColor = bkColor;
        }
        private Bitmap LoadBitmap(string name)
        {
            string CurrentDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            string file_name = name;
            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { file_name = CurrentDirectory + "\\" + file_name; }

            if (File.Exists(file_name))
            {
                return new Bitmap(file_name);
            }

            // not exists, load internal one
            return new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("GpsSample.Graphics." + name));
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
            return Color.FromArgb(34, 34, 34);
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
            buttonMain.Bounds = new Rectangle(0, 508, 160, 80);
            buttonMain.BackgroundImage = LoadBitmap("main.jpg");
            buttonMain.PressedImage = LoadBitmap("main_p.jpg");
            buttonMain.Click += new System.EventHandler(this.buttonMain_Click);

            buttonGraph.Parent = this;
            buttonGraph.Bounds = new Rectangle(0, 508, 160, 80);
            buttonGraph.BackgroundImage = LoadBitmap("map.jpg");
            buttonGraph.PressedImage = LoadBitmap("map_p.jpg");
            buttonGraph.Click += new System.EventHandler(this.buttonGraph_Click);

            buttonOptions.Parent = this;
            buttonOptions.Bounds = new Rectangle(0, 508, 160, 80);
            buttonOptions.BackgroundImage = LoadBitmap("options.jpg");
            buttonOptions.PressedImage = LoadBitmap("options_p.jpg");
            buttonOptions.Click += new System.EventHandler(this.buttonOptions_Click);

            buttonPicLoad.Parent = this;
            buttonPicLoad.Bounds = new Rectangle(320, 508, 160, 80);
            buttonPicLoad.BackgroundImage = LoadBitmap("edit.jpg");
            buttonPicLoad.PressedImage = LoadBitmap("edit_p.jpg");
            buttonPicLoad.Click += new System.EventHandler(this.buttonLoad_Click);

            // zoom in-out buttons
            buttonZoomIn.Parent = this;
            buttonZoomIn.Bounds = new Rectangle(160, 508, 160, 80);
            buttonZoomIn.BackgroundImage = LoadBitmap("zoom_in.jpg");
            buttonZoomIn.PressedImage = LoadBitmap("zoom_in_p.jpg");
            buttonZoomIn.MouseDown += new System.Windows.Forms.MouseEventHandler(this.buttonZoomIn_Click);
            
            buttonZoomOut.Parent = this;
            buttonZoomOut.Bounds = new Rectangle(320, 508, 160, 80);
            buttonZoomOut.BackgroundImage = LoadBitmap("zoom_out.jpg");
            buttonZoomOut.PressedImage = LoadBitmap("zoom_out_p.jpg");
            buttonZoomOut.MouseDown += new System.Windows.Forms.MouseEventHandler(this.buttonZoomOut_Click);

            // bkColor off buttons - show instead of pause, is option set
            buttonPicBkOff.Parent = this;
            buttonPicBkOff.Bounds = new Rectangle(160, 508, 160, 80);
            buttonPicBkOff.BackgroundImage = LoadBitmap("bklight.jpg");
            buttonPicBkOff.PressedImage = LoadBitmap("bklight_p.jpg");
            buttonPicBkOff.Click += new System.EventHandler(this.buttonBklitOff_Click); ;

            // Start Stop Pause buttons --------------

            buttonStart.Parent = this;
            buttonStart.Bounds = new Rectangle(160, 508, 160, 80);
            buttonStart.BackgroundImage = LoadBitmap("start.jpg");
            buttonStart.PressedImage = LoadBitmap("start_p.jpg");
            buttonStart.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDownS);
            buttonStart.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUpS);
            buttonStart.pressed = false;
            buttonStart.BringToFront();

            buttonStop.Parent = this;
            buttonStop.Bounds = new Rectangle(320, 508, 160, 80);
            buttonStop.BackgroundImage = LoadBitmap("stop.jpg");
            buttonStop.PressedImage = LoadBitmap("stop_p.jpg");
            buttonStop.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDownS);
            buttonStop.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUpS);
            buttonStop.pressed = true;

            buttonStop.Enabled = false;
            buttonStart.Enabled = true;

            buttonPicPause.Parent = this;
            buttonPicPause.Bounds = new Rectangle(160, 508, 160, 80);
            buttonPicPause.BackgroundImage = LoadBitmap("pause.jpg");
            buttonPicPause.PressedImage = LoadBitmap("pause_mode.jpg");
            buttonPicPause.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_PauseMouseDown);
            buttonPicPause.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_PauseMouseUp);
            buttonPicPause.pressed = false;

            // buttons on FileDialog tab --------------

            buttonDialogOpen.Parent = this;
            buttonDialogOpen.Bounds = new Rectangle(160, 508, 160, 80);
            buttonDialogOpen.BackgroundImage = LoadBitmap("open.jpg");
            buttonDialogOpen.PressedImage = LoadBitmap("open_p.jpg");
            buttonDialogOpen.Click += new System.EventHandler(this.buttonDialogOpen_Click);

            buttonDialogCancel.Parent = this;
            buttonDialogCancel.Bounds = new Rectangle(0, 508, 160, 80);
            buttonDialogCancel.BackgroundImage = LoadBitmap("cancel.jpg");
            buttonDialogCancel.PressedImage = LoadBitmap("cancel_p.jpg");
            buttonDialogCancel.Click += new System.EventHandler(this.buttonDialogCancel_Click);

            buttonDown.Parent = this;
            buttonDown.Bounds = new Rectangle(320, 508, 160, 80);
            buttonDown.BackgroundImage = LoadBitmap("down.jpg");
            buttonDown.PressedImage = LoadBitmap("down_p.jpg");
            buttonDown.MouseDown += new System.Windows.Forms.MouseEventHandler(this.buttonDialogDown_Click);

            buttonUp.Parent = this;
            buttonUp.Bounds = new Rectangle(320, 428, 160, 80);
            buttonUp.BackgroundImage = LoadBitmap("up.jpg");
            buttonUp.PressedImage = LoadBitmap("up_p.jpg");
            buttonUp.MouseDown += new System.Windows.Forms.MouseEventHandler(this.buttonDialogUp_Click);

            buttonPicSaveKML.Parent = this;
            buttonPicSaveKML.Bounds = new Rectangle(0, 428, 160, 80);
            buttonPicSaveKML.BackgroundImage = LoadBitmap("kml.jpg");
            buttonPicSaveKML.PressedImage = LoadBitmap("kml_p.jpg");
            buttonPicSaveKML.Click += new System.EventHandler(this.buttonSaveKML_Click);

            buttonPicSaveGPX.Parent = this;
            buttonPicSaveGPX.Bounds = new Rectangle(160, 428, 160, 80);
            buttonPicSaveGPX.BackgroundImage = LoadBitmap("gpx.jpg");
            buttonPicSaveGPX.PressedImage = LoadBitmap("gpx_p.jpg");
            buttonPicSaveGPX.Click += new System.EventHandler(this.buttonSaveGPX_Click);

            buttonPrev.Parent = this;
            buttonPrev.Bounds = new Rectangle(160, 508, 160, 80);
            buttonPrev.BackgroundImage = LoadBitmap("left.jpg");
            buttonPrev.PressedImage = LoadBitmap("left_p.jpg");
            buttonPrev.MouseDown += new System.Windows.Forms.MouseEventHandler(this.buttonOptionsPrev_Click);

            buttonNext.Parent = this;
            buttonNext.Bounds = new Rectangle(320, 508, 160, 80);
            buttonNext.BackgroundImage = LoadBitmap("right.jpg");
            buttonNext.PressedImage = LoadBitmap("right_p.jpg");
            buttonNext.MouseDown += new System.Windows.Forms.MouseEventHandler(this.buttonOptionsNext_Click);

            // help
            buttonHelp.Parent = this.tabPageAbout;
            buttonHelp.Bounds = new Rectangle(5, 385, 474, 70);
            buttonHelp.Text = "View readme...";
            buttonHelp.Click += new System.EventHandler(this.buttonHelp_Click);
            buttonHelp.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular);
            buttonHelp.align = 2;

            // button to show/hide view selector
            buttonShowViewSelector.Parent = this.tabPageOptions;
            buttonShowViewSelector.Bounds = new Rectangle(5, 400, 474, 50);
            buttonShowViewSelector.Text = "Select option pages to scroll ...";
            buttonShowViewSelector.Click += new System.EventHandler(this.buttonShowViewOpt_Click);
            buttonShowViewSelector.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular);
            buttonShowViewSelector.align = 3;

            // button to set maps location
            buttonMapsLocation.Parent = this.tabPageOptions;
            buttonMapsLocation.Bounds = new Rectangle(5, 5, 474, 80);
            buttonMapsLocation.Text = "Set maps files location ...";
            buttonMapsLocation.Click += new System.EventHandler(this.buttonMapsLocation_Click);
            buttonMapsLocation.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular);
            buttonMapsLocation.align = 1;

            // button to set i/o location
            buttonIoLocation.Parent = this.tabPageOptions;
            buttonIoLocation.Bounds = new Rectangle(5, 105, 474, 80);
            buttonIoLocation.Text = "Set input/output files location ...";
            buttonIoLocation.Click += new System.EventHandler(this.buttonFileLocation_Click);
            buttonIoLocation.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular);
            buttonIoLocation.align = 1;

            // buttons to load second line
            buttonLoadTrack2Follow.Parent = this.tabPageOptions;
            buttonLoadTrack2Follow.Bounds = new Rectangle(3, 205, 360, 80);
            buttonLoadTrack2Follow.Text = "Load track to follow ...";
            buttonLoadTrack2Follow.Click += new System.EventHandler(this.buttonLoadTrack2Follow_Click);
            buttonLoadTrack2Follow.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular);
            buttonLoadTrack2Follow.align = 1;

            buttonLoad2Clear.Parent = this.tabPageOptions;
            buttonLoad2Clear.Bounds = new Rectangle(363, 205, 117, 80);
            buttonLoad2Clear.Text = "Clear ...";
            buttonLoad2Clear.Click += new System.EventHandler(this.buttonLoad2Clear_Click);
            buttonLoad2Clear.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular);
            buttonLoad2Clear.align = 3;


            // button to set file name
            buttonEnterFileName.Parent = this.tabEnterFileName;
            buttonEnterFileName.Bounds = new Rectangle(252, 100, 220, 80);
            buttonEnterFileName.Text = "Continue ...";
            buttonEnterFileName.Click += new System.EventHandler(this.buttonEnterFileName_Click);
            buttonEnterFileName.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular);
            buttonEnterFileName.align = 3;

            // buttons on CW page
            buttonCWShowKeyboard.Parent = this.tabPageLiveLog;
            buttonCWShowKeyboard.Bounds = new Rectangle(202, 0, 270, 40);
            buttonCWShowKeyboard.Text = "Hide/show keyboard ...";
            buttonCWShowKeyboard.Click += new System.EventHandler(this.buttonCWShowKeyboard_Click);
            buttonCWShowKeyboard.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            buttonCWShowKeyboard.align = 3;

            buttonCWVerify.Parent = this.tabPageLiveLog;
            buttonCWVerify.Bounds = new Rectangle(202, 140, 270, 40);
            buttonCWVerify.Text = "Verify login ...";
            buttonCWVerify.Click += new System.EventHandler(this.buttonCWVerify_Click);
            buttonCWVerify.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            buttonCWVerify.align = 3;

            buttonPrevFileType.Parent = this;
            buttonPrevFileType.Bounds = new Rectangle(0, 428, 160, 80);
            buttonPrevFileType.BackgroundImage = LoadBitmap("left.jpg");
            buttonPrevFileType.PressedImage = LoadBitmap("left_p.jpg");
            buttonPrevFileType.MouseDown += new System.Windows.Forms.MouseEventHandler(this.buttonPrevFileType_Click);

            buttonNextFileType.Parent = this;
            buttonNextFileType.Bounds = new Rectangle(160, 428, 160, 80);
            buttonNextFileType.BackgroundImage = LoadBitmap("right.jpg");
            buttonNextFileType.PressedImage = LoadBitmap("right_p.jpg");
            buttonNextFileType.MouseDown += new System.Windows.Forms.MouseEventHandler(this.buttonNextFileType_Click);

            // Always Fit Label
            labelFileName.Parent = this.tabPageOptions;
            labelFileName.Bounds = new Rectangle(3, 305, 474, 28);
            labelFileName.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            labelFileName.Text = "";

            // No Background Panel for flicker-free paint
            NoBkPanel.Parent = this;
            NoBkPanel.Location = new System.Drawing.Point(0, 0);
            NoBkPanel.Name = "NoBkPanel";
            NoBkPanel.Size = new System.Drawing.Size(480, 508);
            NoBkPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.tabGraph_Paint);
            NoBkPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.tabGraph_MouseMove);
            NoBkPanel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.tabGraph_MouseUp);
            NoBkPanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tabGraph_MouseDown);

            // about tab image, blank image and CW logo image
            AboutTabImage = new Bitmap(asm.GetManifestResourceStream("GpsSample.Graphics.about.jpg"));
            BlankImage = LoadBitmap("blank.jpg");
            CWImage = new Bitmap(asm.GetManifestResourceStream("GpsSample.Graphics.CW_logo.png"));

            NoBkPanel.BringToFront();
            buttonOptions.BringToFront();
            buttonStart.BringToFront();
            buttonPicLoad.BringToFront();

            listBoxFiles.Items.Clear();
            listBoxFiles.Focus();
        }

        private void ScaleControl(Control c)
        {
            int h = Screen.PrimaryScreen.WorkingArea.Height;
            int w = Screen.PrimaryScreen.WorkingArea.Width;
            double sc_y = (double)h / 588.0;
            double sc_x = (double)w / 480.0;
            // if we are in landscape mode, swap to get portrait sizes
            if (Screen.PrimaryScreen.Bounds.Height < Screen.PrimaryScreen.Bounds.Width)
            {
                h = Screen.PrimaryScreen.WorkingArea.Width;
                w = Screen.PrimaryScreen.WorkingArea.Height;
                sc_y = (double)h / 640.0;
                sc_x = (double)w / 428.0;
            }

            c.Top = (int)(c.Top * sc_y);
            c.Left = (int)(c.Left * sc_x);
            c.Width = (int)(c.Width * sc_x);
            c.Height = (int)(c.Height * sc_y);
        }
        private void ScaleToCurrentResolution()
        {
            ScaleControl((Control)buttonMain);
            ScaleControl((Control)buttonGraph);
            ScaleControl((Control)buttonOptions);
            ScaleControl((Control)buttonPicLoad);
            ScaleControl((Control)buttonPicSaveKML);
            ScaleControl((Control)buttonPicSaveGPX);
            ScaleControl((Control)buttonZoomIn);
            ScaleControl((Control)buttonZoomOut);
            ScaleControl((Control)buttonPicBkOff);
            ScaleControl((Control)buttonPicPause);
            ScaleControl((Control)buttonStart);
            ScaleControl((Control)buttonStop);
            ScaleControl((Control)buttonDialogOpen);
            ScaleControl((Control)buttonDialogCancel);
            ScaleControl((Control)buttonIoLocation);
            ScaleControl((Control)buttonMapsLocation);
            ScaleControl((Control)buttonUp);
            ScaleControl((Control)buttonDown);
            ScaleControl((Control)buttonPrev);
            ScaleControl((Control)buttonNext);
            ScaleControl((Control)buttonShowViewSelector);
            ScaleControl((Control)buttonNextFileType);
            ScaleControl((Control)buttonPrevFileType);

            ScaleControl((Control)tabBlank);
            ScaleControl((Control)tabBlank1);
            ScaleControl((Control)tabOpenFile);
            ScaleControl((Control)NoBkPanel);
            ScaleControl((Control)comboGpsPoll);
            ScaleControl((Control)labelGpsActivity);
            ScaleControl((Control)comboUnits);
            ScaleControl((Control)checkStopOnLow);
            ScaleControl((Control)labelRevision);
            ScaleControl((Control)checkExStopTime);
            ScaleControl((Control)labelFileName);
            ScaleControl((Control)listBoxFiles);
            ScaleControl((Control)labelUnits);
            ScaleControl((Control)labelGeoID);

            ScaleControl((Control)numericGeoID);
            ScaleControl((Control)checkGpxRte);
            ScaleControl((Control)checkKmlAlt);

            ScaleControl((Control)tabEnterFileName);
            ScaleControl((Control)textBoxEnterFileName);
            ScaleControl((Control)labelEnterFileName);
            ScaleControl((Control)buttonEnterFileName);
            ScaleControl((Control)checkEditFileName);
            ScaleControl((Control)checkShowBkOff);
            ScaleControl((Control)checkRelativeAlt);
            ScaleControl((Control)labelMultiMaps);
            ScaleControl((Control)labelMapDownload);
            ScaleControl((Control)comboMultiMaps);
            ScaleControl((Control)comboMapDownload);

            ScaleControl((Control)labelKmlOpt2);
            ScaleControl((Control)labelKmlOpt1);
            ScaleControl((Control)comboBoxKmlOptColor);
            ScaleControl((Control)comboBoxKmlOptWidth);
            ScaleControl((Control)labelGpsBaudRate);
            ScaleControl((Control)checkBoxUseGccDll);
            ScaleControl((Control)comboBoxUseGccDllRate);
            ScaleControl((Control)comboBoxUseGccDllCom);

            ScaleControl((Control)buttonCWShowKeyboard);
            ScaleControl((Control)buttonCWVerify);
            ScaleControl((Control)labelCw2);
            ScaleControl((Control)labelCw1);
            ScaleControl((Control)labelCwInfo);
            ScaleControl((Control)labelCwLogMode);
            ScaleControl((Control)comboBoxCwLogMode);
            ScaleControl((Control)textBoxCw2);
            ScaleControl((Control)textBoxCw1);
            ScaleControl((Control)checkUploadGpx);
            ScaleControl((Control)panelCwLogo);

            ScaleControl((Control)comboBoxLine2OptWidth);
            ScaleControl((Control)comboBoxLine2OptColor);
            ScaleControl((Control)labelLine2Opt1);
            ScaleControl((Control)labelLine2Opt2);

            ScaleControl((Control)buttonLoadTrack2Follow);
            ScaleControl((Control)buttonLoad2Clear);
            ScaleControl((Control)buttonHelp);

            ScaleControl((Control)checkPlotTrackAsDots);
            ScaleControl((Control)checkPlotLine2AsDots);

            ScaleControl((Control)labelOptText);
            ScaleControl((Control)checkOptAbout);
            ScaleControl((Control)checkOptLiveLog);
            ScaleControl((Control)checkOptLaps);
            ScaleControl((Control)checkOptMaps);
            ScaleControl((Control)checkOptGps);
            ScaleControl((Control)checkOptKmlGpx);
            ScaleControl((Control)checkOptMain);

            ScaleControl((Control)numericGpxTimeShift);
            ScaleControl((Control)labelGpxTimeShift);
            ScaleControl((Control)checkMapsWhiteBk);

            ScaleControl((Control)comboLapOptions);
            ScaleControl((Control)numericLapOptionsT);
            ScaleControl((Control)numericLapOptionsD);
            ScaleControl((Control)labelLapOptions1);
            ScaleControl((Control)labelLapOptions2);
            ScaleControl((Control)textLapOptions);

            ScaleControl((Control)tabControl);

            int h_delta = Screen.PrimaryScreen.Bounds.Height - Screen.PrimaryScreen.WorkingArea.Height;
            int h = Screen.PrimaryScreen.WorkingArea.Height;
            int w = Screen.PrimaryScreen.WorkingArea.Width;
            double sc_y = (double)h / 588.0;
            double sc_x = (double)w / 480.0;
            // if we are in landscape mode, swap to get portrait sizes
            if (Screen.PrimaryScreen.Bounds.Height < Screen.PrimaryScreen.Bounds.Width) 
            {
                h = Screen.PrimaryScreen.WorkingArea.Width;
                w = Screen.PrimaryScreen.WorkingArea.Height;
                sc_y = (double)h / 640.0;
                sc_x = (double)w / 428.0;
            }

            // main drawing grid
            for (int i = 0; i < MGridX.Length; i++) { MGridX[i] = (int)(MGridX[i]*sc_x); }
            for (int i = 0; i < MGridY.Length; i++) { MGridY[i] = (int)(MGridY[i]*sc_y); }
            MGridDelta = (int)(MGridDelta*sc_y);
            MHeightDelta = (int)(MHeightDelta*sc_y);

            // scale main form
            this.Top = h_delta;
            this.Height = h;
            this.Width = w;
        }

        private bool CheckMapsDirectoryExists()
        {
            if (Directory.Exists(MapsFilesDirectory)) { return true; }

            MessageBox.Show("Resetting maps files location to the application folder", "Folder does not exist!",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

            MapsFilesDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
            return false;
        }
        private bool CheckIoDirectoryExists()
        {
            if (Directory.Exists(IoFilesDirectory)) { return true; }

            MessageBox.Show("Resetting input/output files location to the application folder", "Folder does not exist!",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

            IoFilesDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
            return false;
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
            this.tabEnterFileName = new System.Windows.Forms.Panel();
            this.textBoxEnterFileName = new System.Windows.Forms.TextBox();
            this.labelEnterFileName = new System.Windows.Forms.Label();
            this.labelRevision = new System.Windows.Forms.Label();
            this.tabOpenFile = new System.Windows.Forms.Panel();
            this.listBoxFiles = new System.Windows.Forms.ListBox();
            this.tabBlank1 = new System.Windows.Forms.Panel();
            this.tabBlank = new System.Windows.Forms.Panel();
            this.checkOptMain = new System.Windows.Forms.CheckBox();
            this.checkOptKmlGpx = new System.Windows.Forms.CheckBox();
            this.checkOptAbout = new System.Windows.Forms.CheckBox();
            this.checkOptLiveLog = new System.Windows.Forms.CheckBox();
            this.checkOptLaps = new System.Windows.Forms.CheckBox();
            this.checkOptMaps = new System.Windows.Forms.CheckBox();
            this.labelOptText = new System.Windows.Forms.Label();
            this.checkOptGps = new System.Windows.Forms.CheckBox();
            this.checkStopOnLow = new System.Windows.Forms.CheckBox();
            this.comboGpsPoll = new System.Windows.Forms.ComboBox();
            this.labelGpsActivity = new System.Windows.Forms.Label();
            this.checkBoxUseGccDll = new System.Windows.Forms.CheckBox();
            this.comboBoxUseGccDllRate = new System.Windows.Forms.ComboBox();
            this.comboBoxUseGccDllCom = new System.Windows.Forms.ComboBox();
            this.labelGpsBaudRate = new System.Windows.Forms.Label();
            this.numericGeoID = new System.Windows.Forms.NumericUpDown();
            this.labelGeoID = new System.Windows.Forms.Label();
            this.checkExStopTime = new System.Windows.Forms.CheckBox();
            this.comboUnits = new System.Windows.Forms.ComboBox();
            this.labelUnits = new System.Windows.Forms.Label();
            this.checkEditFileName = new System.Windows.Forms.CheckBox();
            this.checkShowBkOff = new System.Windows.Forms.CheckBox();
            this.checkRelativeAlt = new System.Windows.Forms.CheckBox();
            this.labelLapOptions2 = new System.Windows.Forms.Label();
            this.textLapOptions = new System.Windows.Forms.TextBox();
            this.comboLapOptions = new System.Windows.Forms.ComboBox();
            this.labelLapOptions1 = new System.Windows.Forms.Label();
            this.numericLapOptionsD = new System.Windows.Forms.NumericUpDown();
            this.numericLapOptionsT = new System.Windows.Forms.NumericUpDown();
            this.numericGpxTimeShift = new System.Windows.Forms.NumericUpDown();
            this.labelGpxTimeShift = new System.Windows.Forms.Label();
            this.checkKmlAlt = new System.Windows.Forms.CheckBox();
            this.checkGpxRte = new System.Windows.Forms.CheckBox();
            this.checkPlotTrackAsDots = new System.Windows.Forms.CheckBox();
            this.comboBoxKmlOptWidth = new System.Windows.Forms.ComboBox();
            this.comboBoxKmlOptColor = new System.Windows.Forms.ComboBox();
            this.labelMultiMaps = new System.Windows.Forms.Label();
            this.labelMapDownload = new System.Windows.Forms.Label();
            this.comboMultiMaps = new System.Windows.Forms.ComboBox();
            this.comboMapDownload = new System.Windows.Forms.ComboBox();
            this.labelKmlOpt1 = new System.Windows.Forms.Label();
            this.labelKmlOpt2 = new System.Windows.Forms.Label();
            this.comboBoxLine2OptWidth = new System.Windows.Forms.ComboBox();
            this.comboBoxLine2OptColor = new System.Windows.Forms.ComboBox();
            this.labelLine2Opt1 = new System.Windows.Forms.Label();
            this.labelLine2Opt2 = new System.Windows.Forms.Label();
            this.labelCw2 = new System.Windows.Forms.Label();
            this.labelCw1 = new System.Windows.Forms.Label();
            this.textBoxCw2 = new System.Windows.Forms.TextBox();
            this.textBoxCw1 = new System.Windows.Forms.TextBox();
            this.labelCwInfo = new System.Windows.Forms.Label();
            this.labelCwLogMode = new System.Windows.Forms.Label();
            this.comboBoxCwLogMode = new System.Windows.Forms.ComboBox();
            this.panelCwLogo = new System.Windows.Forms.Panel();
            this.checkMapsWhiteBk = new System.Windows.Forms.CheckBox();
            this.checkPlotLine2AsDots = new System.Windows.Forms.CheckBox();
            this.timerGps = new System.Windows.Forms.Timer();
            this.timerIdleReset = new System.Windows.Forms.Timer();
            this.timerStartDelay = new System.Windows.Forms.Timer();
            this.inputPanel = new Microsoft.WindowsCE.Forms.InputPanel();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageOptions = new System.Windows.Forms.TabPage();
            this.tabPageGps = new System.Windows.Forms.TabPage();
            this.tabPageMainScr = new System.Windows.Forms.TabPage();
            this.tabPageMapScr = new System.Windows.Forms.TabPage();
            this.tabPageKmlGpx = new System.Windows.Forms.TabPage();
            this.tabPageLiveLog = new System.Windows.Forms.TabPage();
            this.checkUploadGpx = new System.Windows.Forms.CheckBox();
            this.tabPageLaps = new System.Windows.Forms.TabPage();
            this.tabPageAbout = new System.Windows.Forms.TabPage();
            this.tabEnterFileName.SuspendLayout();
            this.tabOpenFile.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabPageOptions.SuspendLayout();
            this.tabPageGps.SuspendLayout();
            this.tabPageMainScr.SuspendLayout();
            this.tabPageMapScr.SuspendLayout();
            this.tabPageKmlGpx.SuspendLayout();
            this.tabPageLiveLog.SuspendLayout();
            this.tabPageLaps.SuspendLayout();
            this.tabPageAbout.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabEnterFileName
            // 
            this.tabEnterFileName.Controls.Add(this.textBoxEnterFileName);
            this.tabEnterFileName.Controls.Add(this.labelEnterFileName);
            this.tabEnterFileName.Location = new System.Drawing.Point(0, 0);
            this.tabEnterFileName.Name = "tabEnterFileName";
            this.tabEnterFileName.Size = new System.Drawing.Size(480, 507);
            // 
            // textBoxEnterFileName
            // 
            this.textBoxEnterFileName.Location = new System.Drawing.Point(2, 54);
            this.textBoxEnterFileName.Name = "textBoxEnterFileName";
            this.textBoxEnterFileName.Size = new System.Drawing.Size(474, 41);
            this.textBoxEnterFileName.TabIndex = 1;
            // 
            // labelEnterFileName
            // 
            this.labelEnterFileName.Location = new System.Drawing.Point(9, 15);
            this.labelEnterFileName.Name = "labelEnterFileName";
            this.labelEnterFileName.Size = new System.Drawing.Size(463, 40);
            this.labelEnterFileName.Text = "Enter file name (without extension):";
            // 
            // labelRevision
            // 
            this.labelRevision.Location = new System.Drawing.Point(0, 220);
            this.labelRevision.Name = "labelRevision";
            this.labelRevision.Size = new System.Drawing.Size(480, 140);
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
            this.listBoxFiles.Size = new System.Drawing.Size(480, 437);
            this.listBoxFiles.TabIndex = 0;
            this.listBoxFiles.SelectedIndexChanged += new System.EventHandler(this.listBoxFiles_SelectedIndexChanged);
            // 
            // tabBlank1
            // 
            this.tabBlank1.Location = new System.Drawing.Point(480, 0);
            this.tabBlank1.Name = "tabBlank1";
            this.tabBlank1.Size = new System.Drawing.Size(160, 480);
            // 
            // tabBlank
            // 
            this.tabBlank.Location = new System.Drawing.Point(320, 508);
            this.tabBlank.Name = "tabBlank";
            this.tabBlank.Size = new System.Drawing.Size(160, 80);
            this.tabBlank.Paint += new System.Windows.Forms.PaintEventHandler(this.tabBlank_Paint);
            // 
            // checkOptMain
            // 
            this.checkOptMain.Checked = true;
            this.checkOptMain.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkOptMain.Location = new System.Drawing.Point(2, 37);
            this.checkOptMain.Name = "checkOptMain";
            this.checkOptMain.Size = new System.Drawing.Size(220, 40);
            this.checkOptMain.TabIndex = 7;
            this.checkOptMain.Text = "Main screen";
            this.checkOptMain.Visible = false;
            this.checkOptMain.Click += new System.EventHandler(this.checkOptGps_Click);
            // 
            // checkOptKmlGpx
            // 
            this.checkOptKmlGpx.Checked = true;
            this.checkOptKmlGpx.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkOptKmlGpx.Location = new System.Drawing.Point(243, 87);
            this.checkOptKmlGpx.Name = "checkOptKmlGpx";
            this.checkOptKmlGpx.Size = new System.Drawing.Size(220, 40);
            this.checkOptKmlGpx.TabIndex = 5;
            this.checkOptKmlGpx.Text = "KML/GPX";
            this.checkOptKmlGpx.Visible = false;
            this.checkOptKmlGpx.Click += new System.EventHandler(this.checkOptGps_Click);
            // 
            // checkOptAbout
            // 
            this.checkOptAbout.Checked = true;
            this.checkOptAbout.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkOptAbout.Location = new System.Drawing.Point(2, 187);
            this.checkOptAbout.Name = "checkOptAbout";
            this.checkOptAbout.Size = new System.Drawing.Size(220, 40);
            this.checkOptAbout.TabIndex = 4;
            this.checkOptAbout.Text = "About";
            this.checkOptAbout.Visible = false;
            this.checkOptAbout.Click += new System.EventHandler(this.checkOptGps_Click);
            // 
            // checkOptLiveLog
            // 
            this.checkOptLiveLog.Checked = true;
            this.checkOptLiveLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkOptLiveLog.Location = new System.Drawing.Point(243, 137);
            this.checkOptLiveLog.Name = "checkOptLiveLog";
            this.checkOptLiveLog.Size = new System.Drawing.Size(220, 40);
            this.checkOptLiveLog.TabIndex = 3;
            this.checkOptLiveLog.Text = "Live logging";
            this.checkOptLiveLog.Visible = false;
            this.checkOptLiveLog.Click += new System.EventHandler(this.checkOptGps_Click);
            // 
            // checkOptLaps
            // 
            this.checkOptLaps.Checked = true;
            this.checkOptLaps.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkOptLaps.Location = new System.Drawing.Point(2, 137);
            this.checkOptLaps.Name = "checkOptLaps";
            this.checkOptLaps.Size = new System.Drawing.Size(220, 40);
            this.checkOptLaps.TabIndex = 3;
            this.checkOptLaps.Text = "Laps";
            this.checkOptLaps.Visible = false;
            this.checkOptLaps.Click += new System.EventHandler(this.checkOptGps_Click);
            // 
            // checkOptMaps
            // 
            this.checkOptMaps.Checked = true;
            this.checkOptMaps.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkOptMaps.Location = new System.Drawing.Point(243, 37);
            this.checkOptMaps.Name = "checkOptMaps";
            this.checkOptMaps.Size = new System.Drawing.Size(220, 40);
            this.checkOptMaps.TabIndex = 2;
            this.checkOptMaps.Text = "Maps screen";
            this.checkOptMaps.Visible = false;
            this.checkOptMaps.Click += new System.EventHandler(this.checkOptGps_Click);
            // 
            // labelOptText
            // 
            this.labelOptText.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            this.labelOptText.Location = new System.Drawing.Point(2, 5);
            this.labelOptText.Name = "labelOptText";
            this.labelOptText.Size = new System.Drawing.Size(474, 36);
            this.labelOptText.Text = "Select option pages to scroll";
            this.labelOptText.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.labelOptText.Visible = false;
            // 
            // checkOptGps
            // 
            this.checkOptGps.Checked = true;
            this.checkOptGps.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkOptGps.Location = new System.Drawing.Point(2, 87);
            this.checkOptGps.Name = "checkOptGps";
            this.checkOptGps.Size = new System.Drawing.Size(220, 40);
            this.checkOptGps.TabIndex = 0;
            this.checkOptGps.Text = "GPS";
            this.checkOptGps.Visible = false;
            this.checkOptGps.Click += new System.EventHandler(this.checkOptGps_Click);
            // 
            // checkStopOnLow
            // 
            this.checkStopOnLow.Checked = true;
            this.checkStopOnLow.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkStopOnLow.Location = new System.Drawing.Point(2, 55);
            this.checkStopOnLow.Name = "checkStopOnLow";
            this.checkStopOnLow.Size = new System.Drawing.Size(477, 40);
            this.checkStopOnLow.TabIndex = 16;
            this.checkStopOnLow.Text = "Stop GPS if battery <20%";
            this.checkStopOnLow.Click += new System.EventHandler(this.comboGpsPoll_SelectedIndexChanged);
            // 
            // comboGpsPoll
            // 
            this.comboGpsPoll.Items.Add("always on");
            this.comboGpsPoll.Items.Add("run every 5 sec");
            this.comboGpsPoll.Items.Add("run every 10 sec");
            this.comboGpsPoll.Items.Add("run every 20 sec");
            this.comboGpsPoll.Items.Add("run every 30 sec");
            this.comboGpsPoll.Items.Add("run every 1 min");
            this.comboGpsPoll.Items.Add("run every 2 min");
            this.comboGpsPoll.Items.Add("run every 3 min ");
            this.comboGpsPoll.Items.Add("run every 5 min");
            this.comboGpsPoll.Items.Add("run every 10 min");
            this.comboGpsPoll.Items.Add("run every 20 min");
            this.comboGpsPoll.Items.Add("run every 30 min");
            this.comboGpsPoll.Items.Add("run every 1 hour");
            this.comboGpsPoll.Items.Add("run every 2 hours");
            this.comboGpsPoll.Location = new System.Drawing.Point(154, 5);
            this.comboGpsPoll.Name = "comboGpsPoll";
            this.comboGpsPoll.Size = new System.Drawing.Size(323, 41);
            this.comboGpsPoll.TabIndex = 1;
            this.comboGpsPoll.SelectedIndexChanged += new System.EventHandler(this.comboGpsPoll_SelectedIndexChanged);
            // 
            // labelGpsActivity
            // 
            this.labelGpsActivity.Location = new System.Drawing.Point(2, 7);
            this.labelGpsActivity.Name = "labelGpsActivity";
            this.labelGpsActivity.Size = new System.Drawing.Size(219, 40);
            this.labelGpsActivity.Text = "GPS activity:";
            // 
            // checkBoxUseGccDll
            // 
            this.checkBoxUseGccDll.Location = new System.Drawing.Point(2, 105);
            this.checkBoxUseGccDll.Name = "checkBoxUseGccDll";
            this.checkBoxUseGccDll.Size = new System.Drawing.Size(312, 40);
            this.checkBoxUseGccDll.TabIndex = 24;
            this.checkBoxUseGccDll.Text = "Read GPS data directly:";
            // 
            // comboBoxUseGccDllRate
            // 
            this.comboBoxUseGccDllRate.Items.Add("4800");
            this.comboBoxUseGccDllRate.Items.Add("9600");
            this.comboBoxUseGccDllRate.Items.Add("19200");
            this.comboBoxUseGccDllRate.Items.Add("38400");
            this.comboBoxUseGccDllRate.Items.Add("57600");
            this.comboBoxUseGccDllRate.Items.Add("115200");
            this.comboBoxUseGccDllRate.Location = new System.Drawing.Point(321, 155);
            this.comboBoxUseGccDllRate.Name = "comboBoxUseGccDllRate";
            this.comboBoxUseGccDllRate.Size = new System.Drawing.Size(156, 41);
            this.comboBoxUseGccDllRate.TabIndex = 32;
            // 
            // comboBoxUseGccDllCom
            // 
            this.comboBoxUseGccDllCom.Items.Add("COM1:");
            this.comboBoxUseGccDllCom.Items.Add("COM2:");
            this.comboBoxUseGccDllCom.Items.Add("COM3:");
            this.comboBoxUseGccDllCom.Items.Add("COM4:");
            this.comboBoxUseGccDllCom.Items.Add("COM5:");
            this.comboBoxUseGccDllCom.Items.Add("COM6:");
            this.comboBoxUseGccDllCom.Items.Add("COM7:");
            this.comboBoxUseGccDllCom.Items.Add("COM8:");
            this.comboBoxUseGccDllCom.Items.Add("COM9:");
            this.comboBoxUseGccDllCom.Items.Add("COM10:");
            this.comboBoxUseGccDllCom.Items.Add("COM11:");
            this.comboBoxUseGccDllCom.Items.Add("COM12:");
            this.comboBoxUseGccDllCom.Items.Add("COM13:");
            this.comboBoxUseGccDllCom.Items.Add("COM14:");
            this.comboBoxUseGccDllCom.Location = new System.Drawing.Point(321, 105);
            this.comboBoxUseGccDllCom.Name = "comboBoxUseGccDllCom";
            this.comboBoxUseGccDllCom.Size = new System.Drawing.Size(156, 41);
            this.comboBoxUseGccDllCom.TabIndex = 31;
            // 
            // labelGpsBaudRate
            // 
            this.labelGpsBaudRate.Location = new System.Drawing.Point(181, 155);
            this.labelGpsBaudRate.Name = "labelGpsBaudRate";
            this.labelGpsBaudRate.Size = new System.Drawing.Size(134, 40);
            this.labelGpsBaudRate.Text = "Baud rate:";
            // 
            // numericGeoID
            // 
            this.numericGeoID.Location = new System.Drawing.Point(321, 205);
            this.numericGeoID.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.numericGeoID.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.numericGeoID.Name = "numericGeoID";
            this.numericGeoID.Size = new System.Drawing.Size(156, 36);
            this.numericGeoID.TabIndex = 0;
            // 
            // labelGeoID
            // 
            this.labelGeoID.Location = new System.Drawing.Point(2, 205);
            this.labelGeoID.Name = "labelGeoID";
            this.labelGeoID.Size = new System.Drawing.Size(379, 40);
            this.labelGeoID.Text = "Manual altitude correction, m";
            // 
            // checkExStopTime
            // 
            this.checkExStopTime.Checked = true;
            this.checkExStopTime.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkExStopTime.Location = new System.Drawing.Point(2, 55);
            this.checkExStopTime.Name = "checkExStopTime";
            this.checkExStopTime.Size = new System.Drawing.Size(476, 40);
            this.checkExStopTime.TabIndex = 29;
            this.checkExStopTime.Text = "Exclude stop time";
            this.checkExStopTime.Click += new System.EventHandler(this.checkExStopTime_Click);
            // 
            // comboUnits
            // 
            this.comboUnits.Items.Add("miles / mph");
            this.comboUnits.Items.Add("km / kmh");
            this.comboUnits.Items.Add("naut miles / knots");
            this.comboUnits.Items.Add("miles / mph / ft");
            this.comboUnits.Items.Add("km / min per km");
            this.comboUnits.Items.Add("miles / min per mile / ft");
            this.comboUnits.Items.Add("km / kmh / ft");
            this.comboUnits.Location = new System.Drawing.Point(98, 5);
            this.comboUnits.Name = "comboUnits";
            this.comboUnits.Size = new System.Drawing.Size(379, 41);
            this.comboUnits.TabIndex = 4;
            this.comboUnits.SelectedIndexChanged += new System.EventHandler(this.checkExStopTime_Click);
            // 
            // labelUnits
            // 
            this.labelUnits.Location = new System.Drawing.Point(2, 7);
            this.labelUnits.Name = "labelUnits";
            this.labelUnits.Size = new System.Drawing.Size(219, 40);
            this.labelUnits.Text = "Units:";
            // 
            // checkEditFileName
            // 
            this.checkEditFileName.Location = new System.Drawing.Point(2, 105);
            this.checkEditFileName.Name = "checkEditFileName";
            this.checkEditFileName.Size = new System.Drawing.Size(476, 40);
            this.checkEditFileName.TabIndex = 19;
            this.checkEditFileName.Text = "Ask for log file name";
            // 
            // checkShowBkOff
            // 
            this.checkShowBkOff.Location = new System.Drawing.Point(2, 155);
            this.checkShowBkOff.Name = "checkShowBkOff";
            this.checkShowBkOff.Size = new System.Drawing.Size(476, 40);
            this.checkShowBkOff.TabIndex = 18;
            this.checkShowBkOff.Text = "Show BkLight Off button";
            // 
            // checkRelativeAlt
            // 
            this.checkRelativeAlt.Location = new System.Drawing.Point(2, 205);
            this.checkRelativeAlt.Name = "checkRelativeAlt";
            this.checkRelativeAlt.Size = new System.Drawing.Size(476, 40);
            this.checkRelativeAlt.TabIndex = 20;
            this.checkRelativeAlt.Text = "Show relative altitude";
            // 
            // labelLapOptions2
            // 
            this.labelLapOptions2.Location = new System.Drawing.Point(418, 52);
            this.labelLapOptions2.Name = "labelLapOptions2";
            this.labelLapOptions2.Size = new System.Drawing.Size(60, 40);
            this.labelLapOptions2.Text = "min";
            this.labelLapOptions2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // textLapOptions
            // 
            this.textLapOptions.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textLapOptions.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            this.textLapOptions.Location = new System.Drawing.Point(2, 101);
            this.textLapOptions.Multiline = true;
            this.textLapOptions.Name = "textLapOptions";
            this.textLapOptions.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textLapOptions.Size = new System.Drawing.Size(476, 406);
            this.textLapOptions.TabIndex = 9;
            this.textLapOptions.TabStop = false;
            this.textLapOptions.Text = "Coming next!";
            this.textLapOptions.WordWrap = false;
            // 
            // comboLapOptions
            // 
            this.comboLapOptions.Items.Add("off");
            this.comboLapOptions.Items.Add("manual (press \"pause\")");
            this.comboLapOptions.Items.Add("distance-based");
            this.comboLapOptions.Items.Add("time-based");
            this.comboLapOptions.Location = new System.Drawing.Point(2, 4);
            this.comboLapOptions.Name = "comboLapOptions";
            this.comboLapOptions.Size = new System.Drawing.Size(476, 41);
            this.comboLapOptions.TabIndex = 5;
            // 
            // labelLapOptions1
            // 
            this.labelLapOptions1.Location = new System.Drawing.Point(2, 54);
            this.labelLapOptions1.Name = "labelLapOptions1";
            this.labelLapOptions1.Size = new System.Drawing.Size(203, 40);
            this.labelLapOptions1.Text = "Record lap every";
            // 
            // numericLapOptionsD
            // 
            this.numericLapOptionsD.Location = new System.Drawing.Point(225, 54);
            this.numericLapOptionsD.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.numericLapOptionsD.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.numericLapOptionsD.Name = "numericLapOptionsD";
            this.numericLapOptionsD.Size = new System.Drawing.Size(156, 36);
            this.numericLapOptionsD.TabIndex = 12;
            // 
            // numericLapOptionsT
            // 
            this.numericLapOptionsT.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericLapOptionsT.Location = new System.Drawing.Point(225, 54);
            this.numericLapOptionsT.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.numericLapOptionsT.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.numericLapOptionsT.Name = "numericLapOptionsT";
            this.numericLapOptionsT.Size = new System.Drawing.Size(156, 36);
            this.numericLapOptionsT.TabIndex = 4;
            // 
            // numericGpxTimeShift
            // 
            this.numericGpxTimeShift.Location = new System.Drawing.Point(320, 105);
            this.numericGpxTimeShift.Maximum = new decimal(new int[] {
            12,
            0,
            0,
            0});
            this.numericGpxTimeShift.Minimum = new decimal(new int[] {
            12,
            0,
            0,
            -2147483648});
            this.numericGpxTimeShift.Name = "numericGpxTimeShift";
            this.numericGpxTimeShift.Size = new System.Drawing.Size(156, 36);
            this.numericGpxTimeShift.TabIndex = 19;
            // 
            // labelGpxTimeShift
            // 
            this.labelGpxTimeShift.Location = new System.Drawing.Point(2, 105);
            this.labelGpxTimeShift.Name = "labelGpxTimeShift";
            this.labelGpxTimeShift.Size = new System.Drawing.Size(341, 40);
            this.labelGpxTimeShift.Text = "GPX time adjustment, hours";
            // 
            // checkKmlAlt
            // 
            this.checkKmlAlt.Location = new System.Drawing.Point(2, 5);
            this.checkKmlAlt.Name = "checkKmlAlt";
            this.checkKmlAlt.Size = new System.Drawing.Size(469, 40);
            this.checkKmlAlt.TabIndex = 17;
            this.checkKmlAlt.Text = "Save altitude to KML";
            // 
            // checkGpxRte
            // 
            this.checkGpxRte.Location = new System.Drawing.Point(2, 55);
            this.checkGpxRte.Name = "checkGpxRte";
            this.checkGpxRte.Size = new System.Drawing.Size(469, 40);
            this.checkGpxRte.TabIndex = 2;
            this.checkGpxRte.Text = "Save GPX with \"rte\" tag";
            // 
            // checkPlotTrackAsDots
            // 
            this.checkPlotTrackAsDots.Location = new System.Drawing.Point(2, 5);
            this.checkPlotTrackAsDots.Name = "checkPlotTrackAsDots";
            this.checkPlotTrackAsDots.Size = new System.Drawing.Size(476, 40);
            this.checkPlotTrackAsDots.TabIndex = 38;
            this.checkPlotTrackAsDots.Text = "Plot track as dots";
            // 
            // comboBoxKmlOptWidth
            // 
            this.comboBoxKmlOptWidth.Items.Add("2");
            this.comboBoxKmlOptWidth.Items.Add("4");
            this.comboBoxKmlOptWidth.Items.Add("6");
            this.comboBoxKmlOptWidth.Items.Add("8");
            this.comboBoxKmlOptWidth.Items.Add("10");
            this.comboBoxKmlOptWidth.Items.Add("12");
            this.comboBoxKmlOptWidth.Items.Add("14");
            this.comboBoxKmlOptWidth.Items.Add("16");
            this.comboBoxKmlOptWidth.Location = new System.Drawing.Point(360, 55);
            this.comboBoxKmlOptWidth.Name = "comboBoxKmlOptWidth";
            this.comboBoxKmlOptWidth.Size = new System.Drawing.Size(117, 41);
            this.comboBoxKmlOptWidth.TabIndex = 30;
            // 
            // comboBoxKmlOptColor
            // 
            this.comboBoxKmlOptColor.Items.Add("blue");
            this.comboBoxKmlOptColor.Items.Add("red");
            this.comboBoxKmlOptColor.Items.Add("green");
            this.comboBoxKmlOptColor.Items.Add("yellow");
            this.comboBoxKmlOptColor.Items.Add("white");
            this.comboBoxKmlOptColor.Items.Add("black");
            this.comboBoxKmlOptColor.Items.Add("gray");
            this.comboBoxKmlOptColor.Items.Add("orange");
            this.comboBoxKmlOptColor.Items.Add("sky blue");
            this.comboBoxKmlOptColor.Items.Add("brown");
            this.comboBoxKmlOptColor.Items.Add("purple");
            this.comboBoxKmlOptColor.Items.Add("violet");
            this.comboBoxKmlOptColor.Location = new System.Drawing.Point(109, 55);
            this.comboBoxKmlOptColor.Name = "comboBoxKmlOptColor";
            this.comboBoxKmlOptColor.Size = new System.Drawing.Size(171, 41);
            this.comboBoxKmlOptColor.TabIndex = 29;
            // 
            // labelMultiMaps
            // 
            this.labelMultiMaps.Location = new System.Drawing.Point(2, 257);
            this.labelMultiMaps.Name = "labelMultiMaps";
            this.labelMultiMaps.Size = new System.Drawing.Size(133, 40);
            this.labelMultiMaps.Text = "Multi-maps";
            // 
            // labelMapDownload
            // 
            this.labelMapDownload.Location = new System.Drawing.Point(2, 307);
            this.labelMapDownload.Name = "labelMapDownload";
            this.labelMapDownload.Size = new System.Drawing.Size(133, 40);
            this.labelMapDownload.Text = "Download";
            // 
            // comboMultiMaps
            // 
            this.comboMultiMaps.Items.Add("off");
            this.comboMultiMaps.Items.Add("2 maps, 1x zoom");
            this.comboMultiMaps.Items.Add("2 maps, 2x zoom");
            this.comboMultiMaps.Items.Add("2 maps, 4x zoom");
            this.comboMultiMaps.Items.Add("3 maps, 1x zoom");
            this.comboMultiMaps.Items.Add("3 maps, 2x zoom");
            this.comboMultiMaps.Items.Add("3 maps, 4x zoom");
            this.comboMultiMaps.Items.Add("4 maps, 1x zoom");
            this.comboMultiMaps.Items.Add("4 maps, 2x zoom");
            this.comboMultiMaps.Items.Add("4 maps, 4x zoom");
            this.comboMultiMaps.Items.Add("6 maps, 1x zoom");
            this.comboMultiMaps.Items.Add("6 maps, 2x zoom");
            this.comboMultiMaps.Items.Add("6 maps, 4x zoom");
            this.comboMultiMaps.Items.Add("8 maps, 1x zoom");
            this.comboMultiMaps.Items.Add("8 maps, 2x zoom");
            this.comboMultiMaps.Items.Add("8 maps, 4x zoom");
            this.comboMultiMaps.Location = new System.Drawing.Point(141, 255);
            this.comboMultiMaps.Name = "comboMultiMaps";
            this.comboMultiMaps.Size = new System.Drawing.Size(336, 41);
            this.comboMultiMaps.TabIndex = 34;
            // 
            // comboMapDownload
            // 
            this.comboMapDownload.Items.Add("off");
            this.comboMapDownload.Items.Add("Osmarender");
            this.comboMapDownload.Items.Add("Mapnik");
            this.comboMapDownload.Items.Add("Cyclemap (CloudMade)");
            this.comboMapDownload.Items.Add("OpenPisteMap");
            this.comboMapDownload.Items.Add("CloudMade Web style");
            this.comboMapDownload.Items.Add("CloudMade Mobile style");
            this.comboMapDownload.Items.Add("CloudMade NoNames style");
            this.comboMapDownload.Items.Add("User-defined in osm_server.txt");
            this.comboMapDownload.Location = new System.Drawing.Point(141, 305);
            this.comboMapDownload.Name = "comboMapDownload";
            this.comboMapDownload.Size = new System.Drawing.Size(336, 41);
            this.comboMapDownload.TabIndex = 35;
            this.comboMapDownload.SelectedIndexChanged += new System.EventHandler(this.comboMapDownload_SelectedIndexChanged);
            // 
            // labelKmlOpt1
            // 
            this.labelKmlOpt1.Location = new System.Drawing.Point(2, 57);
            this.labelKmlOpt1.Name = "labelKmlOpt1";
            this.labelKmlOpt1.Size = new System.Drawing.Size(105, 40);
            this.labelKmlOpt1.Text = "Track";
            // 
            // labelKmlOpt2
            // 
            this.labelKmlOpt2.Location = new System.Drawing.Point(285, 57);
            this.labelKmlOpt2.Name = "labelKmlOpt2";
            this.labelKmlOpt2.Size = new System.Drawing.Size(76, 40);
            this.labelKmlOpt2.Text = "width";
            // 
            // comboBoxLine2OptWidth
            // 
            this.comboBoxLine2OptWidth.Items.Add("2");
            this.comboBoxLine2OptWidth.Items.Add("4");
            this.comboBoxLine2OptWidth.Items.Add("6");
            this.comboBoxLine2OptWidth.Items.Add("8");
            this.comboBoxLine2OptWidth.Items.Add("10");
            this.comboBoxLine2OptWidth.Items.Add("12");
            this.comboBoxLine2OptWidth.Items.Add("14");
            this.comboBoxLine2OptWidth.Items.Add("16");
            this.comboBoxLine2OptWidth.Location = new System.Drawing.Point(360, 155);
            this.comboBoxLine2OptWidth.Name = "comboBoxLine2OptWidth";
            this.comboBoxLine2OptWidth.Size = new System.Drawing.Size(117, 41);
            this.comboBoxLine2OptWidth.TabIndex = 39;
            // 
            // comboBoxLine2OptColor
            // 
            this.comboBoxLine2OptColor.Items.Add("blue");
            this.comboBoxLine2OptColor.Items.Add("red");
            this.comboBoxLine2OptColor.Items.Add("green");
            this.comboBoxLine2OptColor.Items.Add("yellow");
            this.comboBoxLine2OptColor.Items.Add("white");
            this.comboBoxLine2OptColor.Items.Add("black");
            this.comboBoxLine2OptColor.Items.Add("gray");
            this.comboBoxLine2OptColor.Items.Add("orange");
            this.comboBoxLine2OptColor.Items.Add("sky blue");
            this.comboBoxLine2OptColor.Items.Add("brown");
            this.comboBoxLine2OptColor.Items.Add("purple");
            this.comboBoxLine2OptColor.Items.Add("violet");
            this.comboBoxLine2OptColor.Location = new System.Drawing.Point(109, 155);
            this.comboBoxLine2OptColor.Name = "comboBoxLine2OptColor";
            this.comboBoxLine2OptColor.Size = new System.Drawing.Size(171, 41);
            this.comboBoxLine2OptColor.TabIndex = 38;
            // 
            // labelLine2Opt1
            // 
            this.labelLine2Opt1.Location = new System.Drawing.Point(2, 157);
            this.labelLine2Opt1.Name = "labelLine2Opt1";
            this.labelLine2Opt1.Size = new System.Drawing.Size(105, 40);
            this.labelLine2Opt1.Text = "2nd line";
            // 
            // labelLine2Opt2
            // 
            this.labelLine2Opt2.Location = new System.Drawing.Point(285, 157);
            this.labelLine2Opt2.Name = "labelLine2Opt2";
            this.labelLine2Opt2.Size = new System.Drawing.Size(76, 40);
            this.labelLine2Opt2.Text = "width";
            // 
            // labelCw2
            // 
            this.labelCw2.Location = new System.Drawing.Point(2, 95);
            this.labelCw2.Name = "labelCw2";
            this.labelCw2.Size = new System.Drawing.Size(140, 40);
            this.labelCw2.Text = "Password:";
            // 
            // labelCw1
            // 
            this.labelCw1.Location = new System.Drawing.Point(2, 45);
            this.labelCw1.Name = "labelCw1";
            this.labelCw1.Size = new System.Drawing.Size(146, 40);
            this.labelCw1.Text = "User name:";
            // 
            // textBoxCw2
            // 
            this.textBoxCw2.Location = new System.Drawing.Point(169, 95);
            this.textBoxCw2.Name = "textBoxCw2";
            this.textBoxCw2.Size = new System.Drawing.Size(308, 41);
            this.textBoxCw2.TabIndex = 2;
            // 
            // textBoxCw1
            // 
            this.textBoxCw1.Location = new System.Drawing.Point(169, 45);
            this.textBoxCw1.Name = "textBoxCw1";
            this.textBoxCw1.Size = new System.Drawing.Size(308, 41);
            this.textBoxCw1.TabIndex = 1;
            // 
            // labelCwInfo
            // 
            this.labelCwInfo.Location = new System.Drawing.Point(2, 307);
            this.labelCwInfo.Name = "labelCwInfo";
            this.labelCwInfo.Size = new System.Drawing.Size(474, 40);
            this.labelCwInfo.Text = "Visit www.crossingways.com for all info";
            // 
            // labelCwLogMode
            // 
            this.labelCwLogMode.Location = new System.Drawing.Point(2, 192);
            this.labelCwLogMode.Name = "labelCwLogMode";
            this.labelCwLogMode.Size = new System.Drawing.Size(160, 40);
            this.labelCwLogMode.Text = "Live logging:";
            // 
            // comboBoxCwLogMode
            // 
            this.comboBoxCwLogMode.Items.Add("off");
            this.comboBoxCwLogMode.Items.Add("1 min");
            this.comboBoxCwLogMode.Items.Add("5 min");
            this.comboBoxCwLogMode.Items.Add("10 min");
            this.comboBoxCwLogMode.Items.Add("20 min");
            this.comboBoxCwLogMode.Items.Add("30 min");
            this.comboBoxCwLogMode.Items.Add("60 min");
            this.comboBoxCwLogMode.Location = new System.Drawing.Point(169, 190);
            this.comboBoxCwLogMode.Name = "comboBoxCwLogMode";
            this.comboBoxCwLogMode.Size = new System.Drawing.Size(308, 41);
            this.comboBoxCwLogMode.TabIndex = 5;
            // 
            // panelCwLogo
            // 
            this.panelCwLogo.Location = new System.Drawing.Point(60, 355);
            this.panelCwLogo.Name = "panelCwLogo";
            this.panelCwLogo.Size = new System.Drawing.Size(353, 103);
            this.panelCwLogo.Paint += new System.Windows.Forms.PaintEventHandler(this.panelCwLogo_Paint);
            // 
            // checkMapsWhiteBk
            // 
            this.checkMapsWhiteBk.Location = new System.Drawing.Point(2, 205);
            this.checkMapsWhiteBk.Name = "checkMapsWhiteBk";
            this.checkMapsWhiteBk.Size = new System.Drawing.Size(469, 40);
            this.checkMapsWhiteBk.TabIndex = 45;
            this.checkMapsWhiteBk.Text = "White background";
            this.checkMapsWhiteBk.Click += new System.EventHandler(this.checkMapsWhiteBk_Click);
            // 
            // checkPlotLine2AsDots
            // 
            this.checkPlotLine2AsDots.Location = new System.Drawing.Point(2, 105);
            this.checkPlotLine2AsDots.Name = "checkPlotLine2AsDots";
            this.checkPlotLine2AsDots.Size = new System.Drawing.Size(469, 40);
            this.checkPlotLine2AsDots.TabIndex = 41;
            this.checkPlotLine2AsDots.Text = "Plot 2nd line as dots";
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
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabPageOptions);
            this.tabControl.Controls.Add(this.tabPageGps);
            this.tabControl.Controls.Add(this.tabPageMainScr);
            this.tabControl.Controls.Add(this.tabPageMapScr);
            this.tabControl.Controls.Add(this.tabPageKmlGpx);
            this.tabControl.Controls.Add(this.tabPageLiveLog);
            this.tabControl.Controls.Add(this.tabPageLaps);
            this.tabControl.Controls.Add(this.tabPageAbout);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.None;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(480, 507);
            this.tabControl.TabIndex = 54;
            // 
            // tabPageOptions
            // 
            this.tabPageOptions.Controls.Add(this.checkOptMain);
            this.tabPageOptions.Controls.Add(this.checkOptKmlGpx);
            this.tabPageOptions.Controls.Add(this.checkOptAbout);
            this.tabPageOptions.Controls.Add(this.checkOptLiveLog);
            this.tabPageOptions.Controls.Add(this.checkOptLaps);
            this.tabPageOptions.Controls.Add(this.checkOptMaps);
            this.tabPageOptions.Controls.Add(this.labelOptText);
            this.tabPageOptions.Controls.Add(this.checkOptGps);
            this.tabPageOptions.Location = new System.Drawing.Point(0, 0);
            this.tabPageOptions.Name = "tabPageOptions";
            this.tabPageOptions.Size = new System.Drawing.Size(480, 463);
            this.tabPageOptions.Text = "Options:";
            // 
            // tabPageGps
            // 
            this.tabPageGps.Controls.Add(this.checkStopOnLow);
            this.tabPageGps.Controls.Add(this.comboGpsPoll);
            this.tabPageGps.Controls.Add(this.labelGpsActivity);
            this.tabPageGps.Controls.Add(this.checkBoxUseGccDll);
            this.tabPageGps.Controls.Add(this.comboBoxUseGccDllRate);
            this.tabPageGps.Controls.Add(this.comboBoxUseGccDllCom);
            this.tabPageGps.Controls.Add(this.labelGpsBaudRate);
            this.tabPageGps.Controls.Add(this.numericGeoID);
            this.tabPageGps.Controls.Add(this.labelGeoID);
            this.tabPageGps.Location = new System.Drawing.Point(0, 0);
            this.tabPageGps.Name = "tabPageGps";
            this.tabPageGps.Size = new System.Drawing.Size(472, 469);
            this.tabPageGps.Text = "GPS";
            // 
            // tabPageMainScr
            // 
            this.tabPageMainScr.Controls.Add(this.comboUnits);
            this.tabPageMainScr.Controls.Add(this.labelUnits);
            this.tabPageMainScr.Controls.Add(this.checkExStopTime);
            this.tabPageMainScr.Controls.Add(this.checkEditFileName);
            this.tabPageMainScr.Controls.Add(this.checkShowBkOff);
            this.tabPageMainScr.Controls.Add(this.checkRelativeAlt);
            this.tabPageMainScr.Location = new System.Drawing.Point(0, 0);
            this.tabPageMainScr.Name = "tabPageMainScr";
            this.tabPageMainScr.Size = new System.Drawing.Size(472, 469);
            this.tabPageMainScr.Text = "Main screen";
            // 
            // tabPageMapScr
            // 
            this.tabPageMapScr.Controls.Add(this.checkMapsWhiteBk);
            this.tabPageMapScr.Controls.Add(this.labelLine2Opt2);
            this.tabPageMapScr.Controls.Add(this.labelKmlOpt2);
            this.tabPageMapScr.Controls.Add(this.checkPlotLine2AsDots);
            this.tabPageMapScr.Controls.Add(this.labelLine2Opt1);
            this.tabPageMapScr.Controls.Add(this.comboBoxLine2OptWidth);
            this.tabPageMapScr.Controls.Add(this.comboBoxLine2OptColor);
            this.tabPageMapScr.Controls.Add(this.checkPlotTrackAsDots);
            this.tabPageMapScr.Controls.Add(this.comboBoxKmlOptWidth);
            this.tabPageMapScr.Controls.Add(this.comboBoxKmlOptColor);
            this.tabPageMapScr.Controls.Add(this.labelKmlOpt1);
            this.tabPageMapScr.Controls.Add(this.labelMultiMaps);
            this.tabPageMapScr.Controls.Add(this.labelMapDownload);
            this.tabPageMapScr.Controls.Add(this.comboMultiMaps);
            this.tabPageMapScr.Controls.Add(this.comboMapDownload);
            this.tabPageMapScr.Location = new System.Drawing.Point(0, 0);
            this.tabPageMapScr.Name = "tabPageMapScr";
            this.tabPageMapScr.Size = new System.Drawing.Size(472, 469);
            this.tabPageMapScr.Text = "Map screen";
            // 
            // tabPageKmlGpx
            // 
            this.tabPageKmlGpx.Controls.Add(this.numericGpxTimeShift);
            this.tabPageKmlGpx.Controls.Add(this.labelGpxTimeShift);
            this.tabPageKmlGpx.Controls.Add(this.checkKmlAlt);
            this.tabPageKmlGpx.Controls.Add(this.checkGpxRte);
            this.tabPageKmlGpx.Location = new System.Drawing.Point(0, 0);
            this.tabPageKmlGpx.Name = "tabPageKmlGpx";
            this.tabPageKmlGpx.Size = new System.Drawing.Size(480, 463);
            this.tabPageKmlGpx.Text = "Kml/Gpx";
            // 
            // tabPageLiveLog
            // 
            this.tabPageLiveLog.Controls.Add(this.checkUploadGpx);
            this.tabPageLiveLog.Controls.Add(this.labelCw2);
            this.tabPageLiveLog.Controls.Add(this.labelCw1);
            this.tabPageLiveLog.Controls.Add(this.textBoxCw2);
            this.tabPageLiveLog.Controls.Add(this.textBoxCw1);
            this.tabPageLiveLog.Controls.Add(this.labelCwInfo);
            this.tabPageLiveLog.Controls.Add(this.labelCwLogMode);
            this.tabPageLiveLog.Controls.Add(this.comboBoxCwLogMode);
            this.tabPageLiveLog.Controls.Add(this.panelCwLogo);
            this.tabPageLiveLog.Location = new System.Drawing.Point(0, 0);
            this.tabPageLiveLog.Name = "tabPageLiveLog";
            this.tabPageLiveLog.Size = new System.Drawing.Size(480, 463);
            this.tabPageLiveLog.Text = "Live log";
            // 
            // checkUploadGpx
            // 
            this.checkUploadGpx.Location = new System.Drawing.Point(6, 250);
            this.checkUploadGpx.Name = "checkUploadGpx";
            this.checkUploadGpx.Size = new System.Drawing.Size(469, 40);
            this.checkUploadGpx.TabIndex = 18;
            this.checkUploadGpx.Text = "Upload GPX file (after saving)";
            // 
            // tabPageLaps
            // 
            this.tabPageLaps.Controls.Add(this.labelLapOptions2);
            this.tabPageLaps.Controls.Add(this.textLapOptions);
            this.tabPageLaps.Controls.Add(this.comboLapOptions);
            this.tabPageLaps.Controls.Add(this.labelLapOptions1);
            this.tabPageLaps.Controls.Add(this.numericLapOptionsD);
            this.tabPageLaps.Controls.Add(this.numericLapOptionsT);
            this.tabPageLaps.Location = new System.Drawing.Point(0, 0);
            this.tabPageLaps.Name = "tabPageLaps";
            this.tabPageLaps.Size = new System.Drawing.Size(472, 469);
            this.tabPageLaps.Text = "Lap stats";
            // 
            // tabPageAbout
            // 
            this.tabPageAbout.Controls.Add(this.labelRevision);
            this.tabPageAbout.Location = new System.Drawing.Point(0, 0);
            this.tabPageAbout.Name = "tabPageAbout";
            this.tabPageAbout.Size = new System.Drawing.Size(472, 469);
            this.tabPageAbout.Text = "About";
            this.tabPageAbout.Paint += new System.Windows.Forms.PaintEventHandler(this.tabAbout_Paint);
            // 
            // Form1
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(480, 588);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.tabBlank);
            this.Controls.Add(this.tabBlank1);
            this.Controls.Add(this.tabEnterFileName);
            this.Controls.Add(this.tabOpenFile);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Location = new System.Drawing.Point(0, 52);
            this.Name = "Form1";
            this.Text = "GPS Cycle Computer";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Closed += new System.EventHandler(this.Form1_Closed);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.Form1_Closing);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            this.tabEnterFileName.ResumeLayout(false);
            this.tabOpenFile.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.tabPageOptions.ResumeLayout(false);
            this.tabPageGps.ResumeLayout(false);
            this.tabPageMainScr.ResumeLayout(false);
            this.tabPageMapScr.ResumeLayout(false);
            this.tabPageKmlGpx.ResumeLayout(false);
            this.tabPageLiveLog.ResumeLayout(false);
            this.tabPageLaps.ResumeLayout(false);
            this.tabPageAbout.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        // The main entry point for the application.
        static void Main(string[] args)
        {
            // get the input file name, if supplied
            if (args.Length != 0)
            {
                if (File.Exists(args[0]))
                {
                    Form1.FirstArgument = args[0];
                }
            }

            Application.Run(new Form1());
        }

        // Create GPS event handlers on form load
        private void Form1_Load(object sender, System.EventArgs e)
        {
            // load settings -----------------
            string CurrentDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            IoFilesDirectory = CurrentDirectory;
            MapsFilesDirectory = CurrentDirectory + "\\maps";

            string file_name = "GpsCycleComputer.dat";
            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }
            if (CurrentDirectory != "")
            { file_name = CurrentDirectory + "\\" + file_name; }

            try
            {
                FileStream fs = new FileStream(file_name, FileMode.OpenOrCreate);
                BinaryReader wr = new BinaryReader(fs, Encoding.ASCII);

                if (wr.PeekChar() != -1) { comboGpsPoll.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { comboUnits.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { wr.ReadInt32(); }  // not used anymore
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkExStopTime.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkStopOnLow.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkGpxRte.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); numericGeoID.Value = i; }

                // load IoFilesDirectory
                int str_len = 0;
                string saved_name = "";
                if (wr.PeekChar() != -1) { str_len = wr.ReadInt32(); }
                for (int i = 0; i < str_len; i++)
                {
                    if (wr.PeekChar() != -1) { saved_name += (char)(wr.ReadInt32()); }
                }
                if (saved_name != "")
                {
                    IoFilesDirectory = saved_name;
                    CheckIoDirectoryExists();
                }
                // more options
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkKmlAlt.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkEditFileName.Checked = (i == 1); }

                // kml line
                if (wr.PeekChar() != -1) { comboBoxKmlOptColor.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { comboBoxKmlOptWidth.SelectedIndex = wr.ReadInt32(); }

                // GCC DLL
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkBoxUseGccDll.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { comboBoxUseGccDllRate.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { comboBoxUseGccDllCom.SelectedIndex = wr.ReadInt32(); }

                // and more ...
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkShowBkOff.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkRelativeAlt.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { comboMultiMaps.SelectedIndex = wr.ReadInt32(); }

                // load MapsFilesDirectory
                str_len = 0;
                saved_name = "";
                if (wr.PeekChar() != -1) { str_len = wr.ReadInt32(); }
                for (int i = 0; i < str_len; i++)
                {
                    if (wr.PeekChar() != -1) { saved_name += (char)(wr.ReadInt32()); }
                }
                if (saved_name != "")
                {
                    MapsFilesDirectory = saved_name;
                    CheckMapsDirectoryExists();
                }

                // ---------- Crossingways option ----------------
                // load CW username
                str_len = 0;
                saved_name = "";
                if (wr.PeekChar() != -1) { str_len = wr.ReadInt32(); }
                for (int i = 0; i < str_len; i++)
                {
                    if (wr.PeekChar() != -1) { saved_name += (char)(wr.ReadInt32()); }
                }
                if (saved_name != "") 
                    { textBoxCw1.Text = saved_name; }

                // load CW hashed password
                str_len = 0;
                saved_name = "";
                if (wr.PeekChar() != -1) { str_len = wr.ReadInt32(); }
                for (int i = 0; i < str_len; i++)
                {
                    if (wr.PeekChar() != -1) { saved_name += (char)(wr.ReadInt32()); }
                }
                if (saved_name != "")
                    { CwHashPassword = saved_name; textBoxCw2.Text = "******"; }

                // Live logging option
                if (wr.PeekChar() != -1) { comboBoxCwLogMode.SelectedIndex = wr.ReadInt32(); }

                // 2nd line and "dots" options
                if (wr.PeekChar() != -1) { comboBoxLine2OptWidth.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { comboBoxLine2OptColor.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkPlotTrackAsDots.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkPlotLine2AsDots.Checked = (i == 1); }

                // pages to show
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkOptAbout.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkOptLiveLog.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkOptMaps.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkOptGps.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkOptKmlGpx.Checked = (i == 1); }

                // GPX, Map Bkgrd and Last ext to open, etc
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); numericGpxTimeShift.Value = i; }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkMapsWhiteBk.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { FileExtentionToOpen = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkOptLaps.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { comboMapDownload.SelectedIndex = wr.ReadInt32(); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkOptMain.Checked = (i == 1); }
                if (wr.PeekChar() != -1) { int i = wr.ReadInt32(); checkUploadGpx.Checked = (i == 1); }

                wr.Close();
                fs.Close();
            }
            catch (Exception /*e*/) { }

            // now allow to save setting on combo change
            DoNotSaveSettingsFlag = false;

            // check if there are any args - then load the file
            if (File.Exists(FirstArgument))
            {
                IoFilesDirectory = Path.GetDirectoryName(FirstArgument);
                LoadGcc(FirstArgument);
            }

            // send indication to GPS driver to wake-up (if it is OFF)
            gps.startGpsService();

            // load maps
            CheckMapsDirectoryExists();
            mapUtil.LoadMaps(MapsFilesDirectory);
            LastOsmMapDownloadIndex = comboMapDownload.SelectedIndex;

            file_name = "osm_server.txt";
            if (CurrentDirectory != "") { file_name = CurrentDirectory + "\\" + file_name; }
            mapUtil.LoadCustomOsmServer(file_name);

            // select option pages to show and apply map bkground option
            FillPagesToShow();
            checkMapsWhiteBk_Click(checkMapsWhiteBk, EventArgs.Empty);
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
                File.Copy(TempFileName, CurrentFileName, true);
                saveCsvLog();
                } catch (Exception /*e*/) { }
            }
        }

        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            
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
            BinaryWriter wr = new BinaryWriter(fs, Encoding.ASCII);

            wr.Write((int)comboGpsPoll.SelectedIndex);
            wr.Write((int)comboUnits.SelectedIndex);
            wr.Write((int)0); // not used anymore
            wr.Write((int)(checkExStopTime.Checked ? 1 : 0));
            wr.Write((int)(checkStopOnLow.Checked ? 1 : 0));
            wr.Write((int)(checkGpxRte.Checked ? 1 : 0));
            wr.Write((int)(0.5 + Decimal.ToDouble(numericGeoID.Value)));

            // the best bit: save IoFilesDirectory as length and chars
            wr.Write((int)(IoFilesDirectory.Length));
            for (int i = 0; i < IoFilesDirectory.Length; i++)
            {
                wr.Write((int)IoFilesDirectory[i]);
            }
            wr.Write((int)(checkKmlAlt.Checked ? 1 : 0));
            wr.Write((int)(checkEditFileName.Checked ? 1 : 0));

            // kml line
            wr.Write((int)comboBoxKmlOptColor.SelectedIndex);
            wr.Write((int)comboBoxKmlOptWidth.SelectedIndex);

            // GCC DLL
            wr.Write((int)(checkBoxUseGccDll.Checked ? 1 : 0));
            wr.Write((int)comboBoxUseGccDllRate.SelectedIndex);
            wr.Write((int)comboBoxUseGccDllCom.SelectedIndex);

            // and more ...
            wr.Write((int)(checkShowBkOff.Checked ? 1 : 0));
            wr.Write((int)(checkRelativeAlt.Checked ? 1 : 0));
            wr.Write((int)comboMultiMaps.SelectedIndex);

            // save MapsFilesDirectory as length and chars
            wr.Write((int)(MapsFilesDirectory.Length));
            for (int i = 0; i < MapsFilesDirectory.Length; i++)
            {
                wr.Write((int)MapsFilesDirectory[i]);
            }

            // ---------- Crossingways option ----------------
            // save  textBoxCw1.Text as length and chars
            wr.Write((int)(textBoxCw1.Text.Length));
            for (int i = 0; i < textBoxCw1.Text.Length; i++)
            {
                wr.Write((int)textBoxCw1.Text[i]);
            }
            // save  CwHashPassword as length and chars
            wr.Write((int)(CwHashPassword.Length));
            for (int i = 0; i < CwHashPassword.Length; i++)
            {
                wr.Write((int)CwHashPassword[i]);
            }
            // live logging options
            wr.Write((int)comboBoxCwLogMode.SelectedIndex);

            // 2nd line and "dots" options
            wr.Write((int)comboBoxLine2OptWidth.SelectedIndex);
            wr.Write((int)comboBoxLine2OptColor.SelectedIndex);
            wr.Write((int)(checkPlotTrackAsDots.Checked ? 1 : 0));
            wr.Write((int)(checkPlotLine2AsDots.Checked ? 1 : 0));

            // pages to show
            wr.Write((int)(checkOptAbout.Checked ? 1 : 0));
            wr.Write((int)(checkOptLiveLog.Checked ? 1 : 0));
            wr.Write((int)(checkOptMaps.Checked ? 1 : 0));
            wr.Write((int)(checkOptGps.Checked ? 1 : 0));
            wr.Write((int)(checkOptKmlGpx.Checked ? 1 : 0));

            // GPX, Map Bkgrd and Last ext to open
            wr.Write((int)(0.5 + Decimal.ToDouble(numericGpxTimeShift.Value)));
            wr.Write((int)(checkMapsWhiteBk.Checked ? 1 : 0));
            wr.Write((int)FileExtentionToOpen);
            wr.Write((int)(checkOptLaps.Checked ? 1 : 0));
            wr.Write((int)comboMapDownload.SelectedIndex);
            wr.Write((int)(checkOptMain.Checked ? 1 : 0));
            wr.Write((int)(checkUploadGpx.Checked ? 1 : 0));

            wr.Close();
            fs.Close();
            } catch (Exception /*e*/) { }
        }

        // main logging function to receive date from GPS
        private void GetGpsData()
        {
            GpsDataOk = false;
            bool time_check_passed = false;

            if (gps.Opened)
            {
                position = gps.GetPosition();

                if (position != null)
                {
                    if (position.LatitudeValid && position.LongitudeValid
                        && position.TimeValid)
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
                            StartBattery = Utils.GetBatteryStatus();

                            try
                            {
                                WriteStartDateTime();
                                writer.Write((double)position.Latitude);
                                writer.Write((double)position.Longitude);
                            }
                            catch (Exception /*e*/) { }

                            utmUtil.setReferencePoint(position.Latitude, position.Longitude);

                            WriteOptionsInfo();

                            // for maps, fill x/y values realtive to the starting point
                            ResetMapPosition();
                        }
                        // write battery info every 3 min
                        WriteBatteryInfo();

                        WriteRecord(position);

                        DoLiveLogging();

                        Counter++;
                        GpsDataOk = true;
                        GpsSearchCount = 0;
                    }
                }
            }
            // if not opened
            else
            {
                CurrentStatusString = "trying to open port";
            }

            // indication that value was not set ...
            if (!GpsDataOk)
            {
                if (position != null)
                {
                    CurrentStatusString = "searching " +
                                           (position.LatitudeValid ? "P" : "") +
                                           (position.SpeedValid ? "S" : "") +
                                           (position.TimeValid ? "T" : "") +
                                           (time_check_passed ? "T+" : "") +
                                           " " + GpsSearchCount;
                }
                else
                {
                    CurrentStatusString = "searching " + GpsSearchCount;
                }
                GpsSearchCount++;
                CurrentGpsLedColor = Color.Red;
            }
            else
            {
                if (!StoppedOnLow) { CurrentStatusString = "gps on"; }
                CurrentGpsLedColor = Color.Green;
            }

            if (BufferDrawMode == BufferDrawModeMain) { NoBkPanel.Invalidate(); }
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
            UInt16 v_int = (UInt16)((pos.SpeedValid ? pos.Speed : 0.0) * 1.852 * 10.0); writer.Write((UInt16)v_int);

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
            CurrentLat = pos.Latitude;
            CurrentLong = pos.Longitude;
            CurrentZ = z_int;
            CurrentSpeed = v_int / 10.0;

            // store data
            AddPlotData((float)CurrentLat, (float)CurrentLong, z_int, t_int, v_int);
            } catch (Exception /*e*/) { }
        }

        private void AddPlotData(float lat, float lng, Int16 z, UInt16 t, UInt16 s)
        {
            // check if we need to increase decimation level
            if(Counter >= PlotDataSize*Decimation)
            {
                for(int i = 0; i < PlotDataSize/2; i++)
                {
                    PlotLat[i] = PlotLat[i * 2];
                    PlotLong[i] = PlotLong[i * 2];
                    PlotZ[i] = PlotZ[i*2];
                    PlotT[i] = PlotT[i*2];
                    PlotS[i] = PlotS[i*2];
                }
                Decimation *= 2;
            }

            PlotLat[Counter / Decimation] = lat;
            PlotLong[Counter / Decimation] = lng;
            PlotZ[Counter/Decimation] = z;
            PlotT[Counter/Decimation] = t;
            PlotS[Counter/Decimation] = s;
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
        private void WriteBatteryInfo()
        {
            if (Counter != 0)
            {
                TimeSpan maxAge = new TimeSpan(0, 3, 0); // 3 min
                if ((LastBatterySave + maxAge) >= DateTime.UtcNow)
                { return; }
            }

            LastBatterySave = DateTime.UtcNow;

            CurrentBattery = Utils.GetBatteryStatus();
            Int16 x = (Int16) CurrentBattery;

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
                    CurrentStatusString = "Stopped on low power";
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
            DateTime start_time = DateTime.Now;

            // file name is constructed as year,month,day, hour, min, sec, all as 2-digit values
            str = (start_time.Year - 2000).ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str;

            str = start_time.Month.ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str;

            str = start_time.Day.ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str;

            str = start_time.Hour.ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += "_" + str;

            str = start_time.Minute.ToString();
            if (str.Length < 2) { str = "0" + str; }
            file_name += str + ".gcc";

            CheckIoDirectoryExists();

            if (IoFilesDirectory == "\\") { file_name = "\\" + file_name; }
            else { file_name = IoFilesDirectory + "\\" + file_name; }

            CurrentFileName = file_name;

            return file_name;
        }

        // New Trace: open file, log start time, etc
        private void StartNewTrace()
        {
            StartTime = DateTime.Now;
            StartTimeUtc = DateTime.UtcNow;
            LastBatterySave = StartTimeUtc;
            LastLiveLogging = StartTimeUtc;

            // create writer and write header
            try {
            fstream = new FileStream(TempFileName, FileMode.Create);
            writer = new BinaryWriter(fstream, Encoding.ASCII);

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
        private void buttonEnterFileName_Click(object sender, EventArgs e)
        {
            // update the file name
            CurrentFileName = textBoxEnterFileName.Text + ".gcc";

            if (IoFilesDirectory == "\\") { CurrentFileName = "\\" + CurrentFileName; }
            else { CurrentFileName = IoFilesDirectory + "\\" + CurrentFileName; }

            tabEnterFileName.SendToBack();
            tabEnterFileName.Height = tabControl.Height;
            inputPanel.Enabled = false;

            DoStart();
        }

        private void buttonStart_Click()
        {
            GenerateFileName();

            // check if we need to show the custom file name panel, or can start loging with default name
            if (checkEditFileName.Checked)
            {
                textBoxEnterFileName.Text = Path.GetFileNameWithoutExtension(CurrentFileName);
                tabEnterFileName.Height = this.Height;
                tabEnterFileName.BringToFront();
                inputPanel.Enabled = true;
                textBoxEnterFileName.Focus();
                textBoxEnterFileName.SelectionStart = textBoxEnterFileName.TextLength;
            }
            else
            {
                DoStart();
            }

        }

        // this is the function which actually starts logging
        private void DoStart()
        {
            buttonStop.Enabled = true;
            buttonStart.Enabled = false;

            buttonGraph.BringToFront();
            buttonPicPause.pressed = false;
            if (checkShowBkOff.Checked) { buttonPicBkOff.BringToFront(); }
            else                        { buttonPicPause.BringToFront(); }
            buttonStop.BringToFront();

            LockGpsTick = false;
            StoppedOnLow = false;

            GpsSearchCount = 0;
            CurrentStatusString = "gps off";
            CurrentLiveLoggingString = "";

            // activate code from GccDll, if option selected
            gps.setOptions(checkBoxUseGccDll.Checked,
                           comboBoxUseGccDllCom.SelectedIndex + 1, // as we number from 1
                           BaudRates[ comboBoxUseGccDllRate.SelectedIndex ]);

            StartNewTrace();

            if (!gps.Opened)
                { gps.Open(); }

            timerGps.Interval = 1000;
            timerGps.Enabled = true;

            timerIdleReset.Enabled = true;
    
            // this is what you need to keep tool running if power turned off
            if (checkShowBkOff.Checked == false)
            {
                try
                {
                    PowerPolicyNotify(0x0003, 1);
                    // need to update registry settings as well to keep GPS on
                    RegistryKey rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Unattended", true);
                    object tmp_obj = rk.GetValue("gpd0:");
                    if (tmp_obj != null) { SaveGpdUnattendedValue = (Int32)tmp_obj; }
                    else { SaveGpdUnattendedValue = 4; } // default is 4
                    rk.SetValue("gpd0:", 0, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                }
                catch (Exception /*e*/) { }
            }
        }
        private void buttonStop_Click()
        {
            LockGpsTick = true;
            timerGps.Enabled = false;
            timerIdleReset.Enabled = false;

            // this is what you need to keep tool running if power turned off
            if (checkShowBkOff.Checked == false)
            {
                try
                {
                    PowerPolicyNotify(0x0003, 0);
                    // need to restore registry settings
                    RegistryKey rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Unattended", true);
                    rk.SetValue("gpd0:", SaveGpdUnattendedValue, RegistryValueKind.DWord);
                }
                catch (Exception /*e*/) { }
            }

            buttonStop.Enabled = false;

            if (gps.Opened) { gps.Close(); }

            try {
            writer.Close();
            fstream.Close();
            } catch (Exception /*e*/) { }
            comboGpsPoll.Enabled = true;
            GpsSearchCount = 0;
            CurrentStatusString = "gps off";
            CurrentLiveLoggingString = "";
            CurrentGpsLedColor = bkColor;

            // reset move/zoom vars (as we switch from fixed zoom into auto zoom mode)
            mapUtil.ZoomValue = 1.0; mapUtil.ScreenShiftX = 0; mapUtil.ScreenShiftY = 0;
            MousePosX = 0;   MousePosY = 0;    MouseMoving = false;

            timerStartDelay.Enabled = true;
            Cursor.Current = Cursors.WaitCursor;
        }
        private void timerStartDelay_Tick(object sender, EventArgs e)
        {
            timerStartDelay.Enabled = false;

            buttonStart.Enabled = true;
            buttonOptions.BringToFront();
            buttonStart.BringToFront();
            buttonPicLoad.BringToFront();

            labelFileName.SetText("");

            // copy file into "permanent place", only if any records
            if (Counter > 0)
            { 
                File.Copy(TempFileName, CurrentFileName, true);
                saveCsvLog();
            }

            Cursor.Current = Cursors.Default;
        }

        // Switch Off backlight, set flag to flash stream
        private void buttonBklitOff_Click(object sender, EventArgs e)
        {
            Utils.SwitchBacklight();
        }

        // Pause toggle
        private void Form1_PauseMouseDown(object sender, MouseEventArgs e)
        {
            // show that the button is pressed
            buttonPicPause.pressed = true;
            buttonPicPause.Invalidate();

            if (StoppedOnLow == false)
            {
                timerGps.Enabled = !timerGps.Enabled;

                if (timerGps.Enabled == false)
                {
                    // if always-on, stop GPS
                    if (comboGpsPoll.SelectedIndex == 0)
                    {
                        if (gps.Opened) { gps.Close(); }
                    }

                    CurrentStatusString = "Pause button pressed";
                    buttonPicPause.pressed = true;   // lock on pressed
                }
                else
                {
                    // if always-on, start GPS. Print proper message string
                    if (comboGpsPoll.SelectedIndex == 0)
                    {
                        if (!gps.Opened) { gps.Open(); }
                        CurrentStatusString = "gps on";
                    }
                    else
                        { CurrentStatusString = "gps off for " + PollGpsTimeSec[comboGpsPoll.SelectedIndex] + " sec"; }

                    buttonPicPause.pressed = false;  // de-press to release pause
                }

                NoBkPanel.Invalidate();
            }
            else // if stopped, do not lock on pressed
            {
                buttonPicPause.pressed = false;
            }
        }
        private void Form1_PauseMouseUp(object sender, MouseEventArgs e)
        {
            buttonPicPause.Invalidate();
            if (buttonPicPause.pressed) { buttonOptions.BringToFront(); }
            else                        { buttonGraph.BringToFront(); }
        }

        // utils to fill gcc file names into the "listBoxFiles", indicating if KML/GPX exist
        private void FillFileNames()
        {
            string[] files = Directory.GetFiles(IoFilesDirectory, "*.gcc");
            Array.Sort(files);

            for (int i = (files.Length - 1); i >= 0; i--)
            {
                string kml_file = Path.GetFileNameWithoutExtension(files[i]) + ".kml";
                if (IoFilesDirectory == "\\") { kml_file = "\\" + kml_file; }
                else { kml_file = IoFilesDirectory + "\\" + kml_file; }

                string gpx_file = Path.GetFileNameWithoutExtension(files[i]) + ".gpx";
                if (IoFilesDirectory == "\\") { gpx_file = "\\" + gpx_file; }
                else { gpx_file = IoFilesDirectory + "\\" + gpx_file; }

                // add indication if KML or GPX files exists for this gcc file
                string status_string = "";
                if (File.Exists(kml_file)) { status_string += "*"; }
                if (File.Exists(gpx_file)) { status_string += "+"; }

                listBoxFiles.Items.Add(status_string + Path.GetFileName(files[i]));
            }
        }
        // read file
        private void buttonLoad_Click(object sender, EventArgs e)
        {
            FolderSetupMode = false;
            FileOpenMode = FileOpenMode_Gcc;

            listBoxFiles.Items.Clear();
            if (isLandscape == false)  // make smaller in portrait, to show buttons
                { listBoxFiles.Height = tabControl.Height - buttonUp.Height - 1; }
            listBoxFiles.BringToFront();

            CheckIoDirectoryExists();
            FillFileNames();

            if (isLandscape == false)
                { tabOpenFile.Height = tabControl.Height - buttonUp.Height; }
            tabOpenFile.BringToFront();

            buttonDialogOpen.BringToFront();
            buttonDialogCancel.BringToFront();
            buttonDown.BringToFront();
            buttonUp.BringToFront();
            buttonPicSaveKML.BringToFront();
            buttonPicSaveGPX.BringToFront();
            tabBlank1.SendToBack();

            if(listBoxFiles.Items.Count != 0)
                { listBoxFiles.SelectedIndex = 0; }

            // set index to the currently selected file
            for (int i = 0; i < listBoxFiles.Items.Count; i++)
            {
                string str = listBoxFiles.Items[i].ToString();
                str = str.Replace("+", ""); // remove * and + for the gpx/kml indication
                str = str.Replace("*", "");
                if (str == CurrentStatusString)
                    { listBoxFiles.SelectedIndex = i; break; }
            }
        }

        private bool LoadGcc(string filename)
        {
            // reset vars for computation
            Counter = 0;
            Decimation = 1;
            MaxSpeed = 0.0; Distance = 0.0; CurrentStoppageTimeSec = 0;
            OldX = 0.0; OldY = 0.0; OldT = 0;
            OriginShiftX = 0.0; OriginShiftY = 0.0;

            int gps_poll_sec = 0;

            CurrentFileName = filename;

            // preset label text for errors
            CurrentStatusString = "File has errors or blank";

            Cursor.Current = Cursors.WaitCursor;

            do
            {
                try
                {
                    FileStream fs = new FileStream(filename, FileMode.Open);
                    BinaryReader rd = new BinaryReader(fs, Encoding.ASCII);

                    // load header "GCC1" (1 is version)
                    if (rd.ReadChar() != 'G') break; if (rd.ReadChar() != 'C') break;
                    if (rd.ReadChar() != 'C') break; if (rd.ReadChar() != 1) break;

                    // read time as 6 bytes: year, month...
                    int t1 = (int)rd.ReadByte(); t1 += 2000;
                    int t2 = (int)rd.ReadByte(); int t3 = (int)rd.ReadByte();
                    int t4 = (int)rd.ReadByte(); int t5 = (int)rd.ReadByte();
                    int t6 = (int)rd.ReadByte();
                    StartTime = new DateTime(t1, t2, t3, t4, t5, t6);

                    // read lat/long
                    StartLat = rd.ReadDouble(); StartLong = rd.ReadDouble();
                    utmUtil.setReferencePoint(StartLat, StartLong);

                    bool is_battery_printed = false;

                    Int16 x_int = 0; Int16 y_int = 0; Int16 z_int = 0; UInt16 v_int = 0; UInt16 t_int = 0;
                    double out_lat = 0.0, out_long = 0.0;

                    while (rd.PeekChar() != -1)
                    {
                        // get 5 short ints
                        try
                        {
                            if (rd.PeekChar() != -1) { x_int = rd.ReadInt16(); } else { break; }
                            if (rd.PeekChar() != -1) { y_int = rd.ReadInt16(); } else { break; }
                            if (rd.PeekChar() != -1) { z_int = rd.ReadInt16(); } else { break; }
                            if (rd.PeekChar() != -1) { v_int = rd.ReadUInt16(); } else { break; }
                            if (rd.PeekChar() != -1) { t_int = rd.ReadUInt16(); } else { break; }
                        }
                        catch (Exception /*e*/) { break; }

                        // check if this is a special record
                        // battery: z_int = 1
                        if ((v_int == 0xFFFF) && (t_int == 0xFFFF) && (z_int == 1))
                        {
                            if (is_battery_printed == false)
                            {
                                StartBattery = x_int;
                                is_battery_printed = true;
                            }
                            CurrentBattery = x_int;
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

                            // convert to lat/long, used in plot arrays
                            utmUtil.getLatLong(real_x, real_y, out out_lat, out out_long);

                            // store data in plot array
                            AddPlotData((float)out_lat, (float)out_long, z_int, t_int, v_int);
                            Counter++;

                            try { rd.PeekChar(); }
                            catch (Exception /*e*/) { break; }
                        }
                    }

                    // store last point
                    CurrentTimeSec = t_int;
                    CurrentLat = out_lat;
                    CurrentLong = out_long;
                    CurrentZ = z_int;
                    CurrentSpeed = v_int / 10.0;

                    rd.Close();
                    fs.Close();

                    CurrentStatusString = Path.GetFileName(filename);

                    // for maps, fill x/y values realtive to the starting point
                    ResetMapPosition();
                }
                catch (Exception /*e*/) { }
            } while (false);
            Cursor.Current = Cursors.Default;

            if (CurrentStatusString == "File has errors or blank") { return false; }
            return true;
        }

        // reset Idle Timer (to stop phone switching off)
        private void timerIdleReset_Tick(object sender, EventArgs e)
        {
            SystemIdleTimerReset();

            // update graph, if selected
            if (BufferDrawMode == BufferDrawModeMaps) 
            {
                NoBkPanel.Invalidate();  
            }
            // if main screen, and not always active, update clock (i.e. let it repaint)
            else if (BufferDrawMode == BufferDrawModeMain)
            {
                if (comboGpsPoll.SelectedIndex != 0) { NoBkPanel.Invalidate(); }
            }
        }

        // start/stop GPS
        private void timerGps_Tick(object sender, EventArgs e)
        {
            if (LockGpsTick) { return; }

            // set a lock for this function (just in case it got stack in GPS calls)
            LockGpsTick = true;

            // permament GPS operation
            if (comboGpsPoll.SelectedIndex == 0)
            {
                // close and open GPS, if searching to too long - this might revive it!
                if ((GpsDataOk == false) && (GpsSearchCount > 120))
                {
                    if (gps.Opened) { gps.Close(); } // first we close it
                    else            { gps.Open(); GpsSearchCount = 0; } // then we open it, and reset count
                    LockGpsTick = false; 
                    return; 
                }

                GetGpsData(); 
                LockGpsTick = false; 
                return; 
            }

            // start/stop GPS operation
            if (gps.Opened)
            {
                GetGpsData();

                // if data was not valid, need to wait before disabling GPS
                // do not search for longer that 3 min (if e.g. temporary lost the signal) - go to sleep
                if ((GpsDataOk == false) && (GpsSearchCount < 180))
                    { LockGpsTick = false; return; }

                // set "long" interval
                timerGps.Interval = 1000 * PollGpsTimeSec[comboGpsPoll.SelectedIndex];

                gps.Close();
                if (!StoppedOnLow) { CurrentStatusString = "gps off for " + PollGpsTimeSec[comboGpsPoll.SelectedIndex] + " sec"; }

                LastPointUtc = DateTime.MinValue;
            }
            else
            {
                gps.Open();
                if (!StoppedOnLow) { CurrentStatusString = "gps opening ..."; }

                // set "short" interval
                timerGps.Interval = 1000;
                // reset search count
                GpsSearchCount = 0;
            }

            LockGpsTick = false;
        }

        #region PInvokes to coredll.dll
        [DllImport("coredll.dll")]
        static extern void SystemIdleTimerReset();

        [DllImport("coredll.dll")]
        static extern void PowerPolicyNotify(UInt32 powermode, UInt32 flags);
        #endregion

        private string replaceCommas(double x)
        {
            string output = x.ToString("0.##########");
            output = output.Replace(",", ".");
            return output;
        }

        private void buttonSaveKML_Click(object sender, EventArgs e)
        {
            if (listBoxFiles.SelectedIndex != -1)
            {
                string gcc_file = listBoxFiles.SelectedItem.ToString();
                gcc_file = gcc_file.Replace("*", ""); // remove * and + for the gpx/kml indication
                gcc_file = gcc_file.Replace("+", "");

                if (IoFilesDirectory == "\\") { gcc_file = "\\" + gcc_file; ; }
                else { gcc_file = IoFilesDirectory + "\\" + gcc_file; }

                if (!LoadGcc(gcc_file))
                {
                    MessageBox.Show("File has errors or blank", "Error loading .gcc file",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    return;
                }
            }
            else
            {
                MessageBox.Show("No files selected", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                return;
            }

            if (Counter == 0)
            {
                MessageBox.Show("File is blank - no records to save to KML", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                return;
            }
            
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                CheckIoDirectoryExists();
                
                string kml_file = Path.GetFileNameWithoutExtension(CurrentFileName) + ".kml";

                if (IoFilesDirectory == "\\") { kml_file = "\\" + kml_file; }
                else                          { kml_file = IoFilesDirectory + "\\" + kml_file; }

                FileStream fs = new FileStream(kml_file, FileMode.Create);
                StreamWriter wr = new StreamWriter(fs);

                // write KML header
                wr.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                wr.WriteLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
                wr.WriteLine("  <Placemark>");
                wr.WriteLine("    <name>" + StartTime.ToString() + "</name>");

                wr.WriteLine("    <Style id=\"yellowLineGreenPoly\">");
                wr.WriteLine("      <LineStyle>");

                // Colors : blue - red - green- yellow- white - black
                if      (comboBoxKmlOptColor.SelectedIndex == 0) { wr.WriteLine("        <color>ffff0000</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 1) { wr.WriteLine("        <color>ff0000ff</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 2) { wr.WriteLine("        <color>ff00ff00</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 3) { wr.WriteLine("        <color>ff00ffff</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 4) { wr.WriteLine("        <color>ffffffff</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 5) { wr.WriteLine("        <color>ff000000</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 6) { wr.WriteLine("        <color>ffc0c0c0</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 7) { wr.WriteLine("        <color>ff0080ff</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 8) { wr.WriteLine("        <color>ffff8000</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 9) { wr.WriteLine("        <color>ff000080</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 10){ wr.WriteLine("        <color>ff800080</color>"); }
                else if (comboBoxKmlOptColor.SelectedIndex == 11){ wr.WriteLine("        <color>ffff0080</color>"); }

                wr.WriteLine("        <width>" + ((comboBoxKmlOptWidth.SelectedIndex + 1)*2).ToString() + "</width>");
                
                wr.WriteLine("      </LineStyle>");
                wr.WriteLine("      <PolyStyle>");
                wr.WriteLine("        <color>7f00ff00</color>");
                wr.WriteLine("      </PolyStyle>");
                wr.WriteLine("    </Style>");

                wr.WriteLine("      <description>");

                // write description for this trip
                string dist_unit, speed_unit, alt_unit, exstop_info;
                GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out exstop_info);
                string dist, speed_cur, speed_avg, speed_max, run_time, last_sample_time, altitude, battery;
                GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time, out last_sample_time, out altitude, out battery);

                wr.WriteLine(dist + " " + dist_unit + " " + run_time + " " + exstop_info);
                wr.WriteLine(speed_cur + " " + speed_avg + " " + speed_max + " " + speed_unit);
                wr.WriteLine("battery " + battery);

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
                if (checkKmlAlt.Checked) { wr.WriteLine("      <altitudeMode>absolute</altitudeMode>"); }
                wr.WriteLine("        <coordinates>");

                // here write coordinates
                for (int i = 0; i < Counter/Decimation; i++)
                {
                    if (checkKmlAlt.Checked)
                    {
                        wr.WriteLine(replaceCommas(PlotLong[i]) + "," + replaceCommas(PlotLat[i]) + "," +
                                     (PlotZ[i] - Decimal.ToDouble(numericGeoID.Value)).ToString());
                    }
                    else
                    {
                        wr.WriteLine(replaceCommas(PlotLong[i]) + "," + replaceCommas(PlotLat[i]));
                    }
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

            // refill listBox, to indicate that KML was saved
            int selected_index = listBoxFiles.SelectedIndex;
            listBoxFiles.Items.Clear();
            FillFileNames();
            if (listBoxFiles.Items.Count != 0) { listBoxFiles.SelectedIndex = selected_index; }
        }
        private void buttonSaveGPX_Click(object sender, EventArgs e)
        {
            if (listBoxFiles.SelectedIndex != -1)
            {
                string gcc_file = listBoxFiles.SelectedItem.ToString();
                gcc_file = gcc_file.Replace("*", ""); // remove * and + for the gpx/kml indication
                gcc_file = gcc_file.Replace("+", "");

                if (IoFilesDirectory == "\\") { gcc_file = "\\" + gcc_file; ; }
                else { gcc_file = IoFilesDirectory + "\\" + gcc_file; }

                if (!LoadGcc(gcc_file))
                {
                    MessageBox.Show("File has errors or blank", "Error loading .gcc file",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    return;
                }
            }
            else
            {
                MessageBox.Show("No files selected", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                return;
            }

            if (Counter == 0)
            {
                MessageBox.Show("File is blank - no records to save to GPX", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                return;
            }

            string gpx_file = "";

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                CheckIoDirectoryExists();
                
                gpx_file = Path.GetFileNameWithoutExtension(CurrentFileName) + ".gpx";

                if (IoFilesDirectory == "\\") { gpx_file = "\\" + gpx_file; }
                else { gpx_file = IoFilesDirectory + "\\" + gpx_file; }

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
                if (checkGpxRte.Checked) { wr.WriteLine("<rte>"); } else { wr.WriteLine("<trk>"); }
                wr.WriteLine("<name>" + StartTime.ToString() + "</name>");

                string dist_unit, speed_unit, alt_unit, exstop_info;
                GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out exstop_info);
                string dist, speed_cur, speed_avg, speed_max, run_time_label, last_sample_time, altitude, battery;
                GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time_label, out last_sample_time, out altitude, out battery);

                wr.WriteLine("<desc><![CDATA[" + dist + " " + dist_unit + " " + run_time_label + " " + exstop_info
                               + " " + speed_cur + " " + speed_avg + " " + speed_max + " " + speed_unit
                               + " battery " + battery
                               + "]]></desc>");

                if (checkGpxRte.Checked == false) { wr.WriteLine("<trkseg>"); }

                // here write coordinates
                for (int i = 0; i < Counter/Decimation; i++)
                {
                    if (checkGpxRte.Checked)
                    {
                        wr.WriteLine("<rtept lat=\"" + replaceCommas(PlotLat[i]) +
                                     "\" lon=\"" + replaceCommas(PlotLong[i]) + "\">");
                    }
                    else
                    {
                        wr.WriteLine("<trkpt lat=\"" + replaceCommas(PlotLat[i]) +
                                     "\" lon=\"" + replaceCommas(PlotLong[i]) + "\">");
                    }

                    wr.WriteLine("<ele>" + (PlotZ[i] - Decimal.ToDouble(numericGeoID.Value)).ToString() + "</ele>");

                    TimeSpan run_time = new TimeSpan(Decimal.ToInt32(numericGpxTimeShift.Value), 0, PlotT[i]);
                    string run_time_str = (StartTime + run_time).ToString("u");
                    run_time_str = run_time_str.Replace(" ", "T");
                    wr.WriteLine("<time>" + run_time_str + "</time>");

                    wr.WriteLine("<speed>" + replaceCommas(PlotS[i] * 0.1) + "</speed>");

                    if (checkGpxRte.Checked) { wr.WriteLine("</rtept>"); } else { wr.WriteLine("</trkpt>"); } 
                }
                // write end of the GPX file
                if (checkGpxRte.Checked == false) { wr.WriteLine("</trkseg>"); }
                if (checkGpxRte.Checked) { wr.WriteLine("</rte>"); } else { wr.WriteLine("</trk>"); }

                wr.WriteLine("</gpx>");
                wr.Close();
                fs.Close();

            }
            catch (Exception /*e*/) { }
            Cursor.Current = Cursors.Default;

            // refill listBox, to indicate that GPX was saved
            int selected_index = listBoxFiles.SelectedIndex;
            listBoxFiles.Items.Clear();
            FillFileNames();
            if (listBoxFiles.Items.Count != 0) { listBoxFiles.SelectedIndex = selected_index; }


            // upload GPX to CW site
            if (checkUploadGpx.Checked && File.Exists(gpx_file))
            {
                if (MessageBox.Show("Do you want to upload GPX file?", "Crossingways.com",
                   MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    Cursor.Current = Cursors.WaitCursor;

                    StreamReader re = File.OpenText(gpx_file);
                    string gpx = null;
                    gpx = re.ReadToEnd();
                    re.Close();

                    CWUtils.UploadGPXViaHTTP(textBoxCw1.Text, CwHashPassword, "GCC Log", gpx);

                    Cursor.Current = Cursors.Default;
                }
            }

        }

        private void saveCsvLog()
        {
            if (Counter == 0)
                { return; }

            try
            {
                CheckIoDirectoryExists();

                string log_file = "log.csv";

                if (IoFilesDirectory == "\\") { log_file = "\\" + log_file; }
                else { log_file = IoFilesDirectory + "\\" + log_file; }

                bool file_exists = File.Exists(log_file);

                FileStream fs = new FileStream(log_file, FileMode.Append);
                StreamWriter wr = new StreamWriter(fs);

                // write header, if this is a new file
                if (!file_exists)
                {
                    string dist_unit, speed_unit, alt_unit, exstop_info;
                    GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out exstop_info);

                    wr.WriteLine("File info,,,,,,Start,Stop,Distance (" + dist_unit +
                                "),Start position, End position,Total time," + speed_unit);
                }
                
                TimeSpan run_time = new TimeSpan(0, 0, PlotT[(Counter / Decimation) - 1]);

                // write record. Separate file name into a few fields
                string fname = Path.GetFileNameWithoutExtension(CurrentFileName);
                fname = fname.Replace(".gcc", "");
                fname = fname.Replace("_", ",");  

                // make sure we have always 5 commas, so other fields are in correct columns
                int comma_counter = 0;
                for (int i = 0; i < fname.Length; i++)
                { 
                    if (fname[i] == ',') { comma_counter++; } 
                }

                for (int i = comma_counter; i < 5; i++)
                    { fname += ","; }

                string dist, speed_cur, speed_avg, speed_max, run_time_str, last_sample_time, altitude, battery;
                GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time_str, out last_sample_time, out altitude, out battery);

                wr.WriteLine(fname + "," +
                    StartTime.ToString() + "," +
                    (StartTime + run_time).ToString() + "," +
                    dist + "," +
                    replaceCommas(StartLat) + "  " + replaceCommas(StartLong) + "," +
                    replaceCommas(CurrentLat) + "  " + replaceCommas(CurrentLong) + "," +
                    run_time_str + "," +
                    speed_cur + " " + speed_avg + " " + speed_max
                    );

                wr.Close();
                fs.Close();

            }
            catch (Exception /*e*/) { }
        }

        private void comboGpsPoll_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DoNotSaveSettingsFlag) return;
            SaveSettings();
        }

        private void checkExStopTime_Click(object sender, EventArgs e)
        {
            if (DoNotSaveSettingsFlag) return;
            SaveSettings();
        }

        // draw main screen ------------------------------------------------
        Bitmap BackBuffer = null;           // the bitmap we draw into
        Graphics BackBufferGraphics = null;
                 
        void PrepareBackBuffer()
        {
            if (   (BackBuffer == null)
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
        Color GetAverageColor()
        {
            Color average_cl = Color.FromArgb(bkColor.R / 2 + foColor.R / 2, bkColor.G / 2 + foColor.G / 2, bkColor.B / 2 + foColor.B / 2);
            return average_cl;
        }
        void DrawMainLabelAndUnits(Graphics g, string str, string units, int x0, int y0)
        {
            Pen p = new Pen(GetAverageColor(), 1);
            Font f = new Font("Arial", 9, FontStyle.Regular);
            SolidBrush br = new SolidBrush(foColor);

            SizeF sz = g.MeasureString(str, f);
            int x1 = x0 + (int)sz.Width;
            int x2 = x0 + (int)sz.Width + MGridDelta*2;
            int y1 = y0 + (int)sz.Height - MGridDelta;
            int y2 = y0 + (int)sz.Height + MGridDelta;
            g.DrawLine(p, x0, y2, x1, y2);
            g.DrawLine(p, x2, y0, x2, y1);
            g.DrawLine(p, x2, y1, x1, y2);
            p.Color = foColor;
            g.DrawString(str, f, br, x0 + MGridDelta, y0);
            g.DrawString(units, f, br, x2 + MGridDelta*3, y0);
        }
        void DrawMainLabelOnRight(Graphics g, string str, int x0, int y0, float font_size)
        {
            Font f = new Font("Arial", font_size, FontStyle.Regular);
            SolidBrush br = new SolidBrush(foColor);
            SizeF sz = g.MeasureString(str, f);
            g.DrawString(str, f, br, x0 - MGridDelta - (int)sz.Width, y0);
        }
        void DrawMainLabelOnLeft(Graphics g, string str, int x0, int y0, float font_size)
        {
            Font f = new Font("Arial", font_size, FontStyle.Regular);
            SolidBrush br = new SolidBrush(foColor);
            g.DrawString(str, f, br, x0 + MGridDelta, y0);
        }
        void DrawMainValues(Graphics g, string str, int x0, int y0, float font_size) // x0,y0 are center/bottom position!
        {
            // split into 2 string (before and after  last comma, dot or semicolumn)
            string str1 = "";
            string str2 = "";
            if (str.Length != 0)
            {
                int pos = -1;
                for (int i = str.Length-1; i >= 0; i--)
                {
                    if ((str[i] == ',') || (str[i] == '.') || (str[i] == ':'))
                        { pos = i; break; }
                }

                if (pos != -1)
                {
                    str1 = str.Substring(0, pos+1);
                    str2 = str.Substring(pos + 1, str.Length - pos - 1);
                }
                else
                    { str1 = str; str2 = ""; }
            }

            // print 2 strings in larger and smaller font
            float smaller_font_size = font_size*2.0f/3.0f;
            if (smaller_font_size < 8.0f) { smaller_font_size = 8.0f; }

            Font f1 = new Font("Arial", font_size, FontStyle.Regular);
            Font f2 = new Font("Arial", smaller_font_size, FontStyle.Regular);
            SolidBrush br = new SolidBrush(foColor);
            Size sz1 = g.MeasureString(str1, f1).ToSize();
            Size sz2 = g.MeasureString(str2, f2).ToSize();

            int x1 = x0 - (sz1.Width + sz2.Width) / 2;
            g.DrawString(str1, f1, br, x1, y0 - sz1.Height);
            g.DrawString(str2, f2, br, x1 + sz1.Width, y0 - (sz2.Height*1.07f) ); // tune the shift
        }
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
        private void GetValuesToDisplay(out string dist, out string speed_cur, out string speed_avg, out string speed_max, out string run_time, out string last_sample_time, out string altitude_str, out string battery)
        {
            if (Counter == 0)
            {
                dist = "0.0"; speed_cur = "0.0"; speed_avg = "0.0"; speed_max = "0.0";
                run_time = "0:00:00"; last_sample_time = ""; altitude_str = "0"; battery = "";
                return;
            }

            double ceff = 1.0;
            if (comboUnits.SelectedIndex == 0) { ceff = 1.0 / 1.609344; }   // miles
            else if (comboUnits.SelectedIndex == 1) { ceff = 1.0; }         // km
            else if (comboUnits.SelectedIndex == 2) { ceff = 1.0 / 1.852; } // naut miles
            else if (comboUnits.SelectedIndex == 3) { ceff = 1.0 / 1.609344; } // miles, but height in feet
            else if (comboUnits.SelectedIndex == 4) { ceff = 1.0; } // km, but speed min/km
            else if (comboUnits.SelectedIndex == 5) { ceff = 1.0 / 1.609344; } // miles, but speed min/mile and ft
            else if (comboUnits.SelectedIndex == 6) { ceff = 1.0; }         // km with ft
            else                                    { ceff = 1.0; }         // default - km

            int time_to_use = (checkExStopTime.Checked ? CurrentTimeSec - CurrentStoppageTimeSec : CurrentTimeSec);

            TimeSpan ts = new TimeSpan(0, 0, time_to_use);
            run_time = ts.ToString();

            TimeSpan ts_all = new TimeSpan(0, 0, CurrentTimeSec);
            last_sample_time = (StartTime + ts_all).ToString("T");

            dist = PrintDist(Distance * 0.001 * ceff);

            double altitude = (CurrentZ - Decimal.ToDouble(numericGeoID.Value));

            // relative altitude mode
            if (checkRelativeAlt.Checked) { altitude = CurrentZ - (double) PlotZ[0]; }

            if ((comboUnits.SelectedIndex == 3) || (comboUnits.SelectedIndex == 5) || (comboUnits.SelectedIndex == 6))
                { altitude /= 0.30480; } // altitude in feet

            altitude_str = altitude.ToString("0");

            double averageSpeed = (time_to_use == 0 ? CurrentSpeed : Distance * 0.001 / (time_to_use / 3600.0));

            DateTime a_date = new DateTime(2008, 1, 1);

            // speed in min/km or min per mile
            if ((comboUnits.SelectedIndex == 4) || (comboUnits.SelectedIndex == 5))
            {
                double current_seckm = (CurrentSpeed > 0.0 ? 3600.0 / (CurrentSpeed * ceff) : 0.0);
                double average_seckm = (averageSpeed > 0.0 ? 3600.0 / (averageSpeed * ceff) : 0.0);
                double max_seckm = (MaxSpeed > 0.0 ? 3600.0 / (MaxSpeed * ceff) : 0.0);

                // limit to 60 min/km, otherwise set to 0
                if (current_seckm > 3599.0) { current_seckm = 0.0; }
                if (average_seckm > 3599.0) { average_seckm = 0.0; }
                if (max_seckm > 3599.0) { max_seckm = 0.0; }

                TimeSpan current_ts = new TimeSpan(0, 0, (int)current_seckm);
                TimeSpan average_ts = new TimeSpan(0, 0, (int)average_seckm);
                TimeSpan max_ts = new TimeSpan(0, 0, (int)max_seckm);

                speed_cur = (a_date + current_ts).ToString("mm:ss");
                speed_avg = (a_date + average_ts).ToString("mm:ss");
                speed_max = (a_date + max_ts).ToString("mm:ss");
            }
            // all other cases
            else
            {
                speed_cur = PrintSpeed(CurrentSpeed * ceff);
                speed_avg = PrintSpeed(averageSpeed * ceff);
                speed_max = PrintSpeed(MaxSpeed * ceff);
            }

            // battery current and estimation
            if (CurrentBattery == -1) { battery = "AC"; }
            else if (CurrentBattery < 0) { battery = "??%"; }
            else { battery = CurrentBattery.ToString() + "%"; }

            // try to estimate charge left
            if ((CurrentBattery > 0) && (StartBattery > 0) && (CurrentBattery < StartBattery) && (CurrentBattery > 20))
            {
                double sec_per_battery_percent = CurrentTimeSec / (double)(StartBattery - CurrentBattery);
                double sec_left = (CurrentBattery - 20) * sec_per_battery_percent;
                TimeSpan battery_left = new TimeSpan(0, 0, (int)sec_left);  
                battery += " " + battery_left.Hours.ToString() + "h" + battery_left.Minutes.ToString() + "m left";
            }
        }
        private void GetUnitLabels(out string dist_unit, out string speed_unit, out string alt_unit, out string exstop_info)
        {
            if (comboUnits.SelectedIndex == 0)
            {
                dist_unit = "miles";
                alt_unit = "m";
                speed_unit = "mph";
            }
            else if (comboUnits.SelectedIndex == 1)
            {
                dist_unit = "km";
                alt_unit = "m";
                speed_unit = "km/h";
            }
            else if (comboUnits.SelectedIndex == 2)
            {
                dist_unit = "naut miles";
                alt_unit = "m";
                speed_unit = "knots";
            }
            else if (comboUnits.SelectedIndex == 3)
            {
                dist_unit = "miles";
                alt_unit = "feet";
                speed_unit = "mph";
            }
            else if (comboUnits.SelectedIndex == 4)
            {
                dist_unit = "km";
                alt_unit = "m";
                speed_unit = "min/km";
            }
            else if (comboUnits.SelectedIndex == 5)
            {
                dist_unit = "miles";
                alt_unit = "feet";
                speed_unit = "min/mile";
            }
            else if (comboUnits.SelectedIndex == 6)
            {
                dist_unit = "km";
                alt_unit = "ft";
                speed_unit = "km/h";
            }
            else
            {
                dist_unit = "miles";
                alt_unit = "m";
                speed_unit = "mph";
            }

            if (checkExStopTime.Checked) { exstop_info = "ex stop"; }
            else { exstop_info = "inc stop"; }
        }
        private void GetGpsSearchFlags(out string gps_status1, out string gps_status2)
        {
            gps_status1 = ""; gps_status2 = "";

            if (position == null)
                { gps_status1 = "no gps data"; return; }

            if (position.SatellitesInViewCountValid)
                { gps_status1 += "S" + position.SatellitesInViewCount.ToString() + " Snr" + position.GetMaxSNR().ToString(); }
            else
                { gps_status1 += "S0 Snr-"; }

            if (position.TimeValid)
            {
                TimeSpan age = DateTime.UtcNow - position.Time;
                int total_sec = (int)age.TotalSeconds;
                if (total_sec > 99) total_sec = 99;
                gps_status2 += "T" + total_sec.ToString();
            }
            else
                { gps_status2 += "T-"; }

            if (position.HorizontalDilutionOfPrecisionValid)
            {
                double x = position.HorizontalDilutionOfPrecision;
                if (x > 50) { x = 50; }
                gps_status2 += " Dh" + x.ToString("#0");
            }
            else
                { gps_status2 += " Dh-"; }
        }
        void DrawMain(Graphics g)
        {
            int MGridXmax = NoBkPanel.Width;
            int MGridYmax = NoBkPanel.Height;
            
            BackBufferGraphics.Clear(bkColor);

            Pen p = new Pen(GetAverageColor(), 1);
               
            // draw lines separating different cells
            BackBufferGraphics.DrawLine(p, MGridX[0], MGridY[1], MGridXmax, MGridY[1]);
            BackBufferGraphics.DrawLine(p, MGridX[1], MGridY[2], MGridXmax, MGridY[2]);
            BackBufferGraphics.DrawLine(p, MGridX[0], MGridY[3], MGridXmax, MGridY[3]);
            BackBufferGraphics.DrawLine(p, MGridX[0], MGridY[4], MGridXmax, MGridY[4]);
            BackBufferGraphics.DrawLine(p, MGridX[2], MGridY[0], MGridX[2], MGridY[1]);
            BackBufferGraphics.DrawLine(p, MGridX[1], MGridY[1], MGridX[1], MGridYmax);

            // draw labels and units
            string dist_unit, speed_unit, alt_unit, exstop_info;
            GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out exstop_info);
            DrawMainLabelAndUnits(BackBufferGraphics, "Time",     "h:m:s",    MGridX[0], MGridY[0]);
            DrawMainLabelAndUnits(BackBufferGraphics, "Speed",    speed_unit, MGridX[0], MGridY[1]);
            DrawMainLabelAndUnits(BackBufferGraphics, "Distance", dist_unit,  MGridX[0], MGridY[3]);
            DrawMainLabelAndUnits(BackBufferGraphics, "Info",     "",         MGridX[0], MGridY[4]);

            string altitude_mode = "Altitude";
            if (checkRelativeAlt.Checked) { altitude_mode += " diff"; }
            DrawMainLabelAndUnits(BackBufferGraphics, altitude_mode, alt_unit, MGridX[1], MGridY[3]);

            DrawMainLabelAndUnits(BackBufferGraphics, "GPS",      "",         MGridX[1], MGridY[4]);
            DrawMainLabelOnRight(BackBufferGraphics, exstop_info, MGridX[2], MGridY[0], 9.0f);
            DrawMainLabelOnRight(BackBufferGraphics, "cur", MGridX[1], MGridY[1], 9.0f);
            DrawMainLabelOnRight(BackBufferGraphics, "avg", MGridXmax, MGridY[1], 9.0f);
            DrawMainLabelOnRight(BackBufferGraphics, "max", MGridXmax, MGridY[2], 9.0f);

            // reduce font for square screens
            float df = 1.0f;
            if (Screen.PrimaryScreen.Bounds.Width == Screen.PrimaryScreen.Bounds.Height) { df = 0.7f; }
            
            // draw the values
            string dist, speed_cur, speed_avg, speed_max, run_time, last_sample_time, altitude, battery;
            GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time, out last_sample_time, out altitude, out battery);
            DrawMainValues(BackBufferGraphics, run_time, (MGridX[0] + MGridX[2]) / 2, MGridY[1], 32.0f * df);
            DrawMainValues(BackBufferGraphics, speed_cur, (MGridX[0] + MGridX[1]) / 2, MGridY[3], 30.0f * df);
            DrawMainValues(BackBufferGraphics, dist, (MGridX[0] + MGridX[1]) / 2, MGridY[4], 26.0f * df);
            DrawMainValues(BackBufferGraphics, speed_avg, (MGridX[1] + MGridXmax) / 2, MGridY[2], 20.0f * df);
            DrawMainValues(BackBufferGraphics, speed_max, (MGridX[1] + MGridXmax) / 2, MGridY[3], 20.0f * df);
            DrawMainValues(BackBufferGraphics, altitude, (MGridX[1] + MGridXmax) / 2, MGridY[4], 26.0f * df);

            // draw GPS cell
            string gps_status1, gps_status2;
            GetGpsSearchFlags(out gps_status1, out gps_status2);
            DrawMainLabelOnLeft(BackBufferGraphics, gps_status1, MGridX[1], MGridY[5] + MHeightDelta, 8.0f);
            DrawMainLabelOnLeft(BackBufferGraphics, gps_status2, MGridX[1], MGridY[5] + MHeightDelta * 2, 8.0f);
            DrawMainLabelOnLeft(BackBufferGraphics, "latitude", MGridX[1], MGridY[5] + MHeightDelta * 3, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, CurrentLat.ToString("0.000000"), MGridXmax, MGridY[5] + MHeightDelta * 3, 8.0f);
            DrawMainLabelOnLeft(BackBufferGraphics, "longitude", MGridX[1], MGridY[5] + MHeightDelta * 4, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, CurrentLong.ToString("0.000000"), MGridXmax, MGridY[5] + MHeightDelta * 4, 8.0f);

            Color tmp_color = CurrentGpsLedColor;
            if (buttonStart.Enabled) // if GPS is NOT running
                { tmp_color = bkColor; }
            else                     // if running - check pause status
            {
                if (buttonPicPause.pressed) { tmp_color = Color.Yellow; }
            }

            SolidBrush br = new SolidBrush(tmp_color);
            BackBufferGraphics.FillRectangle(br, ((MGridX[1] + MGridXmax) / 2) - MHeightDelta, MGridY[4] + MGridDelta, MHeightDelta, MHeightDelta);

            // draw Info cell
            DrawMainLabelOnRight(BackBufferGraphics, CurrentLiveLoggingString + " ", MGridX[1], MGridY[5], 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, CurrentStatusString + " ", MGridX[1], MGridY[5] + MHeightDelta, 8.0f);
            DrawMainLabelOnLeft(BackBufferGraphics, "battery", MGridX[0], MGridY[5] + MHeightDelta * 2, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, battery, MGridX[1], MGridY[5] + MHeightDelta * 2, 8.0f);
            DrawMainLabelOnLeft(BackBufferGraphics, "last sample", MGridX[0], MGridY[5] + MHeightDelta * 3, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, last_sample_time, MGridX[1], MGridY[5] + MHeightDelta * 3, 8.0f);
            DrawMainLabelOnLeft(BackBufferGraphics, "start", MGridX[0], MGridY[5] + MHeightDelta * 4, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, StartTime.ToString(), MGridX[1], MGridY[5] + MHeightDelta * 4, 8.0f);

            // clock
            Utils.DrawClock(BackBufferGraphics, foColor, (MGridX[2] + MGridXmax) / 2, (MGridY[0] + MGridY[1]) / 2, MGridY[1] - MGridY[0], 16.0f * df);

            // compass
            int heading = 0;
            if (position != null)
            {
                if (position.HeadingValid) { heading = (int)position.Heading; }
            }
            int compass_size = (MGridY[5] + MHeightDelta * 3) - MGridY[4];
            Utils.DrawCompass(BackBufferGraphics, foColor, MGridXmax - compass_size / 2, MGridY[4] + compass_size / 2, compass_size, heading);

            g.DrawImage(BackBuffer, 0, 0); // draw back buffer on screen
        }

        // end draw main screen ------------------------------------------------

        // paint graph ------------------------------------------------------
        // To have nice flicker-free picture movement, we paint first into a bitmap which is larger
        // than the screen, then just paint the bitmap into the screen with a correct shift.
        // We need to paint on "no background panel", which has blank OnPaintBackground, to avoid flicker
        // The bitmap is updated as screen shift is complete (i.e. on mouse up).

        private int MousePosX = 0;        
        private int MousePosY = 0;
        private bool MouseMoving = false;

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
            else if (comboUnits.SelectedIndex == 6) { c = 0.001; }            // km with ft
            else                                    { c = 0.001; }            // km
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
            else if (cmb.SelectedIndex == 7) { return Color.FromArgb(255,128,0); }
            else if (cmb.SelectedIndex == 8) { return Color.FromArgb(0, 128, 255); }
            else if (cmb.SelectedIndex == 9) { return Color.FromArgb(128, 0, 0); }
            else if (cmb.SelectedIndex == 10) { return Color.FromArgb(128, 0, 128); }
            else if (cmb.SelectedIndex == 11) { return Color.FromArgb(128, 0, 255); }
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
            else if (comboUnits.SelectedIndex == 6) { return " km"; }
            return " miles";
        }

        private void ResetMapPosition()
        {
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

            if (BufferDrawMode == BufferDrawModeMain) 
            {
                DrawMain(e.Graphics); 
            }
            else                                      
            {
                // plotting in Long (as X) / Lat (as Y) coordinates
                mapUtil.DrawMaps(e.Graphics, BackBuffer, BackBufferGraphics, MouseMoving, 
                                 buttonStop.Enabled, comboMultiMaps.SelectedIndex, GetUnitsConversionCff(), GetUnitsName(),
                                 PlotLong, PlotLat, Counter / Decimation, GetLineColor(comboBoxKmlOptColor), GetLineWidth(comboBoxKmlOptWidth),
                                 checkPlotTrackAsDots.Checked,
                                 Plot2ndLong, Plot2ndLat, Counter2nd, GetLineColor(comboBoxLine2OptColor), GetLineWidth(comboBoxLine2OptWidth),
                                 checkPlotLine2AsDots.Checked);
            }
        }
        private void tabGraph_MouseDown(object sender, MouseEventArgs e)
        {
            if (BufferDrawMode != BufferDrawModeMaps) { return; }

            MouseMoving = false;
            MousePosX = e.X;
            MousePosY = e.Y;
            mapUtil.ScreenShiftSaveX = mapUtil.ScreenShiftX;
            mapUtil.ScreenShiftSaveY = mapUtil.ScreenShiftY;
        }
        private void tabGraph_MouseUp(object sender, MouseEventArgs e)
        {
            if (BufferDrawMode != BufferDrawModeMaps) 
                { NoBkPanel.Invalidate(); return; }

            MouseMoving = false;
            mapUtil.ScreenShiftSaveX = 0;
            mapUtil.ScreenShiftSaveY = 0;
            NoBkPanel.Invalidate();
        }
        private void tabGraph_MouseMove(object sender, MouseEventArgs e)
        {
            if (BufferDrawMode != BufferDrawModeMaps) { return; }

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

        private void getScaleXScaleY(out double sc_x, out double sc_y)
        {
            // the form is designed for 640x480, so scale to current resolution
            int h = Screen.PrimaryScreen.WorkingArea.Height;
            int w = Screen.PrimaryScreen.WorkingArea.Width;
            sc_y = (double)h / 588.0;
            sc_x = (double)w / 480.0;
            // if we are in landscape mode, swap to get portrait sizes
            if (Screen.PrimaryScreen.Bounds.Height < Screen.PrimaryScreen.Bounds.Width)
            {
                h = Screen.PrimaryScreen.WorkingArea.Width;
                w = Screen.PrimaryScreen.WorkingArea.Height;
                sc_y = (double)h / 640.0;
                sc_x = (double)w / 428.0;
            }
        }
        private void tabAbout_Paint(object sender, PaintEventArgs e)
        {
            double sc_x, sc_y;
            getScaleXScaleY(out sc_x, out sc_y);

            Rectangle src_rec = new Rectangle(0, 0, AboutTabImage.Width, AboutTabImage.Height);
            Rectangle dest_rec = new Rectangle((int)(41 * sc_x), 0, (int)(AboutTabImage.Width * sc_x), (int)(AboutTabImage.Height * sc_y));

            e.Graphics.DrawImage(AboutTabImage, dest_rec, src_rec, GraphicsUnit.Pixel);
        }
        private void tabBlank_Paint(object sender, PaintEventArgs e)
        {
            double sc_x, sc_y;
            getScaleXScaleY(out sc_x, out sc_y);

            Rectangle src_rec = new Rectangle(0, 0, BlankImage.Width, BlankImage.Height);
            Rectangle dest_rec = new Rectangle(0, 0, (int)(BlankImage.Width * sc_x), (int)(BlankImage.Height * sc_y));

            e.Graphics.DrawImage(BlankImage, dest_rec, src_rec, GraphicsUnit.Pixel);
        }
        private void panelCwLogo_Paint(object sender, PaintEventArgs e)
        {
            double sc_x, sc_y;
            getScaleXScaleY(out sc_x, out sc_y);

            Rectangle dest_rec = new Rectangle(0, 0, (int)(CWImage.Width * sc_x), (int)(CWImage.Height * sc_y));

            ImageAttributes im_attr = new ImageAttributes();
            im_attr.SetColorKey(CWImage.GetPixel(0, 0), CWImage.GetPixel(0, 0));
 
            e.Graphics.DrawImage(CWImage, dest_rec, 0, 0, CWImage.Width, CWImage.Height, GraphicsUnit.Pixel, im_attr);
        }

        private void buttonMain_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeMain; 
            NoBkPanel.BringToFront(); 
            NoBkPanel.Invalidate();

            if (buttonStart.Enabled == false) // if GPS is running
            {
                if (buttonPicPause.pressed) { buttonOptions.BringToFront(); }
                else                        { buttonGraph.BringToFront(); }
                if (checkShowBkOff.Checked) { buttonPicBkOff.BringToFront(); }
                else                        { buttonPicPause.BringToFront(); }
                buttonStop.BringToFront();
            }
            else   // if GPS is stopped
            {
                buttonOptions.BringToFront();
                buttonStart.BringToFront();
                buttonPicLoad.BringToFront();
            }
        }
        private void buttonGraph_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeMaps;
            NoBkPanel.BringToFront(); 
            NoBkPanel.Invalidate();

            listBoxFiles.Focus();  // need to loose control from any combo/edit boxes, to avoid pop-up on "OK" button press
            buttonMain.BringToFront();
            buttonZoomIn.BringToFront();
            buttonZoomOut.BringToFront();
        }
        private void buttonOptions_Click(object sender, EventArgs e)
        {
            tabControl.BringToFront();
            labelFileName.SetText(""); // clear text from info label
            ShowHideViewOpt(false); // make sure the view selector is hidden

            buttonGraph.BringToFront();
            buttonNext.BringToFront();
            buttonPrev.BringToFront();
        }

        // Load dialog up/down buttons
        private void buttonDialogUp_Click(object sender, EventArgs e)
        {
            if ((listBoxFiles.SelectedIndex != -1) && (listBoxFiles.SelectedIndex > 0))
            {
                listBoxFiles.SelectedIndex--;
            }
        }
        private void buttonDialogDown_Click(object sender, EventArgs e)
        {
            if ((listBoxFiles.SelectedIndex != -1) && (listBoxFiles.SelectedIndex < (listBoxFiles.Items.Count-1)))
            {
                listBoxFiles.SelectedIndex++;
            }
        }

        // start/stop toggle buttons
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
            if (sender == buttonStart) 
            {
                // warn if live logging is activated, and ask if want to proceed
                if (comboBoxCwLogMode.SelectedIndex != 0)
                {
                    if (MessageBox.Show("Live logging is activated, proceed?", "www.crossingways.com",
                                       MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation,
                                       MessageBoxDefaultButton.Button1) == DialogResult.No)
                    {
                        buttonStart.pressed = false;
                        buttonStop.pressed = true;
                        buttonStart.Invalidate();
                        return;
                    }
                }
               
                buttonStart_Click(); 
            }
            else if (sender == buttonStop) 
            { 
                buttonStop_Click(); 
            }
        }

        // Open dialog buttons (used to open file or setup folder)
        private void buttonDialogOpen_Click(object sender, EventArgs e)
        {
            tabBlank1.BringToFront();

            if (FolderSetupMode) { buttonDialogOpen_FolderMode(sender, e); }
            else
            {
                if (FileOpenMode == FileOpenMode_Gcc) { buttonDialogOpen_FileModeGcc(sender, e); }
                else                                  { buttonDialogOpen_FileModeTrack2Follow(); }
            }
        }
        private void buttonDialogOpen_FolderMode(object sender, EventArgs e)
        {
            string label_file_text = "";

            if (MapsFolderSetupMode)
            {
                MapsFilesDirectory = "";
                for (int i = 0; i <= CurrentSubDirIndex; i++)
                {
                    if (i != 0) { MapsFilesDirectory += "\\" + listBoxFiles.Items[i].ToString().Trim(); }
                }
                if (CurrentSubDirIndex == 0) { MapsFilesDirectory = "\\"; }
                label_file_text = "Set to " + MapsFilesDirectory;

                CheckMapsDirectoryExists();
                mapUtil.LoadMaps(MapsFilesDirectory);
                if (Counter != 0) { ResetMapPosition(); }
            }
            else
            {
                IoFilesDirectory = "";
                for (int i = 0; i <= CurrentSubDirIndex; i++)
                {
                    if (i != 0) { IoFilesDirectory += "\\" + listBoxFiles.Items[i].ToString().Trim(); }
                }
                if (CurrentSubDirIndex == 0) { IoFilesDirectory = "\\"; }
                label_file_text = "Set to " + IoFilesDirectory;
            }

            tabOpenFile.SendToBack();

            buttonOptions_Click(sender, e); // show options page and display currently set file
            labelFileName.SetText(label_file_text);

            // need to loose focus from list box - otherwise map do not get MouseMove!???
            listBoxFiles.Items.Clear();
            listBoxFiles.Focus();
        }
        private void buttonDialogOpen_FileModeGcc(object sender, EventArgs e)
        {
            if (listBoxFiles.SelectedIndex != -1)
            {
                string gcc_file = listBoxFiles.SelectedItem.ToString();
                gcc_file = gcc_file.Replace("*", ""); // remove * and + for the gpx/kml indication
                gcc_file = gcc_file.Replace("+", "");

                if (IoFilesDirectory == "\\") { gcc_file = "\\" + gcc_file; ; }
                else { gcc_file = IoFilesDirectory + "\\" + gcc_file; }

                if (LoadGcc(gcc_file))  // loaded OK
                {
                    buttonGraph_Click(sender, e);  // show graphs

                    // need to loose focus from list box - otherwise map do not get MouseMove!???
                    listBoxFiles.Items.Clear();
                    listBoxFiles.Focus();
                }
                else
                {
                    // show message box and stay on file open tab
                    MessageBox.Show("File has errors or blank", "Error loading .gcc file",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                }
            }
            else
            {
                // show message box and stay on file open tab
                MessageBox.Show("No files selected", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }
        }
        private void buttonDialogOpen_FileModeTrack2Follow()
        {
            if (listBoxFiles.SelectedIndex != -1)
            {
                string file_name = listBoxFiles.SelectedItem.ToString();

                bool file_found = true;
                if ((file_name == "No *.gcc files found") || (file_name == "No *.kml files found") || (file_name == "No *.gpx files found"))
                    { file_found = false; }

                if (IoFilesDirectory == "\\") { file_name = "\\" + file_name; ; }
                else { file_name = IoFilesDirectory + "\\" + file_name; }

                bool loaded_ok = true;
                if (file_found)
                {
                    if (FileOpenMode == FileOpenMode_2ndGcc)
                        { loaded_ok = ReadFileUtil.LoadGcc(file_name, PlotDataSize, ref Plot2ndLat, ref Plot2ndLong, ref Plot2ndT, out Counter2nd); }
                    else if (FileOpenMode == FileOpenMode_2ndKml)
                        { loaded_ok = ReadFileUtil.LoadKml(file_name, PlotDataSize, ref Plot2ndLat, ref Plot2ndLong, ref Plot2ndT, out Counter2nd); }
                    else if (FileOpenMode == FileOpenMode_2ndGpx)
                        { loaded_ok = ReadFileUtil.LoadGpx(file_name, PlotDataSize, ref Plot2ndLat, ref Plot2ndLong, ref Plot2ndT, out Counter2nd); }
                }

                if (loaded_ok)  // loaded OK
                {
                    // If a new track-to-follow loaded (and main track not exist) - need to reset map zoom/shift vars
                    if ((Counter2nd != 0) && (Counter == 0)) { ResetMapPosition(); }

                    if (file_found) { labelFileName.SetText(Path.GetFileName(file_name) + " loaded"); }
                    else { labelFileName.SetText(""); }

                    tabBlank1.BringToFront();
                    tabOpenFile.SendToBack();

                    // need to loose focus from list box - otherwise map do not get MouseMove!???
                    listBoxFiles.Items.Clear();
                    listBoxFiles.Focus();

                    buttonGraph.BringToFront();
                    buttonNext.BringToFront();
                    buttonPrev.BringToFront();

                    buttonUp.SendToBack();
                    buttonNextFileType.SendToBack();
                    buttonPrevFileType.SendToBack();
                }
                else
                {
                    // show message box and stay on file open tab
                    MessageBox.Show("Error reading file or it does not have track data", "Error loading file",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                }
            }
            else
            {
                // show message box and stay on file open tab
                MessageBox.Show("No files selected", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }
        }
        private void buttonDialogCancel_Click(object sender, EventArgs e)
        {
            tabBlank1.BringToFront();
            tabOpenFile.SendToBack();

            // need to loose focus from list box - otherwise map do not get MouseMove!???
            listBoxFiles.Items.Clear();
            listBoxFiles.Focus();

            // in file load for 1st GCC - go back to main screeen
            if ((FolderSetupMode == false) && (FileOpenMode == FileOpenMode_Gcc))
            {
                buttonOptions.BringToFront();
                buttonStart.BringToFront();
                buttonPicLoad.BringToFront();
                BufferDrawMode = BufferDrawModeMain;
                NoBkPanel.BringToFront();
            }
            else // in all other cases stay at options screen
            {
                buttonGraph.BringToFront();
                buttonNext.BringToFront();
                buttonPrev.BringToFront();

                buttonUp.SendToBack();
                buttonNextFileType.SendToBack();
                buttonPrevFileType.SendToBack();
            }
        }

        // setting io files and maps locations
        private void buttonFileLocation_Click(object sender, EventArgs e)
        {
            MapsFolderSetupMode = false;
            FillFileLocationListBox();
        }
        private void buttonMapsLocation_Click(object sender, EventArgs e)
        {
            MapsFolderSetupMode = true;
            FillFileLocationListBox();
        }
        private void FillFileLocationListBox()
        {
            // to avoid getting "listBoxFiles_SelectedIndexChanged" called as SelectedIndex changes
            FolderSetupMode = false;
            listBoxFiles.Items.Clear();

            tabOpenFile.Height = tabControl.Height;
            tabBlank1.BringToFront();
            tabOpenFile.BringToFront();
            buttonDialogOpen.BringToFront();
            buttonDialogCancel.BringToFront();
            tabBlank.BringToFront();

            listBoxFiles.Height = tabOpenFile.Height;
            listBoxFiles.BringToFront();

            // select IO or Maps folder to setup
            string dir_to_setup = "";
            if (MapsFolderSetupMode)
            {
                CheckMapsDirectoryExists();
                dir_to_setup = MapsFilesDirectory;
            }
            else
            {
                CheckIoDirectoryExists();
                dir_to_setup = IoFilesDirectory;
            }

            // add current path
            string tmp_str = dir_to_setup;
            while ((tmp_str != "\\") && (tmp_str != ""))
            {
                string last_dir = Path.GetFileName(tmp_str);
                listBoxFiles.Items.Insert(0, last_dir);

                tmp_str = Path.GetDirectoryName(tmp_str);
            }
            // add top
            listBoxFiles.Items.Insert(0, "\\");

            // add indent
            string indent = "   ";
            for (int i = 1; i < listBoxFiles.Items.Count; i++)
            {
                listBoxFiles.Items[i] = indent + listBoxFiles.Items[i].ToString();
                indent += "   "; 
            }

            // set curent SubDirIndex
            CurrentSubDirIndex = listBoxFiles.Items.Count-1;
            listBoxFiles.SelectedIndex = CurrentSubDirIndex;

            // print sub-dirs
            string[] subdirectoryEntries = Directory.GetDirectories(dir_to_setup);
            Array.Sort(subdirectoryEntries);

            foreach (string s in subdirectoryEntries)
            {
                listBoxFiles.Items.Add(indent + Path.GetFileName(s));
            }

            tabOpenFile.BringToFront();
            FolderSetupMode = true;
        }

        private void listBoxFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            // this function works in folder setup mode only
            if (!FolderSetupMode) return;

            // clicked on the same one
            if (CurrentSubDirIndex == listBoxFiles.SelectedIndex) return;

            // to avoid getting this function called recursevely as SelectedIndex changes
            FolderSetupMode = false;

            // check if we are not within the sub-folder list 
            if (listBoxFiles.SelectedIndex <= CurrentSubDirIndex)
            {
                // delete all items after SelectedIndex
                while (listBoxFiles.SelectedIndex < (listBoxFiles.Items.Count - 1))
                {
                    listBoxFiles.Items.RemoveAt(listBoxFiles.Items.Count - 1);
                }

                // set new position for CurrentSubDirIndex
                CurrentSubDirIndex = listBoxFiles.SelectedIndex;
            }
            else
            // yes, we are inside sub-folder list
            {
                string current_sub_folder = listBoxFiles.SelectedItem.ToString();

                // delete all items after CurrentSubDirIndex
                while (CurrentSubDirIndex < (listBoxFiles.Items.Count-1))
                {
                    listBoxFiles.Items.RemoveAt(listBoxFiles.Items.Count - 1);
                }

                // add selected sub-dir last
                listBoxFiles.Items.Add(current_sub_folder);

                // set new position for CurrentSubDirIndex
                CurrentSubDirIndex = listBoxFiles.Items.Count - 1;
            }

            // fill sub-dirs
            // set indent and selected folder name
            string indent = "";
            string new_folder_name = "";
            for (int i = 0; i <= CurrentSubDirIndex; i++)
            {
                indent += "   ";
                if (i != 0) { new_folder_name += "\\" + listBoxFiles.Items[i].ToString().Trim(); }
            }
            if (CurrentSubDirIndex == 0) { new_folder_name = "\\"; }

            // add list sub-folders there
            string[] subdirectoryEntries = Directory.GetDirectories(new_folder_name);
            Array.Sort(subdirectoryEntries);

            foreach (string s in subdirectoryEntries)
            {
                listBoxFiles.Items.Add(indent + Path.GetFileName(s));
            }

            listBoxFiles.SelectedIndex = CurrentSubDirIndex;
            FolderSetupMode = true;
        }

        // controls on Options2 tab (live logging)
        private void buttonCWShowKeyboard_Click(object sender, EventArgs e)
        {
            inputPanel.Enabled = !inputPanel.Enabled;
        }
        private void buttonCWVerify_Click(object sender, EventArgs e)
        {
            if (LockCwVerify) { return; }

            if (textBoxCw2.Text == "******")
            {
                MessageBox.Show("Please retype your password", "www.crossingways.com",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            LockCwVerify = true;

            labelCwInfo.Text = ""; labelCwInfo.Refresh();
            labelCwInfo.Text = CWUtils.VerifyCredentialsOnCrossingwaysViaHTTP(textBoxCw1.Text, textBoxCw2.Text);

            // set hash password
            CwHashPassword = CWUtils.HashPassword(textBoxCw2.Text);
            textBoxCw2.Text = "******";

            Cursor.Current = Cursors.Default;
            LockCwVerify = false;
        }
        private void DoLiveLogging()
        {
            if (comboBoxCwLogMode.SelectedIndex == 0)  // live logging disabled
                { CurrentLiveLoggingString = ""; return; } 

            if (Counter == 0) { return; }      // safety checks
            if (position == null) { return; }

            // check if this is a time to log again
            TimeSpan maxAge = new TimeSpan(0, LiveLoggingTimeMin[comboBoxCwLogMode.SelectedIndex], 0);
            if ((LastLiveLogging + maxAge) >= DateTime.UtcNow) { return; }
            LastLiveLogging = DateTime.UtcNow;

            // proceed with live logging
            string servermessage = CWUtils.UpdatePositionOnCrossingwaysViaHTTP(textBoxCw1.Text, CwHashPassword,
                                   position.Latitude, position.Longitude,
                                   CurrentZ - Decimal.ToDouble(numericGeoID.Value),
                                   (position.HeadingValid ? position.Heading : 0.0), "GpsCC");

            if (servermessage.IndexOf("90 - Could not establish a connection") != -1)
                { CurrentLiveLoggingString = "livelog error!"; }
            else
                { CurrentLiveLoggingString = "livelog ok"; }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (LockResize) { return; }
            DoOrientationSwitch();
        }

        private void DoOrientationSwitch()
        {
            // switch from default to landscape mode
            if ((isLandscape == false) && (Screen.PrimaryScreen.Bounds.Height < Screen.PrimaryScreen.Bounds.Width))
            {
                isLandscape = true;

                // store original buttons location
                LandscapeX1 = buttonStart.Left;     LandscapeX2 = buttonStop.Left;
                LandscapeY1 = buttonPicSaveKML.Top; LandscapeY2 = buttonStart.Top;

                // move to new - all at tab width (480 on VGA)
                int left = tabControl.Width;
                buttonMain.Left = left;       buttonGraph.Left = left;      buttonOptions.Left = left;  buttonPicLoad.Left = left;
                buttonPicSaveKML.Left = left; buttonPicSaveGPX.Left = left; buttonPicBkOff.Left = left; buttonPicPause.Left = left;
                buttonUp.Left = left;         buttonDown.Left = left;       buttonNext.Left = left;     buttonPrev.Left = left;
                buttonZoomIn.Left = left;     buttonZoomOut.Left = left;    buttonStart.Left = left;    buttonStop.Left = left;
                buttonDialogOpen.Left = left; buttonDialogCancel.Left = left;
                buttonNextFileType.Left = left; buttonPrevFileType.Left = left;

                // set height
                int h = buttonMain.Height;
                buttonMain.Top = 0;           buttonGraph.Top = 0;          buttonOptions.Top = 0;  buttonPicLoad.Top = h * 2;
                buttonPicSaveKML.Top = h * 2; buttonPicSaveGPX.Top = h * 3; buttonPicBkOff.Top = h; buttonPicPause.Top = h;
                buttonUp.Top = h*5;           buttonDown.Top = h * 4;       buttonNext.Top = h;     buttonPrev.Top = h * 2;
                buttonZoomIn.Top = h;         buttonZoomOut.Top = h * 2;    buttonStart.Top = h;    buttonStop.Top = h * 2;
                buttonDialogOpen.Top = 0;     buttonDialogCancel.Top = h;
                buttonNextFileType.Top = h * 2; buttonPrevFileType.Top = h * 3; 
            }
            // switch back
            else if ((isLandscape == true) && (Screen.PrimaryScreen.Bounds.Height > Screen.PrimaryScreen.Bounds.Width))
            {
                isLandscape = false;

                // restore left
                buttonMain.Left = 0; buttonGraph.Left = 0; buttonOptions.Left = 0; buttonPicLoad.Left = LandscapeX2;
                buttonPicSaveKML.Left = 0; buttonPicSaveGPX.Left = LandscapeX1; buttonPicBkOff.Left = LandscapeX1; buttonPicPause.Left = LandscapeX1;
                buttonUp.Left = LandscapeX2; buttonDown.Left = LandscapeX2; buttonNext.Left = LandscapeX2; buttonPrev.Left = LandscapeX1;
                buttonZoomIn.Left = LandscapeX1; buttonZoomOut.Left = LandscapeX2; buttonStart.Left = LandscapeX1; buttonStop.Left = LandscapeX2;
                buttonDialogOpen.Left = LandscapeX1; buttonDialogCancel.Left = 0;
                buttonNextFileType.Left = 0; buttonPrevFileType.Left = LandscapeX1;

                // restore height
                buttonMain.Top = LandscapeY2; buttonGraph.Top = LandscapeY2; buttonOptions.Top = LandscapeY2; buttonPicLoad.Top = LandscapeY2;
                buttonPicSaveKML.Top = LandscapeY1; buttonPicSaveGPX.Top = LandscapeY1; buttonPicBkOff.Top = LandscapeY2; buttonPicPause.Top = LandscapeY2;
                buttonUp.Top = LandscapeY1; buttonDown.Top = LandscapeY2; buttonNext.Top = LandscapeY2; buttonPrev.Top = LandscapeY2;
                buttonZoomIn.Top = LandscapeY2; buttonZoomOut.Top = LandscapeY2; buttonStart.Top = LandscapeY2; buttonStop.Top = LandscapeY2;
                buttonDialogOpen.Top = LandscapeY2; buttonDialogCancel.Top = LandscapeY2;
                buttonNextFileType.Top = LandscapeY1; buttonPrevFileType.Top = LandscapeY1;
            }
        }

        // load second line - track to follow
        private void buttonLoadTrack2Follow_Click(object sender, EventArgs e)
        {
            FolderSetupMode = false;
            if (FileExtentionToOpen == FileOpenMode_2ndKml)
            {
                FileOpenMode = FileOpenMode_2ndKml;
                Fill2ndLineFiles("*.kml");
            }
            else if (FileExtentionToOpen == FileOpenMode_2ndGpx)
            {
                FileOpenMode = FileOpenMode_2ndGpx;
                Fill2ndLineFiles("*.gpx");
            }
            else
            {
                FileOpenMode = FileOpenMode_2ndGcc;
                Fill2ndLineFiles("*.gcc");
            }
        }
        private void buttonPrevFileType_Click(object sender, EventArgs e)
        {
            FileExtentionToOpen--;
            if (FileExtentionToOpen < 1) { FileExtentionToOpen = 3; } // set to FileOpenMode_2ndGpx = 3
            buttonLoadTrack2Follow_Click(buttonLoadTrack2Follow, EventArgs.Empty);
        }
        private void buttonNextFileType_Click(object sender, EventArgs e)
        {
            FileExtentionToOpen++;
            if (FileExtentionToOpen > 3) { FileExtentionToOpen = 1; } // set to FileOpenMode_2ndGcc = 1
            buttonLoadTrack2Follow_Click(buttonLoadTrack2Follow, EventArgs.Empty);
        }
        private void Fill2ndLineFiles(string ext)
        {
            listBoxFiles.Items.Clear();

            string[] files = Directory.GetFiles(IoFilesDirectory, ext);
            Array.Sort(files);

            for (int i = (files.Length - 1); i >= 0; i--)
            {
                listBoxFiles.Items.Add(Path.GetFileName(files[i]));
            }
            if (listBoxFiles.Items.Count == 0)
                { listBoxFiles.Items.Add("No " + ext + " files found"); }

            if (isLandscape == false)  // make smaller in portrait, to show buttons
            { listBoxFiles.Height = tabControl.Height - buttonUp.Height - 1; }
            listBoxFiles.BringToFront();

            if (isLandscape == false)
            { tabOpenFile.Height = tabControl.Height - buttonUp.Height; }
            tabOpenFile.BringToFront();

            buttonDialogOpen.BringToFront();
            buttonDialogCancel.BringToFront();
            buttonDown.BringToFront();
            buttonUp.BringToFront();
            buttonNextFileType.BringToFront();
            buttonPrevFileType.BringToFront();
            tabBlank1.SendToBack();

            if (listBoxFiles.Items.Count != 0)
            { listBoxFiles.SelectedIndex = 0; }
        }
        private void buttonLoad2Clear_Click(object sender, EventArgs e)
        {
            // if the track-to-follow already cleared (and not logging) - clear main
            if (Counter2nd == 0)
            {
                if (buttonStop.Enabled)
                    { labelFileName.SetText("Cannot clear track while logging"); }
                else 
                {
                    Counter = 0;
                    StartTime = DateTime.Now;       StartTimeUtc = DateTime.UtcNow;
                    LastBatterySave = StartTimeUtc; LastLiveLogging = StartTimeUtc;
                    MaxSpeed = 0.0; Distance = 0.0; CurrentStoppageTimeSec = 0;
                    FirstSampleValidCount = 0;      GpsSearchCount = 0;
                    CurrentStatusString = "gps off"; CurrentLiveLoggingString = "";

                    labelFileName.SetText("All tracks cleared");
                }
            }
            else // clear track-to-follow first (on the first click)
            {
                Counter2nd = 0;
                labelFileName.SetText("Track to follow cleared");
            }
        }

        // show/hide options view selector
        private void ShowHideViewOpt(bool show)
        {
            labelOptText.Visible = show;
            checkOptAbout.Visible = show;
            checkOptLiveLog.Visible = show;
            checkOptLaps.Visible = show;
            checkOptMaps.Visible = show;
            checkOptGps.Visible = show;
            checkOptKmlGpx.Visible = show;
            checkOptMain.Visible = show;

            buttonHelp.Visible = !show;
            buttonIoLocation.Visible = !show;
            buttonMapsLocation.Visible = !show;
            buttonLoadTrack2Follow.Visible = !show;
            buttonLoad2Clear.Visible = !show;
            labelFileName.Visible = !show;

            if (!show)
            {
                buttonShowViewSelector.Text = "Select option pages to scroll ...";
                buttonShowViewSelector.align = 3;
            }
            else 
            { 
                buttonShowViewSelector.Text = "Done...";
                buttonShowViewSelector.align = 2;
            }
        }
        private string AddIndicator(string s)
        {
            if (s[0] != '-') { s = "-" + s; }
            return s;
        }
        private string RemoveIndicator(string s)
        {
            if (s[0] == '-') { s = s.Remove(0, 1); }
            return s;
        }
        private void FillPagesToShow()
        {
            PagesToShow[0] = 0; NumPagesToShow = 1; // always show the first option page

            if (checkOptGps.Checked) { PagesToShow[NumPagesToShow] = 1; NumPagesToShow++; tabPageGps.Text = RemoveIndicator(tabPageGps.Text); }
            else { tabPageGps.Text = AddIndicator(tabPageGps.Text); }

            if (checkOptMain.Checked) { PagesToShow[NumPagesToShow] = 2; NumPagesToShow++; tabPageMainScr.Text = RemoveIndicator(tabPageMainScr.Text); }
            else { tabPageMainScr.Text = AddIndicator(tabPageMainScr.Text); }

            if (checkOptMaps.Checked) { PagesToShow[NumPagesToShow] = 3; NumPagesToShow++; tabPageMapScr.Text = RemoveIndicator(tabPageMapScr.Text); }
            else { tabPageMapScr.Text = AddIndicator(tabPageMapScr.Text); }

            if (checkOptKmlGpx.Checked) { PagesToShow[NumPagesToShow] = 4; NumPagesToShow++; tabPageKmlGpx.Text = RemoveIndicator(tabPageKmlGpx.Text); }
            else { tabPageKmlGpx.Text = AddIndicator(tabPageKmlGpx.Text); }

            if (checkOptLiveLog.Checked) { PagesToShow[NumPagesToShow] = 5; NumPagesToShow++; tabPageLiveLog.Text = RemoveIndicator(tabPageLiveLog.Text); }
            else { tabPageLiveLog.Text = AddIndicator(tabPageLiveLog.Text); }

            if (checkOptLaps.Checked) { PagesToShow[NumPagesToShow] = 6; NumPagesToShow++; tabPageLaps.Text = RemoveIndicator(tabPageLaps.Text); }
            else { tabPageLaps.Text = AddIndicator(tabPageLaps.Text); }

            if (checkOptAbout.Checked) { PagesToShow[NumPagesToShow] = 7; NumPagesToShow++; tabPageAbout.Text = RemoveIndicator(tabPageAbout.Text); }
            else { tabPageAbout.Text = AddIndicator(tabPageAbout.Text); }

            CurrentOptionsPage = 0;
        }
        private void ShowOptionPageNumber(int x)
        {
            tabControl.BringToFront();
            tabControl.SelectedIndex = x;
            if (x == 0) { ShowHideViewOpt(false); }
            else if (x == 5) { labelCwInfo.Text = "Visit www.crossingways.com for all info"; }
        }
        private void buttonShowViewOpt_Click(object sender, EventArgs e)
        {
            if (labelOptText.Visible)
            {
                ShowHideViewOpt(false);
            }
            else
            {
                ShowHideViewOpt(true);
            }
        }
        private void checkOptGps_Click(object sender, EventArgs e)
        {
            FillPagesToShow();
        }
        private void buttonOptionsNext_Click(object sender, EventArgs e)
        {
            if(NumPagesToShow == 1) { ShowOptionPageNumber(0); return; }

            // exact match
            for(int i = 0; i < NumPagesToShow; i++)
            {
                if (PagesToShow[i] == tabControl.SelectedIndex)
                {
                    CurrentOptionsPage = i;
                    CurrentOptionsPage++;
                    if (CurrentOptionsPage >= NumPagesToShow) { CurrentOptionsPage = NumPagesToShow - 1; }
                    ShowOptionPageNumber(PagesToShow[CurrentOptionsPage]);
                    return;
                }
            }
            // somewhere in between 2 pages                
            for (int i = 0; i < NumPagesToShow-1; i++)
            {
                if ((PagesToShow[i] < tabControl.SelectedIndex) && (PagesToShow[i+1] > tabControl.SelectedIndex))
                {
                    CurrentOptionsPage = i+1;
                    ShowOptionPageNumber(PagesToShow[CurrentOptionsPage]);
                    return;
                }
            }
        }
        private void buttonOptionsPrev_Click(object sender, EventArgs e)
        {
            if (NumPagesToShow == 1) { ShowOptionPageNumber(0); return; }

            // exact match
            for (int i = 0; i < NumPagesToShow; i++)
            {
                if (PagesToShow[i] == tabControl.SelectedIndex)
                {
                    CurrentOptionsPage = i;
                    CurrentOptionsPage--;
                    if (CurrentOptionsPage < 0) { CurrentOptionsPage = 0; }
                    ShowOptionPageNumber(PagesToShow[CurrentOptionsPage]);
                    return;
                }
            }
            // somewhere in between 2 pages                
            for (int i = 0; i < NumPagesToShow - 1; i++)
            {
                if ((PagesToShow[i] < tabControl.SelectedIndex) && (PagesToShow[i + 1] > tabControl.SelectedIndex))
                {
                    CurrentOptionsPage = i;
                    ShowOptionPageNumber(PagesToShow[CurrentOptionsPage]);
                    return;
                }
            }
            // after the last page
            if (tabControl.SelectedIndex > PagesToShow[NumPagesToShow - 1])
            {
                CurrentOptionsPage = NumPagesToShow - 1;
                ShowOptionPageNumber(PagesToShow[CurrentOptionsPage]);
            }
        }

        private void checkMapsWhiteBk_Click(object sender, EventArgs e)
        {
            if (checkMapsWhiteBk.Checked)
            { mapUtil.Back_Color = Color.White; mapUtil.Fore_Color = Color.Black; }
            else
            { mapUtil.Back_Color = bkColor; mapUtil.Fore_Color = foColor; }
        }

        private void comboMapDownload_SelectedIndexChanged(object sender, EventArgs e)
        {
            mapUtil.OsmTilesWebDownload = comboMapDownload.SelectedIndex;

            if (DoNotSaveSettingsFlag) { return; }

            if ((comboMapDownload.SelectedIndex != 0) && (LastOsmMapDownloadIndex == 0))
            {
                MessageBox.Show("Remember to set correct (or new blank) folder for map download\n\nCheck that you are connected to internet", "Map download is ON",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }

            LastOsmMapDownloadIndex = comboMapDownload.SelectedIndex;
        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            string exe_folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = "file:/Readme.htm";
            proc.StartInfo.WorkingDirectory = exe_folder;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }

        // DEBUG printout - make sure it is not called in release
        private void DebugPrintout()
        {
            try
            {
                string log_file = "\\gcc_debug_print.txt";
                if (IoFilesDirectory != "\\") { log_file = IoFilesDirectory + "\\gcc_debug_print.txt"; }

                FileStream fs = new FileStream(log_file, FileMode.Create);
                StreamWriter wr = new StreamWriter(fs);

                string altitude_str;
                double altitude = (CurrentZ - Decimal.ToDouble(numericGeoID.Value));

                // relative altitude mode
                if (checkRelativeAlt.Checked) { altitude = CurrentZ - (double)PlotZ[0]; }

                if ((comboUnits.SelectedIndex == 3) || (comboUnits.SelectedIndex == 5) || (comboUnits.SelectedIndex == 6))
                { altitude /= 0.30480; } // altitude in feet

                altitude_str = altitude.ToString("0");

                wr.WriteLine(" altitude " + altitude_str);
                wr.WriteLine(" CurrentZ " + CurrentZ.ToString());
                wr.WriteLine(" numericGeoID " + Decimal.ToDouble(numericGeoID.Value).ToString());

                wr.Close();
                fs.Close();
            }

            catch (Exception /*e*/) { }
        }

    }

    // button-like control that has a background image.
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
            Rectangle src_rec = new Rectangle(0, 0, backgroundImage.Width, backgroundImage.Height);
            Rectangle dest_rec = new Rectangle(0, 0, this.Width, this.Height);

            if (this.pressed && this.pressedImage != null)
                e.Graphics.DrawImage(this.pressedImage, dest_rec, src_rec, GraphicsUnit.Pixel);
            else
                e.Graphics.DrawImage(this.backgroundImage, dest_rec, src_rec, GraphicsUnit.Pixel);

            base.OnPaint(e);
        }
    }

    // Label which make text always fit (used to display long path, etc)
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

                e.Graphics.DrawString(this.Text,
                        this.Font,
                        new SolidBrush(this.ForeColor),
                        (this.ClientSize.Width - size.Width) / 2, 
                        (this.ClientSize.Height - size.Height) / 2);
            }

            base.OnPaint(e);
        }
    }

    public class NoBackgroundPanel : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }
    }
}
