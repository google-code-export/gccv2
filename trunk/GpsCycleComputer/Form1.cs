//#define DEBUG   -> is defined with Debug build
//#define BETA
//#define SERVICEPACK
//#define GPSPOWERTEST
//#define testClickCurrentPos

using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Reflection;
using System.Xml;
using System.Diagnostics;
using GpsSample.FileSupport;
using Microsoft.Win32;
using GpsUtils;
using LiveTracker;


/* From WIKI:
 1 international knot = 
 1 nautical mile per hour (exactly), 
 1.852 kilometres per hour (exactly),[5] 

 1 mile  =  1.609344 kilometers
 1 foot =.30480 of a meter 
  
 http://utrack.crempa.net/ - for GPX plots
*/

namespace GpsCycleComputer
{

    public class Form1 : System.Windows.Forms.Form
    {
#if DEBUG
        public static int debugBegin = 0;
        public static int debug1 = 0;
        public static int debug2 = 0;
        public static int debug3 = 0;
        public static int debug4 = 0;
        public static string debugStr = "1234567890";
#endif


        System.Globalization.CultureInfo IC = System.Globalization.CultureInfo.InvariantCulture;
        static string CurrentDirectory = "\\";        //directory of application (with \\)
        int importantNewsId = 0;

        public static Gps gps = new Gps();
        GpsPosition position = null;
        UtmUtil utmUtil = new UtmUtil();
        public MapUtil mapUtil = null;
        Graph graph = null;
        private Timer timerButtonHide = new Timer();
        const int timerButtonHideInterval = 9500;

        FileStream fstream;
        BinaryWriter writer;

        // custom buttons

        // button to set i/o location
        PictureButton buttonIoLocation = new PictureButton();
        PictureButton buttonMapsLocation = new PictureButton();
        PictureButton buttonLangLocation = new PictureButton();
        AlwaysFitLabel labelFileName = new AlwaysFitLabel();
        AlwaysFitLabel labelFileNameT2F = new AlwaysFitLabel();
        AlwaysFitLabel labelInfo = new AlwaysFitLabel();

        // CrossingWays buttons on Options2 tab
        PictureButton buttonCWShowKeyboard = new PictureButton();
        PictureButton buttonCWVerify = new PictureButton();

        // show help
        PictureButton buttonColorDlg = new PictureButton();
        PictureButton buttonUpdate = new PictureButton();
        PictureButton buttonHelp = new PictureButton();

        // button to show/hide option view selector
        PictureButton buttonShowViewSelector = new PictureButton();

        public NoBackgroundPanel NoBkPanel = new NoBackgroundPanel();

        public PictureButton button1 = new PictureButton();
        public PictureButton button2 = new PictureButton();
        public PictureButton button3 = new PictureButton();
        PictureButton button4 = new PictureButton();
        PictureButton button5 = new PictureButton();
        PictureButton button6 = new PictureButton();

        public MenuPage mPage = null;

        Bitmap AboutTabImage;
        //Bitmap BlankImage;
        Bitmap CWImage;

        public static Color bkColor;
        public static Color foColor;
        bool dayScheme = false;
        Color bkColor_day = Color.FromArgb(255, 255, 255);
        Color foColor_day = Color.FromArgb(34, 34, 34);
        Color bkColor_night = Color.FromArgb(34, 34, 34);
        Color foColor_night = Color.FromArgb(255, 255, 255);
        Color mapLabelColor = Color.Blue;

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
        //DateTime StartTime;   moved to TSummary
        double StartLat = 0.0;
        double StartLong = 0.0;
        int StartBattery = -255;
        double StartAlt = Int16.MinValue;


        // need to shift origin, to be able to save X/Y as short int in metres
        double OriginShiftX = 0.0;
        double OriginShiftY = 0.0;

        // Interval to to log GPS data (1-60s) or Interval to suspend GPS (Index 0-3 means always on)
        const int IndexDistanceMode = 4;
        const int IndexSuspendMode = 6;
        int[] PollGpsTimeSec = new int[17] { 1,2,5,10,    5, 10,    5, 10, 20, 30, 60, 2*60, 5*60, 10*60, 20*60, 30*60, 60*60};
        DateTime LastPointUtc = DateTime.MinValue;
        int GpsSearchCount = 0;         // x sec until fix
        int FirstSampleDropCount = 0;   // drop first x samples after fix
        int GpsSuspendCounter = 0;      // suspend for x sec
        int GpsLogCounter = 0;          // log every x sec
        double GpsLogDistance = 0;      // log every x m
        int AvgCount = 0;               // first averaging before point is fully valid

        //Drop first points (only available in Start/Stop mode            
        int [] dropFirst = new int [7] { 0, 1, 2, 4, 8, 16, 32 };
        
        // flag to indicate the the data was accepted by GetGpsData and save into file
        const byte GpsNotOk = 0;
        const byte GpsDrop = 1;
        const byte GpsBecameValid = 2;  //setReference if not set; initialize Old and Current variables
        const byte GpsInitVelo = 3;
        const byte GpsAvg = 4;          //average in suspend mode
        public const byte GpsOk = 5;
        const byte GpsBecameInvalid = 6;
        const byte GpsInvalidButTrust = 7;
        public byte GpsDataState = GpsNotOk;

        // to disable timer tick functions, if previous tick has not finished
        bool LockGpsTick = false;

        // flag when logging data and other states
        public enum State
        {
            nothing,
            logging,
            paused,
            logHrOnly,
            pauseHrOnly,        //currently not usable because pause button not shown when logHrOnly
            normalExit
        } public State state = State.nothing;
        bool ContinueAfterPause = false;

        // to indicate that it was stopped on low battery
        bool StoppedOnLow = false;

        // to save battery status (every 3 minutes)
        DateTime LastBatterySave;

        // average and max speed, distance. OldX/Y/T are coordinates/time of prev point.
        double MaxSpeed = 0.0;
        //double Distance = 0.0;   moved to TSummary
        double Odo = 0.0;
        double OldX = 0.0, OldY = 0.0;
        double ReferenceXDist = 0.0, ReferenceYDist = 0.0;

        int OldT = 0;

        // Current time, X/Y relative to starting point, abs height Z and current speed
        int CurrentTimeSec = 0;
        int CurrentStoppageTimeSec = 0;
        bool beepOnStartStop = false;
        public double CurrentLat = 0.0, CurrentLong = 0.0;
        double CurrentX, CurrentY;
        double CurrentAlt = Int16.MinValue;
        double ceff = 1.0;          //convertion factor km/h to other units
        double m2feet = 1.0;        //convertion factor m to feet
        bool CurrentAltInvalid = true;
        double ReferenceAlt = Int16.MaxValue;
        public const double AltThreshold = 30.0;        //gain and loss below theshold will be ignored
        //double ElevationGain = 0.0;   moved to TSummary
        double ElevationSlope = 0.0;
        double ReferencAltSlope = Int16.MinValue;
        double ReferenceXSlope = 0.0;
        double ReferenceYSlope = 0.0;
        //double AltitudeMax = Int16.MinValue;   moved to TSummary
        //double AltitudeMin = Int16.MaxValue;   moved to TSummary
        public double CurrentSpeed = Int16.MinValue*0.1;
        //string CurrentFileName = "";   moved to TSummary
        //string CurrentT2fName = "";   moved to T2fSummary
        string CurrentStatusString = "gps off ";
        public Color CurrentGpsLedColor = Color.Gray;
        int CurrentBattery = -255;
        double CurrentVx = 0.0;     //speed in x direction in m/s
        double CurrentVy = 0.0;     //speed in y direction in m/s
        double CurrentV = Int16.MinValue * 0.1;      //speed in km/h
        public int Heading = 720;          //current heading 720=invalid, but still up
        bool compass_north = false;     //compass in main screen shows north (rather than direction of movement)
        int compass_style = 0;          //0=compass arrow     1=digital   2=letters (in 22.5 degree steps: N NNE NE...)

        public int CurrentPlotIndex = -1;

        int MainConfigAlt2display = 0;  // 0=gain; 1=loss; 2=max; 3=min; 4=slope;    256-260=sequential, 128-132=sequential stopped
        int MainConfigSpeedSource = 0;  // 0=from gps; 1=from position; 2=both; 3=speed + heart_rate; 4=speed + hr + signal
        public enum eConfigDistance
        {
            eDistanceTrip = 0,          // do not change order, because used in string array and for option check
            eDistanceTrack2FollowStart,
            eDistanceTrack2FollowEnd,
            eDistanceOdo,
            eDistance2Destination,
        }
        eConfigDistance MainConfigDistance = eConfigDistance.eDistanceTrip;
        int MainConfigLatFormat = 0;    // 0=00.000000 1=N00°00.0000' 2=N00°00'00.00"
        bool MainConfigNav = false;
        bool MainConfigRelativeAlt = false;

        string clickLatLon = null;      //position of click in map
        double LongShiftUndo = 0.0;       //save before reset map for undo
        double LatShiftUndo = 0.0;
        double ZoomUndo = 0.0;

        // baud rates
        int[] BaudRates = new int[6] { 4800, 9600, 19200, 38400, 57600, 115200 };
        bool logRawNmea = false;

        // get pass the command line arguments
        static string FirstArgument;

        // data used for plotting and saving to KML/GPX
        // decimated, max size is PlotDataSize
        int PlotDataSize = 4096;
        public int PlotCount = 0;
        int Decimation = 1, DecimateCount = 0;
        float[] PlotLat;
        float[] PlotLong;
        public Int16[] PlotZ;
        public Int32[] PlotT;
        public Int16[] PlotS;
        public Int16[] PlotH;        //heart rate
        public Int32[] PlotD;

        // way-points
        const int WayPointDataSize = 128;
        int AudioEnum = 0;     // current enumeration of audio notes 

        public class WayPointInfo      // Structure to store Waypoints, used by track2follow
        {
            public int DataSize;
            public int Count;
            public float[] lat;
            public float[] lon;
            public string[] name;

            public WayPointInfo(int size)   //constructor
            {
                DataSize = size;
                Count = 0;
                lat = new float[size];
                lon = new float[size];
                name = new string[size];
            }
        };
        // Use same datasize for Waypoints in Track and T2F
        public WayPointInfo WayPointsT = new WayPointInfo(WayPointDataSize);
        public WayPointInfo WayPointsT2F = new WayPointInfo(WayPointDataSize);
        int showWayPoints = 1;      //    off / on / on_without_text

        public class TrackSummary
        {
            public string filename;     //with path and extension
            public string name;
            public string desc;
            public DateTime StartTime;
            public double Distance;
            public double AltitudeGain;
            public double AltitudeMax;
            public double AltitudeMin;

            public TrackSummary()
            { Clear(); }
            public void Clear()
            {
                filename = "";
                Clear2();
            }
            public void Clear2()
            {
                name = "";
                desc = "";
                StartTime = DateTime.MinValue;
                Distance = 0.0;
                AltitudeGain = 0.0;
                AltitudeMax = Int16.MinValue;
                AltitudeMin = Int16.MaxValue;
            }
        }
        TrackSummary T2fSummary = new TrackSummary();
        public TrackSummary TSummary = new TrackSummary();

        // lap statistics
        int LapNumber = 0;
        double LapStartD;
        int LapStartT;
        string currentLap = "";
        string lastLap = "";
        int lapManualDistance = 0;
        bool lapManualClick = false;

        // data for plotting 2nd line (track to follow)
        public float[] Plot2ndLat;
        public float[] Plot2ndLong;
        public Int16[] Plot2ndZ;
        public Int32[] Plot2ndT;
        public Int32[] Plot2ndD;
        public int Plot2ndCount = 0;
        int Plot2ndCountUndo = 0;

        private CheckBox checkConfirmStop;
        public CheckBox checkTouchOn;
        private ComboBox comboArraySize;
        private Label labelArraySize;
        public CheckBox checkShowExit;
        public CheckBox checkChangeMenuOptBKL;
        public NumericUpDown numericTrackTolerance;
        private Label labelTrackTolerance;
        private Label labelNavCmd;
        public ComboBox comboNavCmd;

        public HeartBeat oHeartBeat = null;
        private int getHeartRate()
        {
            if (oHeartBeat != null) return oHeartBeat.currentHeartRate;
            else return 0;
        }
        private int getHeartSignalStrength()
        {
            if (oHeartBeat != null) return oHeartBeat.signalStrength;
            else return 0;
        }

        public MapValue[] mapValuesConf = new MapValue[3] { MapValue.speed | MapValue.no_change, MapValue.time | MapValue.dist_2_dest, MapValue._off_ | MapValue.no_change };     //lower 8 bit for normal mode; next 8 bit for t2f mode
        public bool mapValuesShowName = true;
        public bool mapValuesCleanBack = true;
              
        // vars to work with the custom folder open box
        // 1. flag to activate folder open mode or file open mode in the custom dialog
        bool FolderSetupMode = false;
        // index where list of the current directories starts in the listBoxFiles
        int CurrentSubDirIndex;
        // current directory for i/o files
        string IoFilesDirectory;
        // flag that we are setting maps folder, io folder or language folder
        enum FolderMode :byte { Maps, Io, Lang };
        FolderMode folderMode = FolderMode.Maps;
        // current directory for maps files
        string MapsFilesDirectory;
        string LoadedSettingsName = "";
        public string LanguageDirectory;

        // vars to select which file type to open
        const byte FileOpenMode_Gcc = 0;
        const byte FileOpenMode_2ndGcc = 1;
        const byte FileOpenMode_2ndKml = 2;
        const byte FileOpenMode_2ndGpx = 3;
        byte FileOpenMode = FileOpenMode_Gcc;
        byte FileExtentionToOpen = FileOpenMode_2ndGcc;
        bool t2fAppendMode = false;

        // to save registry setting for GPD0: in unnatended mode, to be restored after stopping the program
        //Int32 SaveGpdUnattendedValue = 4;

        // main screen drawing mode (main or maps)
        const byte BufferDrawModeMain = 0;
        const byte BufferDrawModeMaps = 1;
        const byte BufferDrawModeGraph = 2;
        public const byte BufferDrawModeMenu = 3;      //only to have information of the current displayed page
        const byte BufferDrawModeOptions = 4;
        const byte BufferDrawModeLap = 5;
        const byte BufferDrawModeFiledialog = 6;
        const byte BufferDrawModeNavigate = 7;
        const byte BufferDrawModeHelp = 8;
        public byte BufferDrawMode = BufferDrawModeMain;

        public enum TrackEditMode : byte { Off, Track, T2f }
        public TrackEditMode trackEditMode = TrackEditMode.Off;

        // main screen drawing vars (to set position)
        int[] MGridX = new int[4] { 0, 263, 340, 480 };
        int[] MGridY = new int[8] { 0, 120, 184, 248, 324, 364, 368, 508 };
        int MGridDelta = 3;     // delta to have small gap between values and the border
        int MHeightDelta = 27;  // height of an item,  when we print a few values into a single cell
        public float df = 1.0f; // scale for fonts

        // vars for landscape support - move button from bottom to side and rescale
        public static bool isLandscape = false;
        bool LockResize = true;
        int scx_p, scx_q = 480;             //screen scale factor p/q     (q is last pixel value)
        int scy_p, scy_q = 508;             //main work area last value (without buttons and title bar)
        int buttonWidth = 0, buttonHeight = 0;
        bool AppFullScreen = true;

        bool configSyncSystemTime = false;
        bool configNoLogPassiveTime = false;
        int passiveTimeSeconds = 0;

        // Hashed password for CrossingWays, as the text edit will display ***, so we cannot read it from there
        string CwHashPassword = "";
        DateTime LastLiveLogging;
        int[] LiveLoggingTimeMin = new int[7] { 0, 1, 5, 10, 20, 30, 60 };
        public string CurrentLiveLoggingString = "";
        bool LockCwVerify = false;

        // var to show/hide options pages (resize for 16 pages max)
        int[] PagesToShow = new int[16];
        int NumPagesToShow = 0;
        int CurrentOptionsPage = 0;

        // form components
        private Panel tabBlank;
        //private Panel tabBlank1;
        private Panel tabOpenFile;
        private Timer timerGps;
        private ComboBox comboGpsPoll;
        private Label labelGpsActivity;
        private Label labelUnits;
        public ComboBox comboUnits;
        private Timer timerIdleReset;
        private CheckBox checkStopOnLow;
        private Label labelRevision;
        private CheckBox checkExStopTime;
        private ListBox listBoxFiles;
        private NumericUpDown numericGeoID;
        private CheckBox checkGpxRte;
        private CheckBox checkGpxSpeedMs;
        private CheckBox checkKmlAlt;
        private Microsoft.WindowsCE.Forms.InputPanel inputPanel;
        private CheckBox checkEditFileName;
        private CheckBox checkMainConfigSingleTap;
        private Label labelMultiMaps;
        private ComboBox comboMultiMaps;
        private Label labelKmlOpt2;
        private Label labelKmlOpt1;
        public ComboBox comboBoxKmlOptColor;
        private Label labelGpsBaudRate;
        private CheckBox checkBoxUseGccDll;
        private ComboBox comboBoxUseGccDllRate;
        private ComboBox comboBoxUseGccDllCom;
        public ComboBox comboBoxKmlOptWidth;
        private Label labelCw2;
        private Label labelCw1;
        private Label labelCwInfo;
        private Label labelCwLogMode;
        private ComboBox comboBoxCwLogMode;
        private TextBox textBoxCw2;
        private TextBox textBoxCw1;
        public ComboBox comboBoxLine2OptWidth;
        public ComboBox comboBoxLine2OptColor;
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
        private TextBox textLapOptions;
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
        private ComboBox comboDropFirst;
        private Label labelDropFirst;
        private CheckBox checkBeepOnFix;
        private NumericUpDown numericAvg;
        private Label labelCwUrl;
        private TextBox textBoxCwUrl;
        public CheckBox checkkeepAliveReg;
        private CheckBox checkWgs84Alt;
        private Timer timerButton;
        private CheckBox checkLapBeep;
        private Button buttonLapExport;
        private Panel panelCwLogo;
        private Label labelDefaultZoom;
        private NumericUpDown numericZoomRadius;

        private ContextMenu cMenu1 = new ContextMenu();
        private ContextMenu cSubMenu1 = new ContextMenu();
        private ContextMenu cSubMenu2 = new ContextMenu();
        private CheckBox checkGPSOffOnPowerOff;
        private CheckBox checkKeepBackLightOn;
        private CheckBox checkGPXtrkseg;
        public CheckBox checkDownloadOsm;

        string Revision;
        // c-tor. Create classes used, init some components
        public Form1()      //first executed, then Form1_Load()
        {
            //this.Menu = null;
            //this.ControlBox = false;
            
            // Required for Windows Form Designer support
            InitializeComponent();      //3162ms
            
            CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
            if (CurrentDirectory != "\\") { CurrentDirectory += "\\"; }

            mPage = new MenuPage();
            Controls.Add(mPage);
            mapUtil = new MapUtil();
            mapUtil.parent = this;
            graph = new Graph();
            graph.parent = this;

            // set defaults (shall load from file later)
            comboGpsPoll.SelectedIndex = 0;
            comboDropFirst.SelectedIndex = 0;
            comboUnits.SelectedIndex = 1;
            comboBoxKmlOptColor.SelectedIndex = 0;
            comboBoxKmlOptWidth.SelectedIndex = 1;
            comboBoxLine2OptColor.SelectedIndex = 1;
            comboBoxLine2OptWidth.SelectedIndex = 1;
            comboBoxUseGccDllRate.SelectedIndex = 0;
            comboBoxUseGccDllCom.SelectedIndex = 4;
            checkBoxUseGccDll.Checked = true;
            //checkPlotLine2AsDots.Checked = true;
            comboMultiMaps.SelectedIndex = 1;
            //comboMapDownload.SelectedIndex = 0; initializes at runtime
            comboBoxCwLogMode.SelectedIndex = 0;
            comboLapOptions.SelectedIndex = 0;
            comboArraySize.SelectedIndex = 0;
            comboNavCmd.SelectedIndex = 0;      //0=off; 1=voice on; 2=voice only important; 3=beep on; 4=beep only important

            Revision = Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "."
                     + Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString()
#if BETA
                     + "beta" + Assembly.GetExecutingAssembly().GetName().Version.Build.ToString()
#endif
#if SERVICEPACK
                     + "SP" + Assembly.GetExecutingAssembly().GetName().Version.Build.ToString()
#endif
                     ;
            labelRevision.Text = "programming/idea : AndyZap\ncontributors: expo7, AngelGR, Blaustein\n\nversion " + Revision;

            
            CreateCustomControls();         //3350ms
            
                                            //8s until here
            LockResize = false;

            cMenu1.Popup += new EventHandler(cMenu1_Popup);
            this.NoBkPanel.ContextMenu = cMenu1;
            mPage.ContextMenu = cMenu1;
            listBoxFiles.ContextMenu = cMenu1;
            //tabOpenFile.ContextMenu = cMenu1;         //don't work on WinCE  - listBoxFiles has no MouseDown
            //tabOpenFile.MouseDown += new MouseEventHandler(Form1_MouseDownCE);
            mPage.form1ref = this;
            Utils.form1ref = this;

            //for WinCE     -> for WM it is not necessary - does it really not harm on WM?
            contextMenuTimerCE.Tick += new EventHandler(contextMenuTimer_Tick);
            //this.MouseDown += new MouseEventHandler(Form1_MouseDownCE);   //is called in tabGraph_MouseDown
            mPage.MouseDown += new MouseEventHandler(Form1_MouseDownCE);
            mPage.MouseUp += new MouseEventHandler(Form1_MouseUpCE);
            timerButtonHide.Tick += new EventHandler(timerButtonHide_Tick);
        }

        Timer contextMenuTimerCE = new Timer();                     //required for context menu on WinCE
        void contextMenuTimer_Tick(object sender, EventArgs e)
        {
            contextMenuTimerCE.Enabled = false;
            if (MouseButtons == MouseButtons.Left)
            {
                Point pt = this.PointToClient(MousePosition);
                if (Math.Abs(pt.X - MouseClientX) + Math.Abs(pt.Y -MouseClientY) < this.Width / 64)
                    this.NoBkPanel.ContextMenu.Show(this, pt);
            }
        }
        void Form1_MouseDownCE(object sender, MouseEventArgs e)     //required for context menu on WinCE
        {
            MouseClientX = e.X;
            MouseClientY = e.Y;
            contextMenuTimerCE.Interval = 500;
            contextMenuTimerCE.Enabled = true;
        }
        void Form1_MouseUpCE(object sender, MouseEventArgs e)       //required for context menu on WinCE
        {
            contextMenuTimerCE.Enabled = false;
        }

        void AddMenuItem(string caption, bool check)
        {
            MenuItem mi = new MenuItem();
            mi.Text = caption;
            mi.Checked = check;
            mi.Click += new EventHandler(cMenuItem_Click);
            cMenu1.MenuItems.Add(mi);
        }
        void AddMenuItemSub(MenuItem cMenuItem, string caption, bool check, bool enable)
        {
            MenuItem mi = new MenuItem();
            mi.Text = caption;
            mi.Checked = check;
            mi.Enabled = enable;
            mi.Click += new EventHandler(subMenu_Click);
            cMenuItem.MenuItems.Add(mi);
        }
        
        private void cMenu1_Popup(object sender, EventArgs e)
        {
            cMenu1.MenuItems.Clear();
            Point pt = this.PointToClient(MousePosition);
            MouseClientX = pt.X;                 //must do this here too, because MouseDown() doesn't hit
            MouseClientY = pt.Y;

            switch (BufferDrawMode)
            {
                case BufferDrawModeMain:
                    {
                        if (MouseClientY < MGridY[1])
                        {
                            if (MouseClientX < MGridX[2])
                            {
                                AddMenuItem("inc stop", !checkExStopTime.Checked);      //Time
                                AddMenuItem("ex stop", checkExStopTime.Checked);
                                AddMenuItem("beep on start/stop", beepOnStartStop);
                                AddMenuItem("don't log passive time", configNoLogPassiveTime);
                            }
                            else
                            {
                                AddMenuItem("App full screen", AppFullScreen);
                                AddMenuItem("night scheme", !dayScheme);   //Clock
                                AddMenuItem("day scheme", dayScheme);
                                AddMenuItem("sync with GPS", configSyncSystemTime);
                            }
                        }
                        else if (MouseClientY < MGridY[3])
                        {
                            if (MouseClientX < MGridX[1])
                            {
                                AddMenuItem("from gps", MainConfigSpeedSource == 0);        //Speed
                                AddMenuItem("from position", MainConfigSpeedSource == 1);
                                AddMenuItem("both", MainConfigSpeedSource == 2);
                                AddMenuItem("speed + heart rate", MainConfigSpeedSource == 3);
                                AddMenuItem("speed + hr + signal", MainConfigSpeedSource == 4);
                            }
                            else if (MouseClientY < MGridY[2])
                            {
                                //avg
                            }
                            else
                            {
                                //max
                            }
                        }
                        else if (MouseClientY < MGridY[5])
                        {
                            if (MouseClientX < MGridX[1])
                            {                                                     //Distance
                                AddMenuItem("since start of trip", MainConfigDistance == eConfigDistance.eDistanceTrip);        // Distance from start of trip
                                // Distance to Track2Follow only available if a track2follow is loaded
                                if (Plot2ndCount > 0)
                                {
                                    AddMenuItem("to destination", MainConfigDistance == eConfigDistance.eDistance2Destination);
                                    AddMenuItem("to t2f start (bee line)", MainConfigDistance == eConfigDistance.eDistanceTrack2FollowStart);
                                    AddMenuItem("to t2f end (bee line)", MainConfigDistance == eConfigDistance.eDistanceTrack2FollowEnd);
                                }
                                AddMenuItem("ODO", MainConfigDistance == eConfigDistance.eDistanceOdo);
                            }
                            else if (comboLapOptions.SelectedIndex == 0)
                            {
                                if (MouseClientY < MGridY[4])
                                {
                                    AddMenuItem("absolute", !MainConfigRelativeAlt);   //Altitude cur
                                    AddMenuItem("relative", MainConfigRelativeAlt);
                                }
                                else
                                {
                                    AddMenuItem("gain", MainConfigAlt2display == 0);     //Altitude gain...
                                    AddMenuItem("loss", MainConfigAlt2display == 1);
                                    AddMenuItem("max", MainConfigAlt2display == 2);
                                    AddMenuItem("min", MainConfigAlt2display == 3);
                                    AddMenuItem("slope", MainConfigAlt2display == 4);
                                    AddMenuItem("<sequential>", MainConfigAlt2display >= 128);
                                }
                            }
                            else
                            {
                                //Lap
                            }
                        }
                        else if (MouseClientY < MGridY[7])
                        {
                            if (MouseClientX < MGridX[1])
                            {
                                //Info
                            }
                            else
                            {
                                AddMenuItem("beep on gps fix", checkBeepOnFix.Checked);         //GPS
                                AddMenuItem("show navigation here", MainConfigNav);
                                if (MainConfigNav)
                                {
                                    AddMenuItem("navigate backward", mapUtil.nav.backward);
                                    AddMenuItem("recalc min dist. to t2f", false);
                                }
                                else
                                {
                                    
                                    AddMenuItem("compass shows north", compass_north);
                                    AddMenuItem("compass style [" + compass_style + "]", false);
                                    AddMenuItem("LatLon dd.dddddd°", MainConfigLatFormat == 0);
                                    AddMenuItem("LatLon Ndd°mm.mmmm'", MainConfigLatFormat == 1);
                                    AddMenuItem("LatLon Ndd°mm'ss.ss\"", MainConfigLatFormat == 2);
                                }
                                AddMenuItem("log raw nmea", logRawNmea);
                            }
                        }
                        break;
                    }
                case BufferDrawModeMenu:
                    {
                        //MenuPage.
                        mPage.lastSelectedBFkt = (MenuPage.BFkt)mPage.getButtonIndex(MouseClientX, MouseClientY);
                        if (mPage.lastSelectedBFkt == MenuPage.BFkt.load_2follow)
                        {
                            if (Plot2ndCount + WayPointsT2F.Count > 0)
                            {
                                AddMenuItem("show summary", false);
                                AddMenuItem("enter name", false);
                                AddMenuItem("enter description", false);
                                AddMenuItem("save as GPX", false);
                                AddMenuItem("save as KML", false);
                                AddMenuItem("append T2F...", false);
                            }
                        }
                        else if (mPage.lastSelectedBFkt == MenuPage.BFkt.load_gcc)
                        {
                            if (TSummary.StartTime != DateTime.MinValue)
                            {
                                AddMenuItem("show summary", false);
                                AddMenuItem("enter name", false);
                                AddMenuItem("enter description", false);
                            }
                        }
                        else if (mPage.lastSelectedBFkt == MenuPage.BFkt.recall1 || mPage.lastSelectedBFkt == MenuPage.BFkt.recall2 || mPage.lastSelectedBFkt == MenuPage.BFkt.recall3)
                        {
                            AddMenuItem("save settings", false);
                            AddMenuItem("change name", false);
                        }
                        break;
                    }
                case BufferDrawModeGraph:
                    {
                        AddMenuItem("autoscale", false);
                        AddMenuItem("autoscale Y only", false);
                        if (graph.undoPossible) AddMenuItem("undo autoscale", false);
                        if (Plot2ndCount > 0) AddMenuItem("align t2f", graph.alignT2f);
                        if (PlotCount > 0 && Plot2ndCount > 0 && !graph.hideT2f || graph.hideTrack) AddMenuItem("hide track", graph.hideTrack);
                        if (PlotCount > 0 && Plot2ndCount > 0 && !graph.hideTrack || graph.hideT2f) AddMenuItem("hide t2f", graph.hideT2f);
                        if (Plot2ndCount + PlotCount > 0) AddMenuItem("Altitude", graph.sourceY == Graph.SourceY.Alt);
                        if (PlotCount > 0) AddMenuItem("Speed", graph.sourceY == Graph.SourceY.Speed);
                        if (oHeartBeat != null) AddMenuItem("Heart Rate", graph.sourceY == Graph.SourceY.Heart);
                        AddMenuItem("over time", graph.sourceX == Graph.SourceX.Time);
                        AddMenuItem("over distance", graph.sourceX == Graph.SourceX.Distance);
                        AddMenuItem("auto hide buttons", graph.autoHideButtons);
                        AddMenuItem("help", false);
                        break;
                    }
                case BufferDrawModeMaps:
                    {
                        if (trackEditMode == TrackEditMode.T2f)
                        {
                            if (Plot2ndCount > 0)
                                AddMenuItem("remove last point of t2f", false);
                            if (Plot2ndCountUndo > Plot2ndCount)
                                AddMenuItem("undo remove point", false);
                        }
                        // Alternative method to reset map position (same functionality as double click)
                        AddMenuItem("reset map (GPS/last)", mapUtil.ShowTrackToFollowMode == MapUtil.ShowTrackToFollow.T2FOff);
                        if (ZoomUndo != 0.0)
                            AddMenuItem("undo reset map", false);
                        // Show start or end of track to follow (only available if a track2follow is loaded)
                        if (Plot2ndCount > 0)
                        {
                            AddMenuItem("show track2follow - start", mapUtil.ShowTrackToFollowMode == MapUtil.ShowTrackToFollow.T2FStart);
                            AddMenuItem("show track2follow - end", mapUtil.ShowTrackToFollowMode == MapUtil.ShowTrackToFollow.T2FEnd);
                        }
                        AddMenuItem("edit t2f (add points)", trackEditMode == TrackEditMode.T2f);
                        // If logging is activated, we can add a waypoint (faster access as menu page)
                        if (state == State.logging || state == State.paused)
                            AddMenuItem("add waypoint to track", false);
                        AddMenuItem("add waypoint to t2f", false);
                        if (WayPointsT2F.Count > 0)
                            AddMenuItem("remove last waypoint (t2f)", false);
                        
                        if (Plot2ndCount > 0)
                        {
                            AddMenuItem("navigate backward", mapUtil.nav.backward);
                            AddMenuItem("calc min dist. to t2f", false);
                        }
                        if (checkDownloadOsm.Checked)
                            AddMenuItem("reload map tiles", false);

                        //submenu
                        MenuItem mi1 = new MenuItem();
                        MenuItem mi21 = new MenuItem();
                        MenuItem mi22 = new MenuItem();
                        MenuItem mi23 = new MenuItem();
                        mi1.Text = "config map display";
                        mi21.Text = "1st info field"; mi21.Enabled = !mapUtil.hideAllInfos;
                        mi22.Text = "2nd info field"; mi22.Enabled = !mapUtil.hideAllInfos;
                        mi23.Text = "3rd info field"; mi23.Enabled = !mapUtil.hideAllInfos;

                        //add 1st level
                        cMenu1.MenuItems.Add(mi1);
                        //add 2nd level
                        AddMenuItemSub(mi1, "hide all infos (tmp)", mapUtil.hideAllInfos, true);
                        if (PlotCount > 0)
                            AddMenuItemSub(mi1, "hide track (tmp)", mapUtil.hideTrack, true);
                        if (Plot2ndCount > 0)
                            AddMenuItemSub(mi1, "hide t2f (tmp)", mapUtil.hideT2f, true);
                        AddMenuItemSub(mi1, "hide map (tmp)", mapUtil.hideMap, true);

                        AddMenuItemSub(mi1, "-", false, true);      //permanent settings
                        AddMenuItemSub(mi1, "show map label", mapUtil.showMapLabel, !mapUtil.hideAllInfos);
                        if (Plot2ndCount > 0)
                            AddMenuItemSub(mi1, "show navigation", mapUtil.showNav, !mapUtil.hideAllInfos);
                        if (WayPointsT2F.Count > 0)
                            if (showWayPoints == 1)     //switch between off / on / on_without_text
                                AddMenuItemSub(mi1, "show waypoints +names", showWayPoints > 0, true);
                            else
                                AddMenuItemSub(mi1, "show waypoints", showWayPoints > 0, true);
                        AddMenuItemSub(mi1, "auto hide buttons", mapUtil.autoHideButtons, !mapUtil.hideAllInfos);
                        AddMenuItemSub(mi1, "draw map while shifting", mapUtil.drawWhileShift, !mapUtil.hideMap);
                        AddMenuItemSub(mi1, "-", false, true);
                        mi1.MenuItems.Add(mi21);
                        mi1.MenuItems.Add(mi22);
                        mi1.MenuItems.Add(mi23);
                        AddMenuItemSub(mi1, "show names", mapValuesShowName, !mapUtil.hideAllInfos);
                        AddMenuItemSub(mi1, "no map background", mapValuesCleanBack, !mapUtil.hideAllInfos);
                        //add 3rd level
                        AddMapValuesMenuItems(mi21, 0);
                        AddMapValuesMenuItems(mi22, 1);
                        AddMapValuesMenuItems(mi23, 2);
                        break;
                    }
                case BufferDrawModeNavigate:
                    {
                        AddMenuItem("navigate backward", mapUtil.nav.backward);
                        AddMenuItem("recalc min dist. to t2f", false);
                        AddMenuItem("show nav button", mapUtil.show_nav_button);
                        AddMenuItem("play voice test", false);
                    }
                    break;
                case BufferDrawModeFiledialog:
                    {
                        AddMenuItem("rename", false);
                        AddMenuItem("delete", false);
                    }
                    break;
            }
        }

        public enum MapValue
        {
            _off_,
            trip_time,
            time,
            speed,
            avg_speed,
            max_speed,
            distance,
            altitude_1,
            altitude_2,
            battery,
            SEPARATOR,
            no_change      = 256,
            _off__        = 512,
            dist_2_dest = 1024,
            time_2_dest = 2048,
            END
        }

        void AddMapValuesMenuItems(MenuItem mi, int field)
        {
            MapValue n;
            for (n = MapValue._off_; n < MapValue.SEPARATOR; n++)
                AddMenuItemSub(mi, n.ToString(), n == (mapValuesConf[field] & (MapValue)255), true);
            AddMenuItemSub(mi, "-", false, true);
            AddMenuItemSub(mi, "[in t2f mode]", false, false);
            for (n = MapValue.no_change; n < MapValue.END; n = (MapValue)((int)n << 1))
                AddMenuItemSub(mi, n.ToString(), n == (mapValuesConf[field] & (MapValue)~255), true);
        }

