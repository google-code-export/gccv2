//#define DEBUG
//#define BETA
//#define SERVICEPACK

using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Reflection;
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
        public static string debugStr = "b3";
#endif


        System.Globalization.CultureInfo IC = System.Globalization.CultureInfo.InvariantCulture;
        static string CurrentDirectory = "";
        int importantNewsId = 0;

        public static Gps gps = new Gps();
        GpsPosition position = null;
        UtmUtil utmUtil = new UtmUtil();
        MapUtil mapUtil = new MapUtil();

        FileStream fstream;
        BinaryWriter writer;

        // custom buttons

        // button to set i/o location
        PictureButton buttonIoLocation = new PictureButton();
        PictureButton buttonMapsLocation = new PictureButton();
        AlwaysFitLabel labelFileName = new AlwaysFitLabel();

        // CrossingWays buttons on Options2 tab
        PictureButton buttonCWShowKeyboard = new PictureButton();
        PictureButton buttonCWVerify = new PictureButton();

        // show help
        PictureButton buttonHelp = new PictureButton();

        // button to show/hide option view selector
        PictureButton buttonShowViewSelector = new PictureButton();

        NoBackgroundPanel NoBkPanel = new NoBackgroundPanel();

        public PictureButton button1 = new PictureButton();
        public PictureButton button2 = new PictureButton();
        public PictureButton button3 = new PictureButton();
        PictureButton button4 = new PictureButton();
        PictureButton button5 = new PictureButton();
        PictureButton button6 = new PictureButton();

        MenuPage mPage = new MenuPage();

        Bitmap AboutTabImage;
        Bitmap BlankImage;
        Bitmap CWImage;

        public static Color bkColor;
        public static Color foColor;
        bool dayScheme = false;

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
        int StartBattery = -255;
        double StartAlt = Int16.MinValue;


        // need to shift origin, to be able to save X/Y as short int in metres
        double OriginShiftX = 0.0;
        double OriginShiftY = 0.0;

        // Interval to to log GPS data (1-60s) or Interval to suspend GPS (Index 0-3 means always on)
        const int IndexSuspendMode = 4;
        int[] PollGpsTimeSec = new int[15] { 1,2,5,10,    5, 10, 20, 30, 60, 2*60, 5*60, 10*60, 20*60, 30*60, 60*60};
        DateTime LastPointUtc = DateTime.MinValue;
        int GpsSearchCount = 0;         // x sec until fix
        int FirstSampleDropCount = 0;   // drop first x samples after fix
        int GpsSuspendCounter = 0;      // suspend for x sec
        int GpsLogCounter = 0;          // log every x sec
        int AvgCount = 0;               // first averaging before point is fully valid

        //Drop first points (only available in Start/Stop mode            
        int [] dropFirst = new int [7] { 0, 1, 2, 4, 8, 16, 32 };
        
        // flag to indicate the the data was accepted by GetGpsData and save into file
        const byte GpsNotOk = 0;
        const byte GpsDrop = 1;
        const byte GpsBecameValid = 2;  //setReference if not set; initialize Old and Current variables
        const byte GpsInitVelo = 3;
        const byte GpsAvg = 4;          //average in suspend mode
        const byte GpsOk = 5;
        const byte GpsBecameInvalid = 6;
        const byte GpsInvalidButTrust = 7;
        byte GpsDataState = GpsNotOk;

        bool ReferenceSet = false;

        // to disable timer tick functions, if previous tick has not finished
        bool LockGpsTick = false;

        // flag when logging data
        bool Logging = false;
        bool Paused = false;
        bool ContinueAfterPause = false;

        // to indicate that it was stopped on low battery
        bool StoppedOnLow = false;

        // to save battery status (every 3 minutes)
        DateTime LastBatterySave;

        // average and max speed, distance. OldX/Y/T are coordinates/time of prev point.
        double MaxSpeed = 0.0;
        double Distance = 0.0;
        double OldX = 0.0, OldY = 0.0;
        double ReferenceXDist, ReferenceYDist;
        DateTime OldTime;

        int OldT = 0;

        // Current time, X/Y relative to starting point, abs height Z and current speed
        int CurrentTimeSec = 0;
        int CurrentStoppageTimeSec = 0;
        bool beepOnStartStop = false;
        double CurrentLat = 0.0, CurrentLong = 0.0;
        double CurrentX, CurrentY;
        double CurrentAlt = Int16.MinValue;
        double ceff = 1.0;          //convertion factor km/h to other units
        double m2feet = 1.0;        //convertion factor m to feet
        bool CurrentAltInvalid = true;
        double ReferenceAlt = Int16.MaxValue;
        const double AltThreshold = 5.0;        //gain and loss below theshold will be ignored
        double ElevationGain = 0.0;
        double ElevationSlope = 0.0;
        double ReferencAltSlope = Int16.MinValue;
        double ReferenceXSlope = 0.0;
        double ReferenceYSlope = 0.0;
        double AltitudeMax = Int16.MinValue;
        double AltitudeMin = Int16.MaxValue;
        double CurrentSpeed = Int16.MinValue*0.1;
        string CurrentFileName = "";
        string CurrentStatusString = "gps off ";
        public Color CurrentGpsLedColor = Color.Gray;
        int CurrentBattery = -255;
        double CurrentVx = 0.0;     //speed in x direction in m/s
        double CurrentVy = 0.0;     //speed in y direction in m/s
        double CurrentV = Int16.MinValue * 0.1;      //speed in km/h
        int Heading = 720;          //current heading 720=invalid, but still up

        int MainConfigAlt2display = 0;  // 0=gain; 1=loss; 2=max; 3=min; 4=slope
        int MainConfigSpeedSource = 0;  // 0=from gps; 1=from position; 2=both
        public enum eConfigDistance
        {
            eDistanceTrip = 0,          // do not change order, because used in string array and for option check
            eDistanceTrack2FollowStart,
            eDistanceTrack2FollowEnd,
        }
        eConfigDistance MainConfigDistance = eConfigDistance.eDistanceTrip;
        int MainConfigLatFormat = 0;    // 0=00.000000 1=N00°00.0000' 2=N00°00'00.00"

        // baud rates
        int[] BaudRates = new int[6] { 4800, 9600, 19200, 38400, 57600, 115200 };
        bool logRawNmea = false;

        // get pass the command line arguments
        static string FirstArgument;

        // data used for plotting and saving to KML/GPX
        // decimated, max size is PlotDataSize
        const int PlotDataSize = 4096;
        int PlotCount = 0;
        int Decimation = 1, DecimateCount = 0;
        float[] PlotLat = new float[PlotDataSize];
        float[] PlotLong = new float[PlotDataSize];
        Int16[] PlotZ = new Int16[PlotDataSize];
        Int32[] PlotT = new Int32[PlotDataSize];
        Int16[] PlotS = new Int16[PlotDataSize];
        Int16[] PlotV = new Int16[PlotDataSize];        //test ?
        Int32[] PlotD = new Int32[PlotDataSize];

        // check-points
        const int CheckPointDataSize = 128;
        public struct CheckPointInfo // structure to store CheckPoint data (used for tracklog)
        {
            public string name;
            public float lat;
            public float lon;
            public float interval_time;     // time in sec from prev checkpoint
            public float stoppage_time;     // stoppage time in sec from prev checkpoint
            public float interval_distance; // distance in m from prev checkpoint
        };
        CheckPointInfo[] CheckPoints = new CheckPointInfo[CheckPointDataSize];
        int CheckPointCount = 0;

        public class WayPointInfo      // Structure to store Waypoints, used by track2follow
        {
            public int WayPointDataSize;
            public int WayPointCount;
            public float[] lat;
            public float[] lon;
            public string[] name;
            public WayPointInfo(int size)
            {
                WayPointDataSize = size;
                WayPointCount = 0;
                lat = new float[size];
                lon = new float[size];
                name = new string[size];
            }
        };
        // Use same datasize for Waypoints and Checkpoints
        WayPointInfo WayPoints = new WayPointInfo(CheckPointDataSize);        

        // lap statistics
        int LapNumber = 0;
        double LapStartD;
        int LapStartT;
        string currentLap = "";
        string lastLap = "";
        int lapManualDistance = 0;
        bool lapManualClick = false;

        // data for plotting 2nd line (track to follow)
        float[] Plot2ndLat = new float[PlotDataSize];
        float[] Plot2ndLong = new float[PlotDataSize];
        Int32[] Plot2ndT = new Int32[PlotDataSize];
        int Counter2nd = 0;

        // to disable auto-save of controls on option page during startup
        bool DoNotSaveSettingsFlag = true;
              
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
        string LoadedSettingsName = "";

        // vars to select which file type to open
        const byte FileOpenMode_Gcc = 0;
        const byte FileOpenMode_2ndGcc = 1;
        const byte FileOpenMode_2ndKml = 2;
        const byte FileOpenMode_2ndGpx = 3;
        byte FileOpenMode = FileOpenMode_Gcc;
        byte FileExtentionToOpen = FileOpenMode_2ndGcc;

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
        public byte BufferDrawMode = BufferDrawModeMain;

        // main screen drawing vars (to set position)
        int[] MGridX = new int[4] { 0, 263, 340, 480 };
        int[] MGridY = new int[8] { 0, 120, 184, 248, 324, 364, 368, 508 };
        int MGridDelta = 3;     // delta to have small gap between values and the border
        int MHeightDelta = 27;  // height of an item,  when we print a few values into a single cell

        // vars for landscape support - move button from bottom to side and rescale
        public static bool isLandscape = false;
        bool LockResize = true;
        bool scaleFirstRun = true;
        int workX_p = 0, workY_p = 0, workX_l = 0, workY_l = 0;     //working area portrait and landscape


        // Hashed password for CrossingWays, as the text edit will display ***, so we cannot read it from there
        string CwHashPassword = "";
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
        private Panel tabOpenFile;
        private Timer timerGps;
        private ComboBox comboGpsPoll;
        private Label labelGpsActivity;
        private Label labelUnits;
        private ComboBox comboUnits;
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
        private CheckBox checkkeepAliveReg;
        private CheckBox checkWgs84Alt;
        private Timer timerButton;
        private CheckBox checkLapBeep;
        private Button buttonLapExport;
        private Panel panelCwLogo;
        private Label labelDefaultZoom;
        private NumericUpDown numericZoomRadius;

        private ContextMenu cMenu1 = new ContextMenu();
        private CheckBox checkGPSOffOnPowerOff;
        private CheckBox checkKeepBackLightOn;
        private CheckBox checkDispWaypoints;
        private const int MenuItemSize = 7;
        private MenuItem[] cMenuItem = new MenuItem[]{new MenuItem(), new MenuItem(), new MenuItem(), new MenuItem(), new MenuItem(), new MenuItem(), new MenuItem()};

        // c-tor. Create classes used, init some components
        public Form1()
        {
            // Required for Windows Form Designer support
            
            InitializeComponent();      //3162ms
            Controls.Add(mPage);

            // set defaults (shall load from file later)
            comboGpsPoll.SelectedIndex = 0;
            comboDropFirst.SelectedIndex = 0;
            comboUnits.SelectedIndex = 0;
            comboBoxKmlOptColor.SelectedIndex = 0;
            comboBoxKmlOptWidth.SelectedIndex = 1;
            comboBoxLine2OptColor.SelectedIndex = 6;
            comboBoxLine2OptWidth.SelectedIndex = 1;
            comboBoxUseGccDllRate.SelectedIndex = 0;
            comboBoxUseGccDllCom.SelectedIndex = 4;
            checkBoxUseGccDll.Checked = true;
            checkPlotLine2AsDots.Checked = true;
            comboMultiMaps.SelectedIndex = 1;
            comboMapDownload.SelectedIndex = 0;
            comboBoxCwLogMode.SelectedIndex = 0;
            comboLapOptions.SelectedIndex = 0;

            string Revision = Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "."
                            + Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString()
#if BETA
                            + " beta " + Assembly.GetExecutingAssembly().GetName().Version.Build.ToString()
#endif
#if SERVICEPACK
                            + " SP " + Assembly.GetExecutingAssembly().GetName().Version.Build.ToString()
#endif
                            ;
            labelRevision.Text = "programming/idea : AndyZap\ndesign : expo7\nspecial thanks to AngelGR\n\nversion " + Revision;

            CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
            if (CurrentDirectory == "\\") { CurrentDirectory = ""; }

            CreateCustomControls();         //3350ms

            DoOrientationSwitch();          //11ms, 61ms in landscape
                                            //8s until here
            LockResize = false;

            cMenu1.Popup += new EventHandler(cMenu1_Popup);
            for (int i = 0; i < MenuItemSize; i++)
            { cMenuItem[i].Click += new EventHandler(cMenuItem_Click); }
            this.NoBkPanel.ContextMenu = cMenu1;
            mPage.ContextMenu = cMenu1;
            mPage.form1ref = this;
        }

        private void cMenu1_Popup(object sender, EventArgs e)
        {
            cMenu1.MenuItems.Clear();
            for (int i = 0; i < MenuItemSize; i++)
            { cMenuItem[i].Checked = false; }
            int ClientMouseY = MousePosition.Y - Screen.PrimaryScreen.WorkingArea.Top;
            int numItems = 0;

            switch (BufferDrawMode)
            {
                case BufferDrawModeMain:
                    {
                        if (ClientMouseY < MGridY[1])
                        {
                            if (MousePosition.X < MGridX[2])
                            {
                                cMenuItem[0].Text = "inc stop";   //Time
                                cMenuItem[1].Text = "ex stop";
                                cMenuItem[2].Text = "ex stop + beep";
                                numItems = 3;
                                int i = 0;
                                if (checkExStopTime.Checked)
                                {
                                    i++;
                                    if (beepOnStartStop) { i++; }
                                }
                                cMenuItem[i].Checked = true;
                            }
                            else
                            {
                                cMenuItem[0].Text = "night scheme";   //Clock
                                cMenuItem[1].Text = "day scheme";
                                numItems = 2;
                                cMenuItem[dayScheme ? 1 : 0].Checked = true;
                            }
                        }
                        else if (ClientMouseY < MGridY[3])
                        {
                            if (MousePosition.X < MGridX[1])
                            {
                                cMenuItem[0].Text = "from gps";        //Speed
                                cMenuItem[1].Text = "from position";
                                cMenuItem[2].Text = "both";
                                numItems = 3;
                                cMenuItem[MainConfigSpeedSource].Checked = true;
                            }
                            else if (ClientMouseY < MGridY[2])
                            {
                                //avg
                            }
                            else
                            {
                                //max
                            }
                        }
                        else if (ClientMouseY < MGridY[5])
                        {
                            if (MousePosition.X < MGridX[1])
                            {
                                // Distance to Track2Follow only available if a track2follow is loaded
                                if (Counter2nd > 0)
                                {                                //Distance
                                    cMenuItem[0].Text = "since start of trip";        // Distance from start of trip
                                    cMenuItem[1].Text = "to track2follow start";
                                    cMenuItem[2].Text = "to track2follow end";
                                    numItems = 3;
                                    cMenuItem[(int)MainConfigDistance].Checked = true;
                                }
                            }
                            else if (comboLapOptions.SelectedIndex == 0)
                            {
                                if (ClientMouseY < MGridY[4])
                                {
                                    cMenuItem[0].Text = "absolute";   //Altitude cur
                                    cMenuItem[1].Text = "relative";
                                    numItems = 2;
                                    cMenuItem[checkRelativeAlt.Checked ? 1 : 0].Checked = true;
                                }
                                else
                                {
                                    cMenuItem[0].Text = "gain";     //Altitude gain
                                    cMenuItem[1].Text = "loss";
                                    cMenuItem[2].Text = "max";
                                    cMenuItem[3].Text = "min";
                                    cMenuItem[4].Text = "slope";
                                    numItems = 5;
                                    cMenuItem[MainConfigAlt2display].Checked = true;
                                }
                            }
                            else
                            {
                                //Lap
                            }
                        }
                        else if (ClientMouseY < MGridY[7])
                        {
                            if (MousePosition.X < MGridX[1])
                            {
                                //Info
                            }
                            else
                            {
                                cMenuItem[0].Text = "beep on gps fix";         //GPS
                                cMenuItem[1].Text = "log raw nmea";
                                cMenuItem[2].Text = "LatLon dd.dddddd°";
                                cMenuItem[3].Text = "LatLon Ndd°mm.mmmm'";
                                cMenuItem[4].Text = "LatLon Ndd°mm'ss.ss\"";
                                numItems = 5;
                                if (checkBeepOnFix.Checked) { cMenuItem[0].Checked = true; }
                                if (logRawNmea) { cMenuItem[1].Checked = true; }
                                cMenuItem[2+MainConfigLatFormat].Checked = true;
                            }
                        }
                        break;
                    }
                case BufferDrawModeMenu:
                    {
                        //MenuPage.
                        mPage.lastSelectedBFkt = (MenuPage.BFkt)mPage.getButtonIndex(MousePosition.X, MousePosition.Y);
                        if (mPage.lastSelectedBFkt == MenuPage.BFkt.recall1 || mPage.lastSelectedBFkt == MenuPage.BFkt.recall2 || mPage.lastSelectedBFkt == MenuPage.BFkt.recall3)
                        {
                            cMenuItem[0].Text = "save settings";
                            cMenuItem[1].Text = "change name";
                            numItems = 2;
                        }
                        else
                            numItems = 0;
                        break;
                    }
                case BufferDrawModeGraph:
                    {
                        cMenuItem[0].Text = "autoscale";
                        cMenuItem[1].Text = "over time";
                        cMenuItem[2].Text = "over distance";
                        numItems = 3;
                        if (GraphScale == GraphAutoscale) cMenuItem[0].Checked = true;
                        if (GraphOverDistance) cMenuItem[2].Checked = true;
                        else cMenuItem[1].Checked = true;
                        break;
                    }
                case BufferDrawModeMaps:
                    {
                        // Show start or end of track to follow (only available if a track2follow is loaded)
                        if (Counter2nd > 0)
                        {
                            cMenuItem[numItems].Text = "show track2follow - start";
                            if (mapUtil.ShowTrackToFollowMode == MapUtil.ShowTrackToFollow.T2FStart) cMenuItem[numItems].Checked = true;
                            numItems++;
                            cMenuItem[numItems].Text = "show track2follow - end";
                            if (mapUtil.ShowTrackToFollowMode == MapUtil.ShowTrackToFollow.T2FEnd) cMenuItem[numItems].Checked = true;
                            numItems++;
                        }
                        // Alternative method to reset map position (same functionality as double click)
                        cMenuItem[numItems].Text = "reset map (GPS/last)";
                        if (mapUtil.ShowTrackToFollowMode == MapUtil.ShowTrackToFollow.T2FOff) cMenuItem[numItems].Checked = true;
                        numItems++;
                        // If logging is activated, we can add a waypoint (faster access as menu page)
                        if (Logging || Paused)
                        {
                            cMenuItem[numItems].Text = "add waypoint";
                            numItems++;
                        }
                        // If a track with waypoints is loaded, we can switch on / off the waypoints in the map view
                        if( WayPoints.WayPointCount > 0 )
                        {
                            cMenuItem[numItems].Text = "show waypoints";
                            if (checkDispWaypoints.Checked == true) cMenuItem[numItems].Checked = true;
                            numItems++;
                        }
                        if (PlotCount > 0)
                        {
                            cMenuItem[numItems].Text = "hide track";
                            if (mapUtil.hideTrack) cMenuItem[numItems].Checked = true;
                            numItems++;
                        }
                        cMenuItem[numItems].Text = "hide map";
                        if (mapUtil.hideMap) cMenuItem[numItems].Checked = true;
                        numItems++;

                        break;
                    }
            }
            for (int i = 0; i < numItems; i++)
            {
                cMenu1.MenuItems.Add(cMenuItem[i]);
            }
        }
       

        void cMenuItem_Click(object sender, EventArgs e)
        {
            if (((MenuItem)sender).Text == "inc stop")
            { checkExStopTime.Checked = false; beepOnStartStop = false; }
            else if (((MenuItem)sender).Text == "ex stop")
            { checkExStopTime.Checked = true; beepOnStartStop = false; }
            else if (((MenuItem)sender).Text == "ex stop + beep")
            { checkExStopTime.Checked = true; beepOnStartStop = true; }

            else if (((MenuItem)sender).Text == "night scheme")
            {
                dayScheme = false;
                ApplyCustomBackground();
            }
            else if (((MenuItem)sender).Text == "day scheme")
            {
                dayScheme = true;
                ApplyCustomBackground();
            }
            else if (((MenuItem)sender).Text == "from gps")
                MainConfigSpeedSource = 0;
            else if (((MenuItem)sender).Text == "from position")
                MainConfigSpeedSource = 1;
            else if (((MenuItem)sender).Text == "both")
                MainConfigSpeedSource = 2;

            else if (((MenuItem)sender).Text == "since start of trip")
                MainConfigDistance = eConfigDistance.eDistanceTrip;
            else if (((MenuItem)sender).Text == "to track2follow start")
                MainConfigDistance = eConfigDistance.eDistanceTrack2FollowStart;
            else if (((MenuItem)sender).Text == "to track2follow end")
                MainConfigDistance = eConfigDistance.eDistanceTrack2FollowEnd;

            else if (((MenuItem)sender).Text == "absolute")
                checkRelativeAlt.Checked = false;
            else if (((MenuItem)sender).Text == "relative")
                checkRelativeAlt.Checked = true;

            else if (((MenuItem)sender).Text == "gain")
                MainConfigAlt2display = 0;
            else if (((MenuItem)sender).Text == "loss")
                MainConfigAlt2display = 1;
            else if (((MenuItem)sender).Text == "max")
                MainConfigAlt2display = 2;
            else if (((MenuItem)sender).Text == "min")
                MainConfigAlt2display = 3;
            else if (((MenuItem)sender).Text == "slope")
                MainConfigAlt2display = 4;

            else if (((MenuItem)sender).Text == "beep on gps fix")
                checkBeepOnFix.Checked = !checkBeepOnFix.Checked;
            else if (((MenuItem)sender).Text == "log raw nmea")
                logRawNmea = !logRawNmea;
            else if (((MenuItem)sender).Text == "LatLon dd.dddddd°")
                MainConfigLatFormat = 0;
            else if (((MenuItem)sender).Text == "LatLon Ndd°mm.mmmm'")
                MainConfigLatFormat = 1;
            else if (((MenuItem)sender).Text == "LatLon Ndd°mm'ss.ss\"")
                MainConfigLatFormat = 2;
            // menu page
            else if (((MenuItem)sender).Text == "save settings")
            {
                // On the HTC TouchDiamond 2 Device / Windows Mobile 6.5, the returned Mouse Position is the Mouse Position of the
                // PopUp selection and not of the Button itself. The result will be, it´s not possible to 
                // change the name, or safe the data. 
                // Bugfix: Store the the position of the button before opening the popup menu.
                SaveSettings(CurrentDirectory + "\\" + mPage.mBAr[(int)mPage.lastSelectedBFkt].text + ".dat");
                LoadedSettingsName = mPage.mBAr[(int)mPage.lastSelectedBFkt].text;
            }
            else if (((MenuItem)sender).Text == "change name")
            {
                string name = mPage.mBAr[(int)mPage.lastSelectedBFkt].text;
                if (Utils.InputBox("Rename", "input name", ref name) == DialogResult.OK)
                    mPage.mBAr[(int)mPage.lastSelectedBFkt].text = name;
            }
            // graph page
            else if (((MenuItem)sender).Text == "autoscale")
                if (GraphScale != GraphAutoscale) GraphScale = GraphAutoscale; else GraphScale = GraphRedraw;
            else if (((MenuItem)sender).Text == "over time")
                GraphOverDistance = false;
            else if (((MenuItem)sender).Text == "over distance")
                GraphOverDistance = true;
            else if (((MenuItem)sender).Text == "show track2follow - end")
            {
                if (Counter2nd > 0)
                {
                    ResetMapPosition();
                    mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FEnd;
                    NoBkPanel.Invalidate();     // Update Screen
                }
            }
            else if (((MenuItem)sender).Text == "show track2follow - start")
            {
                if (Counter2nd > 0)
                {
                    ResetMapPosition();
                    mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FStart;
                    NoBkPanel.Invalidate();     // Update Screen
                }
            }
            else if (((MenuItem)sender).Text == "add waypoint")
            {
                string wayPoint = "";
                DialogResult Result = Utils.InputBox(null, "Enter waypoint name", ref wayPoint);
                if (Result == DialogResult.OK)
                {
                    WriteCheckPoint(wayPoint);
                }
            }
            else if (((MenuItem)sender).Text == "reset map (GPS/last)")
                ResetMapPosition();
            else if (((MenuItem)sender).Text == "show waypoints")
                checkDispWaypoints.Checked = !checkDispWaypoints.Checked;
            else if (((MenuItem)sender).Text == "hide track")
                mapUtil.hideTrack = !mapUtil.hideTrack;
            else if (((MenuItem)sender).Text == "hide map")
                mapUtil.hideMap = !mapUtil.hideMap;

            else
                MessageBox.Show("no method for menu: " + ((MenuItem)sender).Text);
        }




        private void ApplyCustomBackground()
        {
            bkColor = LoadBkColor();
            foColor = LoadForeColor();

            labelRevision.BackColor = bkColor;
            labelRevision.ForeColor = foColor;

            tabBlank.BackColor = bkColor;
            tabBlank1.BackColor = bkColor;
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
            labelUnits.BackColor = bkColor; labelUnits.ForeColor = foColor;
            listBoxFiles.BackColor = bkColor; listBoxFiles.ForeColor = foColor;
            buttonIoLocation.BackColor = bkColor; buttonIoLocation.ForeColor = foColor;
            buttonMapsLocation.BackColor = bkColor; buttonMapsLocation.ForeColor = foColor;

            checkWgs84Alt.BackColor = bkColor; checkWgs84Alt.ForeColor = foColor;
            numericGeoID.BackColor = bkColor; numericGeoID.ForeColor = foColor;
            numericAvg.BackColor = bkColor; numericAvg.ForeColor = foColor;
            checkGpxRte.BackColor = bkColor; checkGpxRte.ForeColor = foColor;
            checkGpxSpeedMs.BackColor = bkColor; checkGpxSpeedMs.ForeColor = foColor;
            checkKmlAlt.BackColor = bkColor; checkKmlAlt.ForeColor = foColor;
            buttonShowViewSelector.BackColor = bkColor; buttonShowViewSelector.ForeColor = foColor;

            checkEditFileName.BackColor = bkColor; checkEditFileName.ForeColor = foColor;
            checkShowBkOff.BackColor = bkColor; checkShowBkOff.ForeColor = foColor;
            checkkeepAliveReg.BackColor = bkColor; checkkeepAliveReg.ForeColor = foColor;
            checkGPSOffOnPowerOff.BackColor = bkColor; checkGPSOffOnPowerOff.ForeColor = foColor;
            checkKeepBackLightOn.BackColor = bkColor; checkKeepBackLightOn.ForeColor = foColor;
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
            labelCwUrl.BackColor = bkColor;        labelCwUrl.ForeColor = foColor;
            textBoxCwUrl.BackColor = bkColor;      textBoxCwUrl.ForeColor = foColor;
            panelCwLogo.BackColor = bkColor;

            comboBoxLine2OptWidth.BackColor = bkColor; comboBoxLine2OptWidth.ForeColor = foColor;
            comboBoxLine2OptColor.BackColor = bkColor; comboBoxLine2OptColor.ForeColor = foColor;
            labelLine2Opt1.BackColor = bkColor;        labelLine2Opt1.ForeColor = foColor;
            labelLine2Opt2.BackColor = bkColor;        labelLine2Opt2.ForeColor = foColor;

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
            checkDispWaypoints.BackColor = bkColor; checkDispWaypoints.ForeColor = foColor;
            checkMapsWhiteBk.BackColor = bkColor; checkMapsWhiteBk.ForeColor = foColor;

            comboLapOptions.BackColor = bkColor; comboLapOptions.ForeColor = foColor;
            checkLapBeep.BackColor = bkColor; checkLapBeep.ForeColor = foColor;
            buttonLapExport.BackColor = bkColor; buttonLapExport.ForeColor = foColor;
            textLapOptions.BackColor = bkColor; textLapOptions.ForeColor = foColor;
            mPage.BackColor = bkColor; mPage.ForeColor = foColor;
            labelDefaultZoom.BackColor = bkColor; labelDefaultZoom.ForeColor = foColor;
            numericZoomRadius.BackColor = bkColor; numericZoomRadius.ForeColor = foColor;

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
            string file_name = CurrentDirectory + "\\" + name;
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
        private Color LoadBkColor()
        {
            string file_name = CurrentDirectory + (dayScheme ? "\\bk_color.jpg" : "\\bk_color_night.jpg");
#if DEBUG
#else
            if (File.Exists(file_name))
            {
                Bitmap bmp = new Bitmap(file_name);
                return bmp.GetPixel(0, 0);
            }
#endif
            // not exists, load internal one
            return dayScheme ? Color.FromArgb(255, 255, 255) : Color.FromArgb(34, 34, 34);
        }
        private Color LoadForeColor()
        {
            string file_name = CurrentDirectory + (dayScheme ? "\\fore_color.jpg" : "\\fore_color_night.jpg");
#if DEBUG
#else
            if (File.Exists(file_name))
            {
                Bitmap bmp = new Bitmap(file_name);
                return bmp.GetPixel(0, 0);
            }
#endif
            // not exists, load internal one
            return dayScheme ? Color.FromArgb(34, 34, 34) : Color.FromArgb(255, 255, 255);
        }

        private void CreateCustomControls()
        {
            // Create custom buttons ----------------------------
            Assembly asm = Assembly.GetExecutingAssembly();

            // bottom menu --------------

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
            labelFileName.Bounds = new Rectangle(3, 320, 474, 28);
            labelFileName.Font = new Font("Tahoma", 8F, FontStyle.Regular);
            labelFileName.Text = "";

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
            NoBkPanel.DoubleClick += tabGraph_MouseDoubleClick;

            // about tab image, blank image and CW logo image
            AboutTabImage = new Bitmap(asm.GetManifestResourceStream("GpsSample.Graphics.about.jpg"));
            BlankImage = LoadBitmap("blank.jpg");
            CWImage = new Bitmap(asm.GetManifestResourceStream("GpsSample.Graphics.CW_logo.png"));

            /*NoBkPanel.BringToFront();
            showButton(button1, MenuPage.BFkt.options);
            showButton(button2, MenuPage.BFkt.start);
            showButton(button3, MenuPage.BFkt.gps);
            */
            MenuExec(MenuPage.BFkt.main);

            listBoxFiles.Items.Clear();
            listBoxFiles.Focus();
        }


        int scx_p, scx_q;
        int scy_p, scy_q;
        private void ScaleControl(Control c)
        {
            Rectangle r = new Rectangle((c.Left*scx_p+scx_q/2)/scx_q, (c.Top*scy_p+scy_q/2)/scy_q, (c.Width*scx_p+scx_q/2)/scx_q, (c.Height*scy_p+scy_q/2)/scy_q);
            c.Bounds = r;
        }
        
        private void ScaleToCurrentResolution()
        {
            ScaleControl(buttonMapsLocation);
            ScaleControl(buttonIoLocation);
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
            ScaleControl(listBoxFiles);
            ScaleControl(labelUnits);
            ScaleControl(checkWgs84Alt);

            ScaleControl(numericGeoID);
            ScaleControl(numericAvg);
            ScaleControl(checkGpxRte);
            ScaleControl(checkGpxSpeedMs);
            ScaleControl(checkKmlAlt);

            ScaleControl(checkEditFileName);
            ScaleControl(checkShowBkOff);
            ScaleControl(checkkeepAliveReg);
            ScaleControl(checkKeepBackLightOn);
            ScaleControl(checkGPSOffOnPowerOff);
            ScaleControl(checkRelativeAlt);
            ScaleControl(labelMultiMaps);
            ScaleControl(labelMapDownload);
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

            ScaleControl(numericGpxTimeShift);
            ScaleControl(labelGpxTimeShift);
            ScaleControl(checkDispWaypoints);
            ScaleControl(checkMapsWhiteBk);
            ScaleControl(labelDefaultZoom);
            ScaleControl(numericZoomRadius);

            ScaleControl(comboLapOptions);
            ScaleControl(checkLapBeep);
            ScaleControl(buttonLapExport);
            ScaleControl(textLapOptions);
            ScaleControl(mPage);
            ScaleControl(button1);
            ScaleControl(button2);
            ScaleControl(button3);
            ScaleControl(button4);
            ScaleControl(button5);
            ScaleControl(button6);

            ScaleControl(tabBlank);
            ScaleControl(tabBlank1);
            ScaleControl(tabOpenFile);

            ScaleControl(NoBkPanel);
            ScaleControl(tabControl);



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

            MapsFilesDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
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

            IoFilesDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
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
            this.checkExStopTime = new System.Windows.Forms.CheckBox();
            this.comboUnits = new System.Windows.Forms.ComboBox();
            this.labelUnits = new System.Windows.Forms.Label();
            this.checkEditFileName = new System.Windows.Forms.CheckBox();
            this.checkShowBkOff = new System.Windows.Forms.CheckBox();
            this.checkRelativeAlt = new System.Windows.Forms.CheckBox();
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
            this.tabPageMapScr = new System.Windows.Forms.TabPage();
            this.checkDispWaypoints = new System.Windows.Forms.CheckBox();
            this.labelDefaultZoom = new System.Windows.Forms.Label();
            this.tabPageKmlGpx = new System.Windows.Forms.TabPage();
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
            this.numericZoomRadius.Location = new System.Drawing.Point(320, 405);
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
            100,
            0,
            0,
            0});
            this.numericZoomRadius.ValueChanged += new System.EventHandler(this.numericZoomRadiusChanged);
            // 
            // labelRevision
            // 
            this.labelRevision.Location = new System.Drawing.Point(0, 220);
            this.labelRevision.Name = "labelRevision";
            this.labelRevision.Size = new System.Drawing.Size(480, 190);
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
            this.listBoxFiles.Size = new System.Drawing.Size(480, 408);
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
            this.comboGpsPoll.Location = new System.Drawing.Point(153, 5);
            this.comboGpsPoll.Name = "comboGpsPoll";
            this.comboGpsPoll.Size = new System.Drawing.Size(324, 41);
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
            this.checkExStopTime.Location = new System.Drawing.Point(2, 85);
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
            this.comboUnits.Location = new System.Drawing.Point(125, 38);
            this.comboUnits.Name = "comboUnits";
            this.comboUnits.Size = new System.Drawing.Size(352, 41);
            this.comboUnits.TabIndex = 4;
            // 
            // labelUnits
            // 
            this.labelUnits.Location = new System.Drawing.Point(48, 38);
            this.labelUnits.Name = "labelUnits";
            this.labelUnits.Size = new System.Drawing.Size(79, 40);
            this.labelUnits.Text = "Units:";
            // 
            // checkEditFileName
            // 
            this.checkEditFileName.Location = new System.Drawing.Point(2, 131);
            this.checkEditFileName.Name = "checkEditFileName";
            this.checkEditFileName.Size = new System.Drawing.Size(476, 40);
            this.checkEditFileName.TabIndex = 19;
            this.checkEditFileName.Text = "Ask for log file name";
            // 
            // checkShowBkOff
            // 
            this.checkShowBkOff.Location = new System.Drawing.Point(2, 177);
            this.checkShowBkOff.Name = "checkShowBkOff";
            this.checkShowBkOff.Size = new System.Drawing.Size(476, 40);
            this.checkShowBkOff.TabIndex = 18;
            this.checkShowBkOff.Text = "Show BkLight Off button";
            this.checkShowBkOff.Visible = false;
            // 
            // checkRelativeAlt
            // 
            this.checkRelativeAlt.Location = new System.Drawing.Point(2, 223);
            this.checkRelativeAlt.Name = "checkRelativeAlt";
            this.checkRelativeAlt.Size = new System.Drawing.Size(476, 40);
            this.checkRelativeAlt.TabIndex = 20;
            this.checkRelativeAlt.Text = "Show relative altitude";
            // 
            // textLapOptions
            // 
            this.textLapOptions.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textLapOptions.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            this.textLapOptions.Location = new System.Drawing.Point(2, 97);
            this.textLapOptions.Multiline = true;
            this.textLapOptions.Name = "textLapOptions";
            this.textLapOptions.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLapOptions.Size = new System.Drawing.Size(476, 364);
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
            this.numericGpxTimeShift.Location = new System.Drawing.Point(320, 155);
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
            this.labelGpxTimeShift.Location = new System.Drawing.Point(2, 155);
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
            this.labelMultiMaps.Location = new System.Drawing.Point(2, 307);
            this.labelMultiMaps.Name = "labelMultiMaps";
            this.labelMultiMaps.Size = new System.Drawing.Size(133, 40);
            this.labelMultiMaps.Text = "Multi-maps";
            // 
            // labelMapDownload
            // 
            this.labelMapDownload.Location = new System.Drawing.Point(2, 357);
            this.labelMapDownload.Name = "labelMapDownload";
            this.labelMapDownload.Size = new System.Drawing.Size(133, 40);
            this.labelMapDownload.Text = "Download";
            // 
            // comboMultiMaps
            // 
            this.comboMultiMaps.Items.Add("off");
            this.comboMultiMaps.Items.Add("multi maps, 1x zoom");
            this.comboMultiMaps.Items.Add("multi maps, 2x zoom");
            this.comboMultiMaps.Items.Add("multi maps, 4x zoom");
            this.comboMultiMaps.Location = new System.Drawing.Point(139, 305);
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
            this.comboMapDownload.Location = new System.Drawing.Point(139, 355);
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
            this.labelLine2Opt1.Text = "Track2f";
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
            // 
            // textBoxCw1
            // 
            this.textBoxCw1.Location = new System.Drawing.Point(169, 93);
            this.textBoxCw1.Name = "textBoxCw1";
            this.textBoxCw1.Size = new System.Drawing.Size(308, 41);
            this.textBoxCw1.TabIndex = 1;
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
            this.checkMapsWhiteBk.Location = new System.Drawing.Point(2, 255);
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
            this.timerIdleReset.Interval = 15000;
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
            this.tabPageGps.Size = new System.Drawing.Size(472, 469);
            this.tabPageGps.Text = "GPS";
            // 
            // checkKeepBackLightOn
            // 
            this.checkKeepBackLightOn.Location = new System.Drawing.Point(5, 407);
            this.checkKeepBackLightOn.Name = "checkKeepBackLightOn";
            this.checkKeepBackLightOn.Size = new System.Drawing.Size(475, 40);
            this.checkKeepBackLightOn.TabIndex = 60;
            this.checkKeepBackLightOn.Text = "Safe Energy: do not keep Backlight on";
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
            this.tabPageMapScr.Controls.Add(this.checkDispWaypoints);
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
            this.tabPageMapScr.Controls.Add(this.labelMapDownload);
            this.tabPageMapScr.Controls.Add(this.comboMultiMaps);
            this.tabPageMapScr.Controls.Add(this.comboMapDownload);
            this.tabPageMapScr.Location = new System.Drawing.Point(0, 0);
            this.tabPageMapScr.Name = "tabPageMapScr";
            this.tabPageMapScr.Size = new System.Drawing.Size(480, 463);
            this.tabPageMapScr.Text = "Map screen";
            // 
            // checkDispWaypoints
            // 
            this.checkDispWaypoints.Location = new System.Drawing.Point(2, 205);
            this.checkDispWaypoints.Name = "checkDispWaypoints";
            this.checkDispWaypoints.Size = new System.Drawing.Size(261, 33);
            this.checkDispWaypoints.TabIndex = 59;
            this.checkDispWaypoints.Text = "Display waypoints";
            // 
            // labelDefaultZoom
            // 
            this.labelDefaultZoom.Location = new System.Drawing.Point(2, 407);
            this.labelDefaultZoom.Name = "labelDefaultZoom";
            this.labelDefaultZoom.Size = new System.Drawing.Size(314, 36);
            this.labelDefaultZoom.Text = "Default zoom [radius in m]";
            // 
            // tabPageKmlGpx
            // 
            this.tabPageKmlGpx.Controls.Add(this.checkGpxSpeedMs);
            this.tabPageKmlGpx.Controls.Add(this.numericGpxTimeShift);
            this.tabPageKmlGpx.Controls.Add(this.labelGpxTimeShift);
            this.tabPageKmlGpx.Controls.Add(this.checkKmlAlt);
            this.tabPageKmlGpx.Controls.Add(this.checkGpxRte);
            this.tabPageKmlGpx.Location = new System.Drawing.Point(0, 0);
            this.tabPageKmlGpx.Name = "tabPageKmlGpx";
            this.tabPageKmlGpx.Size = new System.Drawing.Size(472, 469);
            this.tabPageKmlGpx.Text = "Kml/Gpx";
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
            this.tabPageLiveLog.Size = new System.Drawing.Size(472, 469);
            this.tabPageLiveLog.Text = "Live log";
            // 
            // textBoxCwUrl
            // 
            this.textBoxCwUrl.Location = new System.Drawing.Point(169, 46);
            this.textBoxCwUrl.Name = "textBoxCwUrl";
            this.textBoxCwUrl.Size = new System.Drawing.Size(308, 41);
            this.textBoxCwUrl.TabIndex = 26;
            this.textBoxCwUrl.Text = "http://www.crossingways.com";
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
            this.tabPageLaps.Size = new System.Drawing.Size(472, 469);
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
            this.tabPageAbout.Size = new System.Drawing.Size(472, 469);
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
            this.Controls.Add(this.tabBlank1);
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
            IoFilesDirectory = CurrentDirectory + "\\tracks";
            MapsFilesDirectory = CurrentDirectory + "\\maps";

            // check if there are any args - then load the file
            string parameterExt = Path.GetExtension(FirstArgument);
            if (parameterExt == ".dat")
                LoadSettings(FirstArgument);
            else
                LoadSettings(CurrentDirectory + "\\GpsCycleComputer.dat");

            // now allow to save setting on combo change
            DoNotSaveSettingsFlag = false;

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
                    if (fs.Load(FirstArgument, ref WayPoints,
                                PlotDataSize, ref Plot2ndLat, ref Plot2ndLong, ref Plot2ndT, out Counter2nd))
                        labelFileName.SetText("Track2follow: " + Path.GetFileName(FirstArgument) + " loaded");   //loaded ok
            }

            // send indication to GPS driver to wake-up (if it is OFF)
            gps.startGpsService();

            mapUtil.LoadCustomOsmServer(CurrentDirectory + "\\osm_server.txt");

            // select option pages to show and apply map bkground option
            FillPagesToShow();
            checkMapsWhiteBk_Click(checkMapsWhiteBk, EventArgs.Empty);

            if (importantNewsId != 404)
            {
                MessageBox.Show("You can use context menu to configure some fields in main page or modify some commands in menu page", "GCC - Important News");
                importantNewsId = 404;
            }

        }

        // close GPS and files on form close
        private void Form1_Closed(object sender, System.EventArgs e)
        {
            LockGpsTick = true;
            timerGps.Enabled = false;

            CloseGps();

            // Stop button enabled - indicate that we need to close streams
            if (Logging || Paused)
            {
                try
                {
                    writer.Close();
                    fstream.Close();

                    saveCsvLog();
                }
                catch (Exception ee)
                {
                    Utils.log.Error(" Form1_Closed ", ee);
                }
            }

            Utils.log = null;
        }

        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings(CurrentDirectory + "\\GpsCycleComputer.dat");
            
            if (Logging || Paused) // means applicaion is still running
            {
                if (MessageBox.Show("Do you want to exit and stop logging?", "GPS is logging!",
                   MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.No)
                {
                    // Cancel the Closing event from closing the form.
                    e.Cancel = true;
                }
            }

        }


        private void LoadSettings(string file_name)
        {
            FileStream fs = null;
            BinaryReader wr = null;
            try
            {
                fs = new FileStream(file_name, FileMode.Open);
                wr = new BinaryReader(fs, Encoding.ASCII);

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
                    CheckIoDirectoryExists();
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
                checkShowBkOff.Checked = 1 == wr.ReadInt32();
                checkRelativeAlt.Checked = 1 == wr.ReadInt32();
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
                {
                    MapsFilesDirectory = saved_name;
                    CheckMapsDirectoryExists();
                    mapUtil.LoadMaps(MapsFilesDirectory);
                }

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
                comboMapDownload.SelectedIndex = wr.ReadInt32();         mapUtil.OsmTilesWebDownload = comboMapDownload.SelectedIndex;
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
                GraphOverDistance = 1 == wr.ReadInt32();
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
                checkDispWaypoints.Checked = 1 == wr.ReadInt32();
				MainConfigDistance = (eConfigDistance) wr.ReadInt32();
                MainConfigLatFormat = wr.ReadInt32();
                LoadedSettingsName = wr.ReadString();
				
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("Configuration file not found: " + file_name, "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }
            catch (EndOfStreamException)
            {
                MessageBox.Show("Unexpected EOF while reading " + file_name + " (Position " + fs.Position + ").\nUsing current (or default) Options for remainder.\n\nThis is ok if you have just updated to a newer version.", "Warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button1);
            }
            catch (Exception ee)
            {
                Utils.log.Error(" LoadSettings ", ee);
            }
            finally
            {
                if (wr != null) wr.Close();
                if (fs != null) fs.Close();
            }
        }

        private void SaveSettings(string file_name)
        {
            // save settings -----------------
            FileStream fs = null;
            BinaryWriter wr = null;
            try
            {
                fs = new FileStream(file_name, FileMode.Create);
                wr = new BinaryWriter(fs, Encoding.ASCII);

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
                wr.Write((checkShowBkOff.Checked ? 1 : 0));
                wr.Write((checkRelativeAlt.Checked ? 1 : 0));
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
                wr.Write((int)(0.5 + Decimal.ToDouble(numericGpxTimeShift.Value)));
                wr.Write(checkMapsWhiteBk.Checked ? 1 : 0);
                wr.Write((int)FileExtentionToOpen);
                wr.Write(checkOptLaps.Checked ? 1 : 0);
                wr.Write(comboMapDownload.SelectedIndex);
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
                wr.Write(GraphOverDistance ? 1 : 0);
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
                wr.Write(checkDispWaypoints.Checked ? 1 : 0);
				wr.Write((int)MainConfigDistance);
                wr.Write(MainConfigLatFormat);
                wr.Write(LoadedSettingsName);

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

        int numsamples = 0;
        int goodsamples = 0;
        bool moving = false;
        // main logging function to receive date from GPS
        private void GetGpsData()
        {
            numsamples++;   //debug
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
                    if (LastPointUtc < position.Time)
                    {                       // OK, time is increasing -> data is valid
                        goodsamples++;  //debug
                        
                        int avg = (int)numericAvg.Value;
                        //int avg = 1;    //debug
                        switch (GpsDataState)
                        {
                            case GpsNotOk:         //GpsDataState is last state
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
                                    if (checkBeepOnFix.Checked) MessageBeep(BeepType.Ok);
                                    AvgCount = avg;
                                    if (ReferenceSet == false)
                                    {
                                        utmUtil.setReferencePoint(position.Latitude, position.Longitude);
                                        ReferenceSet = true;
                                    }
                                    CurrentLat = position.Latitude;         //initialize Old and Current variables
                                    CurrentLong = position.Longitude;
                                    utmUtil.getXY(position.Latitude, position.Longitude, out CurrentX, out CurrentY);
                                    OldX = CurrentX; OldY = CurrentY;
                                    OldTime = position.Time;
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
                                }
                                break;
                            
                            case GpsInitVelo:
                            case GpsAvg:
                            case GpsOk:
                                GpsSearchCount = 0;
                                CurrentGpsLedColor = Color.LightGreen;
                          
                                double x, y;
                                utmUtil.getXY(position.Latitude, position.Longitude, out x, out y);
                                TimeSpan deltaT = position.Time - OldTime;
                                double deltaT_s = deltaT.TotalSeconds;
                                OldTime = position.Time;

                                // Averaging
                                CurrentLat = (CurrentLat * (avg - 1) + position.Latitude) / avg;
                                CurrentLong = (CurrentLong * (avg - 1) + position.Longitude) / avg;
                                //CurrentX = (CurrentX * (avg - 1) + x) / avg;
                                //CurrentY = (CurrentY * (avg - 1) + y) / avg;
                                CurrentX = ((CurrentX + CurrentVx * deltaT_s) * (avg - 1) + x) / avg;   //CurrentVx from last sample
                                CurrentY = ((CurrentY + CurrentVy * deltaT_s) * (avg - 1) + y) / avg;
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

                                if (GpsDataState == GpsInitVelo && deltaT_s > 0)
                                {
                                    CurrentVx = deltaX / deltaT_s;      // m/s
                                    CurrentVy = deltaY / deltaT_s;
                                    CurrentV = 3.6 * Math.Sqrt(CurrentVx * CurrentVx + CurrentVy * CurrentVy);  // 3.6* -> km/h
                                    GpsDataState++;     //=GpsAvg
                                }
                                else if (deltaT_s > 0)
                                {
                                    CurrentVx = (CurrentVx * (2*avg - 1) + (deltaX / deltaT_s)) / (2*avg);
                                    CurrentVy = (CurrentVy * (2*avg - 1) + (deltaY / deltaT_s)) / (2*avg);
                                    CurrentV = 3.6 * Math.Sqrt(CurrentVx * CurrentVx + CurrentVy * CurrentVy);
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
                                    GpsDataState = GpsOk;
                                    CurrentGpsLedColor = Color.Green;

                                    if (Logging && GpsLogCounter <= 0)
                                    {
                                        if (comboGpsPoll.SelectedIndex < IndexSuspendMode)
                                            GpsLogCounter = PollGpsTimeSec[comboGpsPoll.SelectedIndex];

                                        // save and write starting position
                                        if (PlotCount == 0)
                                        {
                                            StartLat = CurrentLat;
                                            StartLong = CurrentLong;
                                            StartTime = DateTime.Now;
                                            StartTimeUtc = DateTime.UtcNow;
                                            StartBattery = Utils.GetBatteryStatus();
                                            StartAlt = Int16.MinValue;
                                            ReferenceXDist = CurrentX; ReferenceYDist = CurrentY;
                                            ReferenceAlt = Int16.MaxValue;
                                            AltitudeMax = Int16.MinValue;
                                            AltitudeMin = Int16.MaxValue;
                                            LapStartD = 0; LapStartT = 0; LapNumber = 0;
                                            lapManualDistance = 0; lapManualClick = false;
                                            currentLap = ""; lastLap = "";
                                            textLapOptions.Text = "";
                                            moving = false;
                                            //utmUtil.setReferencePoint(StartLat, StartLong);
                                            //OldX = 0.0; OldY = 0.0;
                                            //VeloAvgState = 2;       //start velocity calculation new because of serReferencePoint
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

                                            // for maps, fill x/y values realtive to the starting point
                                            ResetMapPosition();
                                        }

                                        TimeSpan run_time = DateTime.UtcNow - StartTimeUtc;

                                        // Safety check 1: make sure elapsed time is not negative
                                        double double_total_sec = run_time.TotalSeconds;
                                        if (double_total_sec < 0.0) double_total_sec = 0.0;

                                        // Safety check 2: make sure new time is increasing
                                        if ((int)double_total_sec < OldT)
                                        {
                                            OldT = (int)double_total_sec;
                                        }
                                        CurrentTimeSec = (int)double_total_sec;

                                        if(ContinueAfterPause)
                                        {                       //force speed 0, so that Pause time calculates to Stoppage time (log v=0 into gcc for reload!)
                                            CurrentSpeed = 0.0;
                                            ContinueAfterPause = false;
                                        }
                                        // compute Stoppage time
                                        if (CurrentSpeed < 1.0)
                                        {
                                            CurrentStoppageTimeSec += CurrentTimeSec - OldT;
                                            if (moving && beepOnStartStop)
                                                MessageBeep(BeepType.IconAsterisk);
                                            moving = false;
                                        }
                                        else
                                        {
                                            if (!moving && beepOnStartStop)
                                                MessageBeep(BeepType.Ok);
                                            moving = true;
                                        }
                                        OldT = CurrentTimeSec;

                                        // Update max speed (in kmh)
                                        if (CurrentSpeed > MaxSpeed)
                                        {
                                            MaxSpeed = CurrentSpeed;
                                        }

                                        // compute distance
                                        Distance += Math.Sqrt((CurrentX - ReferenceXDist) * (CurrentX - ReferenceXDist) + (CurrentY - ReferenceYDist) * (CurrentY - ReferenceYDist));
                                        ReferenceXDist = CurrentX; ReferenceYDist = CurrentY;

                                        // compute elevation gain and min max
                                        if (positionAltitudeValid)
                                        {
                                            if (StartAlt == Int16.MinValue) StartAlt = CurrentAlt;

                                            if (CurrentAlt >= ReferenceAlt + AltThreshold)
                                            {
                                                ElevationGain += CurrentAlt - ReferenceAlt;
                                                ReferenceAlt = CurrentAlt;
                                            }
                                            else if (CurrentAlt < ReferenceAlt)
                                            {
                                                ReferenceAlt = CurrentAlt;
                                            }
                                            if (CurrentAlt > AltitudeMax) AltitudeMax = CurrentAlt;
                                            if (CurrentAlt < AltitudeMin) AltitudeMin = CurrentAlt;
                                        }

                                        // write battery info every 3 min
                                        WriteBatteryInfo();
                                        // if exclude stop time is activated, do not log stop time in file, and
                                        // do not include stop time in live logging.
                                        // Bugfix: even if not moving, the first call must be logged, otherwise
                                        // the start position is logged more than once (because PlotCount=0), which leads 
                                        // to an corrupt log file.
                                        //if (checkExStopTime.Checked == false || moving == true || PlotCount == 0)     //KB: removed this feature because of incorrect average speed and trip time when track loaded back.
                                        //{                                                                             //Also distance calculation differs slightly and incorrect speed graph.
                                                                                                                //to make it work: first and last point with v=0 must be logged (last cached and written right before point with v!=0). No distance accumulation when v=0 (slight change in behaviour).
                                        WriteRecord(CurrentX, CurrentY);
                                        AddPlotData((float)CurrentLat, (float)CurrentLong, (Int16)CurrentAlt, CurrentTimeSec, (Int16)(CurrentSpeed * 10.0), (Int16)(CurrentV * 10), (Int32)Distance);
                                        DoLapStats();
                                        DoLiveLogging();
                                        //}
                                    }// Logging
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
                                { MessageBeep(BeepType.IconExclamation); }
                            GpsDataState = GpsNotOk;
                            break;
                    }
                }

                CurrentStatusString = "GPS";
                if(comboGpsPoll.SelectedIndex >= IndexSuspendMode)     //always on
                    CurrentStatusString += "(" + PollGpsTimeSec[comboGpsPoll.SelectedIndex] + "s)";    //start/stop (suspend) mode
                CurrentStatusString += ": ";
                if (Logging) CurrentStatusString += "Logging ";
                else if (Paused) CurrentStatusString += "Paused ";
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
                        writer.Flush ();
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
                writer.Flush ();
            }
        }

        private void AddPlotData(float lat, float lng, Int16 z, Int32 t, Int16 s, Int16 v, Int32 d)
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
                        PlotV[i] = PlotV[i * 2];
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
                PlotV[PlotCount] = v;
                PlotD[PlotCount] = d;
                PlotCount++;
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
                x = (Byte) (StartTime.Year - 2000);
                writer.Write ((Byte) x);
                x = (Byte) StartTime.Month;
                writer.Write ((Byte) x);
                x = (Byte) StartTime.Day;
                writer.Write ((Byte) x);
                x = (Byte) StartTime.Hour;
                writer.Write ((Byte) x);
                x = (Byte) StartTime.Minute;
                writer.Write ((Byte) x);
                x = (Byte) StartTime.Second;
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
            if (PlotCount != 0)
            {
                TimeSpan maxAge = new TimeSpan(0, 3, 0); // 3 min
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
                    Logging = false;
                    Paused = false;
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
                writer.Flush ();
            }
        }

        // generate a new file name using StartTime (without path without extension)
        private void GenerateFileName()
        {
            DateTime start_time = DateTime.Now;

            // file name is constructed as year,month,day, hour, min, sec, all as 2-digit values
            string file_name = (start_time.Year - 2000).ToString("00")
                    + start_time.Month.ToString("00")
                    + start_time.Day.ToString("00")
                    + "_"
                    + start_time.Hour.ToString("00")
                    + start_time.Minute.ToString("00");

            CheckIoDirectoryExists();

            CurrentFileName = file_name;
        }

        // New Trace: open file, log start time, etc
        private void StartNewTrace()
        {
            StartTime = DateTime.Now;
            StartTimeUtc = DateTime.UtcNow;
            LastBatterySave = StartTimeUtc;
            LastLiveLogging = StartTimeUtc;

            // create writer and write header
            try
            {
                fstream = new FileStream(CurrentFileName, FileMode.Create);
                writer = new BinaryWriter(fstream, Encoding.ASCII);
                writer.Write((char)'G'); writer.Write((char)'C'); writer.Write((char)'C'); writer.Write((Byte)1);
            }
            catch (Exception e)
            {
                Utils.log.Error (" StartNewTrace - create writer and write header ", e);
            }
            finally
            {
                writer.Flush ();
            }

            ReferenceSet = false;
            if (GpsDataState > GpsBecameValid)         // let set new reference point
                GpsDataState = GpsBecameValid;

            OriginShiftX = 0.0;
            OriginShiftY = 0.0;

            MaxSpeed = 0.0;
            Distance = 0.0;
            CurrentTimeSec = 0;
            CurrentStoppageTimeSec = 0;
            //OldX = 0.0;
            //OldY = 0.0;
            OldT = 0;
            ElevationGain = 0.0;

            PlotCount = 0;
            Decimation = 1; DecimateCount = 0;
            CheckPointCount = 0;
            //FirstSampleValidCount = 1;

            LastPointUtc = DateTime.MinValue;
        }


        private void WriteCheckPoint(string name)
        {
            // store new checkpoint
            CheckPoints[CheckPointCount].name = name;
            CheckPoints[CheckPointCount].lat = (float)CurrentLat;
            CheckPoints[CheckPointCount].lon = (float)CurrentLong;
            if (CheckPointCount != 0)
            {
                CheckPoints[CheckPointCount].interval_time = CurrentTimeSec - CheckPoints[CheckPointCount].interval_time;
                CheckPoints[CheckPointCount].stoppage_time = CurrentStoppageTimeSec - CheckPoints[CheckPointCount].stoppage_time;
                CheckPoints[CheckPointCount].interval_distance = (float) (Distance - CheckPoints[CheckPointCount].interval_distance);
            }
            else
            {
                CheckPoints[CheckPointCount].interval_time = CurrentTimeSec;
                CheckPoints[CheckPointCount].stoppage_time = CurrentStoppageTimeSec;
                CheckPoints[CheckPointCount].interval_distance = (float) Distance;
            }

            if (CheckPointCount < (CheckPointDataSize - 1))
            {
                CheckPointCount++;
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
            {
                OpenGps();
            }
            else
            {
                CloseGps();
            }
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
                    KeepToolRunning(true);
                }
            }

            // warn if live logging is activated, and ask if want to proceed
            if (comboBoxCwLogMode.SelectedIndex != 0)
            {
                if (MessageBox.Show("Live logging is activated, proceed?", textBoxCwUrl.Text,
                                   MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation,
                                   MessageBoxDefaultButton.Button1) == DialogResult.No)
                {
                    return;
                }
            }
           
            // If a tracklog already exists, and the distance to the last log point is about max. 1km  
            // than ask, if the current track log should be continued.
            if (PlotCount > 0 && CurrentFileName != "" && 
                Math.Abs(PlotLat[PlotCount - 1] - CurrentLat) < 0.005F && Math.Abs(PlotLong[PlotCount - 1] - CurrentLong) < 0.005F)
            {
                if (MessageBox.Show("Do you wan´t to continue the track log into file:\n" + CurrentFileName, "Continue log file",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    buttonContinue_Click();
                    return;
                }
            }

            GenerateFileName();
            
            // check if we need to show the custom file name panel, or can start loging with default name
            if (checkEditFileName.Checked)
            {
                string value = CurrentFileName;
                if (Utils.InputBox(null, "Enter file name (without extension):", ref value) == DialogResult.OK)
                {
                    CurrentFileName = value;
                }
                else  // if cancel button is pressed, do not start logging.
                {
                    CurrentFileName = "";       // No valid file loaded (if continue button will be pressed later...)
                    return;
                }
            }

            CurrentFileName += ".gcc";
            labelFileName.SetText("Current File Name: " + CurrentFileName);

            //add path
            CurrentFileName = IoFilesDirectory + ((IoFilesDirectory == "\\") ? "" : "\\") + CurrentFileName;

            //Paused = false;         //todo necessary?

            StoppedOnLow = false;

            CurrentLiveLoggingString = "";

            StartNewTrace();

            OpenGps();          //todo error?
            Logging = true;
            MenuExec(MenuPage.BFkt.main);
        }

        private void buttonStop_Click()
        {
            Logging = false;
            Paused = false;
            mPage.mBAr[(int)MenuPage.BFkt.pause] = mPage.mBPause;

            try
            {
                writer.Close ();
                fstream.Close ();
            }
            catch (Exception e)
            {
                Utils.log.Error (" buttonStop_Click - writer close ", e);
            }
            //comboGpsPoll.Enabled = true;
            GpsSearchCount = 0;
            CurrentLiveLoggingString = "";

            // reset move/zoom vars (as we switch from fixed zoom into auto zoom mode)
            ResetMapPosition();

            Cursor.Current = Cursors.WaitCursor;
            CloseGps();
            if (gps.OpenedOrSuspended)
                mPage.mBAr[(int)MenuPage.BFkt.gps_toggle] = mPage.mBGpsOn;
            else
                mPage.mBAr[(int)MenuPage.BFkt.gps_toggle] = mPage.mBGpsOff;
            showButton(button1, MenuPage.BFkt.mPage);
            showButton(button2, MenuPage.BFkt.start);
            showButton(button3, MenuPage.BFkt.gps_toggle);

            labelFileName.SetText("");

            // Delete log files, if no records
            if (PlotCount == 0)
            {
                try
                {
                    File.Delete(CurrentFileName);
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

            Cursor.Current = Cursors.Default;
            NoBkPanel.Invalidate();
        }

        void buttonPause_Click()
        {
            if (Paused)
            {
                OpenGps();
                ContinueAfterPause = true;
                Logging = true;
                Paused = false;
            }
            else if(Logging)
            {
                Logging = false;
                Paused = true;
            }
            MenuExec(MenuPage.BFkt.main);       //show the appropriate buttons
        }

        void buttonContinue_Click()
        {
            if (Logging)
            {
                MessageBox.Show("Logging is already active!");
                return;
            }
            if (Paused)
            {
                if (MessageBox.Show("Active logging is paused.\nDo you want to 'unpause' to continue?", null, MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button1)
                    == DialogResult.Yes)
                { MenuExec(MenuPage.BFkt.pause); }
                return;
            }
            if (PlotCount == 0 || CurrentFileName == "")
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
            ReferenceSet = true;

            // create writer
            try
            {
                fstream = new FileStream(CurrentFileName, FileMode.Append);
                writer = new BinaryWriter(fstream, Encoding.ASCII);
            }
            catch (Exception e)
            {
                Utils.log.Error(" ContinueTrace - append writer ", e);
            }

            OpenGps();          //todo error?
            ContinueAfterPause = true;
            Logging = true;
            MenuExec(MenuPage.BFkt.main);
        }


        // utils to fill gcc file names into the "listBoxFiles", indicating if KML/GPX exist
        private void FillFileNames()
        {
            Cursor.Current = Cursors.WaitCursor;
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
            Cursor.Current = Cursors.Default;
        }
        // read file
        private void buttonLoad_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeFiledialog;
            if (Logging || Paused)
            {
                if (MessageBox.Show("Can't open file while logging.\nStop active Logging and proceed?", null, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1)
                    == DialogResult.Yes)
                { MenuExec(MenuPage.BFkt.stop); }
                else
                { return; }
            }

            FolderSetupMode = false;
            FileOpenMode = FileOpenMode_Gcc;

            listBoxFiles.Items.Clear();
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

        int BFktSave = -1;
        void button1_MouseDown(object sender, MouseEventArgs e)
        {
            timerButton.Interval = 500;
            timerButton.Enabled = true;
        }
        void button1_MouseUp(object sender, MouseEventArgs e)
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
            /*if (!mPage.mBAr[(int)fkt].enabled)
            {
                MessageBeep(BeepType.IconHand);
                return;
            }*/
            switch (fkt)
            {
                case MenuPage.BFkt.main:
                    buttonMain_Click(null, null); break;
                case MenuPage.BFkt.start:
                    buttonStart_Click(); goto case MenuPage.BFkt.main;
                case MenuPage.BFkt.stop:
                    buttonStop_Click(); goto case MenuPage.BFkt.main;
                case MenuPage.BFkt.map:
                    buttonMap_Click(null, null); break;
                case MenuPage.BFkt.pause:
                    buttonPause_Click();
                    break;
                case MenuPage.BFkt.checkpoint:
                    if (Logging || Paused)
                    {
                        string checkpoint = "";
                        DialogResult Result = Utils.InputBox(null, "Enter waypoint name", ref checkpoint);
                        if (Result == DialogResult.OK)
                            WriteCheckPoint(checkpoint);
                    }
                    else MessageBox.Show("Can't add a waypoint when logging is not activated");
                    goto case MenuPage.BFkt.main;

                case MenuPage.BFkt.options:
                    buttonOptions_Click(null, null); break;
                case MenuPage.BFkt.graph_alt:
                    if (BufferDrawMode == BufferDrawModeGraph)
                        GraphDrawSpeed = !GraphDrawSpeed;
                    else
                        GraphDrawSpeed = false;
                    buttonGraph_Click(null, null); break;
                case MenuPage.BFkt.graph_speed:
                    GraphDrawSpeed = true;
                    buttonGraph_Click(null, null); break;

                case MenuPage.BFkt.load_gcc:
                    buttonLoad_Click(null, null); break;
                case MenuPage.BFkt.load_2follow:
                    buttonLoadTrack2Follow_Click(null, null); break;
                case MenuPage.BFkt.load_2clear:
                    buttonLoad2Clear_Click(null, null); break;

                case MenuPage.BFkt.exit:
                    BufferDrawMode = BufferDrawModeMain;    //to avoid exception in mPage OnMouseUp()
                    this.Close();
                    Application.Exit(); break;
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
                    Utils.SwitchBacklight(); break;
                case MenuPage.BFkt.inputLatLon:
                    string LatLon = Lat2String(CurrentLat, false) + "; " + Lat2String(CurrentLong, true);
                    retry:
                    if (Utils.InputBox("Input", "Lat; Lon (separated with semicolon)", ref LatLon) == DialogResult.OK)
                    {
                        int i=0;
                        double Lat, Lon;
                        if (LatString2Double(LatLon, ref i, out Lat) && LatString2Double(LatLon, ref i, out Lon))
                        {
                            // The current Lat/Long values will only be usefull, if GPS is switched off.
                            // If GPS is on, the values are directly overwritten, and will distort an active log
                            if (!Logging)
                            {
                                CurrentLat = Lat;
                                CurrentLong = Lon;
                            }
                            // If a track to follow consists of more than 1 point, (one point is used for this button...)
                            // ask, if the user wants to replace the loaded track to follow 
                            if (Counter2nd > 1)
                            {
                                if (MessageBox.Show("Do you want to replace loaded track to follow with the new Lat/Long values?",
                                    "Overwrite Track2Follow", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button1)
                                    == DialogResult.No)
                                {
                                    return;
                                }
                            }
                            // Replace the existing track2follow with the new coordinates
                            Plot2ndLat[0] = (float)Lat;
                            Plot2ndLong[0] = (float)Lon;
                            Counter2nd = 1;
                            // And Jump in the Map screen directly to the track to follow start position
                            ResetMapPosition();
                            mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FStart;
                            // Jump Directly to the Map view
                            buttonMap_Click(null, null);
                        }
                        else goto retry;

                    }
                    break;

                case MenuPage.BFkt.help:
                    buttonHelp_Click(null, null); break;

                case MenuPage.BFkt.gps_toggle:
                    buttonGPS_Click(null, null); break;

                case MenuPage.BFkt.options_next:
                    buttonOptionsNext_Click(null, null); break;
                case MenuPage.BFkt.options_prev:
                    buttonOptionsPrev_Click(null, null); break;
                case MenuPage.BFkt.map_zoomIn:
                    buttonZoomIn_Click(null, null); break;
                case MenuPage.BFkt.map_zoomOut:
                    buttonZoomOut_Click(null, null); break;
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
                    BufferDrawMode = BufferDrawModeMenu;
                    mPage.BringToFront();
                    mPage.Invalidate();
                    showButton(button1, MenuPage.BFkt.main);
                    if(mPage.UpPossible)
                        showButton(button2, MenuPage.BFkt.mPageUp);
                    else
                        showButton(button2, MenuPage.BFkt.nothing);
                    if(mPage.DownPossible)
                        showButton(button3, MenuPage.BFkt.mPageDown);
                    else
                        showButton(button3, MenuPage.BFkt.nothing);
                    break;
                case MenuPage.BFkt.mPageUp:
                    mPage.MenuUp(); goto case MenuPage.BFkt.mPage;
                case MenuPage.BFkt.mPageDown:
                    mPage.MenuDown(); goto case MenuPage.BFkt.mPage;

                case MenuPage.BFkt.nothing:
                    break;

                default:
                    MessageBox.Show(fkt.ToString());
                    break;
            }
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
            string filename = CurrentDirectory + "\\" + name + ".dat";
            if (File.Exists(filename))
            {
                LoadSettings(filename);
                mPage.Invalidate();         //refresh ev. changed recall names
                MessageBeep(BeepType.Ok);
                // If a changed maps or Input/Output location is displayed, it may become invalid after a recall.
                // To avoid displaying an invalid path, overwrite the existing text.
                labelFileName.SetText("Recall settings: " + name);
                LoadedSettingsName = name;
            }
            else
                MessageBox.Show("File does not exist:\n" + filename, "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
        }

        private bool LoadGcc(string filename)
        {
            // reset vars for computation
            PlotCount = 0;
            Decimation = 1; DecimateCount = 0;
            CheckPointCount = 0;
            MaxSpeed = 0.0; Distance = 0.0; CurrentStoppageTimeSec = 0;
            OldX = 0.0; OldY = 0.0; OldT = 0;
            OriginShiftX = 0.0; OriginShiftY = 0.0;
            ElevationGain = 0.0; ReferenceAlt = Int16.MaxValue;
            AltitudeMax = Int16.MinValue;
            AltitudeMin = Int16.MaxValue;

            int gps_poll_sec = 0;

            // preset label text for errors
            //CurrentStatusString = "File has errors or blank";
            int loadOK = -1;
            CurrentFileName = "";

            Cursor.Current = Cursors.WaitCursor;

            FileStream fs = null;
            BinaryReader rd = null;
            do
            {
                try
                {
                    fs = new FileStream(filename, FileMode.Open);
                    rd = new BinaryReader(fs, Encoding.ASCII);

                    // load header "GCC1" (1 is version (binary!))
                    if (rd.ReadChar() != 'G') break; if (rd.ReadChar() != 'C') break;
                    if (rd.ReadChar() != 'C') break; if (rd.ReadChar() != 1) break;

                    // read time as 6 bytes: year, month...
                    int t1 = (int)rd.ReadByte(); t1 += 2000;
                    int t2 = (int)rd.ReadByte(); int t3 = (int)rd.ReadByte();
                    int t4 = (int)rd.ReadByte(); int t5 = (int)rd.ReadByte();
                    int t6 = (int)rd.ReadByte();
                    StartTime = new DateTime(t1, t2, t3, t4, t5, t6);
                    StartTimeUtc = StartTime.ToUniversalTime();

                    // read lat/long
                    StartLat = rd.ReadDouble(); StartLong = rd.ReadDouble();
                    utmUtil.setReferencePoint(StartLat, StartLong);
                    ReferenceSet = true;
                    StartAlt = Int16.MinValue;

                    bool is_battery_printed = false;

                    Int16 x_int = 0; Int16 y_int = 0; Int16 z_int = 0; Int16 s_int = 0;
                    UInt16 t_16 = 0; UInt16 t_16last = 0; Int32 t_high = 0;
                    double out_lat = 0.0, out_long = 0.0;

                    while (true)    //break with EndOfStreamException
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
                        // battery: z_int = 1
                        if ((s_int == -1) && (t_16 == 0xFFFF) && (z_int == 1))
                        {
                            if (is_battery_printed == false)
                            {
                                StartBattery = x_int;
                                is_battery_printed = true;
                            }
                            CurrentBattery = x_int;
                        }
                        // origin shift: z_int = 0
                        else if ((s_int == -1) && (t_16 == 0xFFFF) && (z_int == 0))
                        {
                            OriginShiftX += x_int;
                            OriginShiftY += y_int;
                        }
                        // which GPS options were selected
                        else if ((s_int == -1) && (t_16 == 0xFFFF) && (z_int == 2))
                        {
                            gps_poll_sec = x_int;
                        }
                        // checkpoint
                        else if ((s_int == -1) && (t_16 == 0xFFFF) && (z_int == 3))
                        {
                            // read checkpoint name, if not blank
                            string name = "";
                            for (int i = 0; i < x_int; i++)
                            {
                                name += (char)(rd.ReadUInt16());
                            }

                            // store new checkpoint
                            CheckPoints[CheckPointCount].name = name;
                            CheckPoints[CheckPointCount].lat = (float)CurrentLat;
                            CheckPoints[CheckPointCount].lon = (float)CurrentLong;
                            if (CheckPointCount != 0)
                            {
                                CheckPoints[CheckPointCount].interval_time = CurrentTimeSec - CheckPoints[CheckPointCount].interval_time;
                                CheckPoints[CheckPointCount].stoppage_time = CurrentStoppageTimeSec - CheckPoints[CheckPointCount].stoppage_time;
                                CheckPoints[CheckPointCount].interval_distance = (float)(Distance - CheckPoints[CheckPointCount].interval_distance);
                            }
                            else
                            {
                                CheckPoints[CheckPointCount].interval_time = CurrentTimeSec;
                                CheckPoints[CheckPointCount].stoppage_time = CurrentStoppageTimeSec;
                                CheckPoints[CheckPointCount].interval_distance = (float)Distance;
                            }

                            if (CheckPointCount < (CheckPointDataSize - 1))
                            {
                                CheckPointCount++;
                            }
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

                            // handle overflow in time
                            if (t_16 < t_16last)
                                t_high += 65536;
                            t_16last = t_16;
                            CurrentTimeSec = t_high + t_16;

                            // compute Stoppage time
                            if (s_int <= 0) { CurrentStoppageTimeSec += CurrentTimeSec - OldT; }
                            OldT = CurrentTimeSec;

                            // update max speed
                            if (s_int * 0.1 > MaxSpeed) { MaxSpeed = s_int * 0.1; }

                            // compute elevation gain
                            if (z_int != Int16.MinValue)        //MinValue = invalid
                            {
                                if (StartAlt == Int16.MinValue) StartAlt = z_int;

                                if (z_int >= ReferenceAlt + AltThreshold)
                                {
                                    ElevationGain += z_int - ReferenceAlt;
                                    ReferenceAlt = z_int;
                                }
                                else if (z_int < ReferenceAlt)
                                {
                                    ReferenceAlt = z_int;
                                }
                                if (z_int > AltitudeMax) AltitudeMax = z_int;
                                if (z_int < AltitudeMin) AltitudeMin = z_int;
                            }

                            // convert to lat/long, used in plot arrays
                            utmUtil.getLatLong(real_x, real_y, out out_lat, out out_long);

                            // store data in plot array
                            AddPlotData((float)out_lat, (float)out_long, z_int, CurrentTimeSec, s_int, 0, (Int32)Distance);   //Todo velocity!

                            // store point (used to update checkpoint data
                            CurrentLat = out_lat;
                            CurrentLong = out_long;
                            CurrentAlt = z_int;
                            CurrentSpeed = s_int * 0.1;
                            /*
                            try             //what was this for?
                            {
                                rd.PeekChar ();
                            }
                            catch (Exception e)
                            {
                                Utils.log.Error (" LoadGcc - PeekChar ", e);
                                break;
                            }*/
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
                MessageBox.Show("File has errors or blank", "Error");
                return false;
            }
            if (loadOK == 0) MessageBox.Show("File has been closed incorrectly\nDo not continue this file!", "Comment");

            labelFileName.SetText("Current File Name: " + Path.GetFileName(filename));
            CurrentFileName = filename;
            return true;
        }

        public enum BeepType
        {
            SimpleBeep = -1,
            IconAsterisk = 0x00000040,
            IconExclamation = 0x00000030,
            IconHand = 0x00000010,
            IconQuestion = 0x00000020,
            Ok = 0x00000000,
        }

        [DllImport("COREDLL.DLL")]
        public static extern bool MessageBeep(BeepType beepType);


        // reset Idle Timer (to stop phone switching off)
        private void timerIdleReset_Tick(object sender, EventArgs e)
        {
            //MessageBeep(BeepType.SimpleBeep);
            
            string ps = new string('\0', 12);
            uint pflag = 0;
            GetSystemPowerState(ps, 12, ref pflag);
            
            //if (ps.StartsWith("on"))
            if ((pflag & 0x00010000) > 0 || ps.StartsWith("on"))
            {
                PowerPolicyNotify(PPN_APPBUTTONPRESSED, 0);   //KB; informs PowerManager PPN_APPBUTTONPRESSED; would switch backlight on (if manual off)
            }
            SystemIdleTimerReset();         //only functions on WM5.0?
        }

        
        // start/stop GPS
        private void timerGps_Tick(object sender, EventArgs e)
        {
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
                else if (GpsSearchCount > 180)
                {
                    SuspendGps(); // first we close it
                    GpsSuspendCounter = 0;  //let it start again at the next tick
                }
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
            }
            CurrentBattery = Utils.GetBatteryStatus();

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

            //test
            //Utils.log.Debug("debugtext");
            //System.Diagnostics.Debug.WriteLineIf(true, "test");

            //dwStartTick = Environment.TickCount;
            //dwIdleSt = GetIdleTime();
            //  You must insert a call to the Sleep(sleep_time) function to allow
            //  idle time to accrue. An example of an appropriate sleep time is
            //  1000 ms.
            //dwStopTick = GetTickCount();
            int it = GetIdleTime();
            //PercentIdle = ((100 * (dwIdleEd - dwIdleSt)) / (dwStopTick - dwStartTick));

            

            //Process proc = Process.GetCurrentProcess();

            //IntPtr phandle = OpenThread(0x400, false, proc.Id);
            //long cr, end, kt, ut;
            //GetThreadTimes(phandle, out cr, out end, out kt, out ut);
            //debugStr = (Environment.TickCount).ToString();
            idleTime = it;
        }
        int idleTime = 0;

        [DllImport("coredll.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern int GetIdleTime();

        [DllImport("coredll.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr OpenThread(int access, bool inherit, int ID);

        [DllImport("coredll.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GetThreadTimes(IntPtr handle, out long creation, out long exit, out long kernel, out long user);


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
                KeepToolRunning(true);
            }
            // Keep backlight on, if Backlight Energy safe option is not set.
            if (checkKeepBackLightOn.Checked == false)
            {
                timerIdleReset.Enabled = true;
            }
            goodsamples = 0; //debug
            numsamples = 0; //debug
            return ret;
        }

        void CloseGps()
        {
            gps.Close();
            GpsDataState = GpsNotOk;
            // buttonGPS.Text = "GPS is off";        //todo
            KeepToolRunning(false);
            // Allow backlight to switch off after timeout
            timerIdleReset.Enabled = false;
        }

        void SuspendGps()
        {
            gps.Suspend();
            //KeepToolRunning(false);
        }

        IntPtr gpsPowerHandle1 = IntPtr.Zero;
        /*IntPtr gpsPowerHandle2 = IntPtr.Zero;
        IntPtr gpsPowerHandle3 = IntPtr.Zero;
        IntPtr gpsPowerHandle4 = IntPtr.Zero;
        IntPtr gpsPowerHandle5 = IntPtr.Zero;*/

        // KeepToolRunning changes the settings of the power manager, to keep the application running, even
        // if the device is switched off via the power button
        void KeepToolRunning(bool run)
        {
            if( run )
            {
                // this is what you need to keep tool running if power turned off
                if (true) //checkShowBkOff.Checked == false)
                {
                    try
                    {
                        PowerPolicyNotify(PPN_UNATTENDEDMODE, 1);
                        if (checkkeepAliveReg.Checked)
                        {
                            RegistryKey rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Unattended", true);
                            rk.SetValue("gpd0:", 0, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                            rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Resuming", true);
                            rk.SetValue("gpd0:", 0, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                            rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Suspend", true);
                            rk.SetValue("gpd0:", 0, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                        }
                        else
                        {
                            gpsPowerHandle1 = SetPowerRequirement("GPD0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                        }



                  /*      switch ((int)numericAvg.Value)
                        {
                            case 1:
                                //problem HTC Diamond HD2 (hardware power button disables gps)
                                //gpsPowerHandle1 = SetPowerRequirement("gps0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                break;
                            case 2:
                                gpsPowerHandle2 = SetPowerRequirement("GPD0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                break;
                            case 3:
                                gpsPowerHandle1 = SetPowerRequirement("gps0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle2 = SetPowerRequirement("gpd0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                break;
                            case 4:
                                gpsPowerHandle3 = SetPowerRequirement("NAV1:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                break;
                            case 5:
                                gpsPowerHandle1 = SetPowerRequirement("gps0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle2 = SetPowerRequirement("gpd0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle3 = SetPowerRequirement("NAV1:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                break;
                            case 6:
                                gpsPowerHandle1 = SetPowerRequirement("gps0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle2 = SetPowerRequirement("gpd0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle3 = SetPowerRequirement("NAV1:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle4 = SetPowerRequirement("COM4:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                break;
                            case 7:
                                gpsPowerHandle1 = SetPowerRequirement("gps0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle2 = SetPowerRequirement("gpd0:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle3 = SetPowerRequirement("NAV1:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle4 = SetPowerRequirement("COM4:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                gpsPowerHandle5 = SetPowerRequirement("COM1:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);
                                break;
                            case 10:
                                // need to update registry settings as well to keep GPS on
                                RegistryKey rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Unattended", true);
                                object tmp_obj = rk.GetValue("gpd0:");
                                if (tmp_obj != null) { SaveGpdUnattendedValue = (Int32)tmp_obj; }
                                else { SaveGpdUnattendedValue = 4; } // default is 4
                                rk.SetValue("gpd0:", 0, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                                break;
                            case 11:
                                rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Unattended", true);
                                rk.SetValue("gpd0:", 0, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                                rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Resuming", true);
                                rk.SetValue("gpd0:", 0, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                                rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Suspend", true);
                                rk.SetValue("gpd0:", 0, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                                break;
                        }*/

                        //bklightPowerHandle = SetPowerRequirement("BKL1:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, "D4", 0);   //bklight goes off, pbtn function
                        //bklightPowerHandle = SetPowerRequirement("BKL1:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, "D0", 0);   //bklight goes off, pbtn function
                        //bklightPowerHandle = SetPowerRequirement("BKL1:", CedevicePowerState.D0, POWER_NAME | POWER_FORCE, null, 0);   //bklight stays on, pbtn no function
                    }
                    catch (Exception e)
                    {
                        Utils.log.Error(" KeepToolRunning 1 ", e);
                    }
                }
            }
            else
            {
                if (true) //checkShowBkOff.Checked == false)
                {
                    try
                    {
                        PowerPolicyNotify(PPN_UNATTENDEDMODE, 0);
                        if (checkkeepAliveReg.Checked)
                        {
                            RegistryKey rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Unattended", true);
                            rk.SetValue("gpd0:", 4, RegistryValueKind.DWord);    // set to 4, i.e. GPS is OFF
                            rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Resuming", true);
                            rk.SetValue("gpd0:", 4, RegistryValueKind.DWord);    // set to 4, i.e. GPS is OFF
                            rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Suspend", true);
                            rk.SetValue("gpd0:", 4, RegistryValueKind.DWord);    // set to 4, i.e. GPS is OFF
                        }
                        else
                        {
                            ReleasePowerRequirement(gpsPowerHandle1);
                        }




                    /*    switch ((int)numericAvg.Value)
                        {
                            case 10:
                                // need to restore registry settings
                                RegistryKey rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Unattended", true);
                                rk.SetValue("gpd0:", SaveGpdUnattendedValue, RegistryValueKind.DWord);
                                break;
                            case 11:
                                rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Unattended", true);
                                rk.SetValue("gpd0:", 4, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                                rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Resuming", true);
                                rk.SetValue("gpd0:", 4, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                                rk = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Power\\State\\Suspend", true);
                                rk.SetValue("gpd0:", 4, RegistryValueKind.DWord);    // set to 0, i.e. GPS is ON
                                break;

                            default:
                                ReleasePowerRequirement(gpsPowerHandle1);
                                ReleasePowerRequirement(gpsPowerHandle2);
                                ReleasePowerRequirement(gpsPowerHandle3);
                                ReleasePowerRequirement(gpsPowerHandle4);
                                ReleasePowerRequirement(gpsPowerHandle5);
                                break;
                        }*/
                        //ReleasePowerRequirement(bklightPowerHandle);
                    }
                    catch (Exception e)
                    {
                        Utils.log.Error(" KeepToolRunning 0 ", e);
                    }
                }
            }
        }

        #region PInvokes to coredll.dll

        [DllImport("coredll.dll")]
        static extern void SystemIdleTimerReset();

        [DllImport("coredll.dll")]
        static extern void PowerPolicyNotify(UInt32 powermode, UInt32 flags);

        [DllImport("coredll.dll")]
        public static extern int GetSystemPowerState(string sb, uint length, ref uint flags);

        [DllImport("coredll.dll", SetLastError = true)]
        public static extern IntPtr SetPowerRequirement(string pvDevice, CedevicePowerState deviceState, uint deviceFlags, string pvSystemState, ulong stateFlags);

        [DllImport("coredll.dll", SetLastError = true)]
        public static extern int ReleasePowerRequirement(IntPtr hPowerReq);

        public const int PPN_UNATTENDEDMODE = 0x0003;
        public const int PPN_APPBUTTONPRESSED = 0x0006;
        public const int POWER_NAME = 0x00000001;
        public const int POWER_FORCE = 0x00001000;

        public enum CedevicePowerState : int
        {
            PwrDeviceUnspecified = -1,
            D0 = 0,
            D1,
            D2,
            D3,
            D4,
        }




        #endregion

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
                    MessageBox.Show("File has errors or blank", "Error loading .gcc file",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
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
            return Path.GetFileNameWithoutExtension(CurrentFileName) + extension;
        }

        private void buttonSaveKML_Click(object sender, EventArgs e)
        {
            String kml_file = getFileName(".kml");
            if ("".Equals(kml_file)) return;

            Cursor.Current = Cursors.WaitCursor;

            if (IoFilesDirectory == "\\") { kml_file = "\\" + kml_file; }
            else { kml_file = IoFilesDirectory + "\\" + kml_file; }

            string distUnit;
            string speedUnit;
            string altUnit;
            string exstop_info;
            GetUnitLabels(out distUnit, out speedUnit, out altUnit, out exstop_info);
            string dist, speedCur, speed_avg, speedMax, run_time_label, last_sample_time, altitude, battery;
            GetValuesToDisplay(out dist, out speedCur, out speed_avg, out speedMax, out run_time_label, out last_sample_time, out altitude, out battery);

            new KmlSupport().Write(kml_file, CheckPointCount, CheckPoints,
                PlotLat, PlotLong, PlotCount, PlotS, PlotT, PlotZ, StartTime,
                comboBoxKmlOptColor, checkKmlAlt,
                distUnit, speedUnit, altUnit, exstop_info,
                dist, speedCur, speed_avg, speedMax, run_time_label, last_sample_time, altitude, battery,
                StartLat, StartLong,
                comboBoxKmlOptWidth);
            
            Cursor.Current = Cursors.Default;

            // refill listBox, to indicate that KML was saved
            int selected_index = listBoxFiles.SelectedIndex;
            listBoxFiles.Items.Clear();
            FillFileNames();
            if (listBoxFiles.Items.Count != 0) { listBoxFiles.SelectedIndex = selected_index; }
        }
        private void buttonSaveGPX_Click(object sender, EventArgs e)
        {
            String gpx_file = getFileName(".gpx");
            if("".Equals(gpx_file)) return;

            Cursor.Current = Cursors.WaitCursor;
           
            if (IoFilesDirectory == "\\") { gpx_file = "\\" + gpx_file; }
            else { gpx_file = IoFilesDirectory + "\\" + gpx_file; }

            string dist_unit, speed_unit, alt_unit, exstop_info;
            GetUnitLabels(out dist_unit, out speed_unit, out alt_unit, out exstop_info);
            string dist, speed_cur, speed_avg, speed_max, run_time_label, last_sample_time, altitude, battery;
            GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time_label, out last_sample_time, out altitude, out battery);

            new GpxSupport().Write(gpx_file, CheckPointCount, 
                CheckPoints,
                checkGpxRte, checkGpxSpeedMs,
                PlotLat, PlotLong, PlotCount,
                PlotS, PlotT, PlotZ, StartTime,
                numericGpxTimeShift,
                dist_unit, speed_unit, alt_unit, exstop_info,
                dist, speed_cur, speed_avg, speed_max, run_time_label, last_sample_time, altitude, battery);
            
            Cursor.Current = Cursors.Default;

            // refill listBox, to indicate that GPX was saved
            int selected_index = listBoxFiles.SelectedIndex;
            listBoxFiles.Items.Clear();
            FillFileNames();
            if (listBoxFiles.Items.Count != 0) { listBoxFiles.SelectedIndex = selected_index; }


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

            string log_file = "log.csv";

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

                    wr.WriteLine("File info,,,,,,Start,Stop,Distance (" + dist_unit +
                                "),Start position, End position,Total time," + speed_unit);
                }

                TimeSpan run_time = new TimeSpan(0, 0, PlotT[PlotCount - 1]);

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
                    StartLat.ToString("0.##########", IC) + "  " + StartLong.ToString("0.##########", IC) + "," +
                    CurrentLat.ToString("0.##########", IC) + "  " + CurrentLong.ToString("0.##########", IC) + "," +
                    run_time_str + "," +
                    speed_cur + " " + speed_avg + " " + speed_max
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
        }

        private void numericZoomRadiusChanged(object sender, EventArgs e)
        {
            mapUtil.DefaultZoomRadius = (int)numericZoomRadius.Value;
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
            if (x >= 100.0) { return x.ToString("0.0"); }
            return x.ToString("0.00");
        }
        private string PrintSpeed(double x)
        {
            if (x >= 100.0) { return x.ToString("0."); }
            return x.ToString("0.0");
        }
        private void GetValuesToDisplay(out string dist, out string speed_cur, out string speed_avg, out string speed_max, out string run_time, out string last_sample_time, out string altitude_str, out string battery)
        {
            // battery current and estimation
            if (CurrentBattery <= -255) { battery = "??% "; }
            else if (CurrentBattery < 0) { battery = "AC " + (-CurrentBattery).ToString() + "% "; }
            else { battery = CurrentBattery.ToString() + "% "; }

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
            //last_sample_time = (StartTime + ts_all).ToString("T");
            if (LastPointUtc != DateTime.MinValue)
                last_sample_time = LastPointUtc.ToLocalTime().ToString("T");
            else
                last_sample_time = "";

            dist = PrintDist(Distance * 0.001 * ceff);

            double altitude = CurrentAlt;
            // relative altitude mode
            if (checkRelativeAlt.Checked) { altitude -= PlotZ[0]; }

            if ((comboUnits.SelectedIndex == 3) || (comboUnits.SelectedIndex == 5) || (comboUnits.SelectedIndex == 6))
                { m2feet = 1.0 / 0.30480; }    // altitude in feet
            else
                { m2feet = 1.0; }

            if (CurrentAlt == Int16.MinValue)
                altitude_str = "---";
            else
                altitude_str = (altitude * m2feet).ToString("0.0");

            double averageSpeed = (time_to_use == 0) ? 0.0 : (Distance * 3.6 / time_to_use);

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

        float df = 1.0f;
        private void DrawMain(Graphics g)           // draw main screen with watch, speed, distance, altitute...
        {
            BackBufferGraphics.Clear(bkColor);

            Pen p = new Pen(GetAverageColor(), 1);
               
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
                if (checkRelativeAlt.Checked) { altitude_mode += " diff"; }
            }
            else
            {
                altitude_mode = "Lap";      // in Lap mode use Altitude field to display Lap values current and last
                if (comboLapOptions.SelectedIndex <= 6) { alt_unit = "min:s"; }
                else { alt_unit = "km"; }
            }

            string[] speed_info = new string[3] { "cur gps", "cur pos", "cur" };
            string[] dist_info = new string[3] {"trip", "t2f st", "t2f end" };
            DrawMainLabelAndUnits(BackBufferGraphics, "Time",     "h:m:s",    MGridX[0], MGridY[0]);
            DrawMainLabelAndUnits(BackBufferGraphics, "Speed",    speed_unit, MGridX[0], MGridY[1]);
            DrawMainLabelAndUnits(BackBufferGraphics, "Distance", dist_unit,  MGridX[0], MGridY[3]);
            DrawMainLabelAndUnits(BackBufferGraphics, "Info",     "",         MGridX[0], MGridY[5]);
            DrawMainLabelAndUnits(BackBufferGraphics, altitude_mode, alt_unit, MGridX[1], MGridY[3]);
            DrawMainLabelAndUnits(BackBufferGraphics, "GPS",      "",         MGridX[1], MGridY[5]);
            DrawMainLabelOnRight(BackBufferGraphics, exstop_info, MGridX[2], MGridY[0], 9.0f);
            DrawMainLabelOnRight(BackBufferGraphics, speed_info[MainConfigSpeedSource], MGridX[1], MGridY[1], 9.0f);
            DrawMainLabelOnRight(BackBufferGraphics, dist_info[Counter2nd == 0 ? (int)eConfigDistance.eDistanceTrip : (int)MainConfigDistance], MGridX[1], MGridY[3], 9.0f);
            DrawMainLabelOnRight(BackBufferGraphics, "avg", MGridX[3], MGridY[1], 9.0f);
            DrawMainLabelOnRight(BackBufferGraphics, "max", MGridX[3], MGridY[2], 9.0f);
            DrawMainLabelOnRight(BackBufferGraphics, "cur", MGridX[3], MGridY[3] + MHeightDelta, 9.0f);
            string label1;
            if (comboLapOptions.SelectedIndex == 0)
            {
                if (MainConfigAlt2display == 0) label1 = "gain";
                else if (MainConfigAlt2display == 1) label1 = "loss";
                else if (MainConfigAlt2display == 2) label1 = "max";
                else if (MainConfigAlt2display == 3) label1 = "min";
                else label1 = "slope";
                DrawMainLabelOnRight(BackBufferGraphics, label1, MGridX[3], MGridY[4], 9.0f);
            }
                        
            // draw the values
            string dist, speed_cur, speed_avg, speed_max, run_time, last_sample_time, altitude, battery;
            GetValuesToDisplay(out dist, out speed_cur, out speed_avg, out speed_max, out run_time, out last_sample_time, out altitude, out battery);
            DrawMainValues(BackBufferGraphics, run_time, (MGridX[0] + MGridX[2]) / 2, MGridY[1], 32.0f * df);
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
            else
                DrawMainValues(BackBufferGraphics, speed_cur, (MGridX[0] + MGridX[1]) / 2, MGridY[3], 30.0f * df);

            if (MainConfigDistance != eConfigDistance.eDistanceTrip && Counter2nd != 0)
            {
                double xCurrent = 0, yCurrent = 0, xTrack, yTrack;
                // If we have no valid GPS connection, and no current gcc file loaded, the reference point is not 
                // set. But if the reference point is set, we are not allowed to change it.
                if (ReferenceSet == false)
                {
                    utmUtil.setReferencePoint(CurrentLat, CurrentLong);
                    //ReferenceSet = true;      ReferencePoint only temporarily necessary; better not fix it to an old CurrentLatLong
                }
                else // reference point ist set
                {
                    utmUtil.getXY(CurrentLat, CurrentLong, out xCurrent, out yCurrent);
                }
                if (MainConfigDistance == eConfigDistance.eDistanceTrack2FollowStart)    // show distance to track2follow start
                {
                    utmUtil.getXY(Plot2ndLat[0], Plot2ndLong[0], out xTrack, out yTrack);
                }
                else  // show Distance to track2follow end
                {
                    utmUtil.getXY(Plot2ndLat[Counter2nd - 1], Plot2ndLong[Counter2nd - 1], out xTrack, out yTrack);
                }
                // Calculate the distance between track and current position
                double deltaS = Math.Sqrt( (xTrack - xCurrent) * (xTrack - xCurrent) + (yTrack - yCurrent) * (yTrack - yCurrent));
                dist = PrintDist(deltaS * GetUnitsConversionCff());
            }    
            DrawMainValues(BackBufferGraphics, dist, (MGridX[0] + MGridX[1]) / 2, MGridY[5], 26.0f * df);
            DrawMainValues(BackBufferGraphics, speed_avg, (MGridX[1] + MGridX[3]) / 2, MGridY[2], 20.0f * df);
            DrawMainValues(BackBufferGraphics, speed_max, (MGridX[1] + MGridX[3]) / 2, MGridY[3], 20.0f * df);

            string altitude2;
            if(MainConfigAlt2display == 0)            //gain
                altitude2 = (ElevationGain * m2feet).ToString("0.0");
            else if (MainConfigAlt2display == 1)        //loss
            {
                double ElevationLoss = 0.0;
                if (StartAlt != Int16.MinValue) ElevationLoss = CurrentAlt - StartAlt - ElevationGain;
                altitude2 = (ElevationLoss * m2feet).ToString("0.0");
            }
            else if (MainConfigAlt2display == 2)        //max
            {
                if (AltitudeMax != Int16.MinValue)
                    altitude2 = (AltitudeMax * m2feet).ToString("0.0");
                else altitude2 = "---";
            }
            else if (MainConfigAlt2display == 3)        //min
            {
                if (AltitudeMin != Int16.MaxValue)
                    altitude2 = (AltitudeMin * m2feet).ToString("0.0");
                else altitude2 = "---";
            }
            else                                //slope
            {
                altitude2 = ElevationSlope.ToString("0.0%   ");
            }

            if (comboLapOptions.SelectedIndex > 0)
            {
                DrawMainValues(BackBufferGraphics, currentLap, (MGridX[1] + MGridX[3]) / 2, MGridY[5], 28.0f * df);
            }
            else
            {
                DrawMainValues(BackBufferGraphics, altitude, (MGridX[1] + MGridX[3]) / 2, MGridY[4], 16.0f * df);
                DrawMainValues(BackBufferGraphics, altitude2, (MGridX[1] + MGridX[3]) / 2, MGridY[5], 16.0f * df);
            }
            

         ///////


            // draw GPS cell
            string gps_status1, gps_status2;
            if (gps.OpenedOrSuspended)
            {
                GetGpsSearchFlags(out gps_status1, out gps_status2);
                DrawMainLabelOnLeft(BackBufferGraphics, gps_status1, MGridX[1], MGridY[6] + MHeightDelta, 8.0f);
                DrawMainLabelOnLeft(BackBufferGraphics, gps_status2, MGridX[1], MGridY[6] + MHeightDelta * 2, 8.0f);
            }

            DrawMainLabelOnLeft(BackBufferGraphics, "latitu.", MGridX[1], MGridY[6] + MHeightDelta * 3, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, Lat2String(CurrentLat, false), MGridX[3], MGridY[6] + MHeightDelta * 3, 8.0f);
            DrawMainLabelOnLeft(BackBufferGraphics, "longit.", MGridX[1], MGridY[6] + MHeightDelta * 4, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, Lat2String(CurrentLong, true), MGridX[3], MGridY[6] + MHeightDelta * 4, 8.0f);
            
            SolidBrush br = new SolidBrush(CurrentGpsLedColor);
            BackBufferGraphics.FillRectangle(br, ((MGridX[1] + MGridX[3]) / 2) - MHeightDelta, MGridY[5] + MGridDelta, MHeightDelta, MHeightDelta);

            // draw Info cell
            DrawMainLabelOnRight(BackBufferGraphics, LoadedSettingsName, MGridX[1], MGridY[5], 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, CurrentLiveLoggingString + " ", MGridX[1], MGridY[6], 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, CurrentStatusString + " ", MGridX[1], MGridY[6] + MHeightDelta, 8.0f);
#if DEBUG
            DrawMainLabelOnLeft(BackBufferGraphics, debugStr, MGridX[0], MGridY[6] + MHeightDelta * 2, 8.0f);
            //DrawMainLabelOnLeft(BackBufferGraphics, debugStr, MGridX[0], 0 + MHeightDelta * 2, 8.0f);
#else
            DrawMainLabelOnLeft(BackBufferGraphics, "battery", MGridX[0], MGridY[6] + MHeightDelta * 2, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, battery, MGridX[1], MGridY[6] + MHeightDelta * 2, 8.0f);
#endif      
            DrawMainLabelOnLeft(BackBufferGraphics, "last sample", MGridX[0], MGridY[6] + MHeightDelta * 3, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, last_sample_time, MGridX[1], MGridY[6] + MHeightDelta * 3, 8.0f);
            DrawMainLabelOnLeft(BackBufferGraphics, "start", MGridX[0], MGridY[6] + MHeightDelta * 4, 8.0f);
            DrawMainLabelOnRight(BackBufferGraphics, StartTime.ToString(), MGridX[1], MGridY[6] + MHeightDelta * 4, 8.0f);

            // clock
            Utils.DrawClock(BackBufferGraphics, foColor, (MGridX[2] + MGridX[3]) / 2, (MGridY[0] + MGridY[1]) / 2, Math.Min(MGridY[1] - MGridY[0], MGridX[3] - MGridX[2]), 16.0f * df);

            // compass
            int compass_size = (MGridY[6] + MHeightDelta * 3) - MGridY[5];
            Utils.DrawCompass(BackBufferGraphics, foColor, MGridX[3] - compass_size / 2, MGridY[5] + compass_size / 2, compass_size, Heading);

            g.DrawImage(BackBuffer, 0, 0); // draw back buffer on screen
        }
        // end draw main screen ------------------------------------------------

        
        int xMin = 0;   //Graph scale
        int xMax = 0;
        int xDiv = 1;
        int yMax = 0;
        int yMin = 0;
        int yDiv = 1;
        int xFactor = 32768;    //factor to convert value to screenPixel /32768
        int yFactor = 32768;
        bool GraphDrawSpeed = false;
        bool GraphOverDistance = true;
        //bool GraphDraw2Follow = false;
         const byte GraphLeave = 0;
         const byte GraphAutoscale = 1;
         const byte GraphMoving = 2;
         const byte GraphMove = 3;
         const byte GraphZooming = 4;
         const byte GraphZoom = 5;
         const byte GraphRedraw = 6;
        byte GraphScale = GraphAutoscale;
        enum MousePos : byte { none, top, bottom, left, right, middle };
        MousePos mousePos = MousePos.none;

        private void DrawGraph(Graphics g)
        {
            if (GraphScale == GraphLeave)
                return;
            if (GraphScale == GraphMoving && (BackBuffer != null))
            {
                mapUtil.DrawMovingImage(g, BackBuffer, MouseShiftX, MouseShiftY);
                return;
            }

            BackBufferGraphics.Clear(bkColor);
            Pen p = new Pen(Color.Gray, 1);
            SolidBrush b = new SolidBrush(GetLineColor(comboBoxKmlOptColor));
            Font f = new Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            Font f2 = new Font("Tahoma", 12F, System.Drawing.FontStyle.Bold);

            int[] x;
            int xUnit;
            string xLabel1, xLabel2;
            short[] y;
            int Dez;
            string title1, title2;

            if (GraphDrawSpeed)
            {
                y = PlotS;
                Dez = 10;
                title1 = "Speed [/"; title2 = "km/h]";
            }
            else
            {
                y = PlotZ;
                Dez = 1;
                title1 = "Altitude [/"; title2 = "m]";
            }
            if (GraphOverDistance)
            {
                x = PlotD;
                xUnit = 1;
                xLabel1 = "Distance [/"; xLabel2 = "m]";
            }
            else
            {
                x = PlotT;
                xUnit = 1;
                xLabel1 = "Time [/"; xLabel2 = "sec]";
            }

            if (PlotCount > 1)
            {
                if (GraphScale == GraphAutoscale)
                {
                    xMin = x[0];
                    xMax = x[PlotCount - 1];
                    yMax = short.MinValue;
                    yMin = short.MaxValue;
                    for (int i = 0; i < PlotCount; i++)
                    {
                        if (y[i] == Int16.MinValue) continue;       //ignore invalid values
                        if (y[i] > yMax) yMax = y[i];
                        if (y[i] < yMin) yMin = y[i];
                    }
                }
                else if (GraphScale == GraphMove)
                {
                    int vz = 1;
                    if (MouseShiftX < 0) { vz = -1; }
                    int tmp = MouseShiftX * vz * 32768 / xFactor;
                    tmp = (tmp / xDiv) * xDiv * vz;
                    xMin -= tmp;
                    xMax -= tmp;

                    vz = 1;
                    if (MouseShiftY < 0)
                        vz = -1;
                    tmp = MouseShiftY * vz * 32768 / yFactor;
                    tmp = (tmp / yDiv) * yDiv * vz;
                    yMin += tmp;
                    yMax += tmp;
                }
                else if (GraphScale == GraphZooming)
                {
                    return;
                }
                else if (GraphScale == GraphZoom)
                {
                    int tmp = MouseShiftX * 32768 / xFactor;
                    if (MousePosX < NoBkPanel.Width*40/480) { xMin -= tmp; }      //20
                    else if (MousePosX > NoBkPanel.Width*440/480) { xMax -= tmp; }    //w-20
                    else if (MouseShiftX > 0) { xMax -= tmp; }
                    else { xMin -= tmp; }
                    
                    tmp = MouseShiftY * 32768 / yFactor;
                    if (MousePosY < NoBkPanel.Width*40/480) { yMax += tmp; }    //20
                    else if (MousePosY > NoBkPanel.Height *460/508) { yMin += tmp; }    //h-24
                    else if (MouseShiftY > 0) { yMin += tmp; }
                    else { yMax += tmp; }
                }
                if (!(Logging && GraphScale == GraphAutoscale)) //when logging keep autoscale
                    GraphScale = GraphRedraw;

                if (GraphOverDistance)
                {
                    if (xMax > 2000)
                    {
                        xUnit = 1000;
                        xLabel2 = "km]";
                    }
                }
                else
                {
                    if (xMax > 120)
                    {
                        xUnit = 60;
                        xLabel2 = "min]";
                    }
                }

                int xMin_min = xMin / xUnit, xMax_min = xMax / xUnit;
                if (xMax_min * xUnit != xMax) xMax_min++;
                xDiv = RoundMinMax(ref xMin_min, ref xMax_min) * xUnit;
                xMin = xMin_min * xUnit; xMax = xMax_min * xUnit;

                yDiv = RoundMinMax(ref yMin, ref yMax);
                if (xMax == xMin) xMax++;   //avoid division by zero
                if (yMax == yMin) yMax++;
                xFactor = NoBkPanel.Width * (8192 * 11) / (3 * (xMax - xMin));    //w-20
                yFactor = NoBkPanel.Height * (4096 * 114) / 127 * 8 / (yMax - yMin);   //h-26
                
                //Draw grid
                int x0 = NoBkPanel.Width *20/480;        //10
                int y0 = NoBkPanel.Height *472/508;     //h-18

                for (int i = xMin; i <= xMax; i+=xDiv)
                {
                    BackBufferGraphics.DrawLine(p, x0 + ((i - xMin) * xFactor) / 32768, y0, x0 + ((i - xMin) * xFactor) / 32768, y0 - ((yMax - yMin) * yFactor) / 32768);
                }
                for (int i = yMin; i <= yMax; i += yDiv)
                {
                    BackBufferGraphics.DrawLine(p, x0, y0 - ((i - yMin) * yFactor) / 32768, x0 + ((xMax - xMin) * xFactor) / 32768, y0 - ((i - yMin) * yFactor) / 32768);
                }

                //Draw text
                BackBufferGraphics.DrawString((xMin / xUnit).ToString(), f, b, NoBkPanel.Width*4/480, NoBkPanel.Height*480/508);
                BackBufferGraphics.DrawString((xMax / xUnit).ToString(), f, b, NoBkPanel.Width * 440 / 480, NoBkPanel.Height * 480 / 508);
                BackBufferGraphics.DrawString(xLabel1 + (xDiv / xUnit) + xLabel2, f, b, NoBkPanel.Width * 160 / 480, NoBkPanel.Height * 480 / 508);
                BackBufferGraphics.DrawString((yMax / Dez).ToString(), f, b, NoBkPanel.Width * 2 / 480, NoBkPanel.Height * 4 / 508);
                BackBufferGraphics.DrawString((yMin / Dez).ToString(), f, b, NoBkPanel.Width * 2 / 480, NoBkPanel.Height *460/508);
                BackBufferGraphics.DrawString(title1 + yDiv / Dez + title2, f2, b, NoBkPanel.Width * 140 / 480, NoBkPanel.Height * 4 / 508);

                //Draw line
                p.Color = GetLineColor(comboBoxKmlOptColor);
                p.Width = GetLineWidth(comboBoxKmlOptWidth) / 2;
                int i1 = 0, i2 = 1;
                while (y[i1] == Int16.MinValue)     //ignore invalids at the beginning
                {
                    i1++;
                    if (++i2 >= PlotCount) break;
                }
                for ( ; i2 < PlotCount; i2++)
                {
                    while (y[i2] == Int16.MinValue)
                    {
                        if (++i2 >= PlotCount) goto exit;
                    }
                    BackBufferGraphics.DrawLine(p, x0 + ((x[i1] - xMin) * xFactor) / 32768, y0 - ((y[i1] - yMin) * yFactor) / 32768, x0 + ((x[i2] - xMin) * xFactor) / 32768, y0 - ((y[i2] - yMin) * yFactor) / 32768);
                    i1 = i2;
                }
            exit: ;
            }
            else
            {
                BackBufferGraphics.DrawString("no data to plot", f2, b, NoBkPanel.Width * 20 / 480, NoBkPanel.Height * 20 / 508);
            }
            g.DrawImage(BackBuffer, 0, 0); // draw back buffer on screen
        }
        private int RoundMinMax(ref int aMin, ref int aMax)
        {
            int div;
            int a = aMax - aMin;
            if(a < 100) div = 1;
            else if(a < 1000) div = 10;
            else if(a < 10000) div = 100;
            else div = 1000;

            a = a / div;

            if(a < 10) div *= 1;
            else if(a < 20) div *= 2;
            else if(a < 50) div *= 5;
            else div *= 10;

            aMin = (aMin / div) * div;
            a = (aMax / div) * div;
            if (a != aMax)
                aMax = a + div;

            return div;
        }



        // paint graph ------------------------------------------------------
        // To have nice flicker-free picture movement, we paint first into a bitmap which is larger
        // than the screen, then just paint the bitmap into the screen with a correct shift.
        // We need to paint on "no background panel", which has blank OnPaintBackground, to avoid flicker
        // The bitmap is updated as screen shift is complete (i.e. on mouse up).

        private int MousePosX = 0;        
        private int MousePosY = 0;
        private bool MouseMoving = false;
        private int MouseShiftX = 0;
        private int MouseShiftY = 0;
        
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
            mapUtil.ShowTrackToFollowMode = MapUtil.ShowTrackToFollow.T2FOff;     // Do not display Track to Follow start/end
        }

        private void tabGraph_Paint(object sender, PaintEventArgs e)        // Update Screen Main view, Map view, or Graph
        {
            PrepareBackBuffer();

            if (BufferDrawMode == BufferDrawModeMain) 
            {
                DrawMain(e.Graphics); 
            }
            else if (BufferDrawMode == BufferDrawModeMaps)
            {
                float[] CurLong = { (float)CurrentLong };
                float[] CurLat = { (float)CurrentLat };
                // plotting in Long (as X) / Lat (as Y) coordinates
                mapUtil.DrawMaps(e.Graphics, BackBuffer, BackBufferGraphics, MouseMoving,
                                 gps.OpenedOrSuspended, comboMultiMaps.SelectedIndex, GetUnitsConversionCff(), GetUnitsName(),
                                 PlotLong, PlotLat, PlotCount, GetLineColor(comboBoxKmlOptColor), GetLineWidth(comboBoxKmlOptWidth),
                                 checkPlotTrackAsDots.Checked, WayPoints, checkDispWaypoints.Checked,
                                 Plot2ndLong, Plot2ndLat, Counter2nd, GetLineColor(comboBoxLine2OptColor), GetLineWidth(comboBoxLine2OptWidth),
                                 checkPlotLine2AsDots.Checked,
                                 CurLong, CurLat, Heading, CurrentGpsLedColor);
            }
            else if (BufferDrawMode == BufferDrawModeGraph)
            {
                DrawGraph(e.Graphics);
            }
        }

        private void tabGraph_MouseDown(object sender, MouseEventArgs e)
        {
            MouseMoving = false;
            MousePosX = e.X;
            MousePosY = e.Y;
            MouseShiftX = 0;
            MouseShiftY = 0;

            if (BufferDrawMode == BufferDrawModeMaps)
            {
                mapUtil.ScreenShiftSaveX = mapUtil.ScreenShiftX;
                mapUtil.ScreenShiftSaveY = mapUtil.ScreenShiftY;
            }
            else if (BufferDrawMode == BufferDrawModeGraph)
            {
                if (e.X < NoBkPanel.Width / 14)
                { mousePos = MousePos.left; }
                else if(e.X > NoBkPanel.Width - NoBkPanel.Width / 14)
                { mousePos = MousePos.right; }
                else if(e.Y < NoBkPanel.Height / 16)
                { mousePos = MousePos.top; }
                else if(e.Y > NoBkPanel.Height - NoBkPanel.Height / 10)
                { mousePos = MousePos.bottom; }
                else
                { mousePos = MousePos.middle; }

                if (mousePos == MousePos.middle)
                    GraphScale = GraphMoving;
                else
                    GraphScale = GraphZooming;
            }
            else if (BufferDrawMode == BufferDrawModeMain)
            {
                if(comboLapOptions.SelectedIndex == 2)
                    lapManualClick = true;
            }
        }
        private void tabGraph_MouseUp(object sender, MouseEventArgs e)
        {
            if (BufferDrawMode == BufferDrawModeMaps)
            {
                mapUtil.ScreenShiftSaveX = 0;
                mapUtil.ScreenShiftSaveY = 0;
            }
            else if (BufferDrawMode == BufferDrawModeGraph)
            {
                if (GraphScale == GraphMoving)
                { GraphScale = GraphMove; }
                else if(GraphScale == GraphZooming)
                { GraphScale = GraphZoom; }
            }
            MouseMoving = false;
            NoBkPanel.Invalidate();
        }
        private void tabGraph_MouseMove(object sender, MouseEventArgs e)
        {
            
            if (e.Button != MouseButtons.None)
            {
                if (BufferDrawMode == BufferDrawModeMaps)
                {
                    mapUtil.ScreenShiftX = mapUtil.ScreenShiftSaveX + (e.X - MousePosX);
                    mapUtil.ScreenShiftY = mapUtil.ScreenShiftSaveY + (e.Y - MousePosY);
                }
                else if (BufferDrawMode == BufferDrawModeGraph)
                {
                    MouseShiftX = e.X - MousePosX;
                    MouseShiftY = e.Y - MousePosY;
                }
                MouseMoving = true;
                NoBkPanel.Invalidate();
            }
            else { MouseMoving = false; }

            
        }
        private void tabGraph_MouseDoubleClick(object sender, EventArgs e)
        {
            if (BufferDrawMode == BufferDrawModeMaps) { ResetMapPosition(); }
            else if (BufferDrawMode == BufferDrawModeGraph)
            {
                GraphScale = GraphAutoscale;
                if (mousePos == MousePos.bottom)
                    GraphOverDistance = !GraphOverDistance;
                else if (mousePos == MousePos.top)
                    GraphDrawSpeed = !GraphDrawSpeed;
            }
            else { return; }
            NoBkPanel.Invalidate();
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
            if (sc_x < sc_y) sc_y = sc_x;

            Rectangle dest_rec = new Rectangle(0, 0, (int)(CWImage.Width * sc_y), (int)(CWImage.Height * sc_y));

            ImageAttributes im_attr = new ImageAttributes();
            im_attr.SetColorKey(CWImage.GetPixel(0, 0), CWImage.GetPixel(0, 0));
 
            e.Graphics.DrawImage(CWImage, dest_rec, 0, 0, CWImage.Width, CWImage.Height, GraphicsUnit.Pixel, im_attr);
        }

        private void buttonMain_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeMain; 
            NoBkPanel.BringToFront(); 
            NoBkPanel.Invalidate();

            if(Paused)
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
            if (Logging || Paused)
                showButton(button2, MenuPage.BFkt.pause);
            else
                showButton(button2, MenuPage.BFkt.start);
            if(Logging)
                showButton(button3, MenuPage.BFkt.stop);
            else
                showButton(button3, MenuPage.BFkt.gps_toggle);

        }

        private void buttonMap_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeMaps;
            NoBkPanel.BringToFront(); 
            NoBkPanel.Invalidate();

            listBoxFiles.Focus();  // need to loose control from any combo/edit boxes, to avoid pop-up on "OK" button press
            showButton(button1, MenuPage.BFkt.main);
            showButton(button2, MenuPage.BFkt.map_zoomIn);
            showButton(button3, MenuPage.BFkt.map_zoomOut);
        }

        
        private void buttonOptions_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeOptions;
            LoadedSettingsName = "";
            tabControl.BringToFront();
            //labelFileName.SetText(""); // clear text from info label
            ShowHideViewOpt(false); // make sure the view selector is hidden

            showButton(button1, MenuPage.BFkt.main);
            showButton(button2, MenuPage.BFkt.options_prev);
            showButton(button3, MenuPage.BFkt.options_next);
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
                if (PlotCount != 0) { ResetMapPosition(); }
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
                labelFileName.SetText("Current File Name: " + gcc_file);

                if (IoFilesDirectory == "\\") { gcc_file = "\\" + gcc_file; ; }
                else { gcc_file = IoFilesDirectory + "\\" + gcc_file; }

                if (LoadGcc(gcc_file))  // loaded OK
                {
                    buttonMap_Click(sender, e);  // show graphs

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
                    IFileSupport fs = null;
                    switch (FileOpenMode)
                    {
                        case FileOpenMode_2ndGcc:
                            fs = new GccSupport(); break;
                        case FileOpenMode_2ndGpx:
                            fs = new GpxSupport(); break;
                        case FileOpenMode_2ndKml:
                            fs = new KmlSupport(); break;
                    }

                    if(fs != null)
                        loaded_ok = fs.Load(file_name, ref WayPoints, PlotDataSize, ref Plot2ndLat, ref Plot2ndLong, ref Plot2ndT, out Counter2nd);
                }

                if (loaded_ok)  // loaded OK
                {
                    // If a new track-to-follow loaded (and main track not exist) - need to reset map zoom/shift vars
                    if ((Counter2nd != 0) && (PlotCount == 0)) { ResetMapPosition(); }

                    if (file_found) { labelFileName.SetText("Track2follow: " + Path.GetFileName(file_name) + " loaded"); }
                    else { labelFileName.SetText(""); }

                    mPage.mBAr[(int)MenuPage.BFkt.load_2clear].text = "Clear2Follow";

                    // need to loose focus from list box - otherwise map do not get MouseMove!???
                    listBoxFiles.Items.Clear();
                    listBoxFiles.Focus();

                    MenuExec(MenuPage.BFkt.map);
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

            labelFileName.SetText("");

            // in folder setup mode - stay at options
            if (FolderSetupMode)
                MenuExec(MenuPage.BFkt.options);
            else // in all other cases stay at menu screen
                MenuExec(MenuPage.BFkt.mPage);
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

            tabBlank1.BringToFront();
            tabOpenFile.BringToFront();
            showButton(button1, MenuPage.BFkt.dialog_cancel);
            showButton(button2, MenuPage.BFkt.dialog_open);
            tabBlank.BringToFront();

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
                MessageBox.Show("Please retype your password", textBoxCwUrl.Text,
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            LockCwVerify = true;

            labelCwInfo.Text = ""; labelCwInfo.Refresh();
            labelCwInfo.Text = CWUtils.VerifyCredentialsOnCrossingwaysViaHTTP(textBoxCwUrl.Text, textBoxCw1.Text, textBoxCw2.Text);

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

            if (PlotCount == 0) { return; }      // safety checks
            if (position == null) { return; }

            // check if this is a time to log again
            TimeSpan maxAge = new TimeSpan(0, LiveLoggingTimeMin[comboBoxCwLogMode.SelectedIndex], 0);
            if ((LastLiveLogging + maxAge) >= DateTime.UtcNow) { return; }
            LastLiveLogging = DateTime.UtcNow;

            // proceed with live logging
            string servermessage = CWUtils.UpdatePositionOnCrossingwaysViaHTTP(textBoxCwUrl.Text, textBoxCw1.Text, CwHashPassword,
                CurrentLat, CurrentLong, (CurrentAlt == Int16.MinValue) ? 0.0 : CurrentAlt, (Heading == 720)? 0.0 : (double)Heading, "GpsCC");

            if (servermessage.IndexOf("90 - Could not establish a connection") != -1)
                { CurrentLiveLoggingString = "livelog error! " + DateTime.Now.ToString("t"); }
            else
                { CurrentLiveLoggingString = "livelog ok " + DateTime.Now.ToString("t"); }
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
                    if (Distance < LapStartD + 50)
                    {
                        lapManualClick = false;
                        return;                                         //keep last value
                    }
                    if (lapManualDistance != 0)
                    {
                        int currentManualSec = (int)((CurrentTimeSec - LapStartT) * lapManualDistance / (Distance - LapStartD));
                        currentLap = (currentManualSec / 60) + ":" + (currentManualSec % 60).ToString("00");
                    }
                    if (lapManualClick)
                    {
                        lastLap = ((CurrentTimeSec - LapStartT) / 60) + ":" + ((CurrentTimeSec - LapStartT) % 60).ToString("00");
                        lapData = "Lap " + ++LapNumber + " - " + (Distance / 1000).ToString("0.00") + " km --- " + lastLap + " min:s\r\n";
                        lapManualDistance = (int)(Distance - LapStartD);
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
            if (Distance < LapStartD + lapInterval / 20)
                return;                                         //keep last value
            int currentLapSec = (int)((CurrentTimeSec - LapStartT) * lapInterval / (Distance - LapStartD));
            currentLap = (currentLapSec / 60) + ":" + (currentLapSec % 60).ToString("00");
            if (Distance < LapStartD + lapInterval)
                return;
            lastLap = ((CurrentTimeSec - LapStartT) / 60) + ":" + ((CurrentTimeSec - LapStartT) % 60).ToString("00");
            lapData = "Lap " + ++LapNumber + " - " + (Distance / 1000).ToString("0.0") + " km --- " + lastLap + " min:s\r\n";
            goto final;

        timeBased:
            if (CurrentTimeSec < LapStartT + lapInterval / 20)
                return;                                         //keep last value
            currentLap = ((Distance - LapStartD) * lapInterval / (CurrentTimeSec - LapStartT) / 1000).ToString("0.00");
            if (CurrentTimeSec < LapStartT + lapInterval)
                return;
            lastLap = ((Distance - LapStartD) / 1000).ToString("0.00");
            lapData = "Lap " + ++LapNumber + " - " + CurrentTimeSec / 60 + " min --- " + lastLap + " km\r\n";

        final:
            WriteCheckPoint(lapData);
        final2:
            if (checkLapBeep.Checked)
                MessageBeep(BeepType.SimpleBeep);
            //textLapOptions.SuspendLayout();
            textLapOptions.Text += lapData;
            textLapOptions.Select(textLapOptions.Text.Length, 0);       //cursor to the end
            textLapOptions.ScrollToCaret();
            //textLapOptions.ResumeLayout();
            LapStartD = Distance;
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
            csv_file += "\\" + Path.GetFileNameWithoutExtension(CurrentFileName) + ".csv";

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
            // switch from default to landscape mode
            if ((isLandscape == false) && (Screen.PrimaryScreen.Bounds.Height < Screen.PrimaryScreen.Bounds.Width))
            {
                workX_l = Screen.PrimaryScreen.WorkingArea.Width;
                workY_l = Screen.PrimaryScreen.WorkingArea.Height;
                isLandscape = true;             // TODO  Rotate Buttons
                if (scaleFirstRun)
                {
                    scx_p = workX_l; scx_q = 640;      //first time do also downscale
                    scy_p = workY_l; scy_q = 508;
                }
                else
                {
                    scx_p = workX_l * 480; scx_q = workX_p * 640;
                    scy_p = workY_l * 588; scy_q = workY_p * 508;
                }
                df = 0.84f;
                ScaleToCurrentResolution();
                
                NoBkPanel.Height = Screen.PrimaryScreen.WorkingArea.Height;
                listBoxFiles.Height = tabControl.Height;

                // move to new - all at tab width (480 on VGA)
                int left = tabControl.Width;
                int h = button1.Height;
                button1.Left = left; button2.Left = left; button3.Left = left;
                button4.Left = left; button5.Left = left; button6.Left = left;
                button1.Top = 0; button2.Top = h; button3.Top = h * 2;
                button4.Top = h * 5; button5.Top = h * 4; button6.Top = h * 3;      //revese order for better grouping
            }
            // switch back to portrait
            else if ((isLandscape == true) && (Screen.PrimaryScreen.Bounds.Height > Screen.PrimaryScreen.Bounds.Width))
            {                           //impossible to come first time here to downscale
                workX_p = Screen.PrimaryScreen.WorkingArea.Width;
                workY_p = Screen.PrimaryScreen.WorkingArea.Height;
                isLandscape = false;
                scx_p = workX_p * 640; scx_q = workX_l * 480;
                scy_p = workY_p * 508; scy_q = workY_l * 588;
                df = 1.0f;
                //df = 480f / 588f * workY_p / workX_p;
                ScaleToCurrentResolution();

                NoBkPanel.Height = Screen.PrimaryScreen.WorkingArea.Height - button1.Height;
                listBoxFiles.Height = tabControl.Height - button4.Height;

                int X1 = Screen.PrimaryScreen.WorkingArea.Width / 3;
                int X2 = X1 * 2;
                int Y1 = Screen.PrimaryScreen.WorkingArea.Height - button1.Height;
                int Y2 = Y1 - button4.Height;
                button1.Left = 0; button2.Left = X1; button3.Left = X2;
                button4.Left = 0; button5.Left = X1; button6.Left = X2;
                button1.Top = Y1; button2.Top = Y1; button3.Top = Y1;
                button4.Top = Y2; button5.Top = Y2; button6.Top = Y2;
            }
            else if (scaleFirstRun)
            {           //only first time downscale resoulion - in portrait
                workX_p = Screen.PrimaryScreen.WorkingArea.Width;
                workY_p = Screen.PrimaryScreen.WorkingArea.Height;
                scx_p = workX_p; scx_q = 480;
                scy_p = workY_p; scy_q = 588;
                if (Screen.PrimaryScreen.Bounds.Height == Screen.PrimaryScreen.Bounds.Width)
                    df = 0.72f;      // reduce font for Square
                ScaleToCurrentResolution();
            }
            scaleFirstRun = false;
        }

        // load second line - track to follow
        private void buttonLoadTrack2Follow_Click(object sender, EventArgs e)
        {
            BufferDrawMode = BufferDrawModeFiledialog;
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
            buttonLoadTrack2Follow_Click(null, EventArgs.Empty);
        }
        private void buttonNextFileType_Click(object sender, EventArgs e)
        {
            FileExtentionToOpen++;
            if (FileExtentionToOpen > 3) { FileExtentionToOpen = 1; } // set to FileOpenMode_2ndGcc = 1
            buttonLoadTrack2Follow_Click(null, EventArgs.Empty);
        }
        private void Fill2ndLineFiles(string ext)
        {
            Cursor.Current = Cursors.WaitCursor;
            listBoxFiles.Items.Clear();

            string[] files = Directory.GetFiles(IoFilesDirectory, ext);
            Array.Sort(files);

            for (int i = (files.Length - 1); i >= 0; i--)
            {
                listBoxFiles.Items.Add(Path.GetFileName(files[i]));
            }
            if (listBoxFiles.Items.Count == 0)
                { listBoxFiles.Items.Add("No " + ext + " files found"); }

            listBoxFiles.BringToFront();
            tabOpenFile.BringToFront();

            showButton(button1, MenuPage.BFkt.dialog_cancel);
            showButton(button2, MenuPage.BFkt.dialog_open);
            showButton(button3, MenuPage.BFkt.dialog_down);
            showButton(button4, MenuPage.BFkt.dialog_prevFileType);
            showButton(button5, MenuPage.BFkt.dialog_nextFileType);
            showButton(button6, MenuPage.BFkt.dialog_up);
            tabBlank1.SendToBack();

            if (listBoxFiles.Items.Count != 0)
            { listBoxFiles.SelectedIndex = 0; }
            Cursor.Current = Cursors.Default;
        }
        private void buttonLoad2Clear_Click(object sender, EventArgs e)
        {
            // if the track-to-follow already cleared (and not logging) - clear main
            if (Counter2nd == 0)
            {
                if (Logging || Paused)
                    { MessageBox.Show("Cannot clear track while logging"); }
                else 
                {
                    PlotCount = 0;
                    Decimation = 1; DecimateCount = 0;
                    StartTime = DateTime.Now;       StartTimeUtc = DateTime.UtcNow;
                    LastBatterySave = StartTimeUtc; LastLiveLogging = StartTimeUtc;
                    CurrentTimeSec = 0; CurrentStoppageTimeSec = 0;
                    CurrentSpeed = 0.0; CurrentV = 0.0;
                    MaxSpeed = 0.0; Distance = 0.0;
                    CurrentAlt = Int16.MinValue;
                    StartAlt = Int16.MinValue;
                    AltitudeMax = Int16.MinValue;
                    AltitudeMin = Int16.MaxValue;
                    Heading = 720;
                    ReferenceSet = false;
                    //FirstSampleValidCount = 1;      GpsSearchCount = 0;
                    //CurrentStatusString = "gps off"; CurrentLiveLoggingString = "";

                    labelFileName.SetText("All tracks cleared");
                }
            }
            else // clear track-to-follow first (on the first click)
            {
                Counter2nd = 0;
                labelFileName.SetText("Track to follow cleared");
                mPage.mBAr[(int)MenuPage.BFkt.load_2clear].text = "ClearTrack";
                WayPoints.WayPointCount = 0;        // Clear Waypoints of T2F
            }
        }

        private void buttonGraph_Click(object sender, EventArgs e)
        {
            //tabBlank.BringToFront();
            //BackBufferGraphics.Clear(BackColor);
            BufferDrawMode = BufferDrawModeGraph;
            showButton(button2, MenuPage.BFkt.graph_alt);
            showButton(button3, MenuPage.BFkt.help);
            GraphScale = GraphAutoscale;
            
            NoBkPanel.BringToFront();
            NoBkPanel.Invalidate();
        }

    /*    private void buttonGraphAlt_Click(object sender, EventArgs e)
        {
            GraphDrawSpeed = false;
            GraphScale = GraphAutoscale;
            NoBkPanel.Invalidate();
        }*/
    /*    private void buttonGraphSpeed_Click(object sender, EventArgs e)
        {
            GraphDrawSpeed = true;
            GraphScale = GraphAutoscale;
            NoBkPanel.Invalidate();
        }*/


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
            //buttonLoadFile.Visible = !show;               //todo ?
            //buttonLoadTrack2Follow.Visible = !show;
            //buttonLoad2Clear.Visible = !show;
            //buttonGraph.Visible = !show;
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

        private void comboMapDownload_SelectedIndexChanged(object sender, EventArgs e)
        {
            mapUtil.OsmTilesWebDownload = comboMapDownload.SelectedIndex;

            if (DoNotSaveSettingsFlag) { return; }

            if (comboMapDownload.SelectedIndex != 0)
            {
                MessageBox.Show("Remember to set correct (or new blank) folder for map download\n\nCheck that you are connected to internet", "Map download is ON",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            }
        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            string exe_folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
            if (exe_folder != "\\") exe_folder += "\\";
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = "file://" + exe_folder + "Readme.htm";
            if (BufferDrawMode == BufferDrawModeGraph)
                proc.StartInfo.FileName += "#graph";
            //proc.StartInfo.WorkingDirectory = exe_folder;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }


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
                if (checkRelativeAlt.Checked) { altitude = CurrentAlt - (double)PlotZ[0]; }

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

    }
}
