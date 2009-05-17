using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace GpsCycleComputer
{
    public enum GpsPollIntervals
    {
        always = 0,



    }

    public enum Units
    {
        miles_mph,
        km_kmh,
        nautMiles_knots,
        miles_mph_ft,
        km_minPerKm,
        miles_minPerMile_ft,
        km_kmh_ft
    }

    public enum CWLogModes
    {


    }

    public enum LapModes
    {


    }

    /// <summary>
    /// Settings class stores all application settings and offers methods to save/load 
    /// settings to/from file.
    /// </summary>
    public class Settings
    {
        Dictionary<String, Color> plotColors = new Dictionary<string, Color>();
        int[] gccDllRates = new int[6] { 4800, 9600, 19200, 38400, 57600, 115200 };

        GpsPollIntervals gpsPoll;
        bool stopOnLowBat;
        bool useGccDll;
        int useGccDllCom;
        int useGccDllRate;
        int geoId;
        Units unit;
        bool exStopTime;
        bool editFileName;
        bool showBkOff;
        bool relativeAlt;
        bool plotTrackAsDots;
        Color plotTrackColor;
        int plotTrackWidth;
        bool plotLine2AsDots;
        Color plotLine2Color;
        int plotLine2Width;
        bool mapsWhiteBk;
        int mapMode;
        int mapDownload;
        bool kmlAlt;
        bool gpxRte;
        int gpxTimeShift;
        String cwUsername;
        String cwHashPassword;
        CWLogModes cwLogMode;
        bool uploadGpx;
        LapModes lapMode;
        int lapTime;
        string ioFilesDirectory;
        string mapFilesDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="Settings"/> class.
        /// </summary>
        public Settings()
        {
            // fill plotColors dictionary
            plotColors.Add("blue", Color.Blue);
            plotColors.Add("red", Color.Red);

            // set default values
            this.stopOnLowBat = true;
            this.useGccDll = true;
            this.useGccDllCom = 4; //Com4
            this.useGccDllRate = gccDllRates[0]; //4800
            this.plotTrackColor = plotColors["blue"];
            


        }
    }
}