        void cMenuItem_Click(object sender, EventArgs e)
        {
            switch (((MenuItem)sender).Text)
            {
                // main page Time
                case "inc stop":
                    checkExStopTime.Checked = false; break;
                case "ex stop":
                    checkExStopTime.Checked = true; break;
                case "beep on start/stop":
                    beepOnStartStop = !beepOnStartStop; break;
                case "don't log passive time":
                    configNoLogPassiveTime = !configNoLogPassiveTime; break;
                // main page Clock
                case "App full screen":
                    AppFullScreen = !AppFullScreen;
                    FormFullScreen(AppFullScreen);
                    //DoOrientationSwitch();
                    break;
                case "night scheme":
                    dayScheme = false;
                    ApplyCustomBackground(); break;
                case "day scheme":
                    dayScheme = true;
                    ApplyCustomBackground(); break;
                case "sync with GPS":
                    configSyncSystemTime = !configSyncSystemTime; break;
                // main page Speed
                case "from gps":
                    MainConfigSpeedSource = 0; break;
                case "from position":
                    MainConfigSpeedSource = 1; break;
                case "both":
                    MainConfigSpeedSource = 2; break;
                case "speed + heart rate":
                    MainConfigSpeedSource = 3; break;
                case "speed + hr + signal":
                    MainConfigSpeedSource = 4; break;
                // main page Distance
                case "since start of trip":
                    MainConfigDistance = eConfigDistance.eDistanceTrip; break;
                case "to destination":
                    MainConfigDistance = eConfigDistance.eDistance2Destination; break;
                case "to t2f start (bee line)":
                    MainConfigDistance = eConfigDistance.eDistanceTrack2FollowStart; break;
                case "to t2f end (bee line)":
                    MainConfigDistance = eConfigDistance.eDistanceTrack2FollowEnd; break;
                case "ODO":
                    MainConfigDistance = eConfigDistance.eDistanceOdo; break;
                // main page Altitude
                case "absolute":
                    MainConfigRelativeAlt = false; break;
                case "relative":
                    MainConfigRelativeAlt = true; break;
                // main page Altitude2
                case "gain":
                    MainConfigAlt2display = 0; break;
                case "loss":
                    MainConfigAlt2display = 1; break;
                case "max":
                    MainConfigAlt2display = 2; break;
                case "min":
                    MainConfigAlt2display = 3; break;
                case "slope":
                    MainConfigAlt2display = 4; break;
                case "<sequential>":
                    MainConfigAlt2display = 256; break;
                // main page GPS
                case "beep on gps fix":
                    checkBeepOnFix.Checked = !checkBeepOnFix.Checked; break;
                case "show navigation here":
                    MainConfigNav = !MainConfigNav; break;
                case "compass shows north":
                    compass_north = !compass_north; break;
                case "compass style [0]":
                case "compass style [1]":
                    compass_style++; break;
                case "compass style [2]":
                    compass_style = 0; break;
                case "log raw nmea":
                    logRawNmea = !logRawNmea; break;
                case "LatLon dd.dddddd°":
                    MainConfigLatFormat = 0; break;
                case "LatLon Ndd°mm.mmmm'":
                    MainConfigLatFormat = 1; break;
                case "LatLon Ndd°mm'ss.ss\"":
                    MainConfigLatFormat = 2; break;
                //***** menu page *******************************************************
                case "show summary":
                    if (mPage.lastSelectedBFkt == MenuPage.BFkt.load_2follow)
                        ShowTrackSummary(T2fSummary);
                    else
                        ShowTrackSummary(TSummary);
                    break;
                case "enter name":
                    if (mPage.lastSelectedBFkt == MenuPage.BFkt.load_2follow)
                        Utils.InputBox("Track 2 Follow", "Name of the track", ref T2fSummary.name);
                    else
                    {
                        string str;
                        if (TSummary.name.Length == 0)
                            str = TSummary.StartTime.ToString();
                        else
                            str = TSummary.name;
                        if (Utils.InputBox("Main Track", "Name of the track", ref str) == DialogResult.OK)
                        {
                            TSummary.name = str;
                            WriteStringRecord(32, str);
                        }
                    }
                    break;
                case "enter description":
                    if (mPage.lastSelectedBFkt == MenuPage.BFkt.load_2follow)
                        Utils.InputBox("Track 2 Follow", "Description of the track", ref T2fSummary.desc);
                    else
                    {
                        string str;
                        if (TSummary.desc.Length == 0)
                            str = CreateTrackDescription();
                        else
                            str = TSummary.desc;
                        if (Utils.InputBox("Main Track", "Description of the track", ref str) == DialogResult.OK)
                        {
                            TSummary.desc = str;
                            WriteStringRecord(33, str);
                        }
                    }
                    break;
                case "save as GPX":
                    SaveT2F(true); break;
                case "save as KML":
                    SaveT2F(false); break;
                case "append T2F...":
                    t2fAppendMode = true;
                    buttonLoadTrack2Follow_Click(null, null); break;
                case "save settings":
                    // On the HTC TouchDiamond 2 Device / Windows Mobile 6.5, the returned Mouse Position is the Mouse Position of the
                    // PopUp selection and not of the Button itself. The result will be, it´s not possible to 
                    // change the name, or safe the data. 
                    // Bugfix: Store the the position of the button before opening the popup menu.
                    SaveSettings(CurrentDirectory + mPage.mBAr[(int)mPage.lastSelectedBFkt].text + ".dat");
                    LoadedSettingsName = mPage.mBAr[(int)mPage.lastSelectedBFkt].text;
                    break;
                case "change name":
                    Utils.InputBox("Rename", "input name", ref mPage.mBAr[(int)mPage.lastSelectedBFkt].text);
                    break;
                //***** graph page ********************************************************
                case "autoscale":
                    graph.scaleCmd = Graph.ScaleCmd.DoAutoscale; break;
                case "autoscale Y only":
                    graph.scaleCmd = Graph.ScaleCmd.DoYAutoscale; break;
                case "undo autoscale": graph.UndoAutoscale(); break;
                case "align t2f": graph.alignT2f = !graph.alignT2f; graph.scaleCmd = Graph.ScaleCmd.DoRedraw; break;
                case "hide track": graph.hideTrack = !graph.hideTrack; graph.scaleCmd = Graph.ScaleCmd.DoRedraw; break;
                case "hide t2f": graph.hideT2f = !graph.hideT2f; graph.scaleCmd = Graph.ScaleCmd.DoRedraw; break;
                case "Altitude": graph.SetSource(Graph.SourceY.Alt, Graph.SourceX.Old); break;
                case "Speed": graph.SetSource(Graph.SourceY.Speed, Graph.SourceX.Old); break;
                case "Heart Rate": graph.SetSource(Graph.SourceY.Heart, Graph.SourceX.Old); break;
                case "over time":
                    graph.SetSource(Graph.SourceY.Old, Graph.SourceX.Time);
                    break;
                case "over distance":
                    graph.SetSource(Graph.SourceY.Old, Graph.SourceX.Distance);
                    break;
                case "auto hide buttons": graph.autoHideButtons ^= true; graph.scaleCmd = Graph.ScaleCmd.DoRedraw; break;
                case "help":
                    buttonHelp_Click(null, null); break;
                //***** map page ************************************************************
                case "reset map (GPS/last)":
                    ResetMapPosition(); break;
                case "undo reset map":
                    mapUtil.LongShift = LongShiftUndo;
                    mapUtil.LatShift = LatShiftUndo;
                    mapUtil.ZoomValue = ZoomUndo;
                    ZoomUndo = 0.0; break;
                case "show track2follow - end":
                        if (Plot2ndCount > 0)
                        {
                            ResetMapPosition();
                            mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FEnd;
                            NoBkPanel.Invalidate();     // Update Screen
                        } break;
                case "show track2follow - start":
                        if (Plot2ndCount > 0)
                        {
                            ResetMapPosition();
                            mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FStart;
                            NoBkPanel.Invalidate();     // Update Screen
                        } break;
                case "edit t2f (add points)":
                    if (trackEditMode != TrackEditMode.T2f)
                        trackEditMode = TrackEditMode.T2f;
                    else
                        trackEditMode = TrackEditMode.Off;
                    MenuExec(MenuPage.BFkt.map);            //show the appropriate buttons
                    break;
                case "remove last point of t2f":
                    Plot2ndCount--;
                    updateNavData();
                    if (Plot2ndCount > 0)
                        T2fSummary.Distance = Plot2ndD[Plot2ndCount - 1];
                    else
                        MenuExec(MenuPage.BFkt.map);            //show the appropriate buttons (remove waypoint2)
                    break;
                case "undo remove point":
                    Plot2ndCount++;
                    T2fSummary.Distance = Plot2ndD[Plot2ndCount-1];
                    MenuExec(MenuPage.BFkt.map);            //show the appropriate buttons
                    break;
                case "add waypoint to track":
                    AddWaypoint(); break;
                case "add waypoint to t2f":
                    AddWaypoint2(true); break;
                case "remove last waypoint (t2f)":
                    WayPointsT2F.Count--; break;
                case "reload map tiles":
                    mapUtil.reDownloadMaps = true; break;

                //***** navigation page *********************************************************
                case "navigate backward":
                    mapUtil.clearNav(!mapUtil.nav.backward);
                    updateNavData();
                    break;
                case "calc min dist. to t2f":
                    mapUtil.clearNav(mapUtil.nav.backward);
                    mapUtil.nav.ShortSearch = 0;
                    break;
                case "show nav button":
                    mapUtil.show_nav_button = !mapUtil.show_nav_button; break;
                case "play voice test":
                    mapUtil.playVoiceTest(); break;

                case "rename":
                    string old_name = listBoxFiles.SelectedItem.ToString();
                    old_name = old_name.Replace("*", "");
                    old_name = old_name.Replace("+", "");
                    string new_name = old_name;
                    if (Utils.InputBox("File", "Rename", ref new_name) == DialogResult.OK)
                    {
                        if (IoFilesDirectory != "\\") { old_name = "\\" + old_name; new_name = "\\" + new_name; }
                        old_name = IoFilesDirectory + old_name;
                        new_name = IoFilesDirectory + new_name;
                        try { File.Move(old_name, new_name); }
                        catch { MessageBox.Show(null, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand, MessageBoxDefaultButton.Button1); }
                        refillListBox();
                    }
                    break;
                case "delete":
                    string file_name = listBoxFiles.SelectedItem.ToString();
                    file_name = file_name.Replace("*", "");
                    file_name = file_name.Replace("+", "");
                    if (DialogResult.OK == MessageBox.Show("Delete File?", file_name, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
                    {
                        if (IoFilesDirectory != "\\") { file_name = "\\" + file_name; }
                        file_name = IoFilesDirectory + file_name;
                        try { File.Delete(file_name); }
                        catch { MessageBox.Show(null, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand, MessageBoxDefaultButton.Button1); }
                        refillListBox();
                    }
                    break;

                default:
                    MessageBox.Show("no method for menu: " + ((MenuItem)sender).Text); break;
            }
            if (BufferDrawMode == BufferDrawModeMenu)
                mPage.deselectButton();
        }


        void subMenu_Click(object sender, EventArgs e)
        {
            int field;
            switch (((MenuItem)((MenuItem)sender).Parent).Text)
            {
                case "1st info field": field = 0; break;
                case "2nd info field": field = 1; break;
                case "3rd info field": field = 2; break;
                default: field = 0; break;
            }

            MapValue normalConf = mapValuesConf[field] & (MapValue)255;
            MapValue t2fConf = mapValuesConf[field] & (MapValue)~255;
            switch (((MenuItem)sender).Text)
            {
                case "hide all infos (tmp)":
                    mapUtil.hideAllInfos ^= true; break;
                case "hide track (tmp)":
                    mapUtil.hideTrack ^= true; break;
                case "hide t2f (tmp)":
                    mapUtil.hideT2f ^= true; break;
                case "hide map (tmp)":
                    mapUtil.hideMap ^= true; break;

                case "show map label":
                    mapUtil.showMapLabel ^= true; break;
                case "show navigation":
                    mapUtil.showNav ^= true; break;
                case "show waypoints":
                case "show waypoints +names":
                    if (++showWayPoints > 2) showWayPoints = 0; break;
                case "auto hide buttons":
                    mapUtil.autoHideButtons ^= true; break;
                case "draw map while shifting":
                    mapUtil.drawWhileShift ^= true; break;

                //info fields
                case "show names": mapValuesShowName = !mapValuesShowName; break;
                case "no map background": mapValuesCleanBack = !mapValuesCleanBack; break;

                case "_off_": mapValuesConf[field] = t2fConf | MapValue._off_; break;
                case "trip_time": mapValuesConf[field] = t2fConf | MapValue.trip_time; break;
                case "time": mapValuesConf[field] = t2fConf | MapValue.time; break;
                case "speed": mapValuesConf[field] = t2fConf | MapValue.speed; break;
                case "avg_speed": mapValuesConf[field] = t2fConf | MapValue.avg_speed; break;
                case "max_speed": mapValuesConf[field] = t2fConf | MapValue.max_speed; break;
                case "distance": mapValuesConf[field] = t2fConf | MapValue.distance; break;
                case "altitude_1": mapValuesConf[field] = t2fConf | MapValue.altitude_1; break;
                case "altitude_2": mapValuesConf[field] = t2fConf | MapValue.altitude_2; break;
                case "battery": mapValuesConf[field] = t2fConf | MapValue.battery; break;

                case "no_change": mapValuesConf[field] = normalConf | MapValue.no_change; break;
                case "_off__": mapValuesConf[field] = normalConf | MapValue._off__; break;
                case "dist_2_dest": mapValuesConf[field] = normalConf | MapValue.dist_2_dest; break;
                case "time_2_dest": mapValuesConf[field] = normalConf | MapValue.time_2_dest; break;
                default:
                    MessageBox.Show("not implemented."); break;
            }
        }

        private void ApplyCustomBackground()
        {
            if(dayScheme) { bkColor = bkColor_day; foColor = foColor_day; }
            else { bkColor = bkColor_night; foColor = foColor_night; }

            labelRevision.BackColor = bkColor;
            labelRevision.ForeColor = foColor;

            tabBlank.BackColor = bkColor;
            //tabBlank1.BackColor = bkColor;
            tabOpenFile.BackColor = bkColor;
            comboGpsPoll.BackColor = bkColor;  comboGpsPoll.ForeColor = foColor;
            labelGpsActivity.BackColor = bkColor; labelGpsActivity.ForeColor = foColor;
            comboDropFirst.BackColor = bkColor; comboDropFirst.ForeColor = foColor;
            labelDropFirst.BackColor = bkColor; labelDropFirst.ForeColor = foColor;
            comboUnits.BackColor = bkColor; comboUnits.ForeColor = foColor;
            checkStopOnLow.BackColor = bkColor; checkStopOnLow.ForeColor = foColor;
            checkBeepOnFix.BackColor = bkColor; checkBeepOnFix.ForeColor = foColor;
            checkExStopTime.BackColor = bkColor; checkExStopTime.ForeColor = foColor;
            labelFileName.BackColor = bkColor; labelFileName.ForeColor = foColor;
            labelFileNameT2F.BackColor = bkColor; labelFileNameT2F.ForeColor = foColor;
            labelInfo.BackColor = bkColor; labelInfo.ForeColor = foColor;
            labelUnits.BackColor = bkColor; labelUnits.ForeColor = foColor;
            listBoxFiles.BackColor = bkColor; listBoxFiles.ForeColor = foColor;
            buttonIoLocation.BackColor = bkColor; buttonIoLocation.ForeColor = foColor;
            buttonMapsLocation.BackColor = bkColor; buttonMapsLocation.ForeColor = foColor;
            buttonLangLocation.BackColor = bkColor; buttonLangLocation.ForeColor = foColor;

            checkWgs84Alt.BackColor = bkColor; checkWgs84Alt.ForeColor = foColor;
            numericGeoID.BackColor = bkColor; numericGeoID.ForeColor = foColor;
            numericAvg.BackColor = bkColor; numericAvg.ForeColor = foColor;
            checkGpxRte.BackColor = bkColor; checkGpxRte.ForeColor = foColor;
            checkGpxSpeedMs.BackColor = bkColor; checkGpxSpeedMs.ForeColor = foColor;
            checkKmlAlt.BackColor = bkColor; checkKmlAlt.ForeColor = foColor;
            checkGPXtrkseg.BackColor = bkColor; checkGPXtrkseg.ForeColor = foColor;
            buttonShowViewSelector.BackColor = bkColor; buttonShowViewSelector.ForeColor = foColor;

            checkEditFileName.BackColor = bkColor; checkEditFileName.ForeColor = foColor;
            //checkShowBkOff.BackColor = bkColor; checkShowBkOff.ForeColor = foColor;
            checkkeepAliveReg.BackColor = bkColor; checkkeepAliveReg.ForeColor = foColor;
            checkGPSOffOnPowerOff.BackColor = bkColor; checkGPSOffOnPowerOff.ForeColor = foColor;
            checkKeepBackLightOn.BackColor = bkColor; checkKeepBackLightOn.ForeColor = foColor;
            checkMainConfigSingleTap.BackColor = bkColor; checkMainConfigSingleTap.ForeColor = foColor;
            checkConfirmStop.BackColor = bkColor; checkConfirmStop.ForeColor = foColor;
            checkTouchOn.BackColor = bkColor; checkTouchOn.ForeColor = foColor;
            labelMultiMaps.BackColor = bkColor; labelMultiMaps.ForeColor = foColor;
            checkDownloadOsm.BackColor = bkColor; checkDownloadOsm.ForeColor = foColor;  
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
            labelCwUrl.BackColor = bkColor;        labelCwUrl.ForeColor = foColor;
            textBoxCwUrl.BackColor = bkColor;      textBoxCwUrl.ForeColor = foColor;
            panelCwLogo.BackColor = bkColor;

            comboBoxLine2OptWidth.BackColor = bkColor; comboBoxLine2OptWidth.ForeColor = foColor;
            comboBoxLine2OptColor.BackColor = bkColor; comboBoxLine2OptColor.ForeColor = foColor;
            labelLine2Opt1.BackColor = bkColor;        labelLine2Opt1.ForeColor = foColor;
            labelLine2Opt2.BackColor = bkColor;        labelLine2Opt2.ForeColor = foColor;

            buttonColorDlg.BackColor = bkColor; buttonColorDlg.ForeColor = foColor;
            buttonUpdate.BackColor = bkColor; buttonUpdate.ForeColor = foColor;
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
            checkShowExit.BackColor = bkColor; checkShowExit.ForeColor = foColor;
            checkChangeMenuOptBKL.BackColor = bkColor; checkChangeMenuOptBKL.ForeColor = foColor;

            numericGpxTimeShift.BackColor = bkColor; numericGpxTimeShift.ForeColor = foColor;
            labelGpxTimeShift.BackColor = bkColor; labelGpxTimeShift.ForeColor = foColor;
            labelArraySize.BackColor = bkColor; labelArraySize.ForeColor = foColor;
            comboArraySize.BackColor = bkColor; comboArraySize.ForeColor = foColor;
            checkMapsWhiteBk.BackColor = bkColor; checkMapsWhiteBk.ForeColor = foColor;

            comboLapOptions.BackColor = bkColor; comboLapOptions.ForeColor = foColor;
            checkLapBeep.BackColor = bkColor; checkLapBeep.ForeColor = foColor;
            buttonLapExport.BackColor = bkColor; buttonLapExport.ForeColor = foColor;
            textLapOptions.BackColor = bkColor; textLapOptions.ForeColor = foColor;
            mPage.BackColor = bkColor; mPage.ForeColor = foColor;
            labelDefaultZoom.BackColor = bkColor; labelDefaultZoom.ForeColor = foColor;
            numericZoomRadius.BackColor = bkColor; numericZoomRadius.ForeColor = foColor;
            labelTrackTolerance.BackColor = bkColor; labelTrackTolerance.ForeColor = foColor;
            numericTrackTolerance.BackColor = bkColor; numericTrackTolerance.ForeColor = foColor;
            labelNavCmd.BackColor = bkColor; labelNavCmd.ForeColor = foColor;
            comboNavCmd.BackColor = bkColor; comboNavCmd.ForeColor = foColor;

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
        public static Bitmap LoadBitmap(string name)
        {
            string file_name = CurrentDirectory + "skin\\" + name;
#if DEBUG
#else
            if (File.Exists(file_name))
            {
                return new Bitmap(file_name);
            }
#endif
            // not exists, load internal one
            return new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("GpsSample.Graphics." + name));
        }

        private void CreateCustomControls()
        {
            // Create custom buttons ----------------------------
            Assembly asm = Assembly.GetExecutingAssembly();

            // bottom menu --------------
            // ColorDlg
            buttonColorDlg.Parent = tabPageMainScr;
            buttonColorDlg.Bounds = new Rectangle(5, 370, 474, 46);
            buttonColorDlg.Text = "Select fore/back/mapLabel-color...";
            buttonColorDlg.Click += new EventHandler(buttonColorDlg_Click);
            buttonColorDlg.Font = new Font("Tahoma", 9F, FontStyle.Regular);
            buttonColorDlg.align = 1;

            // update
            buttonUpdate.Parent = tabPageAbout;
            buttonUpdate.Bounds = new Rectangle(5, 360, 474, 50);
            buttonUpdate.Text = "Check for Updates...";
            buttonUpdate.Click += buttonUpdate_Click;
            buttonUpdate.Font = new Font("Tahoma", 9F, FontStyle.Regular);
            buttonUpdate.align = 2;

            // help
            buttonHelp.Parent = tabPageAbout;
            buttonHelp.Bounds = new Rectangle(5, 410, 474, 50);
            buttonHelp.Text = "View readme...";
            buttonHelp.Click += buttonHelp_Click;
            buttonHelp.Font = new Font("Tahoma", 9F, FontStyle.Regular);
            buttonHelp.align = 2;

            // button to show/hide view selector
            buttonShowViewSelector.Parent = tabPageOptions;
            buttonShowViewSelector.Bounds = new Rectangle(5, 400, 474, 50);
            buttonShowViewSelector.Text = "Select option pages to scroll ...";
            buttonShowViewSelector.Click += buttonShowViewOpt_Click;
            buttonShowViewSelector.Font = new Font("Tahoma", 9F, FontStyle.Regular);
            buttonShowViewSelector.align = 3;

            // button to set maps location
            buttonMapsLocation.Parent = tabPageOptions;
            buttonMapsLocation.Bounds = new Rectangle(5, 0, 474, 80);
            buttonMapsLocation.Text = "Set maps files location ...";
            buttonMapsLocation.Click += buttonMapsLocation_Click;
            buttonMapsLocation.Font = new Font("Tahoma", 10F, FontStyle.Regular);
            buttonMapsLocation.align = 1;

            // button to set i/o location
            buttonIoLocation.Parent = tabPageOptions;
            buttonIoLocation.Bounds = new Rectangle(5, 80, 474, 80);
            buttonIoLocation.Text = "Set input/output files location ...";
            buttonIoLocation.Click += buttonFileLocation_Click;
            buttonIoLocation.Font = new Font("Tahoma", 10F, FontStyle.Regular);
            buttonIoLocation.align = 1;

            // button to set language location
            buttonLangLocation.Parent = tabPageOptions;
            buttonLangLocation.Bounds = new Rectangle(5, 160, 474, 80);
            buttonLangLocation.Text = "Set local\\language directory ...";
            buttonLangLocation.Click += buttonLangLocation_Click;
            buttonLangLocation.Font = new Font("Tahoma", 10F, FontStyle.Regular);
            buttonLangLocation.align = 1;


            // buttons on CW page
            buttonCWShowKeyboard.Parent = tabPageLiveLog;
            buttonCWShowKeyboard.Bounds = new Rectangle(202, 0, 270, 40);
            buttonCWShowKeyboard.Text = "Hide/show keyboard ...";
            buttonCWShowKeyboard.Click += buttonCWShowKeyboard_Click;
            buttonCWShowKeyboard.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            buttonCWShowKeyboard.align = 3;

            buttonCWVerify.Parent = tabPageLiveLog;
            buttonCWVerify.Bounds = new Rectangle(202, 185, 270, 40);
            buttonCWVerify.Text = "Verify login ...";
            buttonCWVerify.Click += buttonCWVerify_Click;
            buttonCWVerify.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            buttonCWVerify.align = 3;

            // Always Fit Label
            labelFileName.Parent = tabPageOptions;
            labelFileName.Bounds = new Rectangle(4, 260, 472, 28);
            labelFileName.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            labelFileName.Text = "Current File Name: ---";

            labelFileNameT2F.Parent = tabPageOptions;
            labelFileNameT2F.Bounds = new Rectangle(4, 290, 472, 28);
            labelFileNameT2F.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            labelFileNameT2F.Text = "Track to Follow: ---";

            labelInfo.Parent = tabPageOptions;
            labelInfo.Bounds = new Rectangle(4, 320, 472, 28);
            labelInfo.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            labelInfo.Text = "Info: ";

            // menu page
            mPage.Parent = this;
            mPage.Bounds = new Rectangle(0, 0, 480, 508);
            mPage.Click += button_Click;

            // universal buttons
            button1.Parent = this;
            button2.Parent = this;
            button3.Parent = this;
            button4.Parent = this;
            button5.Parent = this;
            button6.Parent = this;
            button1.Bounds = new Rectangle(0, 508, 160, 80);
            button2.Bounds = new Rectangle(160, 508, 160, 80);
            button3.Bounds = new Rectangle(320, 508, 160, 80);
            button4.Bounds = new Rectangle(0, 428, 160, 80);
            button5.Bounds = new Rectangle(160, 428, 160, 80);
            button6.Bounds = new Rectangle(320, 428, 160, 80);
            button1.Click += button_Click;
            button2.Click += button_Click;
            button3.Click += button_Click;
            button4.Click += button_Click;
            button5.Click += button_Click;
            button6.Click += button_Click;
            button1.MouseDown += button1_MouseDown;
            button1.MouseUp += button1_MouseUp;
            button1.DoubleClick += button1_DoubleClick;
            

            // No Background Panel for flicker-free paint
            NoBkPanel.Parent = this;
            NoBkPanel.Bounds = new Rectangle(0, 0, 480, 508);
            NoBkPanel.Name = "NoBkPanel";
            NoBkPanel.Paint += tabGraph_Paint;
            NoBkPanel.MouseMove += tabGraph_MouseMove;
            NoBkPanel.MouseUp += tabGraph_MouseUp;
            NoBkPanel.MouseDown += tabGraph_MouseDown;
            NoBkPanel.Click += tabGraph_MouseClick;
            NoBkPanel.DoubleClick += tabGraph_MouseDoubleClick;

            // about tab image, blank image and CW logo image
            AboutTabImage = new Bitmap(asm.GetManifestResourceStream("GpsSample.Graphics.about.jpg"));
            //BlankImage = LoadBitmap("blank.jpg");
            CWImage = new Bitmap(asm.GetManifestResourceStream("GpsSample.Graphics.CW_logo.png"));
        }


        private void ScaleControl(Control c)
        {
            Rectangle r = new Rectangle((c.Left*scx_p+scx_q/2)/scx_q, (c.Top*scy_p+scy_q/2)/scy_q, (c.Width*scx_p+scx_q/2)/scx_q, (c.Height*scy_p+scy_q/2)/scy_q);
            c.Bounds = r;
        }
        private void ScaleButton(Control c)
        {
            c.Width = buttonWidth;
            c.Height = buttonHeight;
        }
        
        private void ScaleToCurrentResolution()
        {
            ScaleControl(buttonMapsLocation);
            ScaleControl(buttonIoLocation);
            ScaleControl(buttonLangLocation);
            ScaleControl(buttonShowViewSelector);
            ScaleControl(comboGpsPoll);
            ScaleControl(comboDropFirst);
            ScaleControl(labelDropFirst);
            ScaleControl(labelGpsActivity);
            ScaleControl(comboUnits);
            ScaleControl(checkStopOnLow);
            ScaleControl(checkBeepOnFix);
            ScaleControl(labelRevision);
            ScaleControl(checkExStopTime);
            ScaleControl(labelFileName);
            ScaleControl(labelFileNameT2F);
            ScaleControl(labelInfo);
            ScaleControl(labelUnits);
            ScaleControl(checkWgs84Alt);

            ScaleControl(numericGeoID);
            ScaleControl(numericAvg);
            ScaleControl(checkGpxRte);
            ScaleControl(checkGpxSpeedMs);
            ScaleControl(checkKmlAlt);
            ScaleControl(checkGPXtrkseg);

            ScaleControl(checkEditFileName);
            //ScaleControl(checkShowBkOff);
            ScaleControl(checkkeepAliveReg);
            ScaleControl(checkKeepBackLightOn);
            ScaleControl(checkGPSOffOnPowerOff);
            ScaleControl(checkMainConfigSingleTap);
            ScaleControl(checkConfirmStop);
            ScaleControl(checkTouchOn);
            ScaleControl(labelMultiMaps);
            ScaleControl(checkDownloadOsm);
            ScaleControl(comboMultiMaps);
            ScaleControl(comboMapDownload);

            ScaleControl(labelKmlOpt2);
            ScaleControl(labelKmlOpt1);
            ScaleControl(comboBoxKmlOptColor);
            ScaleControl(comboBoxKmlOptWidth);
            ScaleControl(labelGpsBaudRate);
            ScaleControl(checkBoxUseGccDll);
            ScaleControl(comboBoxUseGccDllRate);
            ScaleControl(comboBoxUseGccDllCom);

            ScaleControl(buttonCWShowKeyboard);
            ScaleControl(buttonCWVerify);
            ScaleControl(labelCw2);
            ScaleControl(labelCw1);
            ScaleControl(labelCwInfo);
            ScaleControl(labelCwLogMode);
            ScaleControl(comboBoxCwLogMode);
            ScaleControl(textBoxCw2);
            ScaleControl(textBoxCw1);
            ScaleControl(checkUploadGpx);
            ScaleControl(labelCwUrl);
            ScaleControl(textBoxCwUrl);
            ScaleControl(panelCwLogo);

            ScaleControl(comboBoxLine2OptWidth);
            ScaleControl(comboBoxLine2OptColor);
            ScaleControl(labelLine2Opt1);
            ScaleControl(labelLine2Opt2);

            ScaleControl(buttonColorDlg);
            ScaleControl(buttonUpdate);
            ScaleControl(buttonHelp);

            ScaleControl(checkPlotTrackAsDots);
            ScaleControl(checkPlotLine2AsDots);

            ScaleControl(labelOptText);
            ScaleControl(checkOptAbout);
            ScaleControl(checkOptLiveLog);
            ScaleControl(checkOptLaps);
            ScaleControl(checkOptMaps);
            ScaleControl(checkOptGps);
            ScaleControl(checkOptKmlGpx);
            ScaleControl(checkOptMain);
            ScaleControl(checkShowExit);
            ScaleControl(checkChangeMenuOptBKL);

            ScaleControl(numericGpxTimeShift);
            ScaleControl(labelGpxTimeShift);
            ScaleControl(labelArraySize);
            ScaleControl(comboArraySize);
            ScaleControl(checkMapsWhiteBk);
            ScaleControl(labelDefaultZoom);
            ScaleControl(numericZoomRadius);
            ScaleControl(labelTrackTolerance);
            ScaleControl(numericTrackTolerance);
            ScaleControl(labelNavCmd);
            ScaleControl(comboNavCmd);

            ScaleControl(comboLapOptions);
            ScaleControl(checkLapBeep);
            ScaleControl(buttonLapExport);


            //ScaleControl(tabBlank);
            //ScaleControl(tabBlank1);
            
            ScaleControl(NoBkPanel);
            ScaleControl(tabControl);
            ScaleControl(tabOpenFile);
            ScaleControl(listBoxFiles);
            ScaleControl(mPage);
            ScaleControl(textLapOptions);

            ScaleButton(button1);
            ScaleButton(button2);
            ScaleButton(button3);
            ScaleButton(button4);
            ScaleButton(button5);
            ScaleButton(button6);

            mapUtil.ScaleNavSymbols(scx_p, scx_q);

            /*NoBkPanel.Height = this.ClientSize.Height - heighReductionForButtons;
            tabControl.Height = NoBkPanel.Height;
            textLapOptions.Height = NoBkPanel.Height - textLapOptions.Top - comboLapOptions.Height;     //comboLapOptions.Height as dummy for height of tab-selector (ClientSize doesn't do)
            mPage.Height = NoBkPanel.Height;
            */
            if (isLandscape)
                tabOpenFile.Height = NoBkPanel.Height;
            else
                tabOpenFile.Height = NoBkPanel.Height - buttonHeight;
            listBoxFiles.Height = tabOpenFile.Height;

            // main drawing grid
            for (int i = 0; i < MGridX.Length; i++) { MGridX[i] = (MGridX[i]*scx_p+scx_q/2)/scx_q; }
            for (int i = 0; i < MGridY.Length; i++) { MGridY[i] = (MGridY[i]*scy_p+scy_q/2)/scy_q; }
            MGridDelta = (MGridDelta * scy_p+scy_q/2)/scy_q;
            MHeightDelta = (MHeightDelta * scy_p+scy_q/2)/scy_q;

        }

        private bool CheckMapsDirectoryExists()
        {
            if (Directory.Exists(MapsFilesDirectory)) { return true; }

            MessageBox.Show("Resetting maps files location to the application folder", "Folder does not exist!",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

            MapsFilesDirectory = CurrentDirectory;
            return false;
        }
        private bool CheckIoDirectoryExists()
        {
            if (Directory.Exists(IoFilesDirectory)) 
            { 
                return true; 
            }

            MessageBox.Show("Resetting input/output files location to the application folder", "Folder does not exist!",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

            IoFilesDirectory = CurrentDirectory;
            return false;
        }
        private bool CheckLanguageDirectoryExists()
        {
            if (LanguageDirectory == null)
                LanguageDirectory = CurrentDirectory + "local\\eng";
            if (Directory.Exists(LanguageDirectory))
            {
                return true;
            }

            MessageBox.Show("Resetting language directory to default and create it", "Folder does not exist!",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

            LanguageDirectory = CurrentDirectory + "local\\eng";
            Directory.CreateDirectory(LanguageDirectory);
            return false;
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.numericZoomRadius = new System.Windows.Forms.NumericUpDown();
            this.labelRevision = new System.Windows.Forms.Label();
            this.tabOpenFile = new System.Windows.Forms.Panel();
            this.listBoxFiles = new System.Windows.Forms.ListBox();
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
            this.checkExStopTime = new System.Windows.Forms.CheckBox();
            this.comboUnits = new System.Windows.Forms.ComboBox();
            this.labelUnits = new System.Windows.Forms.Label();
            this.checkEditFileName = new System.Windows.Forms.CheckBox();
            this.checkMainConfigSingleTap = new System.Windows.Forms.CheckBox();
            this.textLapOptions = new System.Windows.Forms.TextBox();
            this.comboLapOptions = new System.Windows.Forms.ComboBox();
            this.numericGpxTimeShift = new System.Windows.Forms.NumericUpDown();
            this.labelGpxTimeShift = new System.Windows.Forms.Label();
            this.checkKmlAlt = new System.Windows.Forms.CheckBox();
            this.checkGpxRte = new System.Windows.Forms.CheckBox();
            this.checkPlotTrackAsDots = new System.Windows.Forms.CheckBox();
            this.comboBoxKmlOptWidth = new System.Windows.Forms.ComboBox();
            this.comboBoxKmlOptColor = new System.Windows.Forms.ComboBox();
            this.labelMultiMaps = new System.Windows.Forms.Label();
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
            this.inputPanel = new Microsoft.WindowsCE.Forms.InputPanel();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageOptions = new System.Windows.Forms.TabPage();
            this.tabPageGps = new System.Windows.Forms.TabPage();
            this.checkKeepBackLightOn = new System.Windows.Forms.CheckBox();
            this.checkGPSOffOnPowerOff = new System.Windows.Forms.CheckBox();
            this.checkWgs84Alt = new System.Windows.Forms.CheckBox();
            this.checkkeepAliveReg = new System.Windows.Forms.CheckBox();
            this.numericAvg = new System.Windows.Forms.NumericUpDown();
            this.checkBeepOnFix = new System.Windows.Forms.CheckBox();
            this.comboDropFirst = new System.Windows.Forms.ComboBox();
            this.labelDropFirst = new System.Windows.Forms.Label();
            this.tabPageMainScr = new System.Windows.Forms.TabPage();
            this.checkChangeMenuOptBKL = new System.Windows.Forms.CheckBox();
            this.checkShowExit = new System.Windows.Forms.CheckBox();
            this.comboArraySize = new System.Windows.Forms.ComboBox();
            this.labelArraySize = new System.Windows.Forms.Label();
            this.checkTouchOn = new System.Windows.Forms.CheckBox();
            this.checkConfirmStop = new System.Windows.Forms.CheckBox();
            this.tabPageMapScr = new System.Windows.Forms.TabPage();
            this.comboNavCmd = new System.Windows.Forms.ComboBox();
            this.labelNavCmd = new System.Windows.Forms.Label();
            this.numericTrackTolerance = new System.Windows.Forms.NumericUpDown();
            this.labelTrackTolerance = new System.Windows.Forms.Label();
            this.checkDownloadOsm = new System.Windows.Forms.CheckBox();
            this.labelDefaultZoom = new System.Windows.Forms.Label();
            this.tabPageKmlGpx = new System.Windows.Forms.TabPage();
            this.checkGPXtrkseg = new System.Windows.Forms.CheckBox();
            this.checkGpxSpeedMs = new System.Windows.Forms.CheckBox();
            this.tabPageLiveLog = new System.Windows.Forms.TabPage();
            this.textBoxCwUrl = new System.Windows.Forms.TextBox();
            this.labelCwUrl = new System.Windows.Forms.Label();
            this.checkUploadGpx = new System.Windows.Forms.CheckBox();
            this.tabPageLaps = new System.Windows.Forms.TabPage();
            this.checkLapBeep = new System.Windows.Forms.CheckBox();
            this.buttonLapExport = new System.Windows.Forms.Button();
            this.tabPageAbout = new System.Windows.Forms.TabPage();
            this.timerButton = new System.Windows.Forms.Timer();
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
            // numericZoomRadius
            // 
            this.numericZoomRadius.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericZoomRadius.Location = new System.Drawing.Point(320, 309);
            this.numericZoomRadius.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numericZoomRadius.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericZoomRadius.Name = "numericZoomRadius";
            this.numericZoomRadius.Size = new System.Drawing.Size(155, 36);
            this.numericZoomRadius.TabIndex = 52;
            this.numericZoomRadius.Value = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericZoomRadius.ValueChanged += new System.EventHandler(this.numericZoomRadiusChanged);
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
            this.tabOpenFile.Size = new System.Drawing.Size(480, 428);
            // 
            // listBoxFiles
            // 
            this.listBoxFiles.Location = new System.Drawing.Point(0, 0);
            this.listBoxFiles.Name = "listBoxFiles";
            this.listBoxFiles.Size = new System.Drawing.Size(480, 408);
            this.listBoxFiles.TabIndex = 0;
            this.listBoxFiles.SelectedIndexChanged += new System.EventHandler(this.listBoxFiles_SelectedIndexChanged);
            // 
            // tabBlank
            // 
            this.tabBlank.Location = new System.Drawing.Point(0, 0);
            this.tabBlank.Name = "tabBlank";
            this.tabBlank.Size = new System.Drawing.Size(100, 100);
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
            this.checkStopOnLow.Location = new System.Drawing.Point(5, 97);
            this.checkStopOnLow.Name = "checkStopOnLow";
            this.checkStopOnLow.Size = new System.Drawing.Size(471, 40);
            this.checkStopOnLow.TabIndex = 16;
            this.checkStopOnLow.Text = "Stop GPS if battery <20%";
            this.checkStopOnLow.Click += new System.EventHandler(this.comboGpsPoll_SelectedIndexChanged);
            // 
            // comboGpsPoll
            // 
            this.comboGpsPoll.Items.Add("always on; log ev. 1 sec");
            this.comboGpsPoll.Items.Add("always on; log ev. 2 sec");
            this.comboGpsPoll.Items.Add("always on; log ev. 5 sec");
            this.comboGpsPoll.Items.Add("always on; log ev. 10 sec");
            this.comboGpsPoll.Items.Add("always on; log ev. 5m/30s");
            this.comboGpsPoll.Items.Add("always on; log ev.10m/30s");
            this.comboGpsPoll.Items.Add("run every 5 sec");
            this.comboGpsPoll.Items.Add("run every 10 sec");
            this.comboGpsPoll.Items.Add("run every 20 sec");
            this.comboGpsPoll.Items.Add("run every 30 sec");
            this.comboGpsPoll.Items.Add("run every 1 min");
            this.comboGpsPoll.Items.Add("run every 2 min");
            this.comboGpsPoll.Items.Add("run every 5 min");
            this.comboGpsPoll.Items.Add("run every 10 min");
            this.comboGpsPoll.Items.Add("run every 20 min");
            this.comboGpsPoll.Items.Add("run every 30 min");
            this.comboGpsPoll.Items.Add("run every 1 hour");
            this.comboGpsPoll.Location = new System.Drawing.Point(142, 5);
            this.comboGpsPoll.Name = "comboGpsPoll";
            this.comboGpsPoll.Size = new System.Drawing.Size(334, 41);
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
            this.checkBoxUseGccDll.Location = new System.Drawing.Point(5, 143);
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
            this.comboBoxUseGccDllRate.Location = new System.Drawing.Point(324, 184);
            this.comboBoxUseGccDllRate.Name = "comboBoxUseGccDllRate";
            this.comboBoxUseGccDllRate.Size = new System.Drawing.Size(152, 41);
            this.comboBoxUseGccDllRate.TabIndex = 32;
            // 
            // comboBoxUseGccDllCom
            // 
            this.comboBoxUseGccDllCom.Items.Add("COM0:");
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
            this.comboBoxUseGccDllCom.Items.Add("\\nmea.txt");
            this.comboBoxUseGccDllCom.Location = new System.Drawing.Point(324, 141);
            this.comboBoxUseGccDllCom.Name = "comboBoxUseGccDllCom";
            this.comboBoxUseGccDllCom.Size = new System.Drawing.Size(153, 41);
            this.comboBoxUseGccDllCom.TabIndex = 31;
            // 
            // labelGpsBaudRate
            // 
            this.labelGpsBaudRate.Location = new System.Drawing.Point(184, 189);
            this.labelGpsBaudRate.Name = "labelGpsBaudRate";
            this.labelGpsBaudRate.Size = new System.Drawing.Size(134, 40);
            this.labelGpsBaudRate.Text = "Baud rate:";
            // 
            // numericGeoID
            // 
            this.numericGeoID.Location = new System.Drawing.Point(348, 231);
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
            this.numericGeoID.Size = new System.Drawing.Size(128, 36);
            this.numericGeoID.TabIndex = 0;
            // 
            // checkExStopTime
            // 
            this.checkExStopTime.Checked = true;
            this.checkExStopTime.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkExStopTime.Location = new System.Drawing.Point(4, 51);
            this.checkExStopTime.Name = "checkExStopTime";
            this.checkExStopTime.Size = new System.Drawing.Size(476, 40);
            this.checkExStopTime.TabIndex = 29;
            this.checkExStopTime.Text = "Exclude stop time";
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
            this.comboUnits.Location = new System.Drawing.Point(125, 7);
            this.comboUnits.Name = "comboUnits";
            this.comboUnits.Size = new System.Drawing.Size(352, 41);
            this.comboUnits.TabIndex = 4;
            // 
            // labelUnits
            // 
            this.labelUnits.Location = new System.Drawing.Point(40, 8);
            this.labelUnits.Name = "labelUnits";
            this.labelUnits.Size = new System.Drawing.Size(79, 40);
            this.labelUnits.Text = "Units:";
            // 
            // checkEditFileName
            // 
            this.checkEditFileName.Location = new System.Drawing.Point(4, 97);
            this.checkEditFileName.Name = "checkEditFileName";
            this.checkEditFileName.Size = new System.Drawing.Size(476, 40);
            this.checkEditFileName.TabIndex = 19;
            this.checkEditFileName.Text = "Ask for log file name";
            // 
            // checkMainConfigSingleTap
            // 
            this.checkMainConfigSingleTap.Location = new System.Drawing.Point(4, 143);
            this.checkMainConfigSingleTap.Name = "checkMainConfigSingleTap";
            this.checkMainConfigSingleTap.Size = new System.Drawing.Size(476, 40);
            this.checkMainConfigSingleTap.TabIndex = 20;
            this.checkMainConfigSingleTap.Text = "Single Tap for Config";
            // 
            // textLapOptions
            // 
            this.textLapOptions.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textLapOptions.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            this.textLapOptions.Location = new System.Drawing.Point(0, 100);
            this.textLapOptions.Multiline = true;
            this.textLapOptions.Name = "textLapOptions";
            this.textLapOptions.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLapOptions.Size = new System.Drawing.Size(480, 360);
            this.textLapOptions.TabIndex = 9;
            this.textLapOptions.TabStop = false;
            this.textLapOptions.WordWrap = false;
            // 
            // comboLapOptions
            // 
            this.comboLapOptions.Items.Add("off");
            this.comboLapOptions.Items.Add("auto (not implemented yet)");
            this.comboLapOptions.Items.Add("manual (press main screen)");
            this.comboLapOptions.Items.Add("distance based: 400m");
            this.comboLapOptions.Items.Add("distance based: 1km");
            this.comboLapOptions.Items.Add("distance based: 2km");
            this.comboLapOptions.Items.Add("distance based: 5km");
            this.comboLapOptions.Items.Add("time based: 1min");
            this.comboLapOptions.Items.Add("time based: 2min");
            this.comboLapOptions.Items.Add("time based: 5min");
            this.comboLapOptions.Location = new System.Drawing.Point(2, 4);
            this.comboLapOptions.Name = "comboLapOptions";
            this.comboLapOptions.Size = new System.Drawing.Size(476, 41);
            this.comboLapOptions.TabIndex = 5;
            // 
            // numericGpxTimeShift
            // 
            this.numericGpxTimeShift.Location = new System.Drawing.Point(342, 155);
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
            this.numericGpxTimeShift.Size = new System.Drawing.Size(134, 36);
            this.numericGpxTimeShift.TabIndex = 19;
            // 
            // labelGpxTimeShift
            // 
            this.labelGpxTimeShift.Location = new System.Drawing.Point(2, 155);
            this.labelGpxTimeShift.Name = "labelGpxTimeShift";
            this.labelGpxTimeShift.Size = new System.Drawing.Size(334, 36);
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
            this.checkGpxRte.Text = "Save GPX with \"rte\" tag (see readme)";
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
            this.comboBoxKmlOptWidth.Location = new System.Drawing.Point(360, 47);
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
            this.comboBoxKmlOptColor.Location = new System.Drawing.Point(108, 47);
            this.comboBoxKmlOptColor.Name = "comboBoxKmlOptColor";
            this.comboBoxKmlOptColor.Size = new System.Drawing.Size(171, 41);
            this.comboBoxKmlOptColor.TabIndex = 29;
            // 
            // labelMultiMaps
            // 
            this.labelMultiMaps.Location = new System.Drawing.Point(3, 225);
            this.labelMultiMaps.Name = "labelMultiMaps";
            this.labelMultiMaps.Size = new System.Drawing.Size(133, 40);
            this.labelMultiMaps.Text = "Multi-maps";
            // 
            // comboMultiMaps
            // 
            this.comboMultiMaps.Items.Add("off");
            this.comboMultiMaps.Items.Add("multi maps, 1:1 zoom");
            this.comboMultiMaps.Items.Add("multi maps, 2x zoom");
            this.comboMultiMaps.Items.Add("multi maps, 4x zoom");
            this.comboMultiMaps.Items.Add("multi maps, 0.5x zoom");
            this.comboMultiMaps.Location = new System.Drawing.Point(170, 224);
            this.comboMultiMaps.Name = "comboMultiMaps";
            this.comboMultiMaps.Size = new System.Drawing.Size(305, 41);
            this.comboMultiMaps.TabIndex = 34;
            // 
            // comboMapDownload
            // 
            this.comboMapDownload.Location = new System.Drawing.Point(170, 267);
            this.comboMapDownload.Name = "comboMapDownload";
            this.comboMapDownload.Size = new System.Drawing.Size(305, 41);
            this.comboMapDownload.TabIndex = 35;
            this.comboMapDownload.SelectedIndexChanged += new System.EventHandler(this.comboMapDownload_SelectedIndexChanged);
            // 
            // labelKmlOpt1
            // 
            this.labelKmlOpt1.Location = new System.Drawing.Point(2, 48);
            this.labelKmlOpt1.Name = "labelKmlOpt1";
            this.labelKmlOpt1.Size = new System.Drawing.Size(105, 40);
            this.labelKmlOpt1.Text = "Track";
            // 
            // labelKmlOpt2
            // 
            this.labelKmlOpt2.Location = new System.Drawing.Point(285, 48);
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
            this.comboBoxLine2OptWidth.Location = new System.Drawing.Point(360, 135);
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
            this.comboBoxLine2OptColor.Location = new System.Drawing.Point(109, 135);
            this.comboBoxLine2OptColor.Name = "comboBoxLine2OptColor";
            this.comboBoxLine2OptColor.Size = new System.Drawing.Size(171, 41);
            this.comboBoxLine2OptColor.TabIndex = 38;
            // 
            // labelLine2Opt1
            // 
            this.labelLine2Opt1.Location = new System.Drawing.Point(2, 137);
            this.labelLine2Opt1.Name = "labelLine2Opt1";
            this.labelLine2Opt1.Size = new System.Drawing.Size(105, 40);
            this.labelLine2Opt1.Text = "Track2f";
            // 
            // labelLine2Opt2
            // 
            this.labelLine2Opt2.Location = new System.Drawing.Point(285, 137);
            this.labelLine2Opt2.Name = "labelLine2Opt2";
            this.labelLine2Opt2.Size = new System.Drawing.Size(76, 40);
            this.labelLine2Opt2.Text = "width";
            // 
            // labelCw2
            // 
            this.labelCw2.Location = new System.Drawing.Point(2, 141);
            this.labelCw2.Name = "labelCw2";
            this.labelCw2.Size = new System.Drawing.Size(140, 40);
            this.labelCw2.Text = "Password:";
            // 
            // labelCw1
            // 
            this.labelCw1.Location = new System.Drawing.Point(2, 94);
            this.labelCw1.Name = "labelCw1";
            this.labelCw1.Size = new System.Drawing.Size(146, 40);
            this.labelCw1.Text = "User name:";
            // 
            // textBoxCw2
            // 
            this.textBoxCw2.Location = new System.Drawing.Point(169, 140);
            this.textBoxCw2.Name = "textBoxCw2";
            this.textBoxCw2.Size = new System.Drawing.Size(308, 41);
            this.textBoxCw2.TabIndex = 2;
            this.textBoxCw2.GotFocus += new System.EventHandler(this.CWShowKeyboard);
            this.textBoxCw2.LostFocus += new System.EventHandler(this.CWHideKeyboard);
            // 
            // textBoxCw1
            // 
            this.textBoxCw1.Location = new System.Drawing.Point(169, 93);
            this.textBoxCw1.Name = "textBoxCw1";
            this.textBoxCw1.Size = new System.Drawing.Size(308, 41);
            this.textBoxCw1.TabIndex = 1;
            this.textBoxCw1.GotFocus += new System.EventHandler(this.CWShowKeyboard);
            this.textBoxCw1.LostFocus += new System.EventHandler(this.CWHideKeyboard);
            // 
            // labelCwInfo
            // 
            this.labelCwInfo.Location = new System.Drawing.Point(2, 317);
            this.labelCwInfo.Name = "labelCwInfo";
            this.labelCwInfo.Size = new System.Drawing.Size(474, 40);
            this.labelCwInfo.Text = "Visit www.crossingways.com for all info";
            // 
            // labelCwLogMode
            // 
            this.labelCwLogMode.Location = new System.Drawing.Point(2, 231);
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
            this.comboBoxCwLogMode.Location = new System.Drawing.Point(169, 229);
            this.comboBoxCwLogMode.Name = "comboBoxCwLogMode";
            this.comboBoxCwLogMode.Size = new System.Drawing.Size(308, 41);
            this.comboBoxCwLogMode.TabIndex = 5;
            // 
            // panelCwLogo
            // 
            this.panelCwLogo.Location = new System.Drawing.Point(60, 360);
            this.panelCwLogo.Name = "panelCwLogo";
            this.panelCwLogo.Size = new System.Drawing.Size(353, 103);
            this.panelCwLogo.Paint += new System.Windows.Forms.PaintEventHandler(this.panelCwLogo_Paint);
            // 
            // checkMapsWhiteBk
            // 
            this.checkMapsWhiteBk.Location = new System.Drawing.Point(0, 182);
            this.checkMapsWhiteBk.Name = "checkMapsWhiteBk";
            this.checkMapsWhiteBk.Size = new System.Drawing.Size(469, 40);
            this.checkMapsWhiteBk.TabIndex = 45;
            this.checkMapsWhiteBk.Text = "White background";
            this.checkMapsWhiteBk.Click += new System.EventHandler(this.checkMapsWhiteBk_Click);
            // 
            // checkPlotLine2AsDots
            // 
            this.checkPlotLine2AsDots.Location = new System.Drawing.Point(0, 94);
            this.checkPlotLine2AsDots.Name = "checkPlotLine2AsDots";
            this.checkPlotLine2AsDots.Size = new System.Drawing.Size(469, 40);
            this.checkPlotLine2AsDots.TabIndex = 41;
            this.checkPlotLine2AsDots.Text = "Plot track to follow as dots";
            // 
            // timerGps
            // 
            this.timerGps.Enabled = true;
            this.timerGps.Interval = 1000;
            this.timerGps.Tick += new System.EventHandler(this.timerGps_Tick);
            // 
            // timerIdleReset
            // 
            this.timerIdleReset.Interval = 9000;
            this.timerIdleReset.Tick += new System.EventHandler(this.timerIdleReset_Tick);
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
            this.tabControl.Size = new System.Drawing.Size(480, 508);
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
            this.tabPageOptions.Size = new System.Drawing.Size(480, 464);
            this.tabPageOptions.Text = "Options:";
            // 
            // tabPageGps
            // 
            this.tabPageGps.Controls.Add(this.checkKeepBackLightOn);
            this.tabPageGps.Controls.Add(this.checkGPSOffOnPowerOff);
            this.tabPageGps.Controls.Add(this.checkWgs84Alt);
            this.tabPageGps.Controls.Add(this.checkkeepAliveReg);
            this.tabPageGps.Controls.Add(this.numericAvg);
            this.tabPageGps.Controls.Add(this.checkBeepOnFix);
            this.tabPageGps.Controls.Add(this.comboDropFirst);
            this.tabPageGps.Controls.Add(this.labelDropFirst);
            this.tabPageGps.Controls.Add(this.checkStopOnLow);
            this.tabPageGps.Controls.Add(this.comboGpsPoll);
            this.tabPageGps.Controls.Add(this.labelGpsActivity);
            this.tabPageGps.Controls.Add(this.checkBoxUseGccDll);
            this.tabPageGps.Controls.Add(this.comboBoxUseGccDllRate);
            this.tabPageGps.Controls.Add(this.comboBoxUseGccDllCom);
            this.tabPageGps.Controls.Add(this.labelGpsBaudRate);
            this.tabPageGps.Controls.Add(this.numericGeoID);
            this.tabPageGps.Location = new System.Drawing.Point(0, 0);
            this.tabPageGps.Name = "tabPageGps";
            this.tabPageGps.Size = new System.Drawing.Size(472, 470);
            this.tabPageGps.Text = "GPS";
            // 
            // checkKeepBackLightOn
            // 
            this.checkKeepBackLightOn.Location = new System.Drawing.Point(5, 407);
            this.checkKeepBackLightOn.Name = "checkKeepBackLightOn";
            this.checkKeepBackLightOn.Size = new System.Drawing.Size(475, 40);
            this.checkKeepBackLightOn.TabIndex = 60;
            this.checkKeepBackLightOn.Text = "Safe Energy: do not keep Backlight on";
            this.checkKeepBackLightOn.CheckStateChanged += new System.EventHandler(this.checkKeepBackLightOn_CheckStateChanged);
            // 
            // checkGPSOffOnPowerOff
            // 
            this.checkGPSOffOnPowerOff.Location = new System.Drawing.Point(5, 361);
            this.checkGPSOffOnPowerOff.Name = "checkGPSOffOnPowerOff";
            this.checkGPSOffOnPowerOff.Size = new System.Drawing.Size(472, 40);
            this.checkGPSOffOnPowerOff.TabIndex = 59;
            this.checkGPSOffOnPowerOff.Text = "Safe Energy: GPS off on power off";
            // 
            // checkWgs84Alt
            // 
            this.checkWgs84Alt.Location = new System.Drawing.Point(5, 227);
            this.checkWgs84Alt.Name = "checkWgs84Alt";
            this.checkWgs84Alt.Size = new System.Drawing.Size(338, 40);
            this.checkWgs84Alt.TabIndex = 55;
            this.checkWgs84Alt.Text = "WGS84 altitude;  corr, m:";
            // 
            // checkkeepAliveReg
            // 
            this.checkkeepAliveReg.Location = new System.Drawing.Point(5, 315);
            this.checkkeepAliveReg.Name = "checkkeepAliveReg";
            this.checkkeepAliveReg.Size = new System.Drawing.Size(472, 40);
            this.checkkeepAliveReg.TabIndex = 51;
            this.checkkeepAliveReg.Text = "use alternate method to keep GPS on";
            // 
            // numericAvg
            // 
            this.numericAvg.Location = new System.Drawing.Point(376, 273);
            this.numericAvg.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericAvg.Name = "numericAvg";
            this.numericAvg.Size = new System.Drawing.Size(100, 36);
            this.numericAvg.TabIndex = 46;
            this.numericAvg.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // checkBeepOnFix
            // 
            this.checkBeepOnFix.Location = new System.Drawing.Point(5, 273);
            this.checkBeepOnFix.Name = "checkBeepOnFix";
            this.checkBeepOnFix.Size = new System.Drawing.Size(366, 40);
            this.checkBeepOnFix.TabIndex = 41;
            this.checkBeepOnFix.Text = "Beep on GPS fix         AVG:";
            // 
            // comboDropFirst
            // 
            this.comboDropFirst.Items.Add("none");
            this.comboDropFirst.Items.Add("1  point");
            this.comboDropFirst.Items.Add("2  points");
            this.comboDropFirst.Items.Add("4  points");
            this.comboDropFirst.Items.Add("8  points");
            this.comboDropFirst.Items.Add("16 points");
            this.comboDropFirst.Items.Add("32 points");
            this.comboDropFirst.Location = new System.Drawing.Point(153, 52);
            this.comboDropFirst.Name = "comboDropFirst";
            this.comboDropFirst.Size = new System.Drawing.Size(323, 41);
            this.comboDropFirst.TabIndex = 36;
            this.comboDropFirst.SelectedIndexChanged += new System.EventHandler(this.comboGpsPoll_SelectedIndexChanged);
            // 
            // labelDropFirst
            // 
            this.labelDropFirst.Location = new System.Drawing.Point(1, 54);
            this.labelDropFirst.Name = "labelDropFirst";
            this.labelDropFirst.Size = new System.Drawing.Size(219, 40);
            this.labelDropFirst.Text = "Drop first:";
            // 
            // tabPageMainScr
            // 
            this.tabPageMainScr.Controls.Add(this.checkChangeMenuOptBKL);
            this.tabPageMainScr.Controls.Add(this.checkShowExit);
            this.tabPageMainScr.Controls.Add(this.comboArraySize);
            this.tabPageMainScr.Controls.Add(this.labelArraySize);
            this.tabPageMainScr.Controls.Add(this.checkTouchOn);
            this.tabPageMainScr.Controls.Add(this.checkConfirmStop);
            this.tabPageMainScr.Controls.Add(this.comboUnits);
            this.tabPageMainScr.Controls.Add(this.labelUnits);
            this.tabPageMainScr.Controls.Add(this.checkExStopTime);
            this.tabPageMainScr.Controls.Add(this.checkEditFileName);
            this.tabPageMainScr.Controls.Add(this.checkMainConfigSingleTap);
            this.tabPageMainScr.Location = new System.Drawing.Point(0, 0);
            this.tabPageMainScr.Name = "tabPageMainScr";
            this.tabPageMainScr.Size = new System.Drawing.Size(472, 470);
            this.tabPageMainScr.Text = "Main screen";
            // 
            // checkChangeMenuOptBKL
            // 
            this.checkChangeMenuOptBKL.Location = new System.Drawing.Point(4, 327);
            this.checkChangeMenuOptBKL.Name = "checkChangeMenuOptBKL";
            this.checkChangeMenuOptBKL.Size = new System.Drawing.Size(476, 40);
            this.checkChangeMenuOptBKL.TabIndex = 42;
            this.checkChangeMenuOptBKL.Text = "Change menu \'Options\' <-> \'BKLight\'";
            // 
            // checkShowExit
            // 
            this.checkShowExit.Location = new System.Drawing.Point(4, 281);
            this.checkShowExit.Name = "checkShowExit";
            this.checkShowExit.Size = new System.Drawing.Size(476, 40);
            this.checkShowExit.TabIndex = 39;
            this.checkShowExit.Text = "Show \'Exit\' after Stop";
            // 
            // comboArraySize
            // 
            this.comboArraySize.Items.Add("4k");
            this.comboArraySize.Items.Add("8k");
            this.comboArraySize.Items.Add("16k");
            this.comboArraySize.Items.Add("32k");
            this.comboArraySize.Items.Add("64k");
            this.comboArraySize.Items.Add("128k");
            this.comboArraySize.Items.Add("256k");
            this.comboArraySize.Location = new System.Drawing.Point(345, 415);
            this.comboArraySize.Name = "comboArraySize";
            this.comboArraySize.Size = new System.Drawing.Size(128, 41);
            this.comboArraySize.TabIndex = 36;
            this.comboArraySize.SelectedIndexChanged += new System.EventHandler(this.comboArraySize_SelectedIndexChanged);
            // 
            // labelArraySize
            // 
            this.labelArraySize.Location = new System.Drawing.Point(7, 415);
            this.labelArraySize.Name = "labelArraySize";
            this.labelArraySize.Size = new System.Drawing.Size(332, 40);
            this.labelArraySize.Text = "Data array size (clears data):";
            // 
            // checkTouchOn
            // 
            this.checkTouchOn.Location = new System.Drawing.Point(4, 235);
            this.checkTouchOn.Name = "checkTouchOn";
            this.checkTouchOn.Size = new System.Drawing.Size(476, 40);
            this.checkTouchOn.TabIndex = 32;
            this.checkTouchOn.Text = "Keep touch on while backlight off";
            // 
            // checkConfirmStop
            // 
            this.checkConfirmStop.Location = new System.Drawing.Point(4, 189);
            this.checkConfirmStop.Name = "checkConfirmStop";
            this.checkConfirmStop.Size = new System.Drawing.Size(476, 40);
            this.checkConfirmStop.TabIndex = 30;
            this.checkConfirmStop.Text = "Confirm \'Pause\' and \'Stop\'";
            // 
            // tabPageMapScr
            // 
            this.tabPageMapScr.Controls.Add(this.comboNavCmd);
            this.tabPageMapScr.Controls.Add(this.labelNavCmd);
            this.tabPageMapScr.Controls.Add(this.numericTrackTolerance);
            this.tabPageMapScr.Controls.Add(this.labelTrackTolerance);
            this.tabPageMapScr.Controls.Add(this.checkDownloadOsm);
            this.tabPageMapScr.Controls.Add(this.labelDefaultZoom);
            this.tabPageMapScr.Controls.Add(this.numericZoomRadius);
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
            this.tabPageMapScr.Controls.Add(this.comboMultiMaps);
            this.tabPageMapScr.Controls.Add(this.comboMapDownload);
            this.tabPageMapScr.Location = new System.Drawing.Point(0, 0);
            this.tabPageMapScr.Name = "tabPageMapScr";
            this.tabPageMapScr.Size = new System.Drawing.Size(480, 464);
            this.tabPageMapScr.Text = "Map screen";
            // 
            // comboNavCmd
            // 
            this.comboNavCmd.Items.Add("off");
            this.comboNavCmd.Items.Add("voice on");
            this.comboNavCmd.Items.Add("voice (only important)");
            this.comboNavCmd.Items.Add("beep on");
            this.comboNavCmd.Items.Add("beep (only important)");
            this.comboNavCmd.Location = new System.Drawing.Point(178, 383);
            this.comboNavCmd.Name = "comboNavCmd";
            this.comboNavCmd.Size = new System.Drawing.Size(297, 41);
            this.comboNavCmd.TabIndex = 85;
            // 
            // labelNavCmd
            // 
            this.labelNavCmd.Location = new System.Drawing.Point(3, 388);
            this.labelNavCmd.Name = "labelNavCmd";
            this.labelNavCmd.Size = new System.Drawing.Size(169, 36);
            this.labelNavCmd.Text = "Nav command";
            // 
            // numericTrackTolerance
            // 
            this.numericTrackTolerance.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericTrackTolerance.Location = new System.Drawing.Point(320, 347);
            this.numericTrackTolerance.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numericTrackTolerance.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericTrackTolerance.Name = "numericTrackTolerance";
            this.numericTrackTolerance.Size = new System.Drawing.Size(155, 36);
            this.numericTrackTolerance.TabIndex = 75;
            this.numericTrackTolerance.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // labelTrackTolerance
            // 
            this.labelTrackTolerance.Location = new System.Drawing.Point(3, 347);
            this.labelTrackTolerance.Name = "labelTrackTolerance";
            this.labelTrackTolerance.Size = new System.Drawing.Size(314, 36);
            this.labelTrackTolerance.Text = "Nav track tolerance [m]";
            // 
            // checkDownloadOsm
            // 
            this.checkDownloadOsm.Location = new System.Drawing.Point(0, 268);
            this.checkDownloadOsm.Name = "checkDownloadOsm";
            this.checkDownloadOsm.Size = new System.Drawing.Size(164, 40);
            this.checkDownloadOsm.TabIndex = 66;
            this.checkDownloadOsm.Text = "Download";
            this.checkDownloadOsm.LostFocus += new System.EventHandler(this.comboMapDownload_SelectedIndexChanged);
            // 
            // labelDefaultZoom
            // 
            this.labelDefaultZoom.Location = new System.Drawing.Point(2, 311);
            this.labelDefaultZoom.Name = "labelDefaultZoom";
            this.labelDefaultZoom.Size = new System.Drawing.Size(314, 36);
            this.labelDefaultZoom.Text = "Default zoom [radius in m]";
            // 
            // tabPageKmlGpx
            // 
            this.tabPageKmlGpx.Controls.Add(this.checkGPXtrkseg);
            this.tabPageKmlGpx.Controls.Add(this.checkGpxSpeedMs);
            this.tabPageKmlGpx.Controls.Add(this.numericGpxTimeShift);
            this.tabPageKmlGpx.Controls.Add(this.labelGpxTimeShift);
            this.tabPageKmlGpx.Controls.Add(this.checkKmlAlt);
            this.tabPageKmlGpx.Controls.Add(this.checkGpxRte);
            this.tabPageKmlGpx.Location = new System.Drawing.Point(0, 0);
            this.tabPageKmlGpx.Name = "tabPageKmlGpx";
            this.tabPageKmlGpx.Size = new System.Drawing.Size(472, 470);
            this.tabPageKmlGpx.Text = "Kml/Gpx";
            // 
            // checkGPXtrkseg
            // 
            this.checkGPXtrkseg.Location = new System.Drawing.Point(2, 194);
            this.checkGPXtrkseg.Name = "checkGPXtrkseg";
            this.checkGPXtrkseg.Size = new System.Drawing.Size(469, 40);
            this.checkGPXtrkseg.TabIndex = 23;
            this.checkGPXtrkseg.Text = "Separate GPX trkseg if gap >10s";
            // 
            // checkGpxSpeedMs
            // 
            this.checkGpxSpeedMs.Location = new System.Drawing.Point(2, 105);
            this.checkGpxSpeedMs.Name = "checkGpxSpeedMs";
            this.checkGpxSpeedMs.Size = new System.Drawing.Size(469, 40);
            this.checkGpxSpeedMs.TabIndex = 21;
            this.checkGpxSpeedMs.Text = "Save GPX speed in m/s";
            // 
            // tabPageLiveLog
            // 
            this.tabPageLiveLog.Controls.Add(this.textBoxCwUrl);
            this.tabPageLiveLog.Controls.Add(this.labelCwUrl);
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
            this.tabPageLiveLog.Size = new System.Drawing.Size(472, 470);
            this.tabPageLiveLog.Text = "Live log";
            // 
            // textBoxCwUrl
            // 
            this.textBoxCwUrl.Location = new System.Drawing.Point(169, 46);
            this.textBoxCwUrl.Name = "textBoxCwUrl";
            this.textBoxCwUrl.Size = new System.Drawing.Size(308, 41);
            this.textBoxCwUrl.TabIndex = 26;
            this.textBoxCwUrl.Text = "http://www.crossingways.com";
            this.textBoxCwUrl.GotFocus += new System.EventHandler(this.CWShowKeyboard);
            this.textBoxCwUrl.LostFocus += new System.EventHandler(this.CWHideKeyboard);
            // 
            // labelCwUrl
            // 
            this.labelCwUrl.Location = new System.Drawing.Point(6, 47);
            this.labelCwUrl.Name = "labelCwUrl";
            this.labelCwUrl.Size = new System.Drawing.Size(156, 40);
            this.labelCwUrl.Text = "Server URL:";
            // 
            // checkUploadGpx
            // 
            this.checkUploadGpx.Location = new System.Drawing.Point(6, 274);
            this.checkUploadGpx.Name = "checkUploadGpx";
            this.checkUploadGpx.Size = new System.Drawing.Size(469, 40);
            this.checkUploadGpx.TabIndex = 18;
            this.checkUploadGpx.Text = "Upload GPX file (after saving)";
            // 
            // tabPageLaps
            // 
            this.tabPageLaps.Controls.Add(this.checkLapBeep);
            this.tabPageLaps.Controls.Add(this.buttonLapExport);
            this.tabPageLaps.Controls.Add(this.textLapOptions);
            this.tabPageLaps.Controls.Add(this.comboLapOptions);
            this.tabPageLaps.Location = new System.Drawing.Point(0, 0);
            this.tabPageLaps.Name = "tabPageLaps";
            this.tabPageLaps.Size = new System.Drawing.Size(472, 470);
            this.tabPageLaps.Text = "Lap stats";
            // 
            // checkLapBeep
            // 
            this.checkLapBeep.Location = new System.Drawing.Point(3, 51);
            this.checkLapBeep.Name = "checkLapBeep";
            this.checkLapBeep.Size = new System.Drawing.Size(246, 40);
            this.checkLapBeep.TabIndex = 11;
            this.checkLapBeep.Text = "beep every lap";
            // 
            // buttonLapExport
            // 
            this.buttonLapExport.Location = new System.Drawing.Point(306, 51);
            this.buttonLapExport.Name = "buttonLapExport";
            this.buttonLapExport.Size = new System.Drawing.Size(167, 40);
            this.buttonLapExport.TabIndex = 10;
            this.buttonLapExport.Text = ".csv export";
            this.buttonLapExport.Click += new System.EventHandler(this.buttonLapExport_Click);
            // 
            // tabPageAbout
            // 
            this.tabPageAbout.Controls.Add(this.labelRevision);
            this.tabPageAbout.Location = new System.Drawing.Point(0, 0);
            this.tabPageAbout.Name = "tabPageAbout";
            this.tabPageAbout.Size = new System.Drawing.Size(472, 470);
            this.tabPageAbout.Text = "About";
            this.tabPageAbout.Paint += new System.Windows.Forms.PaintEventHandler(this.tabAbout_Paint);
            // 
            // timerButton
            // 
            this.timerButton.Interval = 1000;
            this.timerButton.Tick += new System.EventHandler(this.timerButton_Tick);
            // 
            // Form1
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(480, 588);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.tabBlank);
            this.Controls.Add(this.tabOpenFile);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Location = new System.Drawing.Point(0, 52);
            this.Name = "Form1";
            this.Text = "GPS Cycle Computer";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Closed += new System.EventHandler(this.Form1_Closed);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.Form1_Closing);
            this.Resize += new System.EventHandler(this.Form1_Resize);
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
            IoFilesDirectory = CurrentDirectory + "tracks";
            MapsFilesDirectory = CurrentDirectory + "maps";

            // check if there are any args - then load the file
            string parameterExt = Path.GetExtension(FirstArgument);
            if (parameterExt == ".dat")
                LoadSettings(FirstArgument);
            else
                LoadSettings(CurrentDirectory + "GpsCycleComputer.dat");
            
            CreateArrays();
            ApplyCustomBackground();

            if (parameterExt == ".gcc")
                LoadGcc(FirstArgument);

            if (parameterExt != null)
            {
                IFileSupport fs = null;
                if (parameterExt == ".gpx")
                    fs = new GpxSupport();
                else if (parameterExt == ".kml")
                    fs = new KmlSupport();

                if (fs != null)
                    if (fs.Load(FirstArgument, ref WayPointsT2F, PlotDataSize, ref Plot2ndLat, ref Plot2ndLong, ref Plot2ndZ, ref Plot2ndT, ref Plot2ndD, ref T2fSummary, ref Plot2ndCount, false))
                    {
                        labelFileNameT2F.SetText("Track to Follow: " + Path.GetFileName(FirstArgument));   //loaded ok
                        T2fSummary.filename = FirstArgument;        //with path!  todo
                    }
                    else
                    {
                        labelFileNameT2F.SetText("Track to Follow: " + Path.GetFileName(FirstArgument) + " load ERROR");
                        T2fSummary.filename = "";
                    }
            }

            // send indication to GPS driver to wake-up (if it is OFF)
            gps.startGpsService();

            // select option pages to show and apply map bkground option
            FillPagesToShow();
            checkMapsWhiteBk_Click(checkMapsWhiteBk, EventArgs.Empty);

            MenuExec(MenuPage.BFkt.main);

            listBoxFiles.Items.Clear();
            //listBoxFiles.Focus();           //remark: this command disturbs FormWindowState.Maximized, therefore after FormWindowState.Maximized

            if (importantNewsId != 404)
            {
                MessageBox.Show("You can use context menu to configure some fields in main page or modify some commands in menu page", "GCC - Important News");
                importantNewsId = 404;
            }
            LoadState(false);
            /*    tests to prevent windows title bar from overlaying gcc in full screen - all not working
            this.BringToFront();
            button1.Focus();
            this.Focus();
            NoBkPanel.BringToFront();
            NoBkPanel.Show();
            NoBkPanel.Focus();*/
        }

        // close GPS and files on form close
        private void Form1_Closed(object sender, System.EventArgs e)
        {
            LockGpsTick = true;
            timerGps.Enabled = false;

            CloseGps();

            // Stop button enabled - indicate that we need to close streams
            if (state == State.logging || state == State.paused || state == State.logHrOnly || state == State.pauseHrOnly)
            {
                try
                {
                    writer.Close();
                    writer = null;
                    fstream.Close();

                    saveCsvLog();
                }
                catch (Exception ee)
                {
                    Utils.log.Error(" Form1_Closed ", ee);
                }
            }
            if(Utils.log.closingMessage != null)
                System.Windows.Forms.MessageBox.Show(Utils.log.closingMessage, "Attention");
            state = State.normalExit;
            SaveState();
            Utils.log = null;
        }

        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings(CurrentDirectory + "GpsCycleComputer.dat");

            if (state == State.logging || state == State.paused || state == State.logHrOnly || state == State.pauseHrOnly) // means applicaion is still logging
            {
                if (MessageBox.Show("Do you want to exit and stop logging?", "GPS is logging!",
                   MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.No)
                {
                    // Cancel the Closing event from closing the form.
                    e.Cancel = true;
                    MenuExec(MenuPage.BFkt.main);
                }
            }
        }

        void FormFullScreen(bool fullScreen)
        {
            if (fullScreen)
            {
                if (Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor == 0     //PPC2003=4.21?  CE5=5.0   WM5=5.1  WM6.x=5.2
                    || Environment.OSVersion.Version.Major == 6)                                            //CE6=6.0
                    this.FormBorderStyle = FormBorderStyle.None;        //WinCE   - in WinMobile this prevents the hardware Win button from showing the home screen
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.WindowState = FormWindowState.Normal;
            }
        }

        private void LoadSettings(string file_name)
        {
            FileStream fs = null;
            BinaryReader wr = null;
            try
            {
                fs = new FileStream(file_name, FileMode.Open, FileAccess.Read);
                wr = new BinaryReader(fs, Encoding.UTF8);

                comboGpsPoll.SelectedIndex = wr.ReadInt32();
                comboUnits.SelectedIndex = wr.ReadInt32();
                beepOnStartStop = 1 == wr.ReadInt32();  // reused
                checkExStopTime.Checked = 1 == wr.ReadInt32();
                checkStopOnLow.Checked = 1 == wr.ReadInt32();
                checkGpxRte.Checked = 1 == wr.ReadInt32();
                numericGeoID.Value = wr.ReadInt32();

                // load IoFilesDirectory
                int str_len = 0;
                string saved_name = "";
                str_len = wr.ReadInt32();
                for (int i = 0; i < str_len; i++)
                {
                    saved_name += (char)(wr.ReadInt32());
                }
                if (saved_name != "")
                {
                    IoFilesDirectory = saved_name;
                }
                // more options
                checkKmlAlt.Checked = 1 == wr.ReadInt32();
                checkEditFileName.Checked = 1 == wr.ReadInt32();

                // kml line
                comboBoxKmlOptColor.SelectedIndex = wr.ReadInt32();
                comboBoxKmlOptWidth.SelectedIndex = wr.ReadInt32();

                // GCC DLL
                checkBoxUseGccDll.Checked = 1 == wr.ReadInt32();
                comboBoxUseGccDllRate.SelectedIndex = wr.ReadInt32();
                comboBoxUseGccDllCom.SelectedIndex = wr.ReadInt32();

                // and more ...
                checkConfirmStop.Checked = 1 == wr.ReadInt32();     //was previous checkShowBkOff.Checked  - reused
                MainConfigRelativeAlt = 1 == wr.ReadInt32();
                int tmp = wr.ReadInt32();
                if (tmp > 3) tmp = 1;               //to avoid Exception, because index shortened - remove in future releases?
                comboMultiMaps.SelectedIndex = tmp;

                // load MapsFilesDirectory
                str_len = 0;
                saved_name = "";
                str_len = wr.ReadInt32();
                for (int i = 0; i < str_len; i++)
                {
                    saved_name += (char)(wr.ReadInt32());
                }
                if (saved_name != "")
                    MapsFilesDirectory = saved_name;

                // ---------- Crossingways option ----------------
                // load CW username
                str_len = 0;
                saved_name = "";
                str_len = wr.ReadInt32();
                for (int i = 0; i < str_len; i++)
                {
                    saved_name += (char)(wr.ReadInt32());
                }
                if (saved_name != "")
                { textBoxCw1.Text = saved_name; }

                // load CW hashed password
                str_len = 0;
                saved_name = "";
                str_len = wr.ReadInt32();
                for (int i = 0; i < str_len; i++)
                {
                    saved_name += (char)(wr.ReadInt32());
                }
                if (saved_name != "")
                { CwHashPassword = saved_name; textBoxCw2.Text = "******"; }
                else { CwHashPassword = ""; textBoxCw2.Text = ""; }

                // Live logging option
                comboBoxCwLogMode.SelectedIndex = wr.ReadInt32();

                // 2nd line and "dots" options
                comboBoxLine2OptWidth.SelectedIndex = wr.ReadInt32();
                comboBoxLine2OptColor.SelectedIndex = wr.ReadInt32();
                checkPlotTrackAsDots.Checked = 1 == wr.ReadInt32();
                checkPlotLine2AsDots.Checked = 1 == wr.ReadInt32();

                // pages to show
                checkOptAbout.Checked = 1 == wr.ReadInt32();
                checkOptLiveLog.Checked = 1 == wr.ReadInt32();
                checkOptMaps.Checked = 1 == wr.ReadInt32();
                checkOptGps.Checked = 1 == wr.ReadInt32();
                checkOptKmlGpx.Checked = 1 == wr.ReadInt32();

                // GPX, Map Bkgrd and Last ext to open, etc
                numericGpxTimeShift.Value = wr.ReadInt32();
                checkMapsWhiteBk.Checked = 1 == wr.ReadInt32();
                FileExtentionToOpen = (byte)wr.ReadInt32();
                checkOptLaps.Checked = 1 == wr.ReadInt32();
                wr.ReadInt32();         //no more used:   comboMapDownload.SelectedIndex = wr.ReadInt32(); mapUtil.OsmTilesWebDownload = comboMapDownload.SelectedIndex;
                checkOptMain.Checked = 1 == wr.ReadInt32();
                checkUploadGpx.Checked = 1 == wr.ReadInt32();

                comboDropFirst.SelectedIndex = wr.ReadInt32();
                checkGpxSpeedMs.Checked = 1 == wr.ReadInt32();

                CurrentLat = wr.ReadDouble();
                CurrentLong = wr.ReadDouble();

                checkBeepOnFix.Checked = 1 == wr.ReadInt32();
                numericAvg.Value = wr.ReadInt32();
                checkkeepAliveReg.Checked = 1 == wr.ReadInt32();
                textBoxCwUrl.Text = wr.ReadString();
                checkWgs84Alt.Checked = 1 == wr.ReadInt32();

                comboLapOptions.SelectedIndex = wr.ReadInt32();
                checkLapBeep.Checked = 1 == wr.ReadInt32();
                graph.sourceX = (Graph.SourceX)wr.ReadInt32(); //graph.GraphOverDistance = 1 == wr.ReadInt32();
                importantNewsId = wr.ReadInt32();

                MainConfigSpeedSource = wr.ReadInt32();
                MainConfigAlt2display = wr.ReadInt32();
                dayScheme = 1 == wr.ReadInt32();
                mapUtil.DefaultZoomRadius = wr.ReadInt32();         numericZoomRadius.Value = mapUtil.DefaultZoomRadius;
                mPage.mBAr[(int)MenuPage.BFkt.recall1].text = wr.ReadString();
                mPage.mBAr[(int)MenuPage.BFkt.recall2].text = wr.ReadString();
                mPage.mBAr[(int)MenuPage.BFkt.recall3].text = wr.ReadString();
                // additional Energy Safe options added
                checkGPSOffOnPowerOff.Checked = 1 == wr.ReadInt32();
                checkKeepBackLightOn.Checked = 1 == wr.ReadInt32();
                showWayPoints = wr.ReadInt32();
				MainConfigDistance = (eConfigDistance) wr.ReadInt32();
                MainConfigLatFormat = wr.ReadInt32();
                LoadedSettingsName = wr.ReadString();
                compass_north = 1 == wr.ReadInt32();
                mapUtil.show_nav_button = 1 == wr.ReadInt32();
                comboNavCmd.SelectedIndex = wr.ReadInt32();
                checkGPXtrkseg.Checked = 1 == wr.ReadInt32();
                compass_style = wr.ReadInt32();
                LanguageDirectory = wr.ReadString();
                bkColor_day = Color.FromArgb(wr.ReadInt32());
                foColor_day = Color.FromArgb(wr.ReadInt32());
                bkColor_night = Color.FromArgb(wr.ReadInt32());
                foColor_night = Color.FromArgb(wr.ReadInt32());
                configSyncSystemTime = 1 == wr.ReadInt32();
                configNoLogPassiveTime = 1 == wr.ReadInt32();
                mapLabelColor = Color.FromArgb(wr.ReadInt32());
                checkTouchOn.Checked = 1 == wr.ReadInt32();
                comboArraySize.SelectedIndex = wr.ReadInt32();
                mapUtil.drawWhileShift = 1 == wr.ReadInt32();
                checkShowExit.Checked = 1 == wr.ReadInt32();
                checkChangeMenuOptBKL.Checked = 1 == wr.ReadInt32();
                graph.alignT2f = 1 == wr.ReadInt32();
                AppFullScreen = 1 == wr.ReadInt32(); //FormFullScreen(AppFullScreen); //here it is not executed, if file not exists (or far too short)
                MainConfigNav = 1 == wr.ReadInt32();
                checkMainConfigSingleTap.Checked = 1 == wr.ReadInt32();
                for (int i = 0; i < 3; i++)
                    mapValuesConf[i] = (MapValue)wr.ReadInt32();
                mapValuesShowName = 1 == wr.ReadInt32();
                mapValuesCleanBack = 1 == wr.ReadInt32();
                mapUtil.showMapLabel = 1 == wr.ReadInt32();
                mapUtil.showNav = 1 == wr.ReadInt32();
                mapUtil.autoHideButtons = 1 == wr.ReadInt32();
                graph.autoHideButtons = 1 == wr.ReadInt32();
                numericTrackTolerance.Value = wr.ReadInt32();

            }
            catch (FileNotFoundException)
            {
                FormFullScreen(AppFullScreen);
                MessageBox.Show("Configuration file not found: " + file_name, "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }
            catch (EndOfStreamException)
            {
                FormFullScreen(AppFullScreen);
                MessageBox.Show("Unexpected EOF while reading " + file_name + " (Position " + fs.Position + ").\nUsing current (or default) Options for remainder.\n\nThis is ok if you have just updated to a newer version.", "Warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button1);
            }
            catch (Exception ee)
            {
                FormFullScreen(AppFullScreen);
                Utils.log.Error(" LoadSettings ", ee);
            }
            finally
            {
                if (wr != null) wr.Close();
                if (fs != null) fs.Close();
            }

            //process the new settings
            FormFullScreen(AppFullScreen);  //here it works if file not exists, but it doesn't work on EndOfStream  (open file and MsgBox?) -> also before MsgBox
            CheckIoDirectoryExists();
            CheckMapsDirectoryExists();
            mapUtil.LoadMaps(MapsFilesDirectory);
            CheckLanguageDirectoryExists();
        }

        private void SaveSettings(string file_name)
        {
            // save settings -----------------
            FileStream fs = null;
            BinaryWriter wr = null;
            try
            {
                fs = new FileStream(file_name, FileMode.Create);
                wr = new BinaryWriter(fs, Encoding.UTF8);

                wr.Write(comboGpsPoll.SelectedIndex);
                wr.Write(comboUnits.SelectedIndex);
                wr.Write((beepOnStartStop? 1 : 0)); // reused
                wr.Write((checkExStopTime.Checked ? 1 : 0));
                wr.Write((checkStopOnLow.Checked ? 1 : 0));
                wr.Write((checkGpxRte.Checked ? 1 : 0));
                wr.Write((int)(0.5 + Decimal.ToDouble(numericGeoID.Value)));

                // the best bit: save IoFilesDirectory as length and chars
                wr.Write((int)(IoFilesDirectory.Length));
                for (int i = 0; i < IoFilesDirectory.Length; i++)
                {
                    wr.Write((int)IoFilesDirectory[i]);
                }
                wr.Write((checkKmlAlt.Checked ? 1 : 0));
                wr.Write((checkEditFileName.Checked ? 1 : 0));

                // kml line
                wr.Write(comboBoxKmlOptColor.SelectedIndex);
                wr.Write(comboBoxKmlOptWidth.SelectedIndex);

                // GCC DLL
                wr.Write((checkBoxUseGccDll.Checked ? 1 : 0));
                wr.Write(comboBoxUseGccDllRate.SelectedIndex);
                wr.Write(comboBoxUseGccDllCom.SelectedIndex);

                // and more ...
                wr.Write((checkConfirmStop.Checked ? 1 : 0));       //was previous checkShowBkOff.Checked   - reused
                wr.Write((MainConfigRelativeAlt ? 1 : 0));
                wr.Write(comboMultiMaps.SelectedIndex);

                // save MapsFilesDirectory as length and chars
                wr.Write((MapsFilesDirectory.Length));
                for (int i = 0; i < MapsFilesDirectory.Length; i++)
                {
                    wr.Write((int)MapsFilesDirectory[i]);
                }

                // ---------- Crossingways option ----------------
                // save  textBoxCw1.Text as length and chars
                wr.Write(textBoxCw1.Text.Length);
                for (int i = 0; i < textBoxCw1.Text.Length; i++)
                {
                    wr.Write((int)textBoxCw1.Text[i]);
                }
                // save  CwHashPassword as length and chars
                if (textBoxCw2.Text != "******")
                    CwHashPassword = "";
                wr.Write(CwHashPassword.Length);
                for (int i = 0; i < CwHashPassword.Length; i++)
                {
                    wr.Write((int)CwHashPassword[i]);
                }
                // live logging options
                wr.Write(comboBoxCwLogMode.SelectedIndex);

                // 2nd line and "dots" options
                wr.Write(comboBoxLine2OptWidth.SelectedIndex);
                wr.Write(comboBoxLine2OptColor.SelectedIndex);
                wr.Write(checkPlotTrackAsDots.Checked ? 1 : 0);
                wr.Write(checkPlotLine2AsDots.Checked ? 1 : 0);

                // pages to show
                wr.Write(checkOptAbout.Checked ? 1 : 0);
                wr.Write(checkOptLiveLog.Checked ? 1 : 0);
                wr.Write(checkOptMaps.Checked ? 1 : 0);
                wr.Write(checkOptGps.Checked ? 1 : 0);
                wr.Write(checkOptKmlGpx.Checked ? 1 : 0);

                // GPX, Map Bkgrd and Last ext to open
                wr.Write((int)Decimal.ToDouble(numericGpxTimeShift.Value));
                wr.Write(checkMapsWhiteBk.Checked ? 1 : 0);
                wr.Write((int)FileExtentionToOpen);
                wr.Write(checkOptLaps.Checked ? 1 : 0);
                wr.Write(0);               //dummy          comboMapDownload.SelectedIndex);
                wr.Write(checkOptMain.Checked ? 1 : 0);
                wr.Write(checkUploadGpx.Checked ? 1 : 0);

                wr.Write(comboDropFirst.SelectedIndex);
                wr.Write(checkGpxSpeedMs.Checked ? 1 : 0);

                wr.Write(CurrentLat);
                wr.Write(CurrentLong);

                wr.Write(checkBeepOnFix.Checked ? 1 : 0);
                wr.Write((int)(numericAvg.Value));
                wr.Write(checkkeepAliveReg.Checked ? 1 : 0);
                wr.Write(textBoxCwUrl.Text);
                wr.Write(checkWgs84Alt.Checked ? 1 : 0);

                wr.Write(comboLapOptions.SelectedIndex);
                wr.Write(checkLapBeep.Checked ? 1 : 0);
                wr.Write((int)graph.sourceX);   //GraphOverDistance ? 1 : 0);
                wr.Write(importantNewsId);

                wr.Write(MainConfigSpeedSource);
                wr.Write(MainConfigAlt2display);
                wr.Write(dayScheme ? 1 : 0);
                wr.Write((int)(numericZoomRadius.Value));
                wr.Write(mPage.mBAr[(int)MenuPage.BFkt.recall1].text);
                wr.Write(mPage.mBAr[(int)MenuPage.BFkt.recall2].text);
                wr.Write(mPage.mBAr[(int)MenuPage.BFkt.recall3].text);
                // additional Energy Safe options added
                wr.Write(checkGPSOffOnPowerOff.Checked ? 1 : 0);
                wr.Write(checkKeepBackLightOn.Checked ? 1 : 0);
                wr.Write(showWayPoints);
				wr.Write((int)MainConfigDistance);
                wr.Write(MainConfigLatFormat);
                wr.Write(LoadedSettingsName);
                wr.Write(compass_north ? 1 : 0);
                wr.Write(mapUtil.show_nav_button ? 1 : 0);
                wr.Write(comboNavCmd.SelectedIndex);
                wr.Write(checkGPXtrkseg.Checked ? 1 : 0);
                wr.Write(compass_style);
                wr.Write(LanguageDirectory);
                wr.Write(bkColor_day.ToArgb());
                wr.Write(foColor_day.ToArgb());
                wr.Write(bkColor_night.ToArgb());
                wr.Write(foColor_night.ToArgb());
                wr.Write(configSyncSystemTime ? 1 : 0);
                wr.Write(configNoLogPassiveTime ? 1 : 0);
                wr.Write(mapLabelColor.ToArgb());
                wr.Write(checkTouchOn.Checked ? 1 : 0);
                wr.Write(comboArraySize.SelectedIndex);
                wr.Write(mapUtil.drawWhileShift ? 1 : 0);
                wr.Write(checkShowExit.Checked ? 1 : 0);
                wr.Write(checkChangeMenuOptBKL.Checked ? 1 : 0);
                wr.Write(graph.alignT2f ? 1 : 0);
                wr.Write(AppFullScreen ? 1 : 0);
                wr.Write(MainConfigNav ? 1 : 0);
                wr.Write(checkMainConfigSingleTap.Checked ? 1 : 0);
                for (int i = 0; i < 3; i++)
                    wr.Write((int)mapValuesConf[i]);
                wr.Write(mapValuesShowName ? 1 : 0);
                wr.Write(mapValuesCleanBack ? 1 : 0);
                wr.Write(mapUtil.showMapLabel ? 1 : 0);
                wr.Write(mapUtil.showNav ? 1 : 0);
                wr.Write(mapUtil.autoHideButtons ? 1 : 0);
                wr.Write(graph.autoHideButtons ? 1 : 0);
                wr.Write((int)numericTrackTolerance.Value);


            }
            catch (Exception e)
            {
                Utils.log.Error(" SaveSettings ", e);
            }
            finally
            {
                if( wr != null) wr.Close();
                if (fs != null) fs.Close();
            }
        }

        private void SaveState()
        {
            //debugBegin = Environment.TickCount;

            StreamWriter wr = null;
            try
            {
                wr = new StreamWriter(CurrentDirectory + "gccState.txt");

                wr.WriteLine(Odo.ToString(IC));
                wr.WriteLine(state.ToString());
                wr.WriteLine(TSummary.filename);
                wr.WriteLine(T2fSummary.filename);
            }
            catch (Exception e)
            {
                Utils.log.Error(" SaveState ", e);
            }
            finally
            {
                if (wr != null) wr.Close();
                mPage.ChangeRestore2Clear();        //change button to Clear2F - Restore is no longer possible
            }
            
            //debugStr = (Environment.TickCount - debugBegin).ToString();
            //MessageBox.Show(debugStr);
        }

        private void LoadState(bool forced)     //forced = false: only load if other than "normalExit"
        {
            //debugBegin = Environment.TickCount;

            StreamReader rd = null;
            try
            {
                rd = new StreamReader(CurrentDirectory + "gccState.txt");

                double odo_r = Convert.ToDouble(rd.ReadLine(), IC);
                if (odo_r > Odo) Odo = odo_r;
                string statestr = rd.ReadLine();
                string logfilename = rd.ReadLine();
                string t2ffilename = rd.ReadLine();
                rd.Close();

                if (statestr != State.normalExit.ToString() || forced)
                {                                       //restore last session
                    if (logfilename != "")
                        LoadGcc(logfilename);

                    if (t2ffilename != "")
                        LoadT2f(t2ffilename, false, false);
                    labelInfo.SetText("Info: Session restored");
                }
                if (statestr == State.logging.ToString())
                {
                    //buttonContinue_Click();
                    OpenGps();
                    MenuExec(MenuPage.BFkt.main);       //show the appropriate buttons
                }
                    

            }
            catch (Exception ee)
            {
                Utils.log.Error(" LoadState ", ee);
            }

            //debugStr = (Environment.TickCount - debugBegin).ToString();
            //MessageBox.Show("Load: " + debugStr);
        }

        private struct SYSTEMTIME
        {
            public short Year;
            public short Month;
            public short DayOfWeek;
            public short Day;
            public short Hour;
            public short Minute;
            public short Second;
            public short Milliseconds;
        }

        [DllImport("coredll.dll")]
        private static extern bool SetSystemTime(ref SYSTEMTIME time);

        bool SyncSystemTime()
        {
            if (position != null && position.TimeValid)
            {
                SYSTEMTIME st = new SYSTEMTIME();

                st.Day = (short)position.Time.Day;
                st.DayOfWeek = 0;
                st.Month = (short)position.Time.Month;
                st.Year = (short)position.Time.Year;
                st.Hour = (short)position.Time.Hour;
                st.Minute = (short)position.Time.Minute;
                st.Second = (short)position.Time.Second;
                st.Milliseconds = (short)position.Time.Millisecond;
                return SetSystemTime(ref st);
            }
            else
                return false;
        }

        private void logHeartRateOnly()
        {
            if (TSummary.StartTime == DateTime.MinValue)
            {
                StartLat = CurrentLat;
                StartLong = CurrentLong;
                TSummary.StartTime = DateTime.Now;
                StartTimeUtc = DateTime.UtcNow;
                try
                {
                    WriteStartDateTime();
                    writer.Write((double)StartLat);
                    writer.Write((double)StartLong);
                }
                catch (Exception e)
                {
                    Utils.log.Error(" logHeartRateOnly - save and write starting position", e);
                }
            }
            TimeSpan run_time = DateTime.UtcNow - StartTimeUtc;
            int total_sec = (int)run_time.TotalSeconds;
            if (ContinueAfterPause)
                if (configNoLogPassiveTime)
                    passiveTimeSeconds = total_sec - CurrentTimeSec;
                else
                    passiveTimeSeconds = 0;
            ContinueAfterPause = false;
            CurrentTimeSec = total_sec - passiveTimeSeconds;
            WriteHeartRateRecord();             // write heart rate before normal record because of LoadGcc()
            WriteRecord(0, 0);
            AddPlotData((float)CurrentLat, (float)CurrentLong, (Int16)CurrentAlt, CurrentTimeSec, (Int16)(CurrentSpeed * 10.0), (Int32)TSummary.Distance, (Int16)getHeartRate());
        }



        //int numsamples = 0; //debug
        //int goodsamples = 0;    //debug
        bool moving = false;
        // main logging function to receive date from GPS
        private void GetGpsData()
        {
#if testClickCurrentPos
            return;
#endif
            //numsamples++;   //debug
            CurrentGpsLedColor = Color.Red;

            position = gps.GetPosition();

            if (position != null)
            {
                bool positionAltitudeValid;
                double positionAltitude;
                if (checkWgs84Alt.Checked)      // altitude related to WGS84ellipsoid instead of MeanSeaLevel
                {
                    positionAltitude = position.EllipsoidAltitude;
                    positionAltitudeValid = position.EllipsoidAltitudeValid;
                }
                else
                {
                    positionAltitude = position.SeaLevelAltitude;
                    positionAltitudeValid = position.SeaLevelAltitudeValid;
                }
                Heading = 720;      //invalid, but still head up
                if (position.HeadingValid)
                    Heading = (int)position.Heading;
                if (position.TimeValid && position.LatitudeValid && position.LongitudeValid)
                {
                    TimeSpan deltaT = position.Time - LastPointUtc;
                    double deltaT_s = deltaT.TotalSeconds;
                    if (deltaT_s > 0)
                    {                       // OK, time is increasing -> data is valid
                        //goodsamples++;  //debug
                        
                        int avg = (int)numericAvg.Value;
                        //int avg = 1;    //debug
                        switch (GpsDataState)
                        {
                            case GpsNotOk:         //GpsDataState is last state
                                if (configSyncSystemTime && (state != State.logging || PlotCount == 0))  //don't change system time in the middle of a log
                                    SyncSystemTime();
                                FirstSampleDropCount = dropFirst[comboDropFirst.SelectedIndex];
                                goto case GpsDrop;
                            case GpsDrop:
                            case GpsBecameValid:
                                if (FirstSampleDropCount > 0)   // wait first few samples to get a "better grip" !
                                {
                                    FirstSampleDropCount--;
                                    GpsSearchCount = 0; //Point received (even if it's dropped). Start search to search for the next one
                                    CurrentGpsLedColor = Color.Yellow;
                                    GpsDataState = GpsDrop;
                                }
                                else
                                {
                                    //GpsDataState = GpsBecameValid;
                                    if (checkBeepOnFix.Checked) Utils.MessageBeep(Utils.BeepType.Ok);
                                    AvgCount = avg;
                                    CurrentLat = position.Latitude;         //initialize Old and Current variables
                                    CurrentLong = position.Longitude;
                                    if (!utmUtil.referenceSet)
                                    {                                       //is executed after StartNewTrace()
                                        utmUtil.setReferencePoint(CurrentLat, CurrentLong);
                                        StartLat = CurrentLat;
                                        StartLong = CurrentLong;
                                    }
                                    utmUtil.getXY(position.Latitude, position.Longitude, out CurrentX, out CurrentY);
                                    OldX = CurrentX; OldY = CurrentY;
                                    CurrentAltInvalid = true;
                                    ReferencAltSlope = Int16.MinValue;
                                    if (MainConfigSpeedSource != 1 && position.SpeedValid)
                                    {
                                        CurrentSpeed = position.Speed * 1.852;
                                    }
                                    else
                                    {
                                        CurrentSpeed = Int16.MinValue * 0.1;
                                        CurrentV = Int16.MinValue * 0.1;
                                    }
                                    CurrentVx = 0.0; CurrentVy = 0.0;
                                    CurrentGpsLedColor = Color.LightGreen;
                                    GpsDataState = GpsInitVelo;
                                    //mapUtil.nav.ShortSearch = 0;        //initiate new search of minDistance
                                }
                                break;
                            
                            case GpsInitVelo:
                            case GpsAvg:
                            case GpsOk:
                                GpsSearchCount = 0;
                                CurrentGpsLedColor = Color.LightGreen;
                          
                                double x, y;
                                utmUtil.getXY(position.Latitude, position.Longitude, out x, out y);

                                // Averaging
                                CurrentLat = (CurrentLat * (avg - 1) + position.Latitude) / avg;
                                CurrentLong = (CurrentLong * (avg - 1) + position.Longitude) / avg;
                                //CurrentX = (CurrentX * (avg - 1) + x) / avg;
                                //CurrentY = (CurrentY * (avg - 1) + y) / avg;
                                if (deltaT_s < 43200)   //deltaT_s < 12h - on date change and slight difference on GPS time and system time it can happen (because date comes from system, not GPS)
                                {
                                    CurrentX = ((CurrentX + CurrentVx * deltaT_s) * (avg - 1) + x) / avg;   //CurrentVx from last sample
                                    CurrentY = ((CurrentY + CurrentVy * deltaT_s) * (avg - 1) + y) / avg;
                                }
                                else
                                {   //skip averaging
                                    CurrentX = x;
                                    CurrentY = y;
                                }
                                double deltaX = CurrentX - OldX;
                                double deltaY = CurrentY - OldY;
                                OldX = CurrentX; OldY = CurrentY;

                                if (positionAltitudeValid)
                                {
                                    if (CurrentAltInvalid)
                                    {   // initialize
                                        CurrentAlt = positionAltitude - (double)numericGeoID.Value;
                                        CurrentAltInvalid = false;
                                        ReferencAltSlope = CurrentAlt;
                                        ReferenceXSlope = CurrentX;
                                        ReferenceYSlope = CurrentY;
                                        ElevationSlope = 0.0;
                                    }
                                    else
                                    {   //averaging
                                        CurrentAlt = (CurrentAlt * (avg - 1) + positionAltitude - (double)numericGeoID.Value) / avg;
                                    }
                                }
                                if (deltaT_s < 43200)   //deltaT_s < 12h - on date change and slight difference on GPS time and system time it can happen (because date comes from system, not GPS)
                                {
                                    if (GpsDataState == GpsInitVelo)
                                    {
                                        CurrentVx = deltaX / deltaT_s;      // m/s
                                        CurrentVy = deltaY / deltaT_s;
                                        CurrentV = 3.6 * Math.Sqrt(CurrentVx * CurrentVx + CurrentVy * CurrentVy);  // 3.6* -> km/h
                                        GpsDataState++;     //=GpsAvg
                                    }
                                    else
                                    {
                                        CurrentVx = (CurrentVx * (2 * avg - 1) + (deltaX / deltaT_s)) / (2 * avg);
                                        CurrentVy = (CurrentVy * (2 * avg - 1) + (deltaY / deltaT_s)) / (2 * avg);
                                        CurrentV = 3.6 * Math.Sqrt(CurrentVx * CurrentVx + CurrentVy * CurrentVy);
                                    }
                                }
                                if (ReferencAltSlope != Int16.MinValue)     // slope
                                {
                                    double deltaS = Math.Sqrt((CurrentX - ReferenceXSlope) * (CurrentX - ReferenceXSlope) + (CurrentY - ReferenceYSlope) * (CurrentY - ReferenceYSlope));
                                    if (deltaS > 2)        // not too short distance
                                    {
                                        ElevationSlope = (ElevationSlope * (2*avg - 1) + ((CurrentAlt - ReferencAltSlope) / deltaS)) / (2*avg);
                                        ReferencAltSlope = CurrentAlt;
                                        ReferenceXSlope = CurrentX;
                                        ReferenceYSlope = CurrentY;
                                    }
                                }

                                if (MainConfigSpeedSource == 1)
                                {
                                    CurrentSpeed = CurrentV;    //CurrentVx,y is averaged  -  average CurrentV anyway? better increase avg of CurrentVx,y because of sign-information
                                }
                                else
                                {
                                    // speed in in kmh - converted from knots (see top of this file)
                                    if (position.SpeedValid)      //invalid? leave old value
                                    {
                                        if (CurrentSpeed == Int16.MinValue * 0.1)
                                            CurrentSpeed = position.Speed * 1.852;  //initialize
                                        else
                                            CurrentSpeed = (CurrentSpeed * (avg - 1) + position.Speed * 1.852) / avg;
                                    }
                                }

                                //process the data
                                if (comboGpsPoll.SelectedIndex < IndexSuspendMode || --AvgCount <= 0)       // in suspend mode wait for averaging
                                {                                                                           // AvgCount can run negative - for 68 years
                                    if(GpsDataState == GpsAvg)
                                        GpsDataState = GpsOk;
                                    CurrentGpsLedColor = Color.Green;
                                    double DeltaDistance = Math.Sqrt((CurrentX - ReferenceXDist) * (CurrentX - ReferenceXDist) + (CurrentY - ReferenceYDist) * (CurrentY - ReferenceYDist));

                                    if ((state == State.logging && (GpsLogCounter <= 0 || DeltaDistance >= GpsLogDistance)) || PlotCount == 0)  //if PlotCount==0 calculate vars temporarily
                                    {
                                        if (comboGpsPoll.SelectedIndex < IndexDistanceMode)
                                        {
                                            GpsLogCounter = PollGpsTimeSec[comboGpsPoll.SelectedIndex];
                                            GpsLogDistance = 10000000.0;
                                        }
                                        else if (comboGpsPoll.SelectedIndex < IndexSuspendMode)
                                        {
                                            GpsLogDistance = PollGpsTimeSec[comboGpsPoll.SelectedIndex];
                                            GpsLogCounter = 30;     //after 30s force a log
                                        }

                                        // save and write starting position
                                        if (TSummary.StartTime == DateTime.MinValue)
                                        {
                                            //StartLat = CurrentLat;        is done on GpsBecameValid after setReferencePoint
                                            //StartLong = CurrentLong;
                                            TSummary.StartTime = DateTime.Now;
                                            StartTimeUtc = DateTime.UtcNow;
                                            LastBatterySave = StartTimeUtc;
                                            StartBattery = Utils.GetBatteryStatus();
                                            StartAlt = Int16.MinValue;
                                            ReferenceXDist = CurrentX; ReferenceYDist = CurrentY;
                                            DeltaDistance = 0.0;
                                            ReferenceAlt = Int16.MaxValue;
                                            //TSummary.AltitudeMax = Int16.MinValue;
                                            //TSummary.AltitudeMin = Int16.MaxValue;
                                            LapStartD = 0; LapStartT = 0; LapNumber = 0;
                                            lapManualDistance = 0; lapManualClick = false;
                                            currentLap = ""; lastLap = "";
                                            textLapOptions.Text = "";
                                            moving = false;
                                            //utmUtil.setReferencePoint(StartLat, StartLong);
                                            //OldX = 0.0; OldY = 0.0;
                                            //VeloAvgState = 2;       //start velocity calculation new because of setReferencePoint
                                            if (state == State.logging)
                                            {
                                                try
                                                {
                                                    WriteStartDateTime();
                                                    writer.Write((double)StartLat);
                                                    writer.Write((double)StartLong);
                                                }
                                                catch (Exception e)
                                                {
                                                    Utils.log.Error(" GetGpsData - save and write starting position", e);
                                                }
                                                finally
                                                {
                                                    //writer.Flush();
                                                }
                                                WriteOptionsInfo();
                                            }
                                            // for maps, fill x/y values realtive to the starting point
                                            ResetMapPosition();
                                        }

                                        TimeSpan run_time = DateTime.UtcNow - StartTimeUtc;
                                        int total_sec = (int)run_time.TotalSeconds;
                                        if (ContinueAfterPause)
                                            if (configNoLogPassiveTime)
                                                passiveTimeSeconds = total_sec - CurrentTimeSec;
                                            else
                                                passiveTimeSeconds = 0;
                                        total_sec -= passiveTimeSeconds;
                                        // Safety check 1: make sure elapsed time is not negative
                                        if (total_sec < 0) total_sec = 0;

                                        // Safety check 2: make sure new time is increasing
                                        if (total_sec < OldT)
                                        {
                                            OldT = total_sec;
                                        }

                                        if (ContinueAfterPause)
                                        {                       //force speed 0, so that Pause time calculates to Stoppage time (log v=0 into gcc for reload!)
                                            CurrentSpeed = 0.0;
                                            ContinueAfterPause = false;
                                            if (state == State.logging)
                                            {
                                                while (total_sec - CurrentTimeSec > 64800)      //handle 16 bit limit in log file
                                                {
                                                    CurrentTimeSec += 64800;
                                                    WriteRecord(CurrentX, CurrentY);        //write current records with time increments < 16 bit (18 hours)
                                                }
                                            }
                                        }
                                        CurrentTimeSec = total_sec;

                                        // compute Stoppage time
                                        if (CurrentSpeed < 1.0)
                                        {
                                            CurrentStoppageTimeSec += CurrentTimeSec - OldT;
                                            if (moving && beepOnStartStop)
                                                Utils.MessageBeep(Utils.BeepType.IconAsterisk);
                                            moving = false;
                                        }
                                        else
                                        {
                                            if (!moving && beepOnStartStop)
                                                Utils.MessageBeep(Utils.BeepType.Ok);
                                            moving = true;
                                        }
                                        OldT = CurrentTimeSec;

                                        // Update max speed (in kmh)
                                        if (CurrentSpeed > MaxSpeed)
                                        {
                                            MaxSpeed = CurrentSpeed;
                                        }

                                        // compute distance
                                        TSummary.Distance += DeltaDistance;
                                        Odo += DeltaDistance;
                                        ReferenceXDist = CurrentX; ReferenceYDist = CurrentY;

                                        // compute elevation gain and min max
                                        if (positionAltitudeValid)
                                        {
                                            if (StartAlt == Int16.MinValue) StartAlt = CurrentAlt;

                                            if (CurrentAlt > ReferenceAlt)
                                            {
                                                TSummary.AltitudeGain += CurrentAlt - ReferenceAlt;
                                                ReferenceAlt = CurrentAlt;
                                            }
                                            else if (CurrentAlt < ReferenceAlt - AltThreshold)
                                            {
                                                ReferenceAlt = CurrentAlt;
                                            }
                                            if (CurrentAlt > TSummary.AltitudeMax) TSummary.AltitudeMax = CurrentAlt;
                                            if (CurrentAlt < TSummary.AltitudeMin) TSummary.AltitudeMin = CurrentAlt;
                                        }

                                        // if exclude stop time is activated, do not log stop time in file, and
                                        // do not include stop time in live logging.
                                        // Bugfix: even if not moving, the first call must be logged, otherwise
                                        // the start position is logged more than once (because PlotCount=0), which leads 
                                        // to an corrupt log file.
                                        //if (checkExStopTime.Checked == false || moving == true || PlotCount == 0)     //KB: removed this feature because of incorrect average speed and trip time when track loaded back.
                                        //{                                                                             //Also distance calculation differs slightly and incorrect speed graph.
                                        //to make it work: first and last point with v=0 must be logged (last cached and written right before point with v!=0). No distance accumulation when v=0 (slight change in behaviour).

                                        if (state == State.logging)
                                        {
                                            if (oHeartBeat != null)
                                                WriteHeartRateRecord();   // write heart rate before normal record because of LoadGcc()
                                            WriteRecord(CurrentX, CurrentY);
                                            // write battery info every 1 min - and flush data
                                            WriteBatteryInfo();
                                            AddPlotData((float)CurrentLat, (float)CurrentLong, (Int16)CurrentAlt, CurrentTimeSec, (Int16)(CurrentSpeed * 10.0), (Int32)TSummary.Distance, (Int16)getHeartRate());
                                            CurrentPlotIndex = PlotCount - 1;
                                            DoLiveLogging();
                                        }
                                        DoLapStats();
                                        //}
                                    }// Logging || tmp vars
                                    else
                                        if (PlotCount < PlotDataSize)     //for graph current point
                                        {
                                            if (PlotCount > 0)
                                            {
                                                PlotD[PlotCount] = PlotD[PlotCount - 1];
                                                PlotT[PlotCount] = PlotT[PlotCount - 1];
                                            }
                                            else
                                            {
                                                PlotD[PlotCount] = (Int32)TSummary.Distance;    //never executed
                                                PlotT[PlotCount] = CurrentTimeSec;
                                            }
                                            PlotZ[PlotCount] = (Int16)CurrentAlt;
                                            PlotS[PlotCount] = (Int16)(CurrentSpeed * 10.0);
                                            PlotH[PlotCount] = (Int16)getHeartRate();
                                            CurrentPlotIndex = PlotCount;
                                        }
                                        else
                                            CurrentPlotIndex = PlotCount - 1;
                                }
                                break;
                            case GpsInvalidButTrust:
                                GpsDataState = GpsOk;
                                goto case GpsOk;
                        }// switch
                    }
                    LastPointUtc = position.Time;   // save last time
                } //if position.TLL valid
                else
                {
                    GpsSearchCount++;
                    switch (GpsDataState)
                    {
                        case GpsNotOk:
                        case GpsDrop:
                            break;
                        case GpsOk:
                            GpsDataState = GpsInvalidButTrust;
                            break;
                        default:
                            if (checkBeepOnFix.Checked && GpsDataState >= GpsAvg)
                            { Utils.MessageBeep(Utils.BeepType.IconExclamation); }
                            GpsDataState = GpsNotOk;
                            break;
                    }
                }

                CurrentStatusString = "GPS";
                if(comboGpsPoll.SelectedIndex >= IndexSuspendMode)     //always on
                    CurrentStatusString += "(" + PollGpsTimeSec[comboGpsPoll.SelectedIndex] + "s)";    //start/stop (suspend) mode
                CurrentStatusString += ": ";
                if (state == State.logging) CurrentStatusString += "Logging ";
                else if (state == State.paused) CurrentStatusString += "Paused ";
                else CurrentStatusString += "On ";
                CurrentStatusString += (comboDropFirst.SelectedIndex > 0) ? ("d" + FirstSampleDropCount + " ") : "";

                CurrentStatusString += (position.TimeValid ? "T" : "t") +
                                       (position.LatitudeValid ? "L" : "l") +
                                       (position.LongitudeValid ? "L" : "l") +
                                       (positionAltitudeValid ? "A" : "a") +
                                       (position.HeadingValid ? "H" : "h") +
                                       (position.SpeedValid ? "S" : "s") +
                                       " ";
                CurrentStatusString += GpsSearchCount;

            }
            else //position == null
            {
                CurrentStatusString = "no data from GPS";
            }
            //debugStr = "bad samples: " + (numsamples - goodsamples) + "/" + numsamples; //debug
        }

        private void WriteHeartRateRecord()   //heart rate record should be written before a normal record (which includes the corresponding time)
        {
            try
            {
                writer.Write((Int16)getHeartRate());
                writer.Write((Int16)0);
                writer.Write((Int16)4);   //heart rate
                writer.Write((UInt16)0xFFFF);
                writer.Write((UInt16)0xFFFF);
            }
            catch (Exception e)
            {
                Utils.log.Error(" WriteRecord - HeartRate Record ", e);
            }
        }

        private void WriteStringRecord(Int16 code, string str)      //code should be >31
        {
            bool justOpened = false;
            try
            {
                if (writer == null)
                {
                    fstream = new FileStream(TSummary.filename, FileMode.Append);
                    writer = new BinaryWriter(fstream, Encoding.Unicode);
                    justOpened = true;
                }
                writer.Write((Int16)str.Length);    //not used
                writer.Write((Int16)0);
                writer.Write((Int16)code);
                writer.Write((UInt16)0xFFFF);
                writer.Write((UInt16)0xFFFF);
                writer.Write(str);
            }
            catch (Exception e)
            {
                Utils.log.Error(" WriteRecord - String ", e);
            }
            if (justOpened)
            {
                writer.Close();
                writer = null;
                fstream.Close();
            }
        }

        // Write record. Position must be valid
        private void WriteRecord(double x, double y)
        {   
            // shift to origin
            x -= OriginShiftX;
            y -= OriginShiftY;

            // check if an origin update is required
            while ((Math.Abs(x) > 30000.0) || (Math.Abs(y) > 30000.0))
            {
                Int16 deltaX = 0;
                if (x > 30000.0) 
                { 
                    x -= 30000.0; 
                    deltaX = 30000; 
                }
                else if (x < -30000.0) 
                { 
                    x += 30000.0; 
                    deltaX = -30000; 
                }

                Int16 deltaY = 0;
                if (y > 30000.0) 
                { 
                    y -= 30000.0; 
                    deltaY = 30000; 
                }
                else if (y < -30000.0) 
                { 
                    y += 30000.0; 
                    deltaY = -30000; 
                }

                // Yes, need an origin shift record
                if ((deltaX != 0) || (deltaY != 0))
                {
                    OriginShiftX += deltaX;
                    OriginShiftY += deltaY;

                    try
                    {
                        writer.Write((Int16)deltaX);
                        writer.Write((Int16)deltaY);
                        writer.Write((Int16)0);         // this is origin update (0)
                        writer.Write((UInt16)0xFFFF);   // status record (0xFFFF/0xFFFF)
                        writer.Write((UInt16)0xFFFF);
                    }
                    catch (Exception e)
                    {
                        Utils.log.Error (" WriteRecord - Origin Shift ", e);
                    }
                    finally
                    {
                        //writer.Flush ();
                    }
                }
            }

            // proceed with "normal" record
            try
            {
                writer.Write ((Int16) x);
                writer.Write ((Int16) y);
                writer.Write ((Int16) CurrentAlt);   //if Altitude becomes invalid inbetween: use last value
                writer.Write((Int16)(CurrentSpeed*10.0));
                writer.Write((UInt16)CurrentTimeSec);
            }
            catch (Exception e)
            {
                Utils.log.Error (" WriteRecord - Normal Record ", e);
            }
            finally
            {
                //writer.Flush ();  Flush is done in WriteBatteryInfo every 1 min
            }
        }

        private void AddPlotData(float lat, float lng, Int16 z, Int32 t, Int16 s, Int32 d, Int16 hr)
        {
            if (DecimateCount == 0)     //when decimating, add only first sample, ignore rest of decimation
            {
                // check if we need to increase decimation level
                if (PlotCount >= PlotDataSize)
                {
                    for (int i = 0; i < PlotDataSize / 2; i++)
                    {
                        PlotLat[i] = PlotLat[i * 2];
                        PlotLong[i] = PlotLong[i * 2];
                        PlotZ[i] = PlotZ[i * 2];
                        PlotT[i] = PlotT[i * 2];
                        PlotS[i] = PlotS[i * 2];
                        PlotH[i] = PlotH[i * 2];
                        PlotD[i] = PlotD[i * 2];
                    }
                    Decimation *= 2;
                    PlotCount /= 2;
                }

                PlotLat[PlotCount] = lat;
                PlotLong[PlotCount] = lng;
                PlotZ[PlotCount] = z;
                PlotT[PlotCount] = t;
                PlotS[PlotCount] = s;
                PlotH[PlotCount] = hr;
                PlotD[PlotCount] = d;
                PlotCount++;
                if (hr != 0) mPage.mBAr[(int)MenuPage.BFkt.graph_heartRate].enabled = true;
            }
            DecimateCount++;
            if (DecimateCount >= Decimation)
                DecimateCount = 0;
        }

        // Write starting date/time to the new file
        private void WriteStartDateTime()
        {
            Byte x;
            try
            {
                x = (Byte) (TSummary.StartTime.Year - 2000);
                writer.Write ((Byte) x);
                x = (Byte) TSummary.StartTime.Month;
                writer.Write ((Byte) x);
                x = (Byte) TSummary.StartTime.Day;
                writer.Write ((Byte) x);
                x = (Byte) TSummary.StartTime.Hour;
                writer.Write ((Byte) x);
                x = (Byte) TSummary.StartTime.Minute;
                writer.Write ((Byte) x);
                x = (Byte) TSummary.StartTime.Second;
                writer.Write ((Byte) x);
            }
            catch (Exception e)
            {
                Utils.log.Error (" WriteStartDateTime ", e);
            }
        }

        // write battery info
        private void WriteBatteryInfo()
        {
            if (PlotCount > 0)
            {
                TimeSpan maxAge = new TimeSpan(0, 1, 0); // 1 min
                if ((LastBatterySave + maxAge) >= DateTime.UtcNow)
                { 
                    return; 
                }
            }

            LastBatterySave = DateTime.UtcNow;

            //CurrentBattery = Utils.GetBatteryStatus();
            Int16 x = (Int16) CurrentBattery;

            try
            {
                writer.Write ((Int16) x);
                writer.Write ((Int16) 0);
                writer.Write ((Int16) 1);         // this is battery status record (1)
                writer.Write ((UInt16) 0xFFFF);   // status record (0xFFFF/0xFFFF)
                writer.Write ((UInt16) 0xFFFF);
            }
            catch (Exception e)
            {
                Utils.log.Error (" WriteBatteryInfo ", e);
            }
            finally
            {
                writer.Flush ();
            }

            // terminate if low power
            if (x > 0)
            {
                if (checkStopOnLow.Checked && (x < 20))
                {
                    state = State.nothing;
                    timerGps.Enabled = false;
                    timerIdleReset.Enabled = false;
                    CloseGps();
                    StoppedOnLow = true;
                    //CurrentStatusString = "Stopped on low power";
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
            catch (Exception e)
            {
                Utils.log.Error (" WriteOptionsInfo ", e);
            }
            finally
            {
                //writer.Flush ();
            }
        }

        // generate a new file name using StartTime (without path without extension)
        private string GenerateFileName()
        {
            DateTime start_time = DateTime.Now;

            // file name is constructed as year,month,day, hour, min, sec, all as 2-digit values
            string file_name = (start_time.Year - 2000).ToString("00")
                    + start_time.Month.ToString("00")
                    + start_time.Day.ToString("00")
                    + "_"
                    + start_time.Hour.ToString("00")
                    + start_time.Minute.ToString("00");

            return file_name;
        }

        private string GenerateEnumeratedAudioFilename()
        {
            string fn;
            while(true)
            {
                fn = TSummary.filename.Remove(TSummary.filename.Length - 4, 4);       //Path.GetFileNameWithoutExtension(TSummary.filename);
                fn += "_" + AudioEnum.ToString("00") + ".wav";
                if (File.Exists(fn))
                {
                    AudioEnum++;
                    continue;
                }
                else return fn;
            }
        }

        // New Trace: open file, log start time, etc
        private void StartNewTrace()
        {
            // create writer and write header
            try
            {
                fstream = new FileStream(TSummary.filename, FileMode.Create);
                writer = new BinaryWriter(fstream, Encoding.Unicode);
                writer.Write((Byte)'G'); writer.Write((Byte)'C'); writer.Write((Byte)'C'); writer.Write((Byte)1);
            }
            catch (Exception e)
            {
                Utils.log.Error (" StartNewTrace - create writer and write header ", e);
            }
            finally
            {
                //writer.Flush ();
            }

            utmUtil.referenceSet = false;
            if (GpsDataState > GpsBecameValid)         // let set new reference point
                GpsDataState = GpsBecameValid;

            OriginShiftX = 0.0;
            OriginShiftY = 0.0;

            MaxSpeed = 0.0;
            CurrentTimeSec = 0;
            CurrentStoppageTimeSec = 0;
            passiveTimeSeconds = 0;
            //OldX = 0.0;
            //OldY = 0.0;
            OldT = 0;
            TSummary.Clear2();

            PlotCount = 0;
            CurrentPlotIndex = -1;
            clearHR();
            Decimation = 1; DecimateCount = 0;
            WayPointsT.Count = 0;
            AudioEnum = 0;
            GpsLogDistance = 0.0;
            //FirstSampleValidCount = 1;

            LastPointUtc = DateTime.MinValue;
            LastLiveLogging = DateTime.MinValue;
        }


        private void WriteCheckPoint(string name)
        {
            if (WayPointsT.Count < WayPointsT.DataSize)
            {
                // store new waypoint
                WayPointsT.name[WayPointsT.Count] = name;
                WayPointsT.lat[WayPointsT.Count] = (float)CurrentLat;
                WayPointsT.lon[WayPointsT.Count] = (float)CurrentLong;
                WayPointsT.Count++;
            }

            try
            {
                Int16 text_length = (Int16)name.Length;

                writer.Write((Int16)text_length);
                writer.Write((Int16)0);
                writer.Write((Int16)3);         // this is check-point record (3)
                writer.Write((UInt16)0xFFFF);   // status record (0xFFFF/0xFFFF)
                writer.Write((UInt16)0xFFFF);

                if (text_length != 0)
                {
                    for (int i = 0; i < text_length; i++)
                    {
                        writer.Write((UInt16)name[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Utils.log.Error(" WriteCheckPoint ", e);
            }
            finally
            {
                writer.Flush();
            }
        }

        private void buttonGPS_Click(object sender, EventArgs e)
        {
            if (!gps.OpenedOrSuspended)
                OpenGps();
            else
                CloseGps();
            MenuExec(MenuPage.BFkt.main);       //show the appropriate buttons
        }

        private void buttonStart_Click()
        {
            /*if (Logging)
            {
                if (Paused)
                    MessageBox.Show("Logging already active, but paused - use 'Pause' button to continue");
                else
                    MessageBox.Show("GCC is already logging!");
                return;
            }*/

            // warn if track logging is activated, but track log will be interrupted, if
            // GPS is switched off on power off
            if (checkGPSOffOnPowerOff.Checked == true)
            {
                if (MessageBox.Show("Safe energy option activated. GPS is switched off on power off.\nLogging will be interrupted on power off. Do you want to keep GPS always on?", "GPS always on",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    // Keep GPS on.
                    checkGPSOffOnPowerOff.Checked = false;
                    //KeepToolRunning(true);        //is done in OpenGps()
                }
            }

            // warn if live logging is activated, and ask if want to proceed
            if (comboBoxCwLogMode.SelectedIndex != 0)
            {
                if (MessageBox.Show("Live logging is activated, proceed?\n('No' disables live logging)", textBoxCwUrl.Text,
                                   MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation,
                                   MessageBoxDefaultButton.Button1) == DialogResult.No)
                {
                    comboBoxCwLogMode.SelectedIndex = 0;
                }
            }
           
            // If a tracklog already exists, and the distance to the last log point is max. 1km  
            // than ask, if the current track log should be continued.
            if (PlotCount > 0 && TSummary.filename != "" && 
                Math.Abs(PlotLat[PlotCount - 1] - CurrentLat) * utmUtil.lat2meter < 1000 && Math.Abs(PlotLong[PlotCount - 1] - CurrentLong) * utmUtil.longit2meter < 1000 &&
                File.Exists(TSummary.filename))
            {
                DialogResult dr = MessageBox.Show("Do you want to continue the track log into file:\n" + Path.GetFileName(TSummary.filename), "Continue log file",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                if (dr == DialogResult.Yes)
                {
                    buttonContinue_Click();
                    return;
                }
                else if(dr == DialogResult.Cancel)
                    return;
            }

            TSummary.filename = GenerateFileName();     //without path and extension
            
            // check if we need to show the custom file name panel, or can start loging with default name
            if (checkEditFileName.Checked)
            {
                if (Utils.InputBox(null, "Enter file name (without extension):", ref TSummary.filename) == DialogResult.Cancel)
                {
                    TSummary.filename = "";       // No valid file loaded (if continue button will be pressed later...)
                    return;                     // if cancel button is pressed, do not start logging.
                }
            }
            TSummary.filename += ".gcc";
            labelFileName.SetText("Current File Name: " + TSummary.filename);
            //add path
            TSummary.filename = IoFilesDirectory + ((IoFilesDirectory == "\\") ? "" : "\\") + TSummary.filename;

            //Paused = false;         //todo necessary?

            StoppedOnLow = false;

            CurrentLiveLoggingString = "";

            StartNewTrace();
            if (oHeartBeat != null && !gps.OpenedOrSuspended)
            {
                if (MessageBox.Show("Only log heart rate?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    state = State.logHrOnly;
            }
            if (state != State.logHrOnly)
            {
                if (OpenGps() == 1)          //todo error?
                    state = State.logging;
            }
            SaveState();
            MenuExec(MenuPage.BFkt.main);
        }

        private void buttonStop_Click(bool closeGPS)
        {
            if (checkConfirmStop.Checked)
                if (MessageBox.Show("Are you sure?", "Stop?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.No)
                    return;
            state = State.nothing;
            mPage.mBAr[(int)MenuPage.BFkt.pause] = mPage.mBPause;

            try
            {
                writer.Close ();
                writer = null;
                fstream.Close ();
            }
            catch (Exception e)
            {
                Utils.log.Error (" buttonStop_Click - writer close ", e);
            }
            //comboGpsPoll.Enabled = true;
            GpsSearchCount = 0;
            CurrentLiveLoggingString = "";
            ContinueAfterPause = false;

            // reset move/zoom vars (as we switch from fixed zoom into auto zoom mode)
            ResetMapPosition();

            Cursor.Current = Cursors.WaitCursor;
            if(closeGPS) CloseGps();
            if (gps.OpenedOrSuspended)
                mPage.mBAr[(int)MenuPage.BFkt.gps_toggle] = mPage.mBGpsOn;
            else
                mPage.mBAr[(int)MenuPage.BFkt.gps_toggle] = mPage.mBGpsOff;
            showButton(button1, MenuPage.BFkt.mPage);
            showButton(button2, MenuPage.BFkt.start);
            showButton(button3, MenuPage.BFkt.gps_toggle);

            // Delete log files, if no records
            if (PlotCount == 0)
            {
                try
                {
                    File.Delete(TSummary.filename);
                }
                catch (Exception ex)
                {
                    Utils.log.Error(" timerStartDelay_Tick - delete empty log ", ex);
                }
            }
            // Save Csv log
            else  
            {
                saveCsvLog ();
            }
            SaveState();

            Cursor.Current = Cursors.Default;
            NoBkPanel.Invalidate();
        }

        void buttonPause_Click()
        {
            if(state == State.logging || state == State.logHrOnly)
            {
                if (checkConfirmStop.Checked)
                    if (MessageBox.Show("Are you sure?", "Pause?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.No)
                        return;
                if (state == State.logging)
                    state = State.paused;
                else
                    state = State.pauseHrOnly;
            }
            else if (state == State.paused)
            {
                OpenGps();
                ContinueAfterPause = true;
                state = State.logging;
            }
            else if (state == State.pauseHrOnly)
            {
                ContinueAfterPause = true;
                state = State.logHrOnly;
            }
            SaveState();
            MenuExec(MenuPage.BFkt.main);       //show the appropriate buttons
        }

        void buttonContinue_Click()
        {
            if (state == State.logging || state == State.logHrOnly)
            {
                MessageBox.Show("Logging is already active!");
                return;
            }
            if (state == State.paused || state == State.pauseHrOnly)
            {
                MenuExec(MenuPage.BFkt.pause);      //unpause to continue
                return;
            }
            if (PlotCount == 0 || TSummary.filename == "")
            {
                if(MessageBox.Show("No valid gcc-file loaded.\nDo you want to start a new track?", null, MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button1)
                    == DialogResult.Yes)
                { MenuExec(MenuPage.BFkt.start); }
                return;
            }

            StoppedOnLow = false;

            LastBatterySave = StartTimeUtc;
            LastLiveLogging = StartTimeUtc;
            CurrentLiveLoggingString = "";
            utmUtil.setReferencePoint(StartLat, StartLong);

            // create writer
            try
            {
                fstream = new FileStream(TSummary.filename, FileMode.Append);
                writer = new BinaryWriter(fstream, Encoding.Unicode);
            }
            catch (Exception e)
            {
                Utils.log.Error(" ContinueTrace - append writer ", e);
            }
            if (oHeartBeat != null && !gps.OpenedOrSuspended)
            {
                if (MessageBox.Show("Only log heart rate?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    state = State.logHrOnly;
            }
            if (state != State.logHrOnly)
            {
                if (GpsDataState == GpsOk)  //(Plotcount is >0)         display this message at once - or never!
                {
                    double xc, yc, xt, yt, dist;            //test distance to previous track -> start
                    utmUtil.getXY(CurrentLat, CurrentLong, out xc, out yc);
                    utmUtil.getXY(PlotLat[PlotCount - 1], PlotLong[PlotCount - 1], out xt, out yt);
                    dist = Math.Sqrt((xc - xt) * (xc - xt) + (yc - yt) * (yc - yt));
                    if (dist > 1000)
                    {
                        if (MessageBox.Show("Last track point is " + (dist / 1000).ToString("#.#") + "km away.\nContinue log file \"" + Path.GetFileName(TSummary.filename) + "\" anyway?", "really continue?",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
                        {
                            buttonStop_Click(false);
                            return;
                        }
                    }
                }

                OpenGps();          //todo error?
                state = State.logging;
            }
            ContinueAfterPause = true;
            SaveState();
            MenuExec(MenuPage.BFkt.main);
        }


        // utils to fill gcc file names into the "listBoxFiles", indicating if KML/GPX exist
        private void FillFileNames()
        {
            string ext = "*.gcc";
            Cursor.Current = Cursors.WaitCursor;
            listBoxFiles.Items.Clear();
            if (FileOpenMode != FileOpenMode_Gcc)
                switch (FileExtentionToOpen)
                {
                    //case FileOpenMode_Gcc:
                    //case FileOpenMode_2ndGcc: ext = "*.gcc"; break;
                    case FileOpenMode_2ndKml: ext = "*.kml"; break;
                    case FileOpenMode_2ndGpx: ext = "*.gpx"; break;
                }
            string[] files = Directory.GetFiles(IoFilesDirectory, ext);
            Array.Sort(files);

            for (int i = (files.Length - 1); i >= 0; i--)
            {
                string status_string = "";
                if (FileOpenMode == FileOpenMode_Gcc)
                {
                    string kml_file = files[i].Remove(files[i].Length - 3, 3) + "kml";
                    string gpx_file = files[i].Remove(files[i].Length - 3, 3) + "gpx";

                    // add indication if KML or GPX files exists for this gcc file
                    if (File.Exists(kml_file)) { status_string += "*"; }
                    if (File.Exists(gpx_file)) { status_string += "+"; }
                }

                listBoxFiles.Items.Add(status_string + Path.GetFileName(files[i]));
            }
            if (listBoxFiles.Items.Count == 0)
            { listBoxFiles.Items.Add("No " + ext + " files found"); }
            Cursor.Current = Cursors.Default;
        }
        // read file
        private void buttonLoad_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeFiledialog;
            if (state == State.logging || state == State.paused || state == State.logHrOnly || state == State.pauseHrOnly)
            {
                if (MessageBox.Show("Can't open file while logging.\nStop active Logging and proceed?", null, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1)
                    == DialogResult.Yes)
                { buttonStop_Click(false); }
                else
                { return; }
            }

            FolderSetupMode = false;
            FileOpenMode = FileOpenMode_Gcc;

            listBoxFiles.BringToFront();

            CheckIoDirectoryExists();
            FillFileNames();

            tabOpenFile.BringToFront();

            showButton(button1, MenuPage.BFkt.dialog_cancel);
            showButton(button2, MenuPage.BFkt.dialog_open);
            showButton(button3, MenuPage.BFkt.dialog_down);
            showButton(button4, MenuPage.BFkt.dialog_saveKml);
            showButton(button5, MenuPage.BFkt.dialog_saveGpx);
            showButton(button6, MenuPage.BFkt.dialog_up);
            //tabBlank1.SendToBack();

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

        int BFktSave = -1;
        void button1_MouseDown(object sender, MouseEventArgs e)
        {
            timerButton.Interval = 500;
            timerButton.Enabled = true;
        }
        void button1_MouseUp(object sender, MouseEventArgs e)
        {
            button1_disableTimer();
        }
        void button1_disableTimer()
        {
            timerButton.Enabled = false;
            if (BFktSave >= 0)
            {
                button1.Tag = BFktSave;
                BFktSave = -1;
            }
        }

        void timerButton_Tick(object sender, EventArgs e)
        {
            timerButton.Enabled = false;
            MenuExec(MenuPage.BFkt.mPage);
            BFktSave = (int)button1.Tag;
            button1.Tag = MenuPage.BFkt.nothing;        //temporarily disable button function - it would trigger on MouseUp (Click)
        }
        void button1_DoubleClick(object sender, EventArgs e)
        {
            MenuExec(MenuPage.BFkt.mPage);
        }

        private void button_Click(object sender, EventArgs e)
        {
            if (((Control)sender).Tag != null)
                MenuExec((MenuPage.BFkt)((Control)sender).Tag);
            listBoxFiles.Focus();       //loose focus from other controls to fire LostFocus event (in options)
            clickLatLon = null;         //erase LatLon string
            timerButtonHide.Interval = timerButtonHideInterval;
        }

        public void showButton(PictureButton b, MenuPage.BFkt fkt)
        {
            b.BackgroundImage = mPage.mBAr[(int)fkt].icon;
            b.PressedImage = mPage.mBAr[(int)fkt].icon_p;
            b.Tag = fkt;
            b.BringToFront();
            b.Invalidate();
        }
        
        public void MenuExec(MenuPage.BFkt fkt)
        {
            button1_disableTimer();         //could otherwise false trigger (if button looses focus before MouseUp)
            /*if (!mPage.mBAr[(int)fkt].enabled)
            {
                MessageBeep(BeepType.IconHand);
                return;
            }*/
            if (!Utils.backlightState)
                Utils.SwitchBacklight(true);

            switch (fkt)
            {
                case MenuPage.BFkt.main:
                    buttonMain_Click(false); break;
                case MenuPage.BFkt.start:
                    buttonStart_Click(); goto case MenuPage.BFkt.main;
                case MenuPage.BFkt.stop:
                    buttonStop_Click(true);
                    buttonMain_Click(checkShowExit.Checked);
                    break;
                case MenuPage.BFkt.map:
                    buttonMap_Click(null, null); break;
                case MenuPage.BFkt.pause:
                    buttonPause_Click();
                    break;
                case MenuPage.BFkt.waypoint:
                    AddWaypoint();
                    goto case MenuPage.BFkt.main;
                case MenuPage.BFkt.options:
                    buttonOptions_Click(null, null); break;
                case MenuPage.BFkt.graph_alt:
                    buttonGraph_Click(Graph.SourceY.Alt); break;
                case MenuPage.BFkt.graph_speed:
                    buttonGraph_Click(Graph.SourceY.Speed); break;
                case MenuPage.BFkt.graph_heartRate:
                    buttonGraph_Click(Graph.SourceY.Heart); break;

                case MenuPage.BFkt.load_gcc:
                    buttonLoad_Click(null, null); break;
                case MenuPage.BFkt.load_2follow:
                    t2fAppendMode = false;
                    buttonLoadTrack2Follow_Click(null, null); break;
                case MenuPage.BFkt.restore_clear2f:
                    if (mPage.buttonIsClear2F)
                    {
                        buttonClearT2F_Click();
                    }
                    else
                    {
                        LoadState(true);        //restore
                        mPage.ChangeRestore2Clear();
                        MenuExec(MenuPage.BFkt.map);
                    }
                    break;

                case MenuPage.BFkt.clearTrack:
                    buttonClearTrack_Click(); break;

                case MenuPage.BFkt.exit:
                    BufferDrawMode = BufferDrawModeMain;    //to avoid exception in mPage OnMouseUp()
                    this.Close();
                    //Application.Exit();   Form1.Close is enough - Exit would exit in any case (also if Close was interrupted)
                    break;
                case MenuPage.BFkt.navigate:
                    BufferDrawMode = BufferDrawModeNavigate;
                    showButton(button1, MenuPage.BFkt.main);
                    showButton(button2, MenuPage.BFkt.map);
                    showButton(button3, MenuPage.BFkt.mPage);
                    NoBkPanel.BringToFront();
                    NoBkPanel.Invalidate();
                    mapUtil.nav.voicePlayed_toRoute = false;
                    mapUtil.nav.voicePlayed_dest = false;
                    mapUtil.corner.voicePlayed = false;
                    break;
                case MenuPage.BFkt.continu:
                    buttonContinue_Click(); break;
                case MenuPage.BFkt.lap:
                    BufferDrawMode = BufferDrawModeLap;
                    ShowOptionPageNumber(6);
                    showButton(button2, MenuPage.BFkt.options_prev);
                    showButton(button3, MenuPage.BFkt.options_next);
                    break;

                case MenuPage.BFkt.recall1:
                    RecallSettings(mPage.mBAr[(int)MenuPage.BFkt.recall1].text); break;
                case MenuPage.BFkt.recall2:
                    RecallSettings(mPage.mBAr[(int)MenuPage.BFkt.recall2].text); break;
                case MenuPage.BFkt.recall3:
                    RecallSettings(mPage.mBAr[(int)MenuPage.BFkt.recall3].text); break;

                case MenuPage.BFkt.backlight_off:
                    Utils.SwitchBacklight(false);
                    buttonMain_Click(false);
                    break;
                case MenuPage.BFkt.inputLatLon:
                    string LatLon = Lat2String(CurrentLat, false) + "; " + Lat2String(CurrentLong, true);
                    retry:
                    if (Utils.InputBox("Input", "Lat; Lon [; waypoint name]", ref LatLon) == DialogResult.OK)
                    {
                        int i=0;
                        double Lat, Lon;
                        bool showCurrent = false;
                        if (LatString2Double(LatLon, ref i, out Lat) && LatString2Double(LatLon, ref i, out Lon))
                        {
                            // ask, if the user wants to replace the loaded track to follow or add the point. 'Cancel' sets the current position
                            if (Plot2ndCount > 0)
                            {
                                DialogResult dr = MessageBox.Show("YES: REPLACE loaded Track2Follow with the new Lat/Long values\nNO: ADD point to t2f\nCANCEL: set 'Current Position' instead",
                                                  "Overwrite Track2Follow", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                                if (dr == DialogResult.Cancel)
                                {
                                    // The current Lat/Long values will only be usefull, if GPS is switched off.
                                    // If GPS is on, the values are directly overwritten, and will distort an active log
                                    if (state != State.logging)
                                    {
                                        CurrentLat = Lat;
                                        CurrentLong = Lon;
                                        showCurrent = true;
                                    }
                                    goto createWaypoint;
                                }
                                if (dr == DialogResult.Yes)
                                    Plot2ndCount = 0;
                            }
                            // Replace the existing track2follow with the new coordinates
                            if (Plot2ndCount == 0)
                            {
                                T2fSummary.Clear();
                                T2fSummary.desc = "Created by Input LatLon";
                            }
                            AddThisLatLonToT2F((float)Lat, (float)Lon);
                            
                        createWaypoint:      //create waypoint if there is an additional name
                            if (LatLon.Length > i)
                            {
                                string waypointname;
                                int idx = LatLon.IndexOf(';', i);
                                if (idx != -1)
                                {
                                    waypointname = LatLon.Substring(idx + 1).Trim();
                                    if (WayPointsT2F.Count < WayPointsT2F.DataSize)
                                    {
                                        WayPointsT2F.name[WayPointsT2F.Count] = waypointname;
                                        WayPointsT2F.lon[WayPointsT2F.Count] = (float)Lon;
                                        WayPointsT2F.lat[WayPointsT2F.Count] = (float)Lat;
                                        WayPointsT2F.Count++;
                                    }
                                    else
                                        MessageBox.Show("Max number of waypoints reached", "Error");
                                }
                            }
                            mPage.ChangeRestore2Clear();
                            // Jump in the Map screen directly to the track to follow new position
                            ResetMapPosition();
                            if (showCurrent)
                                mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FCurrent;
                            else
                                mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FEnd;
                            // Jump Directly to the Map view
                            buttonMap_Click(null, null);
                        }
                        else goto retry;

                    }
                    break;

                case MenuPage.BFkt.help:
                    buttonHelp_Click(null, null); break;
                case MenuPage.BFkt.about:
                    ShowOptionPageNumber(7);
                    break;


                case MenuPage.BFkt.gps_toggle:
                    buttonGPS_Click(null, null); break;

                case MenuPage.BFkt.options_next:
                    buttonOptionsNext_Click(null, null); break;
                case MenuPage.BFkt.options_prev:
                    buttonOptionsPrev_Click(null, null); break;
                case MenuPage.BFkt.map_zoomIn:
                    if (BufferDrawMode == BufferDrawModeMaps)
                    {
                        mapUtil.ZoomIn();
                        NoBkPanel.Invalidate();
                    }
                    else if (BufferDrawMode == BufferDrawModeGraph)
                        graph.GraphZoomIn();
                    break;
                case MenuPage.BFkt.map_zoomOut:
                    if (BufferDrawMode == BufferDrawModeMaps)
                    {
                        mapUtil.ZoomOut();
                        NoBkPanel.Invalidate();
                    }
                    else if (BufferDrawMode == BufferDrawModeGraph)
                        graph.GraphZoomOut();
                    break;
                case MenuPage.BFkt.dialog_open:
                    buttonDialogOpen_Click(null, null); break;
                case MenuPage.BFkt.dialog_cancel:
                    buttonDialogCancel_Click(null, null); break;
                case MenuPage.BFkt.dialog_up:
                    buttonDialogUp_Click(null, null); break;
                case MenuPage.BFkt.dialog_down:
                    buttonDialogDown_Click(null, null); break;
                case MenuPage.BFkt.dialog_nextFileType:
                    buttonNextFileType_Click(null,null); break;
                case MenuPage.BFkt.dialog_prevFileType:
                    buttonPrevFileType_Click(null, null); break;
                case MenuPage.BFkt.dialog_saveKml:
                    buttonSaveKML_Click(null, null); break;
                case MenuPage.BFkt.dialog_saveGpx:
                    buttonSaveGPX_Click(null, null); break;



                case MenuPage.BFkt.mPage:
                    mPage.ResetScroll();
                    mPage.ShowMenu();
                    break;
                case MenuPage.BFkt.mPageUp:
                    mPage.MenuUp();
                    mPage.ShowMenu();
                    break;
                case MenuPage.BFkt.mPageDown:
                    mPage.MenuDown();
                    mPage.ShowMenu();
                    break;
                case MenuPage.BFkt.waypoint2:
                    AddWaypoint2(false);
                    break;

                case MenuPage.BFkt.nothing:
                    break;

                default:
                    MessageBox.Show(fkt.ToString());
                    break;
            }
        }

        private void updateNavData()
        {
            mapUtil.clearNav(mapUtil.nav.backward);
            mapUtil.nav.ShortSearch = 0;
            if (Plot2ndCount > 0)
                mapUtil.GetNavigationData(utmUtil, Plot2ndLong, Plot2ndLat, Plot2ndD, Plot2ndCount, CurrentLong, CurrentLat);
        }

        private void AddThisMousePointToT2F(int ClientMouseX, int ClientMouseY)
        {
            if (Plot2ndCount > 0)
                if (Math.Abs(mapUtil.ToScreenX(Plot2ndLong[Plot2ndCount - 1]) - ClientMouseX) + Math.Abs(mapUtil.ToScreenY(Plot2ndLat[Plot2ndCount - 1]) - ClientMouseY) < 4)
                    return;                 //no double point
            float lon = (float)mapUtil.ToDataX(ClientMouseX);
            float lat = (float)mapUtil.ToDataYexact(ClientMouseY);
            AddThisLatLonToT2F(lat, lon);
        }

        private void AddThisLatLonToT2F(float lat, float lon)
        {
            if (Plot2ndCount >= PlotDataSize)
            {
                for (int i = 0; i < PlotDataSize / 2; i++)      //decimate t2f
                {
                    Plot2ndLong[i] = Plot2ndLong[i * 2];
                    Plot2ndLat[i] = Plot2ndLat[i * 2];
                    Plot2ndZ[i] = Plot2ndZ[i * 2];
                    Plot2ndT[i] = Plot2ndT[i * 2];
                    Plot2ndD[i] = Plot2ndD[i * 2];
                }
                Plot2ndCount /= 2;
                Plot2ndCountUndo = Plot2ndCount;
            }
            Plot2ndLong[Plot2ndCount] = lon;
            Plot2ndLat[Plot2ndCount] = lat;

            if (Plot2ndCount > 0)
            {
                Plot2ndZ[Plot2ndCount] = Plot2ndZ[Plot2ndCount - 1];
                Plot2ndT[Plot2ndCount] = Plot2ndT[Plot2ndCount - 1] + 1;
                if (!utmUtil.referenceSet)
                    utmUtil.setReferencePoint(lat, lon);
                double deltax = (lon - Plot2ndLong[Plot2ndCount-1]) * utmUtil.longit2meter;
                double deltay = (lat - Plot2ndLat[Plot2ndCount-1]) * utmUtil.lat2meter;
                T2fSummary.Distance += Math.Sqrt(deltax * deltax + deltay * deltay);
                Plot2ndD[Plot2ndCount] = (int)T2fSummary.Distance;
            }
            else
            {
                Plot2ndZ[Plot2ndCount] = 0;
                Plot2ndT[Plot2ndCount] = 0;
                Plot2ndD[Plot2ndCount] = 0;
                mPage.ChangeRestore2Clear();
            }

            Plot2ndCount++;
            if (Plot2ndCount > Plot2ndCountUndo)
                Plot2ndCountUndo = Plot2ndCount;
            updateNavData();
            //mapUtil.nav.ShortSearch = 0;
        }

        private void AddWaypoint()      //at current coodinates (cursor coordinates lead to a problem in storing WP in .gcc)
        {
            if (state == State.logging || state == State.paused)
            {
                string waypoint = "";
                string audiofile = GenerateEnumeratedAudioFilename();
                DialogResult Result = Utils.InputBoxRec(null, "Enter waypoint name", ref waypoint, audiofile);
                if (Result == DialogResult.OK)
                {
                    if (File.Exists(audiofile))
                    {
                        waypoint += '\x02';                     //delimiter for link to audio file
                        waypoint += Path.GetFileName(audiofile);
                    }
                    WriteCheckPoint(waypoint);
                }
                else
                {
                    try{File.Delete(audiofile);}
                    catch {};
                }
            }
            else MessageBox.Show("Can't add a waypoint when logging is not activated");
        }
        private void AddWaypoint2(bool cursorCoord)     //true: cursor coordinates, else last point of t2f
        {
            string waypoint = "";
            //string audiofile = GenerateEnumeratedAudioFilename();
            DialogResult Result = Utils.InputBox("Track 2 Follow", "Enter waypoint name (cursor coords)", ref waypoint);
            if (Result == DialogResult.OK)
            {
                //if (File.Exists(audiofile))
                //{
                //    waypoint += '\x02';                     //delimiter for link to audio file
                //    waypoint += Path.GetFileName(audiofile);
                //}
                if (WayPointsT2F.Count < WayPointsT2F.DataSize)
                {
                    WayPointsT2F.name[WayPointsT2F.Count] = waypoint;
                    if (cursorCoord)
                    {
                        WayPointsT2F.lon[WayPointsT2F.Count] = (float)mapUtil.ToDataX(MouseClientX);
                        WayPointsT2F.lat[WayPointsT2F.Count] = (float)mapUtil.ToDataYexact(MouseClientY);
                    }
                    else
                    {
                        WayPointsT2F.lon[WayPointsT2F.Count] = Plot2ndLong[Plot2ndCount - 1];
                        WayPointsT2F.lat[WayPointsT2F.Count] = Plot2ndLat[Plot2ndCount - 1];
                    }
                    WayPointsT2F.Count++;
                    mPage.ChangeRestore2Clear();
                }
                else
                    MessageBox.Show("Max number of waypoints reached", "Error");
            }
            //else
            //{
            //    try { File.Delete(audiofile); }
            //    catch { };
            //}
        }

        private string Lat2String(double lat, bool isLon)
        {
            switch (MainConfigLatFormat)
            {
                case 0:
                    return lat.ToString("0.000000°");
                case 1:
                case 2:
                    string str = "";
                    if (lat < 0)
                    {
                        lat = -lat;
                        if (isLon) str += 'W'; else str += 'S';
                    }
                    else
                        if (isLon) str += 'E'; else str += 'N';
                    double min = (lat % 1 * 60);
                    str += ((int)lat).ToString() + '°';
                    if(MainConfigLatFormat == 1)
                        str += min.ToString("00.0000") + '\'';
                    else
                        str += ((int)min).ToString("00") + '\'' + (min % 1 * 60).ToString("00.00") + '"';
                    return str;
                default:
                    return "";
            }
            
        }

        private bool LatString2Double(string str, ref int i, out double lat)
        {
            int state = 0;
            int numberstart = 0;
            double dez;
            lat = 1.0;
            str += ";";      //add end character to make it work
            for (; i < str.Length; i++)
            {
                switch (state)
                {
                    case 0:
                        if (str[i] == ' ' || str[i] == ';') continue;
                        if ("NnEeOo".IndexOf(str[i]) != -1) continue;       //if (str[i] is one of "NnEeOo")
                        if ("SsWw".IndexOf(str[i]) != -1) { lat = -1.0; continue; }
                        if (Char.IsDigit(str[i]) || str[i] == '-' || str[i] == '.' || str[i] == ',')
                        {
                            numberstart = i;
                            state = 1;
                            continue;
                        }
                        break;
                    case 1:
                        if (Char.IsDigit(str[i]) || str[i] == '-' || str[i] == '.' || str[i] == ',') continue;
                        lat *= Convert.ToDouble(str.Substring(numberstart, i - numberstart));     //read degree
                        if ("°d ".IndexOf(str[i]) != -1) { state = 2; continue; }
                        if (";EeOoWw".IndexOf(str[i]) != -1) return true;
                        else break;
                    case 2:
                        if (str[i] == ' ') continue;
                        if (Char.IsDigit(str[i]) || str[i] == '.' || str[i] == ',')
                        {
                            numberstart = i;
                            state = 3;
                            continue;
                        }
                        if (";EeOoWw".IndexOf(str[i]) != -1) return true;
                        break;
                    case 3:
                        if (Char.IsDigit(str[i]) || str[i] == '.' || str[i] == ',') continue;
                        dez = Convert.ToDouble(str.Substring(numberstart, i - numberstart)) / 60;    //minutes
                        if (lat < 0) lat -= dez;
                        else lat += dez;
                        if ("'m ".IndexOf(str[i]) != -1) { state = 4; continue; }
                        if (";EeOoWw".IndexOf(str[i]) != -1) return true;
                        else break;
                    case 4:
                        if (str[i] == ' ') continue;
                        if (Char.IsDigit(str[i]) || str[i] == '.' || str[i] == ',')
                        {
                            numberstart = i;
                            state = 5;
                            continue;
                        }
                        if (";EeOoWw".IndexOf(str[i]) != -1) return true;
                        else break;
                    case 5:
                        if (Char.IsDigit(str[i]) || str[i] == '.' || str[i] == ',') continue;
                        dez = Convert.ToDouble(str.Substring(numberstart, i - numberstart)) / (60*60);    //seconds
                        if (lat < 0) lat -= dez;
                        else lat += dez;
                        if ("\"s ".IndexOf(str[i]) != -1) { state = 6; continue; }
                        if (";EeOoWw".IndexOf(str[i]) != -1) return true;
                        else break;
                    case 6:
                        if (";EeOoWw".IndexOf(str[i]) != -1) return true;
                        else break;
                    default:
                        break;
                }
                MessageBox.Show("Format error at index " + i);
                return false;
            }
            return false;
        }


        private void RecallSettings(string name)
        {
            string filename = CurrentDirectory + name + ".dat";
            if (File.Exists(filename))
            {
                LoadSettings(filename);
                mPage.Invalidate();         //refresh ev. changed recall names
                Utils.MessageBeep(Utils.BeepType.Ok);
                // If a changed maps or Input/Output location is displayed, it may become invalid after a recall.
                // To avoid displaying an invalid path, overwrite the existing text.
                labelInfo.SetText("Info: Recall settings: " + name);
                LoadedSettingsName = name;
            }
            else
                MessageBox.Show("File does not exist:\n" + filename, "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
        }

        private bool LoadGcc(string filename)       //load as current track
        {
            // reset vars for computation
            PlotCount = 0;
            CurrentPlotIndex = -1;
            clearHR();
            Decimation = 1; DecimateCount = 0;
            WayPointsT.Count = 0;
            AudioEnum = 0;
            MaxSpeed = 0.0;
            ReferenceXDist = 0.0; ReferenceYDist = 0.0;
            OriginShiftX = 0.0; OriginShiftY = 0.0;
            CurrentTimeSec = 0; OldT = 0; CurrentStoppageTimeSec = 0; passiveTimeSeconds = 0;
            ReferenceAlt = Int16.MaxValue;
            TSummary.Clear();
            graph.scaleCmd = Graph.ScaleCmd.DoAutoscaleNoUndo;

            int gps_poll_sec = 0;

            // preset label text for errors
            //CurrentStatusString = "File has errors or blank";
            int loadOK = -1;
            //TSummary.filename = "";

            Cursor.Current = Cursors.WaitCursor;

            FileStream fs = null;
            BinaryReader rd = null;
            do
            {
                try
                {
                    fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                    rd = new BinaryReader(fs, Encoding.Unicode);

                    // load header "GCC1" (1 is version (binary!))
                    if (rd.ReadByte() != 'G') break; if (rd.ReadByte() != 'C') break;
                    if (rd.ReadByte() != 'C') break; if (rd.ReadByte() != 1) break;

                    // read time as 6 bytes: year, month...
                    int t1 = (int)rd.ReadByte(); t1 += 2000;
                    int t2 = (int)rd.ReadByte(); int t3 = (int)rd.ReadByte();
                    int t4 = (int)rd.ReadByte(); int t5 = (int)rd.ReadByte();
                    int t6 = (int)rd.ReadByte();
                    TSummary.StartTime = new DateTime(t1, t2, t3, t4, t5, t6);
                    StartTimeUtc = TSummary.StartTime.ToUniversalTime();

                    // read lat/long
                    StartLat = rd.ReadDouble(); StartLong = rd.ReadDouble();
                    utmUtil.setReferencePoint(StartLat, StartLong);
                    StartAlt = Int16.MinValue;

                    bool is_battery_printed = false;

                    Int16 x_int = 0; Int16 y_int = 0; Int16 z_int = 0; Int16 s_int = 0;
                    UInt16 t_16 = 0; UInt16 t_16last = 0; Int32 t_high = 0;
                    double out_lat = 0.0, out_long = 0.0;
                    Int16 heartRate = 0;
                    UInt64 recordError = 0UL;

                    bool loop = true;
                    while (loop)    //break with EndOfStreamException (or unknown record)
                    {
                        // get 5 short ints
                        try
                        {
                            loadOK = 1;
                            x_int = rd.ReadInt16();
                            loadOK = 0;
                            y_int = rd.ReadInt16();
                            z_int = rd.ReadInt16();
                            s_int = rd.ReadInt16();
                            t_16 = rd.ReadUInt16();
                        }
                        catch (EndOfStreamException) { break; }
                        catch (Exception e)
                        {
                            loadOK = -2;
                            Utils.log.Error(" LoadGcc - get 5 short ints", e);
                            break;
                        }

                        // check if this is a special record
                        if ((s_int == -1) && (t_16 == 0xFFFF))
                        {
                            switch (z_int)
                            {
                                case 0: // origin shift: z_int = 0
                                    OriginShiftX += x_int;
                                    OriginShiftY += y_int;
                                    break;
                                case 1: // battery: z_int = 1
                                    if (is_battery_printed == false)
                                    {
                                        StartBattery = x_int;
                                        is_battery_printed = true;
                                    }
                                    CurrentBattery = x_int;
                                    break;
                                case 2: // which GPS options were selected
                                    gps_poll_sec = x_int;
                                    break;
                                case 3: // waypoint
                                    // read waypoint name, if not blank
                                    string name = "";
                                    for (int i = 0; i < x_int; i++)
                                    {
                                        name += (char)(rd.ReadUInt16());
                                    }

                                    if (WayPointsT.Count < WayPointsT.DataSize)
                                    {
                                        // store new waypoint
                                        WayPointsT.name[WayPointsT.Count] = name;
                                        WayPointsT.lat[WayPointsT.Count] = (float)CurrentLat;
                                        WayPointsT.lon[WayPointsT.Count] = (float)CurrentLong;
                                        WayPointsT.Count++;
                                    }

                                    break;
                                case 4: // heart rate
                                    heartRate = x_int;
                                    break;

                                case 32: // name
                                    TSummary.name = rd.ReadString();
                                    break;
                                case 33: // desc
                                    TSummary.desc = rd.ReadString();
                                    break;
                                default:
                                    if ((1UL << z_int & recordError) == 0)
                                    {
                                        if (MessageBox.Show("unknown special record " + z_int + " at " + PlotCount + "\ntry to continue load anyway?", "Load Error",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1)
                                            == DialogResult.Cancel)
                                            loop = false;
                                        recordError |= 1UL << z_int;
                                    }
                                    if (loop && z_int >= 32)
                                        rd.ReadString();   //read unknown string in order to have correct record bounds
                                    break;
                            }
                        }
                        else    // "normal" record
                        {
                            // compute distance
                            double real_x = OriginShiftX + x_int;
                            double real_y = OriginShiftY + y_int;

                            double deltax = real_x - ReferenceXDist;
                            double deltay = real_y - ReferenceYDist;
                            TSummary.Distance += Math.Sqrt(deltax * deltax + deltay * deltay);
                            ReferenceXDist = real_x; ReferenceYDist = real_y;

                            // handle overflow in time
                            if (t_16 < t_16last)
                                t_high += 65536;
                            t_16last = t_16;
                            CurrentTimeSec = t_high + t_16;

                            // compute Stoppage time
                            if (s_int < 10) { CurrentStoppageTimeSec += CurrentTimeSec - OldT; }    //speed < 1.0 kmh
                            OldT = CurrentTimeSec;

                            // update max speed
                            if (s_int * 0.1 > MaxSpeed) { MaxSpeed = s_int * 0.1; }

                            // compute elevation gain
                            if (z_int != Int16.MinValue)        //MinValue = invalid
                            {
                                if (StartAlt == Int16.MinValue) StartAlt = z_int;

                                if (z_int > ReferenceAlt)
                                {
                                    TSummary.AltitudeGain += z_int - ReferenceAlt;
                                    ReferenceAlt = z_int;
                                }
                                else if (z_int < ReferenceAlt - AltThreshold)
                                {
                                    ReferenceAlt = z_int;
                                }
                                if (z_int > TSummary.AltitudeMax) TSummary.AltitudeMax = z_int;
                                if (z_int < TSummary.AltitudeMin) TSummary.AltitudeMin = z_int;
                            }

                            // convert to lat/long, used in plot arrays
                            utmUtil.getLatLong(real_x, real_y, out out_lat, out out_long);

                            // store data in plot array
                            AddPlotData((float)out_lat, (float)out_long, z_int, CurrentTimeSec, s_int, (Int32)TSummary.Distance, heartRate);
                            heartRate = 0;      //set to 0, in case next point includes no hr
                            // store point (used to update waypoint data
                            CurrentLat = out_lat;
                            CurrentLong = out_long;
                            CurrentAlt = z_int;
                            CurrentSpeed = s_int * 0.1;
                        }
                    }
                    //CurrentStatusString = Path.GetFileName(filename);

                    // for maps, fill x/y values realtive to the starting point
                    ResetMapPosition();
                }
                catch (Exception e)
                {
                    loadOK = -3;
                    Utils.log.Error (" LoadGcc ", e);
                }
            } while (false);
            if(rd != null) rd.Close();
            if(fs != null) fs.Close();

            Cursor.Current = Cursors.Default;

            if (loadOK < 0)
            {
                MessageBox.Show("File has errors or blank", "Error loading .gcc file", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                labelFileName.SetText("Current File Name: " + Path.GetFileName(filename) + " load ERROR");
                return false;
            }
            if (loadOK == 0) MessageBox.Show("File has been closed incorrectly\nDo not continue this file!", "Comment");

            labelFileName.SetText("Current File Name: " + Path.GetFileName(filename));
            TSummary.filename = filename;
            SaveState();
            return true;
        }

        private bool LoadT2f(string filename, bool showSummary, bool append)
        {
            bool loaded_ok = false;
            IFileSupport fs = null;
            int Plot2ndCount_old;
            int WaypointsCountOld;
            if (append)
            {
                Plot2ndCount_old = Plot2ndCount;
                WaypointsCountOld = WayPointsT2F.Count;
            }
            else
            {
                Plot2ndCount_old = 0;
                WaypointsCountOld = 0;
            }
            switch (Path.GetExtension(filename).ToLower())
            {
                case ".gcc":
                    fs = new GccSupport(); break;
                case ".gpx":
                    fs = new GpxSupport(); break;
                case ".kml":
                    fs = new KmlSupport(); break;
                default:
                    MessageBox.Show("wrong extension");
                    return false;
            }
            if (fs != null)
            {
                mapUtil.clearNav(false);
                loaded_ok = fs.Load(filename, ref WayPointsT2F, PlotDataSize, ref Plot2ndLat, ref Plot2ndLong, ref Plot2ndZ, ref Plot2ndT, ref Plot2ndD, ref T2fSummary, ref Plot2ndCount, append);
                graph.scaleCmd = Graph.ScaleCmd.DoAutoscaleNoUndo;
            }
            if (loaded_ok)  // loaded OK
            {
                // If a new track-to-follow loaded (and main track not exist) - need to reset map zoom/shift vars
                if ((Plot2ndCount + WayPointsT2F.Count != 0) && (PlotCount == 0)) { ResetMapPosition(); }

                labelFileNameT2F.SetText("Track to Follow: " + Path.GetFileName(filename));

                if (Plot2ndCount - Plot2ndCount_old == 0 && WayPointsT2F.Count - WaypointsCountOld > 0)        //if no track -> copy WP-data to T2F
                {
                    if (DialogResult.Yes == MessageBox.Show("Use waypoints as track to navigate?", "File only has Waypoints", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
                    {
                        double OldLong, OldLat;
                        if (Plot2ndCount > 0)
                        {
                            OldLong = Plot2ndLong[Plot2ndCount - 1];
                            OldLat = Plot2ndLat[Plot2ndCount - 1];
                        }
                        else
                        {
                            OldLong = WayPointsT2F.lon[WaypointsCountOld];
                            OldLat = WayPointsT2F.lat[WaypointsCountOld];
                        }
                        double deltax, deltay;
                        UtmUtil utmUtil2 = new UtmUtil();       //use extra utmUtil
                        utmUtil2.setReferencePoint(OldLat, OldLong);
                        for (int i = WaypointsCountOld; i < WayPointsT2F.Count; i++)
                        {
                            if (Plot2ndCount >= PlotDataSize)     // check if we need to decimate arrays
                            {
                                for (int j = 0; j < PlotDataSize / 2; j++)
                                {
                                    Plot2ndLat[j] = Plot2ndLat[j * 2];
                                    Plot2ndLong[j] = Plot2ndLong[j * 2];
                                    Plot2ndZ[j] = Plot2ndZ[j * 2];
                                    Plot2ndT[j] = Plot2ndT[j * 2];
                                    Plot2ndD[j] = Plot2ndD[j * 2];
                                }
                                Plot2ndCount = PlotDataSize / 2;
                                //Decimation *= 2;  //use all new data
                            }
                            Plot2ndLong[Plot2ndCount] = WayPointsT2F.lon[i];
                            Plot2ndLat[Plot2ndCount] = WayPointsT2F.lat[i];
                            deltax = (Plot2ndLong[Plot2ndCount] - OldLong) * utmUtil2.longit2meter;
                            deltay = (Plot2ndLat[Plot2ndCount] - OldLat) * utmUtil2.lat2meter;
                            T2fSummary.Distance += Math.Sqrt(deltax * deltax + deltay * deltay);
                            OldLong = Plot2ndLong[Plot2ndCount]; OldLat = Plot2ndLat[Plot2ndCount];
                            Plot2ndD[Plot2ndCount] = (int)T2fSummary.Distance;
                            Plot2ndT[Plot2ndCount] = Plot2ndCount == 0 ? 0 : Plot2ndT[Plot2ndCount - 1] + 1;
                            Plot2ndZ[Plot2ndCount] = 0;
                            Plot2ndCount++;
                        }
                    }
                }
                Plot2ndCountUndo = Plot2ndCount;
                updateNavData();
                if (showSummary) loaded_ok = DialogResult.OK == ShowTrackSummary(T2fSummary);
            }
            else
            {
                labelFileNameT2F.SetText("Track to Follow: " + Path.GetFileName(filename) + " load ERROR");
                T2fSummary.desc = "Error loading file";
                MessageBox.Show("Error reading file or it does not have track data", "Error loading file",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }
            return loaded_ok;
        }

        void SaveT2F(bool asGPX)
        {
            again:
            string filename = Path.GetFileNameWithoutExtension(T2fSummary.filename);
            if (Utils.InputBox("Input", "Filename (without ext)", ref filename) == DialogResult.OK)
            {
                if (filename.Length == 0)
                    if (MessageBox.Show("Filename is empty", "Error", MessageBoxButtons.RetryCancel,
                        MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1) == DialogResult.Cancel)
                        return;
                    else
                        goto again;
                if (IoFilesDirectory == "\\") { filename = "\\" + filename; }
                else { filename = IoFilesDirectory + "\\" + filename; }
                if (asGPX)
                    filename += ".gpx";
                else
                    filename += ".kml";
                if (File.Exists(filename))
                    if (MessageBox.Show("File exists. Overwrite?", "Warning", MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Cancel)
                        return;
                Cursor.Current = Cursors.WaitCursor;
                T2fSummary.filename = filename;
                if (asGPX)
                    new GpxSupport().Write(filename, WayPointsT2F,
                    checkGpxRte, checkGpxSpeedMs, checkGPXtrkseg,
                    Plot2ndLat, Plot2ndLong, Plot2ndCount,
                    null, Plot2ndT, Plot2ndZ, null,
                    T2fSummary,
                    numericGpxTimeShift);
                else
                    new KmlSupport().Write(filename, WayPointsT2F,
                    Plot2ndLat, Plot2ndLong, Plot2ndCount,
                    null, Plot2ndT, Plot2ndZ,
                    T2fSummary,
                    comboBoxKmlOptColor, checkKmlAlt,
                    comboBoxKmlOptWidth);
                Cursor.Current = Cursors.Default;
            }
        }

        DialogResult ShowTrackSummary(TrackSummary ts)
        {
            string altMax, altMin, altUnit, descr;
            double m2feet;
            if ((comboUnits.SelectedIndex == 3) || (comboUnits.SelectedIndex == 5) || (comboUnits.SelectedIndex == 6))
            {
                m2feet = 1.0 / 0.30480;    // altitude in feet
                altUnit = "feet";
            }
            else
            {
                m2feet = 1.0;
                altUnit = "m";
            }
            if (ts.AltitudeMax != Int16.MinValue)
                altMax = (ts.AltitudeMax * m2feet).ToString("0.#");
            else
                altMax = "--";
            if (ts.AltitudeMin != Int16.MaxValue)
                altMin = (ts.AltitudeMin * m2feet).ToString("0.#");
            else
                altMin = "--";
            if (ts.desc.Length > 120)
                descr = ts.desc.Substring(0, 120);
            else
                descr = ts.desc;

            return MessageBox.Show("Name = " + ts.name
                            + "\nDesc = " + descr
                            + "\nStartTime = " + ts.StartTime.ToString()
                            + "\nDistance = " + (ts.Distance * GetUnitsConversionCff()).ToString("0.##") + GetUnitsName()
                            + "\nAltitude Gain = " + (ts.AltitudeGain * m2feet).ToString("0.#") + altUnit
                            + "\nAltitude Max = " + altMax + altUnit
                            + "\nAltitude Min = " + altMin + altUnit,
                            (ts.filename.Length > 0) ? Path.GetFileName(ts.filename) : "",     //avoid display of "Error"
                            MessageBoxButtons.OKCancel, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }



        int tickCounter = 0;
        // start/stop GPS
        private void timerGps_Tick(object sender, EventArgs e)
        {
            //Debug.WriteLine("timerGPS-tick");
            //uint pflag = 0;
            //Utils.GetSystemPowerState(debugStr, 4, ref pflag);
            //debugStr = pflag.ToString();

            GpsLogCounter--;    //can run negative (for 68 years)
            if (LockGpsTick) { return; }
                        
            // set a lock for this function (just in case it got stack in GPS calls)
            LockGpsTick = true;

            System.Threading.Thread.Sleep(100); //KB to give System (touchPanel, GPS,...) computing time

            if(gps.Opened)
            {
                GetGpsData();

                if (GpsDataState == GpsOk && comboGpsPoll.SelectedIndex >= IndexSuspendMode)    //start/stop (suspend) mode
                {
                    SuspendGps();
                    GpsSuspendCounter = PollGpsTimeSec[comboGpsPoll.SelectedIndex];
                }
                // close and open GPS, if searching too long - this might revive it!
                else if (GpsSearchCount > 300)
                {
                    SuspendGps(); // first we close it
                    GpsSuspendCounter = 0;  //let it start again at the next tick
                }
                if (Plot2ndCount > 0 && (comboNavCmd.SelectedIndex > 0 || BufferDrawMode == BufferDrawModeMaps || BufferDrawMode == BufferDrawModeGraph
                                        || BufferDrawMode == BufferDrawModeNavigate || (BufferDrawMode == BufferDrawModeMain && MainConfigNav)))
                {
                    mapUtil.GetNavigationData(utmUtil, Plot2ndLong, Plot2ndLat, Plot2ndD, Plot2ndCount, CurrentLong, CurrentLat);
                    mapUtil.DoVoiceCommand();
                }
                mapUtil.doneVoiceCommand = false;
            }

            else if(gps.Suspended)
            {
                if (--GpsSuspendCounter < 0)
                {
                    OpenGps();
                }
                else
                {
                    CurrentStatusString = "GPS: suspended for " +  GpsSuspendCounter + "s ";
                }
                CurrentGpsLedColor = Color.Gray;
            }

            else if (StoppedOnLow)
            {
                CurrentStatusString = "GPS: stopped on low power";
                CurrentGpsLedColor = bkColor;
            }

            else  //gps off
            {
                GpsDataState = GpsNotOk;
                CurrentStatusString = "GPS: off ";
                CurrentGpsLedColor = bkColor;
                if (state == State.logHrOnly)
                    CurrentStatusString += "; logging HR";
                else if (state == State.pauseHrOnly)
                    CurrentStatusString += "; HR paused";
            }
            CurrentBattery = Utils.GetBatteryStatus();

            if (MainConfigSpeedSource == 3 || MainConfigSpeedSource == 4)
            {
                if (oHeartBeat == null)
                    oHeartBeat = new HeartBeat();
                else
                    oHeartBeat.Tick();
            }
            else
                if (oHeartBeat != null)
                {
                    oHeartBeat.Close();
                    oHeartBeat = null;
                }
            if (state == State.logHrOnly)
                logHeartRateOnly();

/*            string ps = new string('\0', 12);
            uint pflag = 0;
            GetSystemPowerState(ps, 12, ref pflag);

            // Update the screen only, if the device is switched on. During unattended state, the display is off, and 
            // we do not update the screen. Avoid unnecessary CPU load / battery consumption
            // After a first test, we do not have any improvement in Battery lifetime. Therefore not used...
            if ( (pflag & 0x00400000) == 0 )    // POWER_STATE_UNATTENDED - Unattended state
*/          {
                NoBkPanel.Invalidate();
            }
            LockGpsTick = false;

            tickCounter++;
            if (MainConfigAlt2display >= 256)
                if ((tickCounter & 3) == 0)           //cycle Alt2display every 4s
                    if (++MainConfigAlt2display > 260)
                        MainConfigAlt2display = 256;

            //test
            //Utils.log.Debug("debugtext");
            //System.Diagnostics.Debug.WriteLineIf(true, "test");

            //dwStartTick = Environment.TickCount;
            //dwIdleSt = GetIdleTime();
            //  You must insert a call to the Sleep(sleep_time) function to allow
            //  idle time to accrue. An example of an appropriate sleep time is
            //  1000 ms.
            //dwStopTick = GetTickCount();
            //int it = GetIdleTime();
            //PercentIdle = ((100 * (dwIdleEd - dwIdleSt)) / (dwStopTick - dwStartTick));

            

            //Process proc = Process.GetCurrentProcess();

            //IntPtr phandle = OpenThread(0x400, false, proc.Id);
            //long cr, end, kt, ut;
            //GetThreadTimes(phandle, out cr, out end, out kt, out ut);
            //debugStr = (Environment.TickCount).ToString();
            //idleTime = it;
        }
        //int idleTime = 0;

        //[DllImport("coredll.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //internal static extern int GetIdleTime();

        //[DllImport("coredll.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //internal static extern IntPtr OpenThread(int access, bool inherit, int ID);

        //[DllImport("coredll.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //public static extern bool GetThreadTimes(IntPtr handle, out long creation, out long exit, out long kernel, out long user);


        int OpenGps()
        {
            if (gps.Opened)
                return 1;
            // activate code from GccDll, if option selected
            gps.setOptions(checkBoxUseGccDll.Checked,
                comboBoxUseGccDllCom.SelectedIndex + (logRawNmea ? 100 : 0),    // + 100 indicates to log raw nmea data
               BaudRates[comboBoxUseGccDllRate.SelectedIndex]);
            int ret = gps.Open();
            if (ret != 1)
            {
                MessageBox.Show("Can't open GPS port. Error " + ret);
                return ret;
            }
            //buttonGPS.Text = "GPS is on";     //todo
            GpsSearchCount = 0;
            if (GpsDataState > GpsBecameValid)
                GpsDataState = GpsBecameValid;
            timerGps.Enabled = true;
            CurrentStatusString = "gps opening ...";
            // Only if option "Save Energy-GPS off when power off" is not activated, keep 
            // GPS always on  
            if( checkGPSOffOnPowerOff.Checked == false )
            {
                Utils.KeepGpsRunning(true);
            }
            // Keep backlight on, if Backlight Energy safe option is not set.
            if (checkKeepBackLightOn.Checked == false)
            {
                timerIdleReset.Enabled = true;
            }
            //goodsamples = 0; //debug
            //numsamples = 0; //debug
            return ret;
        }

        void CloseGps()
        {
            gps.Close();
            GpsDataState = GpsNotOk;
            // buttonGPS.Text = "GPS is off";        //todo
            Utils.KeepGpsRunning(false);
            // Allow backlight to switch off after timeout
            timerIdleReset.Enabled = false;
        }

        void SuspendGps()
        {
            gps.Suspend();
            //KeepToolRunning(false);
        }




        private String getFileName(string extension)
        {
            if (listBoxFiles.SelectedIndex != -1)
            {
                string gcc_file = listBoxFiles.SelectedItem.ToString();
                gcc_file = gcc_file.Replace("*", ""); // remove * and + for the gpx/kml indication
                gcc_file = gcc_file.Replace("+", "");

                if (IoFilesDirectory == "\\")
                {
                    gcc_file = "\\" + gcc_file;
                }
                else
                {
                    gcc_file = IoFilesDirectory + "\\" + gcc_file;
                }

                if (!LoadGcc(gcc_file))
                {
                    return "";
                }
            }
            else
            {
                MessageBox.Show("No files selected", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                return "";
            }

            if (PlotCount == 0)
            {
                MessageBox.Show("File is blank - no records to save to KML", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                return "";
            }

            CheckIoDirectoryExists();
            return Path.GetFileNameWithoutExtension(TSummary.filename) + extension;
        }

        private string CreateTrackDescription()
        {
            string dist_unit, speed_unit, alt_unit, exstop_info;
            GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out exstop_info);
            string dist, speed_cur, speed_avg, speed_max, run_time_label, last_sample_time, altitude, altitude2, battery;
            GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time_label, out last_sample_time, out altitude, out altitude2, out battery);
            string desc = dist + " " + dist_unit + " " + run_time_label + " " + exstop_info
                               + " " + speed_cur + " " + speed_avg + " " + speed_max + " " + speed_unit
                               + " battery " + battery;
            return desc;
        }

        private void refillListBox()
        {
            int selected_index = listBoxFiles.SelectedIndex;
            FillFileNames();
            if (listBoxFiles.Items.Count != 0) { listBoxFiles.SelectedIndex = selected_index; }
        }
        private void buttonSaveKML_Click(object sender, EventArgs e)
        {
            String kml_file = getFileName(".kml");
            if ("".Equals(kml_file)) return;

            Cursor.Current = Cursors.WaitCursor;

            if (IoFilesDirectory == "\\") { kml_file = "\\" + kml_file; }
            else { kml_file = IoFilesDirectory + "\\" + kml_file; }

            if (TSummary.name.Length == 0)
                TSummary.name = TSummary.StartTime.ToString();
            if (TSummary.desc.Length == 0)
                TSummary.desc = CreateTrackDescription();

            new KmlSupport().Write(kml_file, WayPointsT,
                PlotLat, PlotLong, PlotCount, PlotS, PlotT, PlotZ,
                TSummary,
                comboBoxKmlOptColor, checkKmlAlt,
                comboBoxKmlOptWidth);
            
            Cursor.Current = Cursors.Default;
            refillListBox();    // refill listBox, to indicate that KML was saved
        }
        private void buttonSaveGPX_Click(object sender, EventArgs e)
        {
            String gpx_file = getFileName(".gpx");
            if("".Equals(gpx_file)) return;

            Cursor.Current = Cursors.WaitCursor;
           
            if (IoFilesDirectory == "\\") { gpx_file = "\\" + gpx_file; }
            else { gpx_file = IoFilesDirectory + "\\" + gpx_file; }

            if (TSummary.name.Length == 0)
                TSummary.name = TSummary.StartTime.ToString();
            if (TSummary.desc.Length == 0)
                TSummary.desc = CreateTrackDescription();

            new GpxSupport().Write(gpx_file, WayPointsT,
                checkGpxRte, checkGpxSpeedMs, checkGPXtrkseg,
                PlotLat, PlotLong, PlotCount,
                PlotS, PlotT, PlotZ, PlotH,
                TSummary, numericGpxTimeShift);
            
            Cursor.Current = Cursors.Default;
            refillListBox();    // refill listBox, to indicate that GPX was saved

            // upload GPX to CW site
            if (checkUploadGpx.Checked && File.Exists(gpx_file))
            {
                if (MessageBox.Show("Do you want to upload GPX file?", textBoxCwUrl.Text,
                   MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    Cursor.Current = Cursors.WaitCursor;

                    StreamReader re = File.OpenText(gpx_file);
                    string gpx = null;
                    gpx = re.ReadToEnd();
                    re.Close();

                    string resp = CWUtils.UploadGPXViaHTTP(textBoxCwUrl.Text, textBoxCw1.Text, CwHashPassword, "GCC Log", gpx);

                    Cursor.Current = Cursors.Default;
                    MessageBox.Show(resp, textBoxCwUrl.Text);
                }
            }

        }

        private void saveCsvLog()
        {
            if (PlotCount == 0)
                { return; }
            CheckIoDirectoryExists();

            string log_file = "log1.csv";

            if (IoFilesDirectory == "\\") { log_file = "\\" + log_file; }
            else { log_file = IoFilesDirectory + "\\" + log_file; }

            bool file_exists = File.Exists(log_file);

            FileStream fs = null;
            StreamWriter wr = null;
            try
            {
                fs = new FileStream(log_file, FileMode.Append);
                wr = new StreamWriter(fs);

                // write header, if this is a new file
                if (!file_exists)
                {
                    string dist_unit, speed_unit, alt_unit, exstop_info;
                    GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out exstop_info);

                    wr.WriteLine("File name;;Start time;Stop time;;Start position;End position;;Distance [" + dist_unit +
                                "];Trip time (incl stop);Ride time (excl stop);;Speed avg [" + speed_unit + "];Speed max [" + speed_unit + "];;Altitude gain [m]");
                }

                TimeSpan trip_time = new TimeSpan(0, 0, CurrentTimeSec);

                // write record. Separate file name into a few fields
                string fname = Path.GetFileNameWithoutExtension(TSummary.filename);

                string dist, speed_cur, speed_avg, speed_max, run_time_str, last_sample_time, altitude, altitude2, battery;
                GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time_str, out last_sample_time, out altitude, out altitude2, out battery);

                wr.WriteLine(fname + ";;" +
                    TSummary.StartTime.ToString() + ";" +
                    (TSummary.StartTime + trip_time).ToString() + ";;" +
                    StartLat.ToString("0.##########") + " " + StartLong.ToString("0.##########") + ";" +
                    PlotLat[PlotCount - 1].ToString("0.##########") + " " + PlotLong[PlotCount - 1].ToString("0.##########") + ";;" +
                    dist + ";" +
                    trip_time.ToString() + ";" +
                    new TimeSpan(0, 0, CurrentTimeSec - CurrentStoppageTimeSec).ToString() + ";;" +
                    speed_avg + ";" + speed_max + ";;" +
                    TSummary.AltitudeGain.ToString("0.#")
                    );

            }
            catch (Exception e)
            {
                Utils.log.Error(" saveCsvLog ", e);
            }
            finally
            {
                if(wr != null) wr.Close();
                if(fs != null) fs.Close();
            }
        }

        private void comboGpsPoll_SelectedIndexChanged(object sender, EventArgs e)
        {
            GpsLogCounter = 0;
            GpsLogDistance = 0.0;
        }

        private void numericZoomRadiusChanged(object sender, EventArgs e)
        {
            mapUtil.DefaultZoomRadius = (int)numericZoomRadius.Value;
        }


        // draw main screen ------------------------------------------------
        Bitmap BackBuffer = null;           // the bitmap we draw into
        public Graphics BackBufferGraphics = null;
                 
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

                Font f = new Font("Arial", 9, FontStyle.Regular);
                SizeF s = BackBufferGraphics.MeasureString("Ne0", f);
                df = NoBkPanel.Height / (s.Height * 18.0f);
                float dfh = NoBkPanel.Width / (s.Width * 10.9f);
                //debugStr = df.ToString("#.###") + " " + dfh.ToString("#.###");
                if (dfh < df)
                    df = dfh;
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
            Font f = new Font("Arial", 9 * df, FontStyle.Regular);
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
            float smaller_font_size = font_size*(2.0f/3.0f);
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
            if (x >= 10000.0) { return x.ToString("0"); }
            if (x >= 100.0) { return x.ToString("0.0"); }
            return x.ToString("0.00");
        }
        private string PrintSpeed(double x)
        {
            if (x >= 100.0) { return x.ToString("0."); }
            return x.ToString("0.0");
        }
        private void GetValuesToDisplay(out string dist, out string speed_cur, out string speed_avg, out string speed_max, out string run_time, out string last_sample_time, out string altitude_str, out string altitude2_str, out string battery)
        {
            // battery current and estimation
            if (CurrentBattery <= -255) { battery = "??%"; }
            else if (CurrentBattery < 0) { battery = "AC " + (-CurrentBattery).ToString() + "%"; }
            else { battery = CurrentBattery.ToString() + "%"; }

            // try to estimate charge left
            if ((CurrentBattery > 0) && (StartBattery > 0) && (CurrentBattery < StartBattery) && (CurrentBattery > 20))
            {
                double sec_per_battery_percent = CurrentTimeSec / (double)(StartBattery - CurrentBattery);
                int min_left = (int)((CurrentBattery - 20) * sec_per_battery_percent / 60);
                int hour = min_left / 60;
                int min = min_left % 60;
                battery += hour + "h" + min + "m left";
            }

            //if (PlotCount == 0 && GpsDataState == GpsNotOk)
            //{
            //    dist = "0.0"; speed_cur = "0.0"; speed_avg = "0.0"; speed_max = "0.0";
            //    run_time = "00:00:00"; last_sample_time = ""; altitude_str = "0";
            //    return;
            //}

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

            //TimeSpan ts_all = new TimeSpan(0, 0, CurrentTimeSec);
            //last_sample_time = (TSummary.StartTime + ts_all).ToString("T");
            if (LastPointUtc != DateTime.MinValue)
                last_sample_time = LastPointUtc.ToLocalTime().ToString("T");
            else
                last_sample_time = "";

            dist = PrintDist(TSummary.Distance * 0.001 * ceff);

            double altitude = CurrentAlt;
            // relative altitude mode
            if (MainConfigRelativeAlt) { altitude -= PlotZ[0]; }

            if ((comboUnits.SelectedIndex == 3) || (comboUnits.SelectedIndex == 5) || (comboUnits.SelectedIndex == 6))
                { m2feet = 1.0 / 0.30480; }    // altitude in feet
            else
                { m2feet = 1.0; }

            if (CurrentAlt == Int16.MinValue)
                altitude_str = "---";
            else
                altitude_str = (altitude * m2feet).ToString("0.0");

            int MainConfigAlt2display_ = MainConfigAlt2display & 127;
            if (MainConfigAlt2display_ == 0)            //gain
                altitude2_str = (TSummary.AltitudeGain * m2feet).ToString("0.0");
            else if (MainConfigAlt2display_ == 1)        //loss
            {
                double ElevationLoss = 0.0;
                if (StartAlt != Int16.MinValue) ElevationLoss = CurrentAlt - StartAlt - TSummary.AltitudeGain;
                altitude2_str = (ElevationLoss * m2feet).ToString("0.0");
            }
            else if (MainConfigAlt2display_ == 2)        //max
            {
                if (TSummary.AltitudeMax != Int16.MinValue)
                    altitude2_str = (TSummary.AltitudeMax * m2feet).ToString("0.0");
                else altitude2_str = "---";
            }
            else if (MainConfigAlt2display_ == 3)        //min
            {
                if (TSummary.AltitudeMin != Int16.MaxValue)
                    altitude2_str = (TSummary.AltitudeMin * m2feet).ToString("0.0");
                else altitude2_str = "---";
            }
            else                                //slope
            {
                altitude2_str = ElevationSlope.ToString("0.0%");
            }

            double averageSpeed = (time_to_use == 0) ? 0.0 : (TSummary.Distance * 3.6 / time_to_use);

            // speed in min/km or min per mile
            if ((comboUnits.SelectedIndex == 4) || (comboUnits.SelectedIndex == 5))
            {
                DateTime a_date = new DateTime(2008, 1, 1);

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
                if (CurrentSpeed == Int16.MinValue * 0.1)
                    speed_cur = "---";
                else
                    speed_cur = PrintSpeed(CurrentSpeed * ceff);
                speed_avg = PrintSpeed(averageSpeed * ceff);
                speed_max = PrintSpeed(MaxSpeed * ceff);
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
            if (beepOnStartStop) { exstop_info += " b"; }
        }

        public int GetValuesToDisplayInMap(ref string[] arStrName, ref string[] arStrValue, ref string[] arStrUnit)
        {
            string trip_time;       //
            //string time;
            string speed;           //
            string avg_speed;       //
            string max_speed;       //
            string distance;        //
            string altitude_1;      //
            string altitude_2;      //
            string battery;         //
            //string dist_to_dest;
            //string time_to_dest;

            string dist_unit;
            string speed_unit;
            string alt_unit;
            string dummy;

            GetValuesToDisplay(out distance, out speed, out avg_speed, out max_speed, out trip_time, out dummy, out altitude_1, out altitude_2, out battery);
            GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out dummy);
            int number = 3;
            for (int i = 0; i<3; i++)
            {
                MapValue conf = mapValuesConf[i];
                if (Plot2ndCount == 0 || (conf & MapValue.no_change) > 0)
                    conf &= (MapValue)255;
                else
                    conf &= MapValue.dist_2_dest | MapValue.time_2_dest;

                arStrName[i] = conf.ToString();
                switch (conf)
                {
                    case MapValue._off_:
                    case MapValue._off__:
                        arStrName[i] = null;
                        arStrValue[i] = null;
                        arStrUnit[i] = null;
                        number--; continue;// break;
                    case MapValue.trip_time:
                        arStrValue[i] = trip_time.Substring(0, 5); arStrUnit[i] = ""; break;
                    case MapValue.time:
                        arStrValue[i] = DateTime.Now.ToString("t");
                        try { arStrValue[i] = arStrValue[i].Substring(0, 5); }
                        catch { }       //do nothing if exception: str shorter than 5
                        arStrUnit[i] = "";
                        break;
                    case MapValue.speed:
                        arStrValue[i] = speed; arStrUnit[i] = speed_unit; break;
                    case MapValue.avg_speed:
                        arStrValue[i] = avg_speed; arStrUnit[i] = speed_unit; break;
                    case MapValue.max_speed:
                        arStrValue[i] = max_speed; arStrUnit[i] = speed_unit; break;
                    case MapValue.distance:
                        arStrValue[i] = distance; arStrUnit[i] = dist_unit; break;
                    case MapValue.altitude_1:
                        arStrValue[i] = altitude_1; arStrUnit[i] = alt_unit; break;
                    case MapValue.altitude_2:
                        int a2 = MainConfigAlt2display & 127;
                        switch (a2)
                        {
                            case 0: arStrName[i] = "alt_gain"; break;
                            case 1: arStrName[i] = "alt_loss"; break;
                            case 2: arStrName[i] = "alt_max"; break;
                            case 3: arStrName[i] = "alt_min"; break;
                            case 4: arStrName[i] = "alt_slope"; break;
                        }
                        arStrValue[i] = altitude_2;
                        if (a2 != 4)
                            arStrUnit[i] = alt_unit;
                        else arStrUnit[i] = "";
                        break;
                    case MapValue.battery:
                        //battery = "45%3h66m left";
                        int indexPerc = battery.IndexOf('%');
                        if ((tickCounter & 4) > 0 && battery.Length > indexPerc + 1)
                        { arStrValue[i] = battery.Substring(indexPerc + 1, battery.Length - indexPerc - 6); arStrUnit[i] = "left"; }
                        else
                        { arStrValue[i] = battery.Substring(0, indexPerc); arStrUnit[i] = "%"; }
                        break;
                    case MapValue.dist_2_dest:    //distance to destin
                        arStrValue[i] = PrintDist(mapUtil.nav.Distance2Dest * GetUnitsConversionCff());
                        arStrUnit[i] = dist_unit;
                        break;
                    case MapValue.time_2_dest:    //time to destin
                        int seconds = (int)(mapUtil.nav.Distance2Dest / TSummary.Distance * (CurrentTimeSec - CurrentStoppageTimeSec));
                        arStrValue[i] = new TimeSpan(0, 0, seconds).ToString().Substring(0, 5);
                        arStrUnit[i] = "";
                        break;
                    default:
                        arStrValue[i] = "???";
                        break;
                }
            }
            return number;
        }

        private void GetGpsSearchFlags(out string gps_status1, out string gps_status2)
        {
            gps_status1 = "S"; gps_status2 = "";

            if (position == null)
                { gps_status1 = "no gps data"; return; }

            if (position.SatelliteCountValid)
                gps_status1 += position.SatelliteCount.ToString();
            else
                gps_status1 += "-";
            gps_status1 += "/";
            if (position.SatellitesInViewCountValid)
                gps_status1 += position.SatellitesInViewCount.ToString() + " Snr" + position.GetMaxSNR().ToString();
            else
                gps_status1 += "- Snr-";

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
                float x = position.HorizontalDilutionOfPrecision;
                if (x > 99) { x = 99; }
                gps_status2 += " Dh" + x.ToString("#0.0");
            }
            else
                { gps_status2 += " Dh-"; }
        }

        private void DrawMain(Graphics g)           // draw main screen with watch, speed, distance, altitute...
        {
            BackBufferGraphics.Clear(bkColor);
            Pen p = new Pen(GetAverageColor(), 1);
            float df_8 = df * 8;
            float df_9 = df * 9;
               
            // draw lines separating different cells
            BackBufferGraphics.DrawLine(p, MGridX[0], MGridY[1], MGridX[3], MGridY[1]);
            BackBufferGraphics.DrawLine(p, MGridX[1], MGridY[2], MGridX[3], MGridY[2]);
            BackBufferGraphics.DrawLine(p, MGridX[0], MGridY[3], MGridX[3], MGridY[3]);
            BackBufferGraphics.DrawLine(p, MGridX[0], MGridY[5], MGridX[3], MGridY[5]);
            BackBufferGraphics.DrawLine(p, MGridX[2], MGridY[0], MGridX[2], MGridY[1]);
            BackBufferGraphics.DrawLine(p, MGridX[1], MGridY[1], MGridX[1], MGridY[7]);

            // draw labels and units
            string dist_unit, speed_unit, alt_unit, exstop_info;
            GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out exstop_info);
            string altitude_mode = "Altitude";
            if (comboLapOptions.SelectedIndex == 0)
            {
                if (MainConfigRelativeAlt) { altitude_mode += " rel"; }
            }
            else
            {
                altitude_mode = "Lap";      // in Lap mode use Altitude field to display Lap values current and last
                if (comboLapOptions.SelectedIndex <= 6) { alt_unit = "min:s"; }
                else { alt_unit = "km"; }
            }

            string[] speed_info = new string[5] { "cur gps", "cur pos", "cur", "cur + hr", "cur+hr+s" };
            string[] dist_info = new string[5] { "trip", "t2f start", "t2f end", "ODO", "to destin." };
            string label1;
            //DrawMainLabelAndUnits(BackBufferGraphics, "Time",     "h:m:s",    MGridX[0], MGridY[0]);  moved down
            DrawMainLabelAndUnits(BackBufferGraphics, "Speed",    speed_unit, MGridX[0], MGridY[1]);
            DrawMainLabelAndUnits(BackBufferGraphics, "Distance", dist_unit,  MGridX[0], MGridY[3]);
            DrawMainLabelAndUnits(BackBufferGraphics, "Info",     "",         MGridX[0], MGridY[5]);
            DrawMainLabelAndUnits(BackBufferGraphics, altitude_mode, alt_unit, MGridX[1], MGridY[3]);
            if (MainConfigNav && Plot2ndCount > 0)
                label1 = "Nav";
            else
                label1 = "GPS";
            DrawMainLabelAndUnits(BackBufferGraphics, label1,      "",         MGridX[1], MGridY[5]);
            DrawMainLabelOnRight(BackBufferGraphics, exstop_info, MGridX[2], MGridY[0], df_9);
            DrawMainLabelOnRight(BackBufferGraphics, speed_info[MainConfigSpeedSource], MGridX[1], MGridY[1], df_9);
            DrawMainLabelOnRight(BackBufferGraphics, dist_info[Plot2ndCount == 0 && (MainConfigDistance == eConfigDistance.eDistanceTrack2FollowStart || MainConfigDistance == eConfigDistance.eDistanceTrack2FollowEnd || MainConfigDistance == eConfigDistance.eDistance2Destination) ? (int)eConfigDistance.eDistanceTrip : (int)MainConfigDistance], MGridX[1], MGridY[3], df_9);
            DrawMainLabelOnRight(BackBufferGraphics, "avg", MGridX[3], MGridY[1], df_9);
            DrawMainLabelOnRight(BackBufferGraphics, "max", MGridX[3], MGridY[2], df_9);
            DrawMainLabelOnRight(BackBufferGraphics, "cur", MGridX[3], MGridY[3] + MHeightDelta, df_9);
            int MainConfigAlt2display_ = MainConfigAlt2display & 127;
            if (comboLapOptions.SelectedIndex == 0)
            {
                if (MainConfigAlt2display >= 256) label1 = "."; else label1 = "";
                if (MainConfigAlt2display_ == 0) label1 += "gain";
                else if (MainConfigAlt2display_ == 1) label1 += "loss";
                else if (MainConfigAlt2display_ == 2) label1 += "max";
                else if (MainConfigAlt2display_ == 3) label1 += "min";
                else label1 += "slope";
                DrawMainLabelOnRight(BackBufferGraphics, label1, MGridX[3], MGridY[4], df_9);
            }
                        
            // draw the values
            string dist, speed_cur, speed_avg, speed_max, run_time, last_sample_time, altitude, altitude2, battery;
            GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time, out last_sample_time, out altitude, out altitude2, out battery);
            string timeunit = "h:m:s";
            if (run_time.Length > 8) timeunit = "d.h:m:s";
            DrawMainLabelAndUnits(BackBufferGraphics, "Time", timeunit, MGridX[0], MGridY[0]);
            DrawMainValues(BackBufferGraphics, run_time, (MGridX[0] + MGridX[2]) / 2, MGridY[1], 30.0f * 8f / run_time.Length * df);
            if (MainConfigSpeedSource == 2)
            {
                string v_cur;
                if (CurrentV == Int16.MinValue * 0.1)
                    v_cur = "---";
                else
                    v_cur = (CurrentV*ceff).ToString("0.0");
                DrawMainValues(BackBufferGraphics, speed_cur, (MGridX[0] + MGridX[1]) / 2, MGridY[2] + MHeightDelta * 3 / 4, 18.0f * df);
                DrawMainValues(BackBufferGraphics, v_cur, (MGridX[0] + MGridX[1]) / 2, MGridY[3], 18.0f * df);
            }
            else if (MainConfigSpeedSource == 3 || MainConfigSpeedSource == 4)
            {
                DrawMainValues(BackBufferGraphics, speed_cur, (MGridX[0] + MGridX[1]) / 2, MGridY[2] + MHeightDelta * 3 / 4, 18.0f * df);
                string hr_cur = getHeartRate().ToString();
                if (MainConfigSpeedSource == 4)
                    hr_cur += " " + getHeartSignalStrength().ToString();
                DrawMainValues(BackBufferGraphics, hr_cur, (MGridX[0] + MGridX[1]) / 2, MGridY[3], 18.0f * df);
            }
            else
                DrawMainValues(BackBufferGraphics, speed_cur, (MGridX[0] + MGridX[1]) / 2, MGridY[3], 30.0f * df);

            if ((MainConfigDistance == eConfigDistance.eDistanceTrack2FollowStart || MainConfigDistance == eConfigDistance.eDistanceTrack2FollowEnd || MainConfigDistance == eConfigDistance.eDistance2Destination) && Plot2ndCount != 0)
            {
                double xCurrent = 0, yCurrent = 0, xTrack, yTrack, deltaS;
                // If we have no valid GPS connection, and no current gcc file loaded, the reference point is not 
                // set. But if the reference point is set, we are not allowed to change it.
                if (!utmUtil.referenceSet)
                {
                    utmUtil.setReferencePoint(CurrentLat, CurrentLong);
                    utmUtil.referenceSet = false;      //ReferencePoint only temporarily necessary; better not fix it to an old CurrentLatLong
                }
                else // reference point ist set
                {
                    utmUtil.getXY(CurrentLat, CurrentLong, out xCurrent, out yCurrent);
                }
                if (MainConfigDistance == eConfigDistance.eDistanceTrack2FollowStart)    // show distance to track2follow start
                {
                    utmUtil.getXY(Plot2ndLat[0], Plot2ndLong[0], out xTrack, out yTrack);
                    deltaS = Math.Sqrt((xTrack - xCurrent) * (xTrack - xCurrent) + (yTrack - yCurrent) * (yTrack - yCurrent));  // Calculate the distance between track and current position
                }
                else if (MainConfigDistance == eConfigDistance.eDistanceTrack2FollowEnd) // show Distance to track2follow end
                {
                    utmUtil.getXY(Plot2ndLat[Plot2ndCount - 1], Plot2ndLong[Plot2ndCount - 1], out xTrack, out yTrack);
                    deltaS = Math.Sqrt((xTrack - xCurrent) * (xTrack - xCurrent) + (yTrack - yCurrent) * (yTrack - yCurrent));
                }
                else //if (MainConfigDistance == eConfigDistance.eDistance2Destination)
                    deltaS = mapUtil.nav.Distance2Dest;
                dist = PrintDist(deltaS * GetUnitsConversionCff());
            }
            else if (MainConfigDistance == eConfigDistance.eDistanceOdo)
            { dist = PrintDist(Odo * GetUnitsConversionCff()); }
            DrawMainValues(BackBufferGraphics, dist, (MGridX[0] + MGridX[1]) / 2, MGridY[5], 26.0f * df);
            DrawMainValues(BackBufferGraphics, speed_avg, (MGridX[1] + MGridX[3]) / 2, MGridY[2], 20.0f * df);
            DrawMainValues(BackBufferGraphics, speed_max, (MGridX[1] + MGridX[3]) / 2, MGridY[3], 20.0f * df);

            if (comboLapOptions.SelectedIndex > 0)
            {
                DrawMainValues(BackBufferGraphics, currentLap, (MGridX[1] + MGridX[3]) / 2, MGridY[5], 28.0f * df);
            }
            else
            {
                DrawMainValues(BackBufferGraphics, altitude, (MGridX[1] + MGridX[3]) / 2, MGridY[4], 16.0f * df);
                if (MainConfigAlt2display_ == 4) altitude2 += "  ";     //shift value left
                DrawMainValues(BackBufferGraphics, altitude2, (MGridX[1] + MGridX[3]) / 2, MGridY[5], 16.0f * df);
            }
            

         ///////


            // draw Info cell
            string statusStr;
            if (CurrentLiveLoggingString == "") statusStr = LoadedSettingsName; else statusStr = CurrentLiveLoggingString;
            DrawMainLabelOnRight(BackBufferGraphics, statusStr + " ", MGridX[1], MGridY[6], df_8);
            DrawMainLabelOnRight(BackBufferGraphics, CurrentStatusString + " ", MGridX[1], MGridY[6] + MHeightDelta, df_8);
#if DEBUG
            DrawMainLabelOnLeft(BackBufferGraphics, debugStr, MGridX[0], MGridY[6] + MHeightDelta * 2, df_8);
            //DrawMainLabelOnLeft(BackBufferGraphics, debugStr, MGridX[0], 0 + MHeightDelta * 2, df_8);
#else
            DrawMainLabelOnLeft(BackBufferGraphics, "battery", MGridX[0], MGridY[6] + MHeightDelta * 2, df_8);
            DrawMainLabelOnRight(BackBufferGraphics, battery, MGridX[1], MGridY[6] + MHeightDelta * 2, df_8);
#endif
            DrawMainLabelOnLeft(BackBufferGraphics, "last sample", MGridX[0], MGridY[6] + MHeightDelta * 3, df_8);
            DrawMainLabelOnRight(BackBufferGraphics, last_sample_time, MGridX[1], MGridY[6] + MHeightDelta * 3, df_8);
            DrawMainLabelOnLeft(BackBufferGraphics, "start", MGridX[0], MGridY[6] + MHeightDelta * 4, df_8);
            DrawMainLabelOnRight(BackBufferGraphics, TSummary.StartTime.ToString(), MGridX[1], MGridY[6] + MHeightDelta * 4, df_8);

            // clock
            Utils.DrawClock(BackBufferGraphics, foColor, (MGridX[2] + MGridX[3]) / 2, (MGridY[0] + MGridY[1]) / 2, Math.Min(MGridY[1] - MGridY[0], MGridX[3] - MGridX[2]), 14.0f * df);

            // draw GPS cell
            
            SolidBrush br = new SolidBrush(CurrentGpsLedColor);
            BackBufferGraphics.FillRectangle(br, ((MGridX[1] + MGridX[3]) / 2) - MHeightDelta, MGridY[5] + MGridDelta, MHeightDelta, MHeightDelta);
            
            
            int compass_size = (MGridY[6] + MHeightDelta * 3) - MGridY[5];
            if (MainConfigNav && Plot2ndCount > 0)
            {   //draw navigation arrow and symbol
                Color col = GetLineColor(comboBoxLine2OptColor);
                SolidBrush sb = new SolidBrush(col);
                if (Heading == 720)
                    col = Color.DimGray;
                mapUtil.DrawArrow(BackBufferGraphics, MGridX[3] - compass_size / 2 - MGridDelta, MGridY[5] + compass_size / 2 + MGridDelta, mapUtil.nav.Angle100mAhead - Heading, compass_size / 2, col);
                if (mapUtil.nav.Symbol != null)
                {
                    mapUtil.DrawNavSymbol(BackBufferGraphics, sb, MGridX[1] + 2 * MGridDelta, MGridY[6] + MHeightDelta * 125 / 100, mapUtil.nav.Symbol, mapUtil.nav.orient, mapUtil.nav.SkyDirection, true);
                    BackBufferGraphics.DrawString(mapUtil.nav.strCmd, new Font("Arial", 10 * df, FontStyle.Bold), sb, MGridX[1] + 2 * MGridDelta, MGridY[6] + MHeightDelta * 3);
                }
                DrawMainLabelOnLeft(BackBufferGraphics, mapUtil.nav.strDistance2Dest, MGridX[1] + MGridDelta, MGridY[6] + MHeightDelta * 4, df_8);
            }
            else
            {   // gps info
                if (gps.OpenedOrSuspended)
                {
                    string gps_status1, gps_status2;
                    GetGpsSearchFlags(out gps_status1, out gps_status2);
                    DrawMainLabelOnLeft(BackBufferGraphics, gps_status1, MGridX[1] + MGridDelta, MGridY[6] + MHeightDelta, df_8);
                    DrawMainLabelOnLeft(BackBufferGraphics, gps_status2, MGridX[1] + MGridDelta, MGridY[6] + MHeightDelta * 2, df_8);
                }
                DrawMainLabelOnLeft(BackBufferGraphics, "latitu.", MGridX[1] + MGridDelta, MGridY[6] + MHeightDelta * 3, df_8);
                DrawMainLabelOnRight(BackBufferGraphics, Lat2String(CurrentLat, false), MGridX[3], MGridY[6] + MHeightDelta * 3, df_8);
                DrawMainLabelOnLeft(BackBufferGraphics, "longit.", MGridX[1] + MGridDelta, MGridY[6] + MHeightDelta * 4, df_8);
                DrawMainLabelOnRight(BackBufferGraphics, Lat2String(CurrentLong, true), MGridX[3], MGridY[6] + MHeightDelta * 4, df_8);

                // compass
                if (compass_style == 0)
                {
                    Utils.DrawCompass(BackBufferGraphics, foColor, MGridX[3] - compass_size / 2, MGridY[5] + compass_size / 2, compass_size, Heading, compass_north);
                }
                else
                {
                    string str1, str2;
                    int cvalue, offset = 0;
                    if (compass_north)
                    {
                        str1 = "north";
                        cvalue = -Heading;
                        if (cvalue < 0)
                            cvalue += 360;
                    }
                    else
                    {
                        str1 = "heading";
                        cvalue = Heading;
                    }
                    if (compass_style == 1)         //digital
                    {
                        if (Heading == 720)
                            str2 = "---°";
                        else
                            str2 = cvalue.ToString() + "°";
                    }
                    else        //compass_style == 2    letters
                    {
                        if (Heading == 720)
                            str2 = "---";
                        else
                        {
                            int index = ((cvalue * 8 + 180) / 360) & 0x7;
                            offset = ((cvalue * 8 + 180) % 360 - 180) * compass_size / 360;
                            string[] letter = new string[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                            str2 = letter[index];
                        }
                    }
                    br.Color = foColor;
                    Font f1 = new Font("Arial", df_9, FontStyle.Regular);
                    Font f2 = new Font("Arial", 16.0f * df, FontStyle.Regular);
                    Size sz1 = g.MeasureString(str1, f1).ToSize();
                    BackBufferGraphics.DrawString(str1, f1, br, MGridX[3] - sz1.Width, MGridY[5]);

                    sz1 = g.MeasureString(str2, f2).ToSize();
                    int xpos;
                    if (compass_style == 2)
                    {
                        BackBufferGraphics.DrawLine(p, MGridX[3], MGridY[5] + MHeightDelta + sz1.Height, MGridX[3] - compass_size, MGridY[5] + MHeightDelta + sz1.Height);
                        p.Color = Color.Red;
                        p.Width = compass_size / 16;
                        xpos = MGridX[3] - compass_size / 2 + offset;
                        BackBufferGraphics.DrawLine(p, xpos, MGridY[5] + MHeightDelta, xpos, MGridY[5] + MHeightDelta + sz1.Height);
                        xpos = MGridX[3] - compass_size / 2 - sz1.Width / 2;
                    }
                    else
                        xpos = MGridX[3] - sz1.Width;
                    BackBufferGraphics.DrawString(str2, f2, br, xpos, MGridY[5] + MHeightDelta);
                }
            }

            g.DrawImage(BackBuffer, 0, 0); // draw back buffer on screen
        }
        // end draw main screen ------------------------------------------------





        // paint graph ------------------------------------------------------
        // To have nice flicker-free picture movement, we paint first into a bitmap which is larger
        // than the screen, then just paint the bitmap into the screen with a correct shift.
        // We need to paint on "no background panel", which has blank OnPaintBackground, to avoid flicker
        // The bitmap is updated as screen shift is complete (i.e. on mouse up).

        public int MouseClientX = 0;        //Position in client coordinates from NoBkPanel
        public int MouseClientY = 0;
        public bool MouseMoving = false;
        public bool ClickMoving = false;
        public int MouseShiftX = 0;
        public int MouseShiftY = 0;
        
        public double GetUnitsConversionCff()
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
        public Color GetLineColor(ComboBox cmb)
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
        public int GetLineWidth(ComboBox cmb)
        {
            return ((cmb.SelectedIndex + 1) * 2);
        }
        public string GetUnitsName()
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
            LongShiftUndo = mapUtil.LongShift;
            LatShiftUndo = mapUtil.LatShift;
            ZoomUndo = mapUtil.ZoomValue;
            // reset move/zoom vars
            mapUtil.ZoomValue = 1.0;
            mapUtil.LongShift = 0.0;
            mapUtil.LatShift = 0.0;
            //mapUtil.ScreenShiftSaveX = 0;
            //mapUtil.ScreenShiftSaveY = 0;
            //MousePosX = 0;
            //MousePosY = 0;
            MouseMoving = false;
            mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FOff;     // Do not display Track to Follow start/end
            clickLatLon = null;
        }

        private void tabGraph_Paint(object sender, PaintEventArgs e)        // Update Screen Main view, Map view, or Graph
        {
            //int begin = Environment.TickCount;
            if ((BufferDrawMode == BufferDrawModeMaps && (mapUtil.autoHideButtons || mapUtil.hideAllInfos))
                || (BufferDrawMode == BufferDrawModeGraph && graph.autoHideButtons))
                NoBkPanel.Size = this.Size;
            else
                NoBkPanel.Size = tabControl.Size;

            PrepareBackBuffer();

            if (BufferDrawMode == BufferDrawModeMain)
            {
                DrawMain(e.Graphics);
            }
            else if (BufferDrawMode == BufferDrawModeMaps)
            {
                // plotting in Long (as X) / Lat (as Y) coordinates
                //int begin = Environment.TickCount;
                mapUtil.DrawMaps(e.Graphics, BackBuffer, BackBufferGraphics, utmUtil, MouseMoving,
                                 gps.OpenedOrSuspended, comboMultiMaps.SelectedIndex, GetUnitsConversionCff(), GetUnitsName(),
                                 PlotLong, PlotLat, PlotCount, GetLineColor(comboBoxKmlOptColor), GetLineWidth(comboBoxKmlOptWidth),
                                 checkPlotTrackAsDots.Checked, WayPointsT, WayPointsT2F, showWayPoints,
                                 Plot2ndLong, Plot2ndLat, Plot2ndCount, GetLineColor(comboBoxLine2OptColor), GetLineWidth(comboBoxLine2OptWidth),
                                 checkPlotLine2AsDots.Checked,
                                 CurrentLong, CurrentLat, Heading, clickLatLon, mapLabelColor);
                //System.Diagnostics.Debug.WriteLine((Environment.TickCount - begin).ToString());
            }
            else if (BufferDrawMode == BufferDrawModeGraph)
            {
                graph.DrawGraph(e.Graphics, BackBuffer, BackBufferGraphics);
            }
            else if (BufferDrawMode == BufferDrawModeNavigate)
            {
                mapUtil.DrawNavigate(e.Graphics, BackBuffer, BackBufferGraphics, Plot2ndLong, Plot2ndLat, Plot2ndCount, (float)CurrentLong, (float)CurrentLat, Heading, GetLineColor(comboBoxLine2OptColor));
            }
            //e.Graphics.DrawString((Environment.TickCount - begin).ToString(), new Font("Arial", 14, FontStyle.Bold), new SolidBrush(Color.Blue), 10, 500);
        }

        private void tabGraph_MouseDown(object sender, MouseEventArgs e)
        {
            Form1_MouseDownCE(sender, e);
            
            MouseMoving = false;
            //MouseClientX = e.X;   moved to Form1_MouseDownCE
            //MouseClientY = e.Y;
            //ClientMouseX = e.X;
            //ClientMouseY = e.Y - this.Top;
            MouseShiftX = 0;
            MouseShiftY = 0;

            if (BufferDrawMode == BufferDrawModeMaps)
            {
                mapUtil.LongShiftSave = mapUtil.LongShift;
                mapUtil.LatShiftSave = mapUtil.LatShift;
                mapUtil.Lat2PixelSave = mapUtil.Lat2Pixel;
            }
            else if (BufferDrawMode == BufferDrawModeGraph)
            {
                if (e.X < NoBkPanel.Width / 14)
                { graph.mousePos = Graph.MousePos.left; }
                //else if(e.X > NoBkPanel.Width - NoBkPanel.Width / 14)
                //{ graph.mousePos = Graph.MousePos.right; }
                else if(e.Y < NoBkPanel.Height / 10)
                { graph.mousePos = Graph.MousePos.top; }
                else if(e.Y > NoBkPanel.Height - NoBkPanel.Height / 10)
                { graph.mousePos = Graph.MousePos.bottom; }
                else
                { graph.mousePos = Graph.MousePos.middle; }

                graph.moveState = Graph.MoveState.MoveStart;
                graph.SaveScale();
            }
            else if (BufferDrawMode == BufferDrawModeMain)
            {
                if(comboLapOptions.SelectedIndex == 2)
                    lapManualClick = true;
            }
            //Debug.WriteLine("Down " + MouseClientX + " " + MouseClientY + " " + graph.mousePos.ToString());
        }
        private void tabGraph_MouseUp(object sender, MouseEventArgs e)
        {
            Form1_MouseUpCE(sender, e);
            if (BufferDrawMode == BufferDrawModeMaps)
            {
                if (MouseMoving)        //prevent shift when canceling menue
                {
                    mapUtil.ShiftMap(e.X - MouseClientX, e.Y - MouseClientY);
                }
                //mapUtil.ScreenShiftSaveX = mapUtil.ScreenShiftX;
                //mapUtil.ScreenShiftSaveY = mapUtil.ScreenShiftY;
                if (Math.Abs(e.X - MouseClientX) + Math.Abs(e.Y - MouseClientY) < 10)   //don't execute if mouse moved, but allow small difference since many devices have slightly different coords in down and up
                {                                                                       //remark: Click() also triggers on move (and has no coordinates)
                    clickLatLon = Lat2String(mapUtil.ToDataYexact(MouseClientY), false);
                    clickLatLon += " ";
                    clickLatLon += Lat2String(mapUtil.ToDataX(MouseClientX), true);
                    if (trackEditMode == TrackEditMode.T2f)
                    {
                        AddThisMousePointToT2F(MouseClientX, MouseClientY);
                        if (Plot2ndCount > 0)
                            showButton(button1, MenuPage.BFkt.waypoint2);
                    }
                    else
                        buttonHideToggle();
                }
            }
            else if (BufferDrawMode == BufferDrawModeGraph)
            {
                MouseShiftX = e.X - MouseClientX;
                MouseShiftY = e.Y - MouseClientY;
                if (graph.moveState == Graph.MoveState.MoveStart)
                    if (Math.Abs(MouseShiftX) + Math.Abs(MouseShiftY) > 10)
                        graph.moveState = Graph.MoveState.MoveEnd;
                    else
                        graph.moveState = Graph.MoveState.Nothing;
                else if (graph.moveState == Graph.MoveState.Move)
                    graph.moveState = Graph.MoveState.MoveEnd;
                if (graph.moveState == Graph.MoveState.Nothing)
                    buttonHideToggle();
            }
            MouseMoving = false;
            //Debug.WriteLine("Up " + MouseShiftX);
            NoBkPanel.Invalidate();
        }
        private void tabGraph_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.None)
            {
                if (BufferDrawMode == BufferDrawModeMaps)
                {
                    mapUtil.ShiftMap(e.X - MouseClientX, e.Y - MouseClientY);
                }
                else if (BufferDrawMode == BufferDrawModeGraph)
                {
                    MouseShiftX = e.X - MouseClientX;
                    MouseShiftY = e.Y - MouseClientY;
                    graph.moveState = Graph.MoveState.Move;
                }
                MouseMoving = true;   //only shift image in backbuffer
                //Debug.WriteLine("Move " + MouseShiftX);
                NoBkPanel.Invalidate();
            }
            //else { MouseMoving = false; }
        }
        private void tabGraph_MouseClick(object sender, EventArgs e)
        {
            /*
            double x=1;
            debugBegin = Environment.TickCount;
            for (int i = 1; i < 10001; i++)
                x=Math.Atan2(i, x);
            Debug.WriteLine("atan " + (Environment.TickCount - debugBegin).ToString());

            debugBegin = Environment.TickCount;
            for (int i = 0; i < 10000; i++)
                x=Math.Min(x,i);
            Debug.WriteLine("min " + (Environment.TickCount - debugBegin).ToString());

            debugBegin = Environment.TickCount;
            for (int i = 0; i < 10000; i++)
                x= i / x;
            Debug.WriteLine("div " + (Environment.TickCount - debugBegin).ToString());

            debugBegin = Environment.TickCount;
            for (int i = 0; i < 10000; i++)
                x = i * x;
            Debug.WriteLine("mult " + (Environment.TickCount - debugBegin).ToString());
            */


            ClickMoving = MouseMoving;
            if (!Utils.IsSystemPowerStateOn())
            {
                if (MouseClientX > NoBkPanel.Width / 2)
                    MenuExec(MenuPage.BFkt.map);
                Utils.SwitchBacklight(true);
            }

            else if (BufferDrawMode == BufferDrawModeMaps)
            {
                if (Plot2ndCount > 0 && mapUtil.showNav && !mapUtil.show_nav_button && gps.OpenedOrSuspended &&
                    MouseClientX > NoBkPanel.Width * 13 / 20 && MouseClientY < NoBkPanel.Height * 7 / 20)
                    MenuExec(MenuPage.BFkt.navigate);
                else
                {
                    mapUtil.ClearMapError();
#if testClickCurrentPos
                    double oldLat = CurrentLat;
                    double oldLong = CurrentLong;
                    CurrentLat = mapUtil.ToDataY(MouseClientY);
                    CurrentLong = mapUtil.ToDataX(MouseClientX);
                    if (!utmUtil.referenceSet)
                        utmUtil.setReferencePoint(CurrentLat, CurrentLong);
                    Heading = (int)(180 / Math.PI * Math.Atan2((CurrentLong - oldLong) * utmUtil.longit2meter, (CurrentLat - oldLat) * utmUtil.lat2meter));
                    if (Heading < 0)
                        Heading += 360;
#endif
                }
            }
            else if (BufferDrawMode == BufferDrawModeMain)
            {
                if (MouseClientX > MGridX[1] && MouseClientY >= MGridY[4] && MouseClientY < MGridY[5])
                {
                    if (MainConfigAlt2display >= 256)
                        MainConfigAlt2display -= 128;
                    else if (MainConfigAlt2display >= 128)
                        MainConfigAlt2display += 128;
                }

                if (checkMainConfigSingleTap.Checked)
                {
                    tabGraph_MainConfig();
                }

            }
            else if (BufferDrawMode == BufferDrawModeGraph)
                graph.scaleCmd = Graph.ScaleCmd.DoRedraw;
            else if (BufferDrawMode == BufferDrawModeNavigate)
            {
                mapUtil.nav.voicePlayed_toRoute = false;
                mapUtil.nav.voicePlayed_dest = false;
                mapUtil.corner.voicePlayed = false;
            }
            //Debug.WriteLine("Click");
        }

        private void tabGraph_MouseDoubleClick(object sender, EventArgs e)
        {
            if (MouseMoving || ClickMoving)
                return;
            if (BufferDrawMode == BufferDrawModeMaps)
            {
                if (trackEditMode == TrackEditMode.Off)
                    ResetMapPosition();
            }
            else if (BufferDrawMode == BufferDrawModeGraph)
            {
                if (graph.mousePos == Graph.MousePos.bottom)
                    graph.SetSource(Graph.SourceY.Old, (1 - graph.sourceX));
                else if (graph.mousePos == Graph.MousePos.top)
                    graph.DrawSourceNext();
                else if (graph.mousePos == Graph.MousePos.middle)
                    graph.scaleCmd = Graph.ScaleCmd.DoAutoscale;
            }
            else if (BufferDrawMode == BufferDrawModeMain)
            {
                if (!checkMainConfigSingleTap.Checked)
                tabGraph_MainConfig();
            }
            else { return; }
            //Debug.WriteLine("DoubleClick");
            NoBkPanel.Invalidate();
        }
        private void tabGraph_MainConfig()
        {
            if (MouseClientX < MGridX[2] && MouseClientY < MGridY[1])   //Time
            {
                checkExStopTime.Checked = !checkExStopTime.Checked;
            }
            else if (MouseClientX > MGridX[2] && MouseClientY < MGridY[1])  //Clock
            {
                dayScheme = !dayScheme;
                ApplyCustomBackground();
            }
            else if (MouseClientX < MGridX[1])
            {
                if (MouseClientY < MGridY[3])   //Speed
                {
                    if (oHeartBeat != null)
                        if (MainConfigSpeedSource < 3)
                            MainConfigSpeedSource = 3;
                        else
                            MainConfigSpeedSource = 0;
                }
                else if (MouseClientY < MGridY[5])  //Distance
                {
                    if (MainConfigDistance == eConfigDistance.eDistanceTrip)
                        MainConfigDistance = eConfigDistance.eDistance2Destination;
                    else if (MainConfigDistance == eConfigDistance.eDistance2Destination)
                        MainConfigDistance = eConfigDistance.eDistanceOdo;
                    else
                        MainConfigDistance = eConfigDistance.eDistanceTrip;

                    if (Plot2ndCount == 0 && MainConfigDistance == eConfigDistance.eDistance2Destination)
                        MainConfigDistance = eConfigDistance.eDistanceOdo;
                }
                else                            //Info
                {
                }
            }
            else
            {
                if (MouseClientY < MGridY[3])
                {
                }
                else if (MouseClientY < MGridY[4])  //Altitude
                {
                    MainConfigRelativeAlt = !MainConfigRelativeAlt;
                }
                else if (MouseClientY < MGridY[5])  //Alt2
                {
                    if (MainConfigAlt2display <= 4)
                        if (++MainConfigAlt2display > 4)
                            MainConfigAlt2display = 0;
                }
                else                                //GPS
                {
                    if (Plot2ndCount > 0)
                        MainConfigNav = !MainConfigNav;
                }
            }
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
            int h = this.ClientSize.Height;
            int w = this.ClientSize.Width;
            if (isLandscape)  //Screen.PrimaryScreen.Bounds.Height < Screen.PrimaryScreen.Bounds.Width)
            {
                sc_y = (double)h / 508.0;
                sc_x = (double)w / 640.0;
            }
            else
            {
                sc_y = (double)h / 588.0;
                sc_x = (double)w / 480.0;
            }
        }
        private void tabAbout_Paint(object sender, PaintEventArgs e)
        {
            double sc_x, sc_y;
            getScaleXScaleY(out sc_x, out sc_y);
            if (sc_x < sc_y) sc_y = sc_x;

            Rectangle src_rec = new Rectangle(0, 0, AboutTabImage.Width, AboutTabImage.Height);
            Rectangle dest_rec = new Rectangle((labelRevision.Width - (int)(AboutTabImage.Width * sc_y))/2, 0, (int)(AboutTabImage.Width * sc_y), (int)(AboutTabImage.Height * sc_y));

            e.Graphics.DrawImage(AboutTabImage, dest_rec, src_rec, GraphicsUnit.Pixel);
        }
        //private void tabBlank_Paint(object sender, PaintEventArgs e)
        //{
        //    double sc_x, sc_y;
        //    getScaleXScaleY(out sc_x, out sc_y);

        //    Rectangle src_rec = new Rectangle(0, 0, BlankImage.Width, BlankImage.Height);
        //    Rectangle dest_rec = new Rectangle(0, 0, (int)(BlankImage.Width * sc_x), (int)(BlankImage.Height * sc_y));

        //    e.Graphics.DrawImage(BlankImage, dest_rec, src_rec, GraphicsUnit.Pixel);
        //}
        private void panelCwLogo_Paint(object sender, PaintEventArgs e)
        {
            double sc_x, sc_y;
            getScaleXScaleY(out sc_x, out sc_y);
            if (sc_x < sc_y) sc_y = sc_x;

            Rectangle dest_rec = new Rectangle(0, 0, (int)(CWImage.Width * sc_y), (int)(CWImage.Height * sc_y));

            ImageAttributes im_attr = new ImageAttributes();
            im_attr.SetColorKey(CWImage.GetPixel(0, 0), CWImage.GetPixel(0, 0));
 
            e.Graphics.DrawImage(CWImage, dest_rec, 0, 0, CWImage.Width, CWImage.Height, GraphicsUnit.Pixel, im_attr);
        }

        private void buttonMain_Click(bool showExit)
        {
            BufferDrawMode = BufferDrawModeMain; 
            NoBkPanel.BringToFront(); 
            NoBkPanel.Invalidate();

            if(state == State.paused || state == State.pauseHrOnly)
                mPage.mBAr[(int)MenuPage.BFkt.pause] = mPage.mBPauseMode;
            else
                mPage.mBAr[(int)MenuPage.BFkt.pause] = mPage.mBPause;

            if (gps.OpenedOrSuspended) // if GPS is running
                mPage.mBAr[(int)MenuPage.BFkt.gps_toggle] = mPage.mBGpsOn;
            else
                mPage.mBAr[(int)MenuPage.BFkt.gps_toggle] = mPage.mBGpsOff;

            if (gps.OpenedOrSuspended)
                showButton(button1, MenuPage.BFkt.map);
            else
                showButton(button1, MenuPage.BFkt.mPage);

            if (state == State.logging || state == State.paused || state == State.pauseHrOnly)
                showButton(button2, MenuPage.BFkt.pause);
            else if (state == State.logHrOnly)
                showButton(button2, MenuPage.BFkt.graph_heartRate);
            else
                showButton(button2, MenuPage.BFkt.start);

            if (showExit)
                showButton(button3, MenuPage.BFkt.exit);
            else if (state == State.logging || state == State.logHrOnly)
                showButton(button3, MenuPage.BFkt.stop);
            else
                showButton(button3, MenuPage.BFkt.gps_toggle);
        }

        private void buttonMap_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeMaps;
            listBoxFiles.Focus();  // need to loose control from any combo/edit boxes, to avoid pop-up on "OK" button press
            
            if (Plot2ndCount > 0 && trackEditMode == TrackEditMode.T2f)
                showButton(button1, MenuPage.BFkt.waypoint2);
            else if (Plot2ndCount > 0 && mapUtil.show_nav_button && gps.OpenedOrSuspended)
                showButton(button1, MenuPage.BFkt.navigate);
            else
                showButton(button1, MenuPage.BFkt.main);
            showButton(button2, MenuPage.BFkt.map_zoomIn);
            showButton(button3, MenuPage.BFkt.map_zoomOut);

            NoBkPanel.BringToFront();
            NoBkPanel.Invalidate();
            timerButtonHide.Enabled = false;
            if (trackEditMode == TrackEditMode.T2f)
            {
                button1.BringToFront();
                button2.BringToFront();
                button3.BringToFront();
            }
        }

        
        private void buttonOptions_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeOptions;
            LoadedSettingsName = "";
            initializeOptions();
            tabControl.BringToFront();
            //labelFileName.SetText(""); // clear text from info label
            ShowHideViewOpt(false); // make sure the view selector is hidden

            showButton(button1, MenuPage.BFkt.main);
            showButton(button2, MenuPage.BFkt.options_prev);
            showButton(button3, MenuPage.BFkt.options_next);
        }

        void buttonColorDlg_Click(object sender, EventArgs e)
        {
            Color foCol = foColor;
            Color bkCol = bkColor;
            ColorDlg cd = new ColorDlg();
            cd.Text = "Fore color";
            cd.CurrentColor = foColor;
            if (DialogResult.OK == cd.ShowDialog())
            {
                foCol = cd.CurrentColor;
            }
            cd.Text = "Back color";
            cd.CurrentColor = bkColor;
            if (DialogResult.OK == cd.ShowDialog())
            {
                bkCol = cd.CurrentColor;
            }
            if (Math.Abs(foCol.R - bkCol.R) + Math.Abs(foCol.G - bkCol.G) + Math.Abs(foCol.B - bkCol.B) < 75)
            {
                MessageBox.Show("Fore and back color are too close together.\nIgnoring setting", "Warning");
            }
            else
            {
                if (dayScheme)
                {
                    foColor_day = foCol;
                    bkColor_day = bkCol;
                }
                else
                {
                    foColor_night = foCol;
                    bkColor_night = bkCol;
                }
                ApplyCustomBackground();
            }
            cd.Text = "Map label color";
            cd.CurrentColor = mapLabelColor;
            if (DialogResult.OK == cd.ShowDialog())
            {
                mapLabelColor = cd.CurrentColor;
            }
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


        // Open dialog buttons (used to open file or setup folder)
        private void buttonDialogOpen_Click(object sender, EventArgs e)
        {
            //tabBlank.BringToFront();

            if (FolderSetupMode) { buttonDialogOpen_FolderMode(sender, e); }
            else
            {
                if (FileOpenMode == FileOpenMode_Gcc) { buttonDialogOpen_FileModeGcc(); }
                else                                  { buttonDialogOpen_FileModeTrack2Follow(); }
            }
        }
        private void buttonDialogOpen_FolderMode(object sender, EventArgs e)
        {
            string label_info_text = "Info: ";
            bool retry = false;

            if (folderMode == FolderMode.Maps)
            {
                MapsFilesDirectory = "";
                for (int i = 0; i <= CurrentSubDirIndex; i++)
                {
                    if (i != 0) { MapsFilesDirectory += "\\" + listBoxFiles.Items[i].ToString().Trim(); }
                }
                if (CurrentSubDirIndex == 0) { MapsFilesDirectory = "\\"; }
                label_info_text += "Map set to: " + MapsFilesDirectory;

                CheckMapsDirectoryExists();
                mapUtil.LoadMaps(MapsFilesDirectory);
                if (PlotCount > 0) { ResetMapPosition(); }
            }
            else if (folderMode == FolderMode.Io)
            {
                IoFilesDirectory = "";
                for (int i = 0; i <= CurrentSubDirIndex; i++)
                {
                    if (i != 0) { IoFilesDirectory += "\\" + listBoxFiles.Items[i].ToString().Trim(); }
                }
                if (CurrentSubDirIndex == 0) { IoFilesDirectory = "\\"; }
                label_info_text += "IO set to: " + IoFilesDirectory;
            }
            else    //folderMode == FolderMode.Lang
            {
                string ldir = "";
                for (int i = 0; i <= CurrentSubDirIndex; i++)
                {
                    if (i != 0) { ldir += "\\" + listBoxFiles.Items[i].ToString().Trim(); }
                }
                if (CurrentSubDirIndex == 0) { ldir = "\\"; }
                if (!File.Exists(ldir + "\\Ten.wav"))
                {
                    if (MessageBox.Show("required .wav files not present - choose e.g. directory 'eng'", "Error",
                                            MessageBoxButtons.RetryCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1)
                                            == DialogResult.Retry)
                        retry = true;
                }
                else
                {
                    LanguageDirectory = ldir;
                    label_info_text += "Language set to: " + LanguageDirectory;
                }
            }
            if (!retry)
            {
                tabOpenFile.SendToBack();

                buttonOptions_Click(sender, e); // show options page and display currently set file
                labelInfo.SetText(label_info_text);

                // need to loose focus from list box - otherwise map do not get MouseMove!???
                listBoxFiles.Items.Clear();
                listBoxFiles.Focus();
            }
        }
        private void buttonDialogOpen_FileModeGcc()
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
                    if (DialogResult.OK == ShowTrackSummary(TSummary))
                    {
                        // need to loose focus from list box - otherwise map do not get MouseMove!???
                        listBoxFiles.Items.Clear();
                        listBoxFiles.Focus();
                        tabBlank.BringToFront();
                        MenuExec(MenuPage.BFkt.map);
                    }
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
                ///
                if (file_found)
                {
                    if (LoadT2f(file_name, true, t2fAppendMode))
                    {
                        SaveState();
                        // need to loose focus from list box - otherwise map do not get MouseMove!???
                        listBoxFiles.Items.Clear();
                        listBoxFiles.Focus();
                        tabBlank.BringToFront();
                        MenuExec(MenuPage.BFkt.map);
                    }
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
            tabBlank.BringToFront();
            tabOpenFile.SendToBack();

            // need to loose focus from list box - otherwise map do not get MouseMove!???
            listBoxFiles.Items.Clear();
            listBoxFiles.Focus();

            //labelFileName.SetText("");

            // in folder setup mode - stay at options
            if (FolderSetupMode)
                MenuExec(MenuPage.BFkt.options);
            else // in all other cases stay at menu screen
                MenuExec(MenuPage.BFkt.mPage);
        }

        // setting io files and maps locations
        private void buttonFileLocation_Click(object sender, EventArgs e)
        {
            folderMode = FolderMode.Io;
            FillFileLocationListBox();
        }
        private void buttonMapsLocation_Click(object sender, EventArgs e)
        {
            folderMode = FolderMode.Maps;
            FillFileLocationListBox();
        }
        private void buttonLangLocation_Click(object sender, EventArgs e)
        {
            folderMode = FolderMode.Lang;
            FillFileLocationListBox();
        }
        private void FillFileLocationListBox()
        {
            // to avoid getting "listBoxFiles_SelectedIndexChanged" called as SelectedIndex changes
            FolderSetupMode = false;
            listBoxFiles.Items.Clear();

            //tabBlank1.BringToFront();
            tabOpenFile.BringToFront();
            showButton(button1, MenuPage.BFkt.dialog_cancel);
            showButton(button2, MenuPage.BFkt.dialog_open);
            showButton(button3, MenuPage.BFkt.nothing);
            tabBlank.BringToFront();

            listBoxFiles.BringToFront();

            // select IO or Maps folder to setup
            string dir_to_setup = "";
            if (folderMode == FolderMode.Maps)
            {
                CheckMapsDirectoryExists();
                dir_to_setup = MapsFilesDirectory;
            }
            else if (folderMode == FolderMode.Io)
            {
                CheckIoDirectoryExists();
                dir_to_setup = IoFilesDirectory;
            }
            else    //folderMode == FolderMode.Lang
            {
                CheckLanguageDirectoryExists();
                dir_to_setup = LanguageDirectory;
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
            try
            {
                // add list sub-folders there
                string[] subdirectoryEntries = Directory.GetDirectories(new_folder_name);
                Array.Sort(subdirectoryEntries);

                foreach (string s in subdirectoryEntries)
                {
                    listBoxFiles.Items.Add(indent + Path.GetFileName(s));
                }
            }
            catch(Exception ex)
            {
                if (MessageBox.Show("Continue anyway?", "Exception", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1) == DialogResult.No)
                    throw (ex);
            }
            listBoxFiles.SelectedIndex = CurrentSubDirIndex;
            FolderSetupMode = true;
        }

        // controls on Options2 tab (live logging)
        private void buttonCWShowKeyboard_Click(object sender, EventArgs e)
        {
            inputPanel.Enabled = !inputPanel.Enabled;
        }
        private void CWShowKeyboard(object sender, EventArgs e)
        {
            inputPanel.Enabled = true;
        }
        private void CWHideKeyboard(object sender, EventArgs e)
        {
            inputPanel.Enabled = false;
        }
        private void buttonCWVerify_Click(object sender, EventArgs e)
        {
            if (LockCwVerify) { return; }
            if (string.IsNullOrEmpty(textBoxCw1.Text) || string.IsNullOrEmpty(textBoxCw2.Text))
            {
                MessageBox.Show("Please enter username and pasword!");
                return;
            }
            if (textBoxCw2.Text != "******")
            {
                // set hash password
                CwHashPassword = CWUtils.HashPassword(textBoxCw2.Text);
                textBoxCw2.Text = "******";
            }

            Cursor.Current = Cursors.WaitCursor;
            LockCwVerify = true;

            labelCwInfo.Text = ""; labelCwInfo.Refresh();
            labelCwInfo.Text = CWUtils.VerifyCredentialsOnCrossingwaysViaHTTP(textBoxCwUrl.Text, textBoxCw1.Text, CwHashPassword);

            Cursor.Current = Cursors.Default;
            LockCwVerify = false;
        }
        private void DoLiveLogging()
        {
            if (comboBoxCwLogMode.SelectedIndex == 0)  // live logging disabled
                { CurrentLiveLoggingString = ""; return; } 

            if (TSummary.StartTime == DateTime.MinValue) { return; }      // safety checks
            if (position == null) { return; }

            // check if this is a time to log again
            TimeSpan maxAge = new TimeSpan(0, LiveLoggingTimeMin[comboBoxCwLogMode.SelectedIndex], 0);
            if ((LastLiveLogging + maxAge) >= DateTime.UtcNow) { return; }
            LastLiveLogging = DateTime.UtcNow;

            // proceed with live logging
            CWUtils.UpdatePositionOnCrossingwaysViaHTTP(textBoxCwUrl.Text, textBoxCw1.Text, CwHashPassword,
                CurrentLat, CurrentLong, (CurrentAlt == Int16.MinValue) ? 0.0 : CurrentAlt, (Heading == 720)? 0 : Heading, "GpsCC", this);

            // CurrentLiveLoggingString is set async through UpdatePositionOnCrossingwaysViaHTTP
        }

        private void DoLapStats()
        {
            int lapInterval = 0;
            string lapData = "";
            switch (comboLapOptions.SelectedIndex)
            {
                case 0:     //off
                    return;
                case 1:     // auto
                    return;
                case 2:     // manual
                    if (TSummary.Distance < LapStartD + 50)
                    {
                        lapManualClick = false;
                        return;                                         //keep last value
                    }
                    if (lapManualDistance != 0)
                    {
                        int currentManualSec = (int)((CurrentTimeSec - LapStartT) * lapManualDistance / (TSummary.Distance - LapStartD));
                        currentLap = (currentManualSec / 60) + ":" + (currentManualSec % 60).ToString("00");
                    }
                    if (lapManualClick)
                    {
                        lastLap = ((CurrentTimeSec - LapStartT) / 60) + ":" + ((CurrentTimeSec - LapStartT) % 60).ToString("00");
                        lapData = "Lap " + ++LapNumber + " - " + (TSummary.Distance / 1000).ToString("0.00") + " km --- " + lastLap + " min:s\r\n";
                        lapManualDistance = (int)(TSummary.Distance - LapStartD);
                        lapManualClick = false;
                        goto final2;
                    }
                    return;

                case 3:     // distance based: 1km
                    lapInterval = 400;
                    goto distanceBased;
                case 4:     // distance based: 1km
                    lapInterval = 1000;
                        goto distanceBased;
                case 5:     // distance based: 2km
                    lapInterval = 2000;
                        goto distanceBased;
                case 6:     // distance based: 5km
                    lapInterval = 5000;
                        goto distanceBased;

                case 7:     // time based: 1min
                    lapInterval = 60;
                        goto timeBased;
                case 8:     // time based: 2min
                    lapInterval = 120;
                        goto timeBased;
                case 9:     // time based: 5min
                    lapInterval = 300;
                        goto timeBased;
                default:
                    return;
            }

        distanceBased:
            if (TSummary.Distance < LapStartD + lapInterval / 20)
                return;                                         //keep last value
            int currentLapSec = (int)((CurrentTimeSec - LapStartT) * lapInterval / (TSummary.Distance - LapStartD));
            currentLap = (currentLapSec / 60) + ":" + (currentLapSec % 60).ToString("00");
            if (TSummary.Distance < LapStartD + lapInterval)
                return;
            lastLap = ((CurrentTimeSec - LapStartT) / 60) + ":" + ((CurrentTimeSec - LapStartT) % 60).ToString("00");
            lapData = "Lap " + ++LapNumber + " - " + (TSummary.Distance / 1000).ToString("0.0") + " km --- " + lastLap + " min:s\r\n";
            goto final;

        timeBased:
            if (CurrentTimeSec < LapStartT + lapInterval / 20)
                return;                                         //keep last value
            currentLap = ((TSummary.Distance - LapStartD) * lapInterval / (CurrentTimeSec - LapStartT) / 1000).ToString("0.00");
            if (CurrentTimeSec < LapStartT + lapInterval)
                return;
            lastLap = ((TSummary.Distance - LapStartD) / 1000).ToString("0.00");
            lapData = "Lap " + ++LapNumber + " - " + CurrentTimeSec / 60 + " min --- " + lastLap + " km\r\n";

        final:
            if (state == State.logging)
                WriteCheckPoint(lapData);
        final2:
            if (checkLapBeep.Checked)
                Utils.MessageBeep(Utils.BeepType.SimpleBeep);
            //textLapOptions.SuspendLayout();
            textLapOptions.Text += lapData;
            textLapOptions.Select(textLapOptions.Text.Length, 0);       //cursor to the end
            textLapOptions.ScrollToCaret();
            //textLapOptions.ResumeLayout();
            LapStartD = TSummary.Distance;
            LapStartT = CurrentTimeSec;
        }

        private void buttonLapExport_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            String csv_data = textLapOptions.Text;
            csv_data = csv_data.Replace(' ', ';');

            CheckIoDirectoryExists();
            String csv_file = IoFilesDirectory;
            if (csv_file == "\\") { csv_file = ""; }
            csv_file += "\\" + Path.GetFileNameWithoutExtension(TSummary.filename) + ".csv";

            FileStream fs = null;
            StreamWriter wr = null;
            try
            {
                fs = new FileStream(csv_file, FileMode.Create);
                wr = new StreamWriter(fs);

                wr.Write(csv_data);
            }
            catch (Exception ee)
            {
                Utils.log.Error(" buttonLapExport_Click ", ee);
            }
            finally
            {
                if (wr != null) wr.Close();
                if (fs != null) fs.Close();
            }
            Cursor.Current = Cursors.Default;
        }



        private void Form1_Resize(object sender, EventArgs e)
        {
            if (LockResize) { return; }
            DoOrientationSwitch();
        }

        private void DoOrientationSwitch()
        {
            if (this.ClientSize.Height < this.ClientSize.Width)   // landscape mode
            {
                isLandscape = true;
                buttonWidth = this.ClientSize.Width / 4;
                buttonHeight = this.ClientSize.Height / 6;
                // move buttons to new location
                int left = this.ClientSize.Width - buttonWidth;
                button1.Left = left; button2.Left = left; button3.Left = left;
                button4.Left = left; button5.Left = left; button6.Left = left;
                button1.Top = 0; button2.Top = buttonHeight; button3.Top = buttonHeight * 2;
                button4.Top = buttonHeight * 5; button5.Top = buttonHeight * 4; button6.Top = buttonHeight * 3;      //reverse order for better grouping
                tabBlank.Bounds = new Rectangle(left, buttonHeight * 3, buttonWidth, buttonHeight * 3);
                scx_p = this.ClientSize.Width - buttonWidth;   //scx_q = workX_last;
                scy_p = this.ClientSize.Height;                //scy_q = workY_last;
                //df = 0.84f;       //df is set in PrepareBackBuffer()
            }
            else    // portrait
            {
                isLandscape = false;
                buttonWidth = this.ClientSize.Width / 3;
                buttonHeight = buttonWidth / 2;
                // move buttons to new location
                int X1 = buttonWidth;
                int X2 = X1 * 2;
                int Y1 = this.ClientSize.Height - buttonHeight;
                int Y2 = Y1 - buttonHeight;
                button1.Left = 0; button2.Left = X1; button3.Left = X2;
                button4.Left = 0; button5.Left = X1; button6.Left = X2;
                button1.Top = Y1; button2.Top = Y1; button3.Top = Y1;
                button4.Top = Y2; button5.Top = Y2; button6.Top = Y2;
                tabBlank.Bounds = new Rectangle(0, Y2, this.ClientSize.Width, buttonHeight);
                scx_p = this.ClientSize.Width;                     //scx_q = workX_last;
                scy_p = this.ClientSize.Height - buttonHeight;   //scy_q = workY_last;
            }
            if (scx_p != scx_q || scy_p != scy_q)
            {
                ScaleToCurrentResolution();
                scx_q = scx_p;      //scx_q is last value
                scy_q = scy_p;
            }
            //graph.scaleCmd = Graph.ScaleCmd.DoAutoscale;
        }

        // load second line - track to follow
        private void buttonLoadTrack2Follow_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeFiledialog;
            FolderSetupMode = false;
            FileOpenMode = FileOpenMode_2ndGcc;
            FillFileNames();

            listBoxFiles.BringToFront();
            tabOpenFile.BringToFront();

            showButton(button1, MenuPage.BFkt.dialog_cancel);
            showButton(button2, MenuPage.BFkt.dialog_open);
            showButton(button3, MenuPage.BFkt.dialog_down);
            showButton(button4, MenuPage.BFkt.dialog_prevFileType);
            showButton(button5, MenuPage.BFkt.dialog_nextFileType);
            showButton(button6, MenuPage.BFkt.dialog_up);
            //tabBlank1.SendToBack();

            if (listBoxFiles.Items.Count > 0)
            { listBoxFiles.SelectedIndex = 0; }
        }
        private void buttonPrevFileType_Click(object sender, EventArgs e)
        {
            FileExtentionToOpen--;
            if (FileExtentionToOpen < 1) { FileExtentionToOpen = 3; } // set to FileOpenMode_2ndGpx = 3
            buttonLoadTrack2Follow_Click(null, EventArgs.Empty);
        }
        private void buttonNextFileType_Click(object sender, EventArgs e)
        {
            FileExtentionToOpen++;
            if (FileExtentionToOpen > 3) { FileExtentionToOpen = 1; } // set to FileOpenMode_2ndGcc = 1
            buttonLoadTrack2Follow_Click(null, EventArgs.Empty);
        }
        private void clearHR()
        {
            for (int i = 0; i < PlotDataSize; i++)
                PlotH[i] = 0;
            mPage.mBAr[(int)MenuPage.BFkt.graph_heartRate].enabled = false;
        }
        private void buttonClearTrack_Click()
        {
            if (state == State.logging || state == State.paused || state == State.logHrOnly || state == State.pauseHrOnly)
                { MessageBox.Show("Cannot clear track while logging"); }
            else 
            {
                PlotCount = 0;
                CurrentPlotIndex = -1;
                clearHR();
                Decimation = 1; DecimateCount = 0;
                StartTimeUtc = DateTime.MinValue;
                LastBatterySave = StartTimeUtc; LastLiveLogging = StartTimeUtc;
                CurrentTimeSec = 0; CurrentStoppageTimeSec = 0; passiveTimeSeconds = 0;
                CurrentSpeed = 0.0; CurrentV = 0.0;
                MaxSpeed = 0.0;
                CurrentAlt = Int16.MinValue;
                StartAlt = Int16.MinValue;
                Heading = 720;
                utmUtil.referenceSet = false;
                //FirstSampleValidCount = 1;      GpsSearchCount = 0;
                //CurrentStatusString = "gps off"; CurrentLiveLoggingString = "";

                labelInfo.SetText("Info: Track cleared");
                labelFileName.SetText("Current File Name: ---");
                TSummary.Clear();
                WayPointsT.Count = 0;

                mPage.Invalidate();     //update menu page to have correct enabled state
            }
        }
        private void buttonClearT2F_Click()
        {
            Plot2ndCount = 0;
            labelFileNameT2F.SetText("Track to Follow: ---");
            labelInfo.SetText("Info: Track to Follow cleared");
            WayPointsT2F.Count = 0;        // Clear Waypoints of T2F
            T2fSummary.Clear();
            mapUtil.clearNav(false);

            mPage.Invalidate();     //update menu page to have correct enabled state
        }
            

        private void buttonGraph_Click(Graph.SourceY src)
        {
            graph.SetSource(src, Graph.SourceX.Old);
            BufferDrawMode = BufferDrawModeGraph;
            showButton(button2, MenuPage.BFkt.map_zoomIn);
            showButton(button3, MenuPage.BFkt.map_zoomOut);
            
            NoBkPanel.BringToFront();
            NoBkPanel.Invalidate();
            timerButtonHide.Enabled = false;
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

            buttonColorDlg.Visible = !show;
            buttonUpdate.Visible = !show;
            buttonHelp.Visible = !show;
            buttonIoLocation.Visible = !show;
            buttonMapsLocation.Visible = !show;
            buttonLangLocation.Visible = !show;
            //buttonLoadFile.Visible = !show;               //todo ?
            //buttonLoadTrack2Follow.Visible = !show;
            //buttonLoad2Clear.Visible = !show;
            //buttonGraph.Visible = !show;
            labelFileName.Visible = !show;
            labelFileNameT2F.Visible = !show;
            labelInfo.Visible = !show;

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
            ShowHideViewOpt(!labelOptText.Visible);
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

        bool donothandleSelectedIndexChanged = false;
        private void initializeOptions()
        {
            donothandleSelectedIndexChanged = true;
            comboMapDownload.Items.Clear();
            int selectedIndex = -1;      //for the moment separate var to avoid fireing SelectedIndexChanged()
            if (!File.Exists(CurrentDirectory + "osm_servers.txt"))
            {
                FileStream fsw = null;
                StreamWriter sw = null;
                try
                {
                    fsw = new FileStream(CurrentDirectory + "osm_servers.txt", FileMode.Create, FileAccess.Write);
                    sw = new StreamWriter(fsw);
                    sw.WriteLine("OpenStreetMap;          http://a.tile.openstreetmap.org/");
                    sw.WriteLine("Cyclemap;               http://c.tile.thunderforest.com/cycle/");
                }
                catch (Exception ex)
                {
                    Utils.log.Error(" Write osm_servers.txt ", ex);
                }
                finally
                {
                    if (sw != null) sw.Close();
                    if (fsw != null) fsw.Close();
                }
            }
            FileStream fs = null;           //Read file
            StreamReader sr = null;
            try
            {
                fs = new FileStream(CurrentDirectory + "osm_servers.txt", FileMode.Open, FileAccess.Read);
                sr = new StreamReader(fs);
                string s = "";
                while (true)
                {
                    s = sr.ReadLine();       // serverName; serverURL
                    if (s == null) break;
                    string[] sAr = s.Split(';');
                    int i = comboMapDownload.Items.Add(sAr[0].Trim());
                    if (sAr[1].Trim() == mapUtil.OsmServer)
                        selectedIndex = i;
                }
            }
            catch (Exception ex)
            {
                Utils.log.Error(" Load1 osm_servers.txt ", ex);
            }
            finally
            {
                if (sr != null) sr.Close();
                if (fs != null) fs.Close();
            }
            if (selectedIndex == -1)        //server address not found in osm_servers.txt
            {
                int i = comboMapDownload.Items.Add(mapUtil.OsmServer);  //add http address
                selectedIndex = i;
            }
            comboMapDownload.SelectedIndex = selectedIndex;
            donothandleSelectedIndexChanged = false;
        }
        private void comboMapDownload_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (donothandleSelectedIndexChanged) return;        //inhibit while initializing combobox

            FileStream fs = null;
            StreamReader sr = null;
            try
            {
                fs = new FileStream(CurrentDirectory + "osm_servers.txt", FileMode.Open, FileAccess.Read);
                sr = new StreamReader(fs);
                string s = "";
                string[] sAr = null; ;
                string newServer;
                int i;
                for (i = 0; i <= comboMapDownload.SelectedIndex; i++)
                {
                    s = sr.ReadLine();       // serverName; serverURL
                }
                if (s == null)
                    newServer = (string)comboMapDownload.SelectedItem;       //extra server address (not included in osm_servers.txt)
                else
                {
                    sAr = s.Split(';');
                    if (sAr.Length < 2 || sAr[1].Trim() == "")
                        newServer = null;
                    else
                        newServer = sAr[1].Trim();
                }
                if (newServer != null && newServer != mapUtil.OsmServer)
                {
                    if (
                    MessageBox.Show("Do you really want to change the Download Server for map:\n" + MapsFilesDirectory + "\n\nPress Cancel to change the Map Files Directory first", "Change Server?",
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.OK)
                        mapUtil.SaveOsmServer(newServer);
                    else
                        initializeOptions();        //reload old selection
                }
                else
                    mapUtil.SaveOsmServer(mapUtil.OsmServer);       //save download state
            }
            catch (Exception ex)
            {
                Utils.log.Error(" Load2 osm_servers.txt ", ex);
                checkDownloadOsm.Checked = false;
                mapUtil.SaveOsmServer("");
            }
            finally
            {
                if (sr != null) sr.Close();
                if (fs != null) fs.Close();
            }
        }

        private void comboArraySize_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (PlotLat != null)
            {
                if (state >= State.logging)
                    buttonStop_Click(false);
                buttonClearTrack_Click();
                buttonClearT2F_Click();
            }
            CreateArrays();
        }
        void CreateArrays()
        {
            PlotDataSize = 4096 * (1 << comboArraySize.SelectedIndex);

            PlotLat = new float[PlotDataSize];
            PlotLong = new float[PlotDataSize];
            PlotZ = new Int16[PlotDataSize];
            PlotT = new Int32[PlotDataSize];
            PlotS = new Int16[PlotDataSize];
            PlotH = new Int16[PlotDataSize];        //heart rate
            PlotD = new Int32[PlotDataSize];

            Plot2ndLat = new float[PlotDataSize];
            Plot2ndLong = new float[PlotDataSize];
            Plot2ndZ = new Int16[PlotDataSize];
            Plot2ndT = new Int32[PlotDataSize];
            Plot2ndD = new Int32[PlotDataSize];
        }

        private void buttonUpdate_Click(object sender, EventArgs e)
        {
            Utils.update(Revision);
        }

        //WebBrowser webbrowser = null;
        TextBox textbox = null;
        Button buttonHelpEnd = null;
        byte BufferDrawMode_Save;
        private void buttonHelp_Click(object sender, EventArgs e)
        {
            string file = "";
            //string uri = "file://" + CurrentDirectory + "Readme.htm";
            //if (BufferDrawMode == BufferDrawModeGraph)
            //    uri += "#graph";
            
            //RegistryKey rk = Registry.ClassesRoot.OpenSubKey("htmlfile\\Shell\\Open\\Command");
            //string ie = (string)rk.GetValue("Default");
            bool ok = false;
            string browser = null;
            if (File.Exists("\\Windows\\iexplore.exe"))
                browser = "\\Windows\\iexplore.exe";
            else if (File.Exists("\\Windows\\iesample.exe"))        //some CE devices use iesample
                browser = "\\Windows\\iesample.exe";
            if (browser != null)
            {
                try
                {
                    System.Diagnostics.Process proc = new System.Diagnostics.Process();
                    //proc.StartInfo.FileName = "\"file://" + CurrentDirectory + "Readme.htm";
                    proc.StartInfo.FileName = browser;                //start explicitly IE, because some other brosers (UC Browser) can not display files
                    proc.StartInfo.Arguments = CurrentDirectory + "Readme.htm";
                    if (BufferDrawMode == BufferDrawModeGraph)
                        proc.StartInfo.Arguments += "#graph";
                    proc.StartInfo.UseShellExecute = true;
                    proc.Start();
                    ok = true;
                    
                    /*
                    file = CurrentDirectory + "Readme.htm";
                    StreamReader sr = new StreamReader(file);
                    string doc = sr.ReadToEnd();
                    if (BufferDrawMode_Save == BufferDrawModeGraph)
                        doc = doc.Remove(0, doc.IndexOf("<a name=\"graph\"></a>"));
                    webbrowser = new WebBrowser();
                    webbrowser.Parent = this;
                    webbrowser.Dock = DockStyle.Fill;
                    webbrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(eventHandler_ShowCloseButton);
                    //webbrowser.Url = new Uri("http://127.0.0.1");// new Uri("\"file:" + file + "\"");
                    //webbrowser.Stop();
                    webbrowser.DocumentText = doc;                                          //WinCE gives IOException - can not display doc?
                    webbrowser.BringToFront();
                    webbrowser.Focus();     //to bring Windows title bar in background
                    ok = true;
                    */
                }
                catch
                {
                    ok = false;
                }
            }
            if (!ok)
            {
                try
                {
                    BufferDrawMode_Save = BufferDrawMode;
                    BufferDrawMode = BufferDrawModeHelp;
                    file = CurrentDirectory + "Readme.txt";         //max 64kB!!
                    StreamReader sr = new StreamReader(file);
                    string doc = sr.ReadToEnd();
                    textbox = new TextBox();
                    textbox.Parent = this;
                    textbox.Dock = DockStyle.Fill;
                    textbox.Multiline = true;
                    textbox.ScrollBars = ScrollBars.Vertical;
                    textbox.Text = doc;
                    if (BufferDrawMode_Save == BufferDrawModeGraph)
                    {
                        textbox.SelectionStart = doc.Length - 1;    //scroll to the end, then back to "Graph" to have it at top
                        textbox.ScrollToCaret();
                        textbox.SelectionStart = doc.IndexOf(". Graph\r");
                        textbox.ScrollToCaret();
                    }
                    textbox.BringToFront();
                    textbox.Focus();
                    eventHandler_ShowCloseButton(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Cannot display '" + file + "'\n" + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    HelpEndClick(null, null);
                }
            }
        }

        void eventHandler_ShowCloseButton(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (buttonHelpEnd != null) return;
            buttonHelpEnd = new Button();
            buttonHelpEnd.Parent = this;
            int size = Math.Min(this.Width, this.Height) / 12;
            buttonHelpEnd.Top = 0;
            buttonHelpEnd.Left = this.Width - 2 * size;
            buttonHelpEnd.Width = size;
            buttonHelpEnd.Height = size;
            buttonHelpEnd.Text = "X";
            buttonHelpEnd.Click += HelpEndClick;
            buttonHelpEnd.BringToFront();
        }
        private void HelpEndClick(object sender, EventArgs e)
        {
            //if (webbrowser != null) { webbrowser.Dispose(); webbrowser = null; }
            if (textbox != null) { textbox.Dispose(); textbox = null; }
            if (buttonHelpEnd != null) { buttonHelpEnd.Dispose(); buttonHelpEnd = null; }
            BufferDrawMode = BufferDrawMode_Save;
        }

        private void checkKeepBackLightOn_CheckStateChanged(object sender, EventArgs e)
        {
            if (checkKeepBackLightOn.Checked) timerIdleReset.Enabled = false;
            else if (gps.OpenedOrSuspended) timerIdleReset.Enabled = true;
        }

        private void timerIdleReset_Tick(object sender, EventArgs e)        //Designer requires this function to be in Form1
        {
            Utils.timerIdleReset_Tick();
        }
        private void timerButtonHide_Tick(object sender, EventArgs e)
        {
            timerButtonHide.Enabled = false;
            if (BufferDrawMode == BufferDrawModeMaps || BufferDrawMode == BufferDrawModeGraph)
                NoBkPanel.BringToFront();
        }
        private void buttonHideToggle()
        {
            if (timerButtonHide.Enabled)
                buttonHide();
            else
                buttonUnHide();
        }
        private void buttonHide()
        {
            NoBkPanel.BringToFront();
            timerButtonHide.Enabled = false;
        }
        private void buttonUnHide()
        {
            button1.BringToFront();
            button2.BringToFront();
            button3.BringToFront();
            timerButtonHide.Interval = timerButtonHideInterval;
            timerButtonHide.Enabled = true;
        }
        

#if DEBUG
        // DEBUG printout - make sure it is not called in release
        private void DebugPrintout()
        {
            string log_file = "\\gcc_debug_print.txt";
            if (IoFilesDirectory != "\\") { log_file = IoFilesDirectory + "\\gcc_debug_print.txt"; }

            FileStream fs = null;
            StreamWriter wr = null;
            try
            {
                fs = new FileStream(log_file, FileMode.Create);
                wr = new StreamWriter(fs);

                string altitude_str;
                double altitude = CurrentAlt;

                // relative altitude mode
                if (MainConfigRelativeAlt) { altitude = CurrentAlt - (double)PlotZ[0]; }

                if ((comboUnits.SelectedIndex == 3) || (comboUnits.SelectedIndex == 5) || (comboUnits.SelectedIndex == 6))
                { altitude /= 0.30480; } // altitude in feet

                altitude_str = altitude.ToString("0");

                wr.WriteLine(" altitude " + altitude_str);
                wr.WriteLine(" CurrentAlt " + CurrentAlt.ToString());
                wr.WriteLine(" numericGeoID " + Decimal.ToDouble(numericGeoID.Value).ToString());

            }
            catch (Exception e)
            {
                Utils.log.Error(" DebugPrintout ", e);
            }
            finally
            {
                if(wr != null) wr.Close();
                if(fs != null) fs.Close();
            }
        }




#endif
    }
}
