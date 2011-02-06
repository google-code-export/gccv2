#region Using directives

using System;
using System.Collections;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;

#endregion

namespace GpsUtils
{
    // Utilities to work with JPEG maps and plot a track
    public class MapUtil
    {
        public Color Back_Color = Color.White;
        public Color Fore_Color = Color.Black;

        public MapUtil() 
        {
            for (int i = 0; i < MaxNumMaps; i++)
            {
                Maps[i].bmp = null;
                Maps[i].fname = "";
            }
        }

        public struct MapInfo
        {
            public string fname;                   // filename in the current I/O folder
            public double lat1, lon1, lat2, lon2;  // lat/long of the bottom left and top right corners
            public int sizeX, sizeY;               // X/Y image size
            public double overlap;                 // overlap in % of the current view (to select best map)
            public double zoom_level;              // ratio of the map screen size to the original size
            public double qfactor;                 // quality function, based on map zoom level
            public bool was_removed;               // flag that map was competely covered and was removed
            public int scrX1, scrX2, scrY1, scrY2; // current screen coordinates in pixels
            public Bitmap bmp;                     // the bitmap
        };

        // structure to read GMI files reference points
        public struct RefPointInfo
        {
            public int i;     // value in pixels
            public double f;  // corresponding value of lat/long
        };

        const int MaxNumMaps = 512;
        public int NumMaps = 0;
        private int NumBitmaps = 0;        // max num bitmaps to plot and store (depending on the algorithm)
        public MapInfo[] Maps = new MapInfo[MaxNumMaps];
        private string MapsFilesDirectory;

        public enum ShowTrackToFollow
        {
            T2FOff,
            T2FStart,
            T2FEnd
        };

        // Show the current position on the map or the start/stop position of the track
        public ShowTrackToFollow ShowTrackToFollowMode = ShowTrackToFollow.T2FOff;

        private int ScreenX = 100;         // screen (drawing) size in pixels
        private int ScreenY = 100;
        private double Data2Screen = 1.0;  // Coefficient to convert from data to screen values
        public double ZoomValue = 1.0;    // zoom coefficient, to multiply Data2Screen
        public int DefaultZoomRadius = 100;    //default zoom in m (in either direction)
        private double DataLongMin = 1.0E9, DataLongMax = -1.0E9, DataLatMin = 1.0E9, DataLatMax = -1.0E9;
        private double Meter2Lat = 1.0, Meter2Long = 1.0, RatioMeterLatLong = 1.0;

        UtmUtil utmUtil = new UtmUtil();

        // vars required to support screen shifting
        public int ScreenShiftX = 0;      // (total) screen shift (pixels)
        public int ScreenShiftY = 0;
        public int ScreenShiftSaveX = 0;  // screen shift as mouse move started (pixels)
        public int ScreenShiftSaveY = 0;

        // vars to work with OpenStreetMap tiles
        public int OsmTilesWebDownload = 0;  // 0 - web download "off", or the server index from a predefined server list
        private bool OsmTilesMode = false;
        private const int OsmNumZoomLevels = 19;    //19  is too high - most servers do not respond on zoom level 18
        private bool[] OsmZoomAvailable = new bool[OsmNumZoomLevels]; // if a directories for this zoom level found (true/false)
        private const int OsmTmpReadArraySize = 4096;
        private byte[] OsmTmpReadArray = new byte[OsmTmpReadArraySize];
        private string OsmCustomServer = "http://";
        private string OsmFileExtension = "";

        // map error codes
        private const int __MapNoError = 0;
        private const int __MapErrorReading = 1;
        private const int __MapErrorDownload = 2;
        private const int __MapErrorOutOfMemory = 3;

        private int MapErrors = __MapNoError;


        // return true if only sub-directories with names "0".."18" exist in this folder
        public bool DetectOsmTiles()
        {
            for (int i = 0; i < OsmNumZoomLevels; i++) { OsmZoomAvailable[i] = false; }

            string[] zoom_dirs = Directory.GetDirectories(MapsFilesDirectory);

            // nothing found
            if (zoom_dirs.Length == 0) { return false; }

            for (int i = 0; i < zoom_dirs.Length; i++)
            {
                string dir_name = Path.GetFileName(zoom_dirs[i]);
                bool found = false;
                for (int allowed_i = 0; allowed_i < OsmNumZoomLevels; allowed_i++)
                {
                    if (dir_name == allowed_i.ToString()) { found = true; break; }
                }
                if (!found)  { return false; }
            }

            // fill array with avaliable zoom levels
            for (int i = 0; i < zoom_dirs.Length; i++)
            {
                string dirz_name = Path.GetFileName(zoom_dirs[i]);
                int zoom_level = Convert.ToInt32(dirz_name);
                OsmZoomAvailable[zoom_level] = true;
            }

            // detect file extension .png or .jpg
            try
            {
                string[] x_dirs = Directory.GetDirectories(zoom_dirs[0]);
                string[] y_names = Directory.GetFiles(x_dirs[0]);
                OsmFileExtension = Path.GetExtension(y_names[0]);
            }
            catch
            {
                OsmFileExtension = ".png";
            }
            return true;
        }
        // hyperbolic sine seems not supported in CF .NET
        public double __sinh(double x)
        {
            return (0.5 * (Math.Exp(x) - Math.Exp(-x)));
        }
        public double OsmXTile2Long(double n, int xtile)
        {
            return (xtile / n * 360.0 - 180.0);
        }
        public double OsmYTile2Lat(double n, int ytile)
        {
            double lat_rad = Math.Atan(__sinh(Math.PI * (1.0 - 2.0 * ytile / n)));
            return (lat_rad * 180.0 / Math.PI);
        }
        public int OsmLong2XTile(double n, double lon_deg)
        {
            return (int)((lon_deg + 180.0) / 360.0 * n);
        }
        public int OsmLat2YTile(double n, double lat_deg)
        {
            double lat_rad = lat_deg * Math.PI / 180.0;
            return (int)((1.0 - Math.Log(Math.Tan(lat_rad) + (1 / Math.Cos(lat_rad))) / Math.PI) / 2.0 * n);
        }
        public void LoadCustomOsmServer(string file_name)
        {
            if (File.Exists(file_name))
            {
                FileStream fs = null;
                StreamReader sr = null;
                try
                {
                    fs = new FileStream(file_name, FileMode.Open);
                    sr = new StreamReader(fs);
                    if (sr.Peek() != -1)
                    {
                        OsmCustomServer = sr.ReadLine().Trim();
                    }
                }
                catch (Exception e)
                {
                    Utils.log.Error(" LoadCustomOsmServer ", e);
                }
                finally
                {
                    if(sr != null) sr.Close();
                    if(fs != null) fs.Close();
                }
            }
        }
        public void DownloadOsmTile(string tile_name)
        {
            /* hard-coded server names: 
Osmarender                     http://tah.openstreetmap.org/Tiles/tile/
Mapnik                         http://tile.openstreetmap.org/mapnik/
Cyclemap (CloudMade)           http://c.andy.sandbox.cloudmade.com/tiles/cycle/
OpenPisteMap                   http://openpistemap.org/tiles/contours/     new:     http://tiles.openpistemap.org/contours/
CloudMade Web style            http://tile.cloudmade.com/8bafab36916b5ce6b4395ede3cb9ddea/1/256/
CloudMade Mobile style         http://tile.cloudmade.com/8bafab36916b5ce6b4395ede3cb9ddea/2/256/
CloudMade NoNames style        http://tile.cloudmade.com/8bafab36916b5ce6b4395ede3cb9ddea/3/256/
User-defined server (read server name from osm_server.txt)             
             */

            string server_name = "http://tah.openstreetmap.org/Tiles/tile/";
            if      (OsmTilesWebDownload == 1) { server_name = "http://tah.openstreetmap.org/Tiles/tile/"; }
            else if (OsmTilesWebDownload == 2) { server_name = "http://tile.openstreetmap.org/mapnik/"; }
            else if (OsmTilesWebDownload == 3) { server_name = "http://c.andy.sandbox.cloudmade.com/tiles/cycle/"; }
            else if (OsmTilesWebDownload == 4) { server_name = "http://tiles.openpistemap.org/contours/"; }
            else if (OsmTilesWebDownload == 5) { server_name = "http://tile.cloudmade.com/8bafab36916b5ce6b4395ede3cb9ddea/1/256/"; }
            else if (OsmTilesWebDownload == 6) { server_name = "http://tile.cloudmade.com/8bafab36916b5ce6b4395ede3cb9ddea/2/256/"; }
            else if (OsmTilesWebDownload == 7) { server_name = "http://tile.cloudmade.com/8bafab36916b5ce6b4395ede3cb9ddea/3/256/"; }
            else if (OsmTilesWebDownload == 8) { server_name = OsmCustomServer; }

            try
            {
                String forecastAdress = tile_name.Replace(MapsFilesDirectory + "\\", server_name);
                forecastAdress = forecastAdress.Replace("\\", "/");

                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(forecastAdress);
                httpRequest.Credentials = CredentialCache.DefaultCredentials;
                httpRequest.Timeout = 10000;          // 10 sec timeouts
                httpRequest.ReadWriteTimeout = 10000;

                HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();

                if (httpResponse.StatusCode != HttpStatusCode.OK) 
                {
                    Utils.log.Debug ("Map download error - " + forecastAdress + " HTTPResponse = " + httpResponse.StatusCode + " - " + httpResponse.StatusDescription);
                    MapErrors = __MapErrorDownload; 
                    return; 
                }

                System.IO.Stream dataStream = httpResponse.GetResponseStream();

                Directory.CreateDirectory(Path.GetDirectoryName(tile_name));

                FileStream fs = new FileStream(tile_name, FileMode.Create);
                BinaryWriter wr = new BinaryWriter(fs, Encoding.UTF8);

                do
                {
                    int n = dataStream.Read(OsmTmpReadArray, 0, OsmTmpReadArraySize);
                    if(n == 0) { break; }
                    wr.Write(OsmTmpReadArray, 0, n);
                }
                while (true);

                wr.Close();
                fs.Close();
                dataStream.Close();
                httpResponse.Close();
            }
            catch (Exception e)
            {
                Utils.log.Error (" DownloadOsmTile ", e);
                MapErrors = __MapErrorDownload;
            }
        }
        public void FillOsmTiles()
        {
            if (NumMaps != 0)
            {
                IComparer comp_bitmap = new BitmapExistComparer();
                Array.Sort(Maps, 0, NumMaps, comp_bitmap);

                // do not delete map records which hold a bitmap, so we do not need to reload it again
                NumMaps = 0;
                for (int i = 0; i < MaxNumMaps; i++)
                {
                    if (Maps[i].bmp != null) { NumMaps++; }
                    else { break; }
                }
            }
            int num_stored_bitmaps = NumMaps;

            for (int i = 0; i < MaxNumMaps; i++)
            {
                Maps[i].zoom_level = 0.0;
                Maps[i].overlap = 0.0;
                Maps[i].qfactor = 0.0;
            }

            double longMin = ToDataX(0);
            double longMax = ToDataX(ScreenX);
            double latMin = ToDataY(ScreenY);
            double latMax = ToDataY(0);

            for (int iz = 0; iz < OsmNumZoomLevels; iz++) // start filling map array from the lowest zoom level
            {
                if ((OsmTilesWebDownload == 0) && (OsmZoomAvailable[iz] == false)) { continue; }

                double n = (double)(1 << iz); // num tiles in this zoom level

                int xtile_min = OsmLong2XTile(n, longMin);
                int xtile_max = OsmLong2XTile(n, longMax);
                int ytile_min = OsmLat2YTile(n, latMax);
                int ytile_max = OsmLat2YTile(n, latMin);

                // do not load very small tiles - cannot plot them anyway
                int num_tiles_in_this_zoom = (ytile_max - ytile_min + 1) * (xtile_max - xtile_min + 1);
                if (num_tiles_in_this_zoom > 32) { break; }

                // do a quick check if a central tile exist when we have lots of times, - skip if not exists
                if ((num_tiles_in_this_zoom > 9) && (OsmTilesWebDownload == 0))
                {
                    int center_x = (xtile_min + xtile_max) / 2;
                    int center_y = (ytile_min + ytile_max) / 2;
                    string center_file_name = MapsFilesDirectory + "\\" + iz.ToString() +
                                              "\\" + center_x.ToString() + "\\" + center_y.ToString() + OsmFileExtension;

                    if(File.Exists(center_file_name) == false)  { continue; }
                }

                for (int ix = xtile_min; ix <= xtile_max; ix++)
                {
                    for (int iy = ytile_min; iy <= ytile_max; iy++)
                    {
                        Maps[NumMaps].fname = MapsFilesDirectory + "\\" + iz.ToString() +
                                              "\\" + ix.ToString() + "\\" + iy.ToString() + OsmFileExtension;

                        Maps[NumMaps].lon1 = OsmXTile2Long(n, ix);
                        Maps[NumMaps].lon2 = OsmXTile2Long(n, ix+1);
                        Maps[NumMaps].lat2 = OsmYTile2Lat(n, iy);
                        Maps[NumMaps].lat1 = OsmYTile2Lat(n, iy+1);

                        // check that this record is not exist in the area with stored bitmaps
                        bool record_exist_with_bitmaps = false;
                        for (int k = 0; k < num_stored_bitmaps; k++)
                        {
                            if ((Maps[NumMaps].lat1 == Maps[k].lat1) &&
                                (Maps[NumMaps].lat2 == Maps[k].lat2) &&
                                (Maps[NumMaps].lon1 == Maps[k].lon1))
                            {
                                record_exist_with_bitmaps = true;
                                break;
                            }
                        }
                        if (!record_exist_with_bitmaps)
                        {
                            NumMaps++;
                        }

                        // hopefully having 512 tiles within THIS screen is enough
                        if (NumMaps >= MaxNumMaps) { return; }
                    }
                }
            }
        }

        public void LoadMaps(string mapsFilesDirectory)
        {
            NumMaps = 0;
            NumBitmaps = 0;
            string jpg_name = "";
            MapsFilesDirectory = mapsFilesDirectory;

            // dispose of all old maps and reset vars
            RemoveBitmaps(0);
            for (int i = 0; i < MaxNumMaps; i++)
            {
                Maps[i].zoom_level = 0.0;
                Maps[i].overlap = 0.0;
                Maps[i].qfactor = 0.0;
            }

            OsmTilesMode = DetectOsmTiles();
            if (OsmTilesWebDownload != 0) { OsmTilesMode = true; } // if want to download - force OsmTilesMode

            if (OsmTilesMode)
            {
                // all OSM tiles are 256x256
                for (int i = 0; i < MaxNumMaps; i++)
                {
                    Maps[i].sizeX = 256;
                    Maps[i].sizeY = 256;
                }
                return;
            }

            // loop over possible map names and load existing map info
            Cursor.Current = Cursors.WaitCursor;

            // load jpeg and jpg files
            string[] jfiles1 = Directory.GetFiles(mapsFilesDirectory, "*.jpg");
            string[] jfiles2 = Directory.GetFiles(mapsFilesDirectory, "*.jpeg");

            string[] jpg_files = new string[jfiles1.Length + jfiles2.Length];
            int tmpi = 0;
            for (int i = 0; i < jfiles1.Length; i++)
            { jpg_files[tmpi] = jfiles1[i]; tmpi++; }
            for (int i = 0; i < jfiles2.Length; i++)
            { jpg_files[tmpi] = jfiles2[i]; tmpi++; }

            Array.Sort(jpg_files);

            // set "." as decimal separator for reading the map info
            NumberFormatInfo number_info = new NumberFormatInfo();
            number_info.NumberDecimalSeparator = ".";

            for (int i = 0; i < jpg_files.Length; i++)
            {
                jpg_name = Path.GetFileName(jpg_files[i]);

                // kml file name
                string kml_file = Path.GetFileNameWithoutExtension(jpg_files[i]) + ".kml";
                if (mapsFilesDirectory == "\\") { kml_file = "\\" + kml_file; }
                else { kml_file = mapsFilesDirectory + "\\" + kml_file; }

                // text file name
                string txt_file = Path.GetFileNameWithoutExtension(jpg_files[i]) + ".txt";
                if (mapsFilesDirectory == "\\") { txt_file = "\\" + txt_file; }
                else { txt_file = mapsFilesDirectory + "\\" + txt_file; }

                // gmi file name
                string gmi_file = Path.GetFileNameWithoutExtension(jpg_files[i]) + ".gmi";
                if (mapsFilesDirectory == "\\") { gmi_file = "\\" + gmi_file; }
                else { gmi_file = mapsFilesDirectory + "\\" + gmi_file; }

                // check that at least one file exists
                if ((File.Exists(kml_file) == false)
                    && (File.Exists(txt_file) == false)
                    && (File.Exists(gmi_file) == false)) { continue; }

                // load maps dimensions
                if (Utils.GetJpegSize(jpg_files[i], out Maps[NumMaps].sizeX, out Maps[NumMaps].sizeY) != 0) { continue; }

                // make sure we do not load empty images
                if ((Maps[NumMaps].sizeX == 0) || (Maps[NumMaps].sizeY == 0)) { continue; }

                Maps[NumMaps].fname = jpg_files[i];

                bool kml_has_problems = false;
                bool gmi_has_problems = false;

                try
                {
                    // load kml file
                    if (File.Exists(kml_file))
                    {
                        double center_lat = 0.0, center_long = 0.0, center_range = 0.0;

                        FileStream fs = new FileStream(kml_file, FileMode.Open);
                        StreamReader sr = new StreamReader(fs);
                        string line = "";
                        while (sr.Peek() != -1)
                        {
                            line = sr.ReadLine().Trim();
                            if (line == "</kml>") { break; }

                            // replace all "," with "." to read correctly
                            line = line.Replace(",", ".");

                            if (line.StartsWith("<longitude>"))
                            {
                                line = line.Replace("<longitude>", "");
                                line = line.Replace("</longitude>", "");
                                center_long = Convert.ToDouble(line.Trim(), number_info);
                            }
                            else if (line.StartsWith("<latitude>"))
                            {
                                line = line.Replace("<latitude>", "");
                                line = line.Replace("</latitude>", "");
                                center_lat = Convert.ToDouble(line.Trim(), number_info);
                            }
                            else if (line.StartsWith("<range>"))
                            {
                                line = line.Replace("<range>", "");
                                line = line.Replace("</range>", "");
                                center_range = Convert.ToDouble(line.Trim(), number_info);
                            }
                        }
                        sr.Close();
                        fs.Close();

                        // compute map lat/long
                        if ((center_lat != 0.0) && (center_long != 0.0) && (center_range != 0.0))
                        {
                            double sizex = center_range * Math.Tan(30.11 * (Math.PI / 180.0));
                            double sizey = sizex * Maps[NumMaps].sizeY /Maps[NumMaps].sizeX;
                            double tmp;

                            utmUtil.setReferencePoint(center_lat, center_long);
                            utmUtil.getLatLong(-sizex, 0.0, out tmp, out Maps[NumMaps].lon1);
                            utmUtil.getLatLong( sizex, 0.0, out tmp, out Maps[NumMaps].lon2);
                            utmUtil.getLatLong(0.0,-sizey, out Maps[NumMaps].lat1, out tmp);
                            utmUtil.getLatLong(0.0, sizey, out Maps[NumMaps].lat2, out tmp);
                        }
                        else
                            { kml_has_problems = true; }
                    }
                    else if (File.Exists(txt_file)) // load text file (if KML does not exist)
                    {
                        FileStream fs = new FileStream(txt_file, FileMode.Open);
                        StreamReader sr = new StreamReader(fs);
                        Maps[NumMaps].lat1 = Convert.ToDouble(sr.ReadLine().Trim().Replace(",", "."), number_info);
                        Maps[NumMaps].lon1 = Convert.ToDouble(sr.ReadLine().Trim().Replace(",", "."), number_info);
                        Maps[NumMaps].lat2 = Convert.ToDouble(sr.ReadLine().Trim().Replace(",", "."), number_info);
                        Maps[NumMaps].lon2 = Convert.ToDouble(sr.ReadLine().Trim().Replace(",", "."), number_info);

                        sr.Close();
                        fs.Close();
                    }
                    else // load GMI file (if KML and TXT do not exist)
                    {
                        FileStream fs = new FileStream(gmi_file, FileMode.Open);
                        StreamReader sr = new StreamReader(fs);

                        // check header
                        string header = sr.ReadLine().Trim();
                        if (header.IndexOf("Map Calibration data file v") != 0) { gmi_has_problems = true; }

                        // read image size and check if the size match
                        if(!gmi_has_problems)
                        {
                            sr.ReadLine().Trim(); // skip image name

                            int image_x = Convert.ToInt32(sr.ReadLine().Trim());
                            int image_y = Convert.ToInt32(sr.ReadLine().Trim());

                            if ((image_x != Maps[NumMaps].sizeX) || (image_y != Maps[NumMaps].sizeY)) { gmi_has_problems = true; }
                        }

                        // now read the reference points
                        if (!gmi_has_problems)
                        {
                            // need to extract points with min and max X/Y coordinates (if we have more than 2 ref points)
                            int num_ref_points = 0;
                            RefPointInfo minX, maxX, minY, maxY;
                            minX.i = int.MaxValue; maxX.i = int.MinValue; minX.f = 0; maxX.f = 0;
                            minY.i = int.MaxValue; maxY.i = int.MinValue; minY.f = 0; maxY.f = 0;

                            string line = "";
                            try
                            {
                                while (sr.Peek() != -1)
                                {
                                    line = sr.ReadLine().Trim();
                                    string[] words = line.Split(';');
                                    if (words.Length != 4) { continue; }

                                    // read X and long (0th and 2nd words)
                                    int tmp_i = Convert.ToInt32(words[0].Trim());
                                    double tmp_f = Convert.ToDouble(words[2].Trim().Replace(",", "."), number_info);
                                    if (tmp_i <= minX.i) { minX.i = tmp_i; minX.f = tmp_f; }
                                    if (tmp_i >= maxX.i) { maxX.i = tmp_i; maxX.f = tmp_f; }

                                    // read Y and lat (1st and 3rd words). Move the Y origin to the bottom of the picture (from the top)
                                    tmp_i = Maps[NumMaps].sizeY - Convert.ToInt32(words[1].Trim());
                                    tmp_f = Convert.ToDouble(words[3].Trim().Replace(",", "."), number_info);
                                    if (tmp_i <= minY.i) { minY.i = tmp_i; minY.f = tmp_f; }
                                    if (tmp_i >= maxY.i) { maxY.i = tmp_i; maxY.f = tmp_f; }

                                    num_ref_points++;
                                }
                            }
                            catch (FormatException /*e*/)
                            {
                                // It's OK. 
                                // We ignore the extra info after the ref points.
                            }

                            if ((num_ref_points > 1) && (minX.i != maxX.i) && (minY.i != maxY.i))
                            {
                                Maps[NumMaps].lat1 = minY.f - minY.i*(maxY.f-minY.f)/(maxY.i-minY.i);
                                Maps[NumMaps].lon1 = minX.f - minX.i*(maxX.f-minX.f)/(maxX.i-minX.i);
                                Maps[NumMaps].lat2 = maxY.f + (Maps[NumMaps].sizeY-maxY.i)*(maxY.f-minY.f)/(maxY.i-minY.i);
                                Maps[NumMaps].lon2 = maxX.f + (Maps[NumMaps].sizeX-maxX.i)*(maxX.f-minX.f)/(maxX.i-minX.i);
                            }
                            else { gmi_has_problems = true; } 
                        }

                        sr.Close();
                        fs.Close();
                    }
                }
                catch (Exception e)
                {
                    Utils.log.Error (" LoadMaps - Cannot load data for map " + jpg_name + ", map loading cancelled", e);
                    MessageBox.Show("Cannot load data for map " + jpg_name + ", map loading cancelled", "Error reading map info",
                                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    break;
                }
                if (NumMaps >= MaxNumMaps)
                {
                    MessageBox.Show("Cannot load more than " + MaxNumMaps.ToString() + " maps", "Error reading maps",
                                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    break;
                }
                if (kml_has_problems) 
                {  
                    MessageBox.Show("Error reading corresponding KML file: " + kml_file, "Error reading maps",
                                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    break;
                }
                if (gmi_has_problems) 
                {  
                    MessageBox.Show("Error reading corresponding GMI file: " + gmi_file, "Error reading maps",
                                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    break;
                }

                // make sure 1 is smaller that 2
                if (Maps[NumMaps].lat1 > Maps[NumMaps].lat2)
                    { double tmp = Maps[NumMaps].lat1; Maps[NumMaps].lat1 = Maps[NumMaps].lat2; Maps[NumMaps].lat2 = tmp; }
                if (Maps[NumMaps].lon1 > Maps[NumMaps].lon2)
                    { double tmp = Maps[NumMaps].lon1; Maps[NumMaps].lon1 = Maps[NumMaps].lon2; Maps[NumMaps].lon2 = tmp; }

                NumMaps++;
            }

            Cursor.Current = Cursors.Default;
        }
        private void RemoveBitmaps(int num_reload)
        {
            // assuming that the Maps array is sorted - dispose of the bitmaps outside "num_reload" we can keep at once
            for (int i = num_reload; i < NumMaps; i++)
            {
                if (Maps[i].bmp != null)
                {
                    Maps[i].bmp.Dispose();
                    Maps[i].bmp = null;
                }
            }

            // clean all entries (i.e. MaxNumMaps), if num_reload is 0 (to make sure nothing has left there
            if (num_reload == 0)
            {
                for (int i = 0; i < MaxNumMaps; i++)
                {
                    if (Maps[i].bmp != null)
                    {
                        Maps[i].bmp.Dispose();
                        Maps[i].bmp = null;
                    }
                }
            }
        }
        private int GetLineOverLap(int a1, int a2, int b1, int b2)
        {
            // return length of an overlapped part of two segments, a and b

            // no overlap
            if ((a2 <= b1) || (a1 >= b2)) { return 0; }

            if (a1 <= b1)
            {
                if (a2 > b2) { return b2 - b1; }
                else { return a2 - b1; }
            }
            else
            {
                if (b2 > a2) { return a2 - a1; }
                else { return b2 - a1; }
            }
        }

        public class SingleMapComparer : IComparer
        {
            int IComparer.Compare(Object x1, Object y1)
            {
                if ((x1 is MapInfo) && (y1 is MapInfo))
                {
                    MapInfo x = (MapInfo) x1;
                    MapInfo y = (MapInfo) y1;

                    // map with no overlap is always worse
                    if ((x.overlap != 0.0) && (y.overlap == 0.0)) { return -1; }
                    if ((x.overlap == 0.0) && (y.overlap != 0.0)) { return 1; }
                    if ((x.overlap == 0.0) && (y.overlap == 0.0)) { return 0; }

                    // maps which was removed is always worse
                    if ((x.was_removed == false) && (y.was_removed == true)) { return -1; }
                    if ((x.was_removed == true) && (y.was_removed == false)) { return 1; }
                    if (x.was_removed && y.was_removed) { return 0; }

                    // single-map algorithm
                    if ((x.overlap > 0.65) && (y.overlap > 0.65)) // both have overlap > 0.65
                    {
                        if (x.zoom_level == y.zoom_level) { return 0; }
                        else if (x.zoom_level < y.zoom_level) { return -1; }
                        else { return 1; }
                    }
                    else if ((x.overlap > 0.65) && (y.overlap <= 0.65)) // x has over > 0.65, y not
                    {
                        return -1;
                    }
                    else if ((x.overlap <= 0.65) && (y.overlap > 0.65)) // y has over > 0.65, x not
                    {
                        return 1;
                    }
                    else if ((x.overlap <= 0.65) && (y.overlap <= 0.65)) // both have overlap <= 0.65
                    {
                        if (x.zoom_level == y.zoom_level) { return 0; }
                        else if (x.zoom_level < y.zoom_level) { return -1; }
                        else { return 1; }
                    }
                    { return 0; }
                }
                else
                    { return 0; }
            }
        }
        public class MultiMapComparer : IComparer
        {
            int IComparer.Compare(Object x1, Object y1)
            {
                if ((x1 is MapInfo) && (y1 is MapInfo))
                {
                    MapInfo x = (MapInfo)x1;
                    MapInfo y = (MapInfo)y1;

                    // map with no overlap is always worse
                    if ((x.overlap != 0.0) && (y.overlap == 0.0)) { return -1; }
                    if ((x.overlap == 0.0) && (y.overlap != 0.0)) { return 1; }
                    if ((x.overlap == 0.0) && (y.overlap == 0.0)) { return 0; }

                    // maps which was removed is always worse
                    if ((x.was_removed == false) && (y.was_removed == true)) { return -1; }
                    if ((x.was_removed == true) && (y.was_removed == false)) { return 1; }
                    if (x.was_removed && y.was_removed) { return 0; }

                    // detect close q-factors (within 5%) - this one shall be compared on overlap
                    double ratio = x.qfactor / y.qfactor;
                    if ((ratio > 0.95) && (ratio < 1.05) && (x.overlap != y.overlap))
                    {
                        if (x.overlap > y.overlap) { return -1; }
                        else { return 1; }
                    }

                    // if q-factors are quite different - then compare on q-factor
                    if (x.qfactor == y.qfactor) { return 0; }
                    else if (x.qfactor > y.qfactor) { return -1; }
                    else { return 1; }
                }
                else
                { return 0; }
            }
        }
        public class BitmapExistComparer : IComparer
        {
            int IComparer.Compare(Object x1, Object y1)
            {
                if ((x1 is MapInfo) && (y1 is MapInfo))
                {
                    MapInfo x = (MapInfo)x1;
                    MapInfo y = (MapInfo)y1;

                    // map with bitmaps loaded - put first
                    if ((x.bmp != null) && (y.bmp == null)) { return -1; }
                    if ((x.bmp == null) && (y.bmp != null)) { return 1; }

                    return 0;
                }
                else
                { return 0; }
            }
        }
        public void SelectBestMap(int MapMode)
        {
            // no maps loaded - do not have any bitmaps at all
            if (NumMaps == 0) { NumBitmaps = 0; return; }

            // to shift the best scale (based on the algorithm), at which the peak of quality is reached
            double scaleCorrection = 1.0;

            // set number of bitmaps to use at the same time and the scale correction
            if      (MapMode == 0)  { NumBitmaps = 1; scaleCorrection = 1.0; }
            else if (MapMode == 1)  { NumBitmaps = 4; scaleCorrection = 1.0; }
            else if (MapMode == 2)  { NumBitmaps = 4; scaleCorrection = 0.5;}
            else if (MapMode == 3)  { NumBitmaps = 4; scaleCorrection = 0.25;}
            else    { NumBitmaps = 1; scaleCorrection = 1.0;}

            // compute overlap for each map and map qfactor (function from the zoom level)
            double screen_area = (double)ScreenX * (double)ScreenY;

            for (int i = 0; i < NumMaps; i++)
            {
                double overlap_area = (double)GetLineOverLap(Maps[i].scrX1, Maps[i].scrX2, 0, ScreenX) *
                                      (double)GetLineOverLap(Maps[i].scrY2, Maps[i].scrY1, 0, ScreenY);

                Maps[i].overlap = overlap_area / screen_area;

                double zoom_level = scaleCorrection * (double)(Maps[i].scrX2 - Maps[i].scrX1) / (double)Maps[i].sizeX;

                Maps[i].zoom_level = zoom_level;

                // qfactor has a peak at given zoom (e.g. 1:1), and drops if zoom is different
                Maps[i].qfactor = (zoom_level > 1.0 ? (1.0 / zoom_level) : zoom_level);
            }

            // reset "was_removed" flag
            for (int i = 0; i < NumMaps; i++) { Maps[i].was_removed = false; }

            // Sort
            if (MapMode == 0) // method as use for 1 map - get a map with 0.65 overlap and take with best scale
            {
                IComparer comp_old = new SingleMapComparer();
                bool is_map_not_exist = false;
                int iteration_counter = 0;
                do
                {
                    Array.Sort(Maps, 0, NumMaps, comp_old);
                    is_map_not_exist = AreSomeOsmMapsNotExist();
                    iteration_counter++;
                }
                while (is_map_not_exist && (iteration_counter < 5));
            }
            else  // multimap - compare by the best zoom level. 
            {
                IComparer comp_qual = new MultiMapComparer();
                bool is_map_not_exist = false;
                int iteration_counter = 0;
                do
                {
                    Array.Sort(Maps, 0, NumMaps, comp_qual);
                    is_map_not_exist = AreSomeOsmMapsNotExist();
                    AreAnyCompletelyCoveredMapRemoved();
                    double coverage = 0.0;
                    for (int i = 0; i < NumBitmaps; i++)
                    {
                        if (!Maps[i].was_removed)
                            coverage += Maps[i].overlap;
                    }
                    if (coverage == 0.0)
                    {
                        NumBitmaps = 64;
                        is_map_not_exist = true;
                    }
                    else if (coverage < 0.98)
                    {
                        NumBitmaps = Math.Min(64, (int)(NumBitmaps / coverage + 3));
                        is_map_not_exist = true;
                    }
                    iteration_counter++;
                }
                while ((is_map_not_exist) && (iteration_counter < 5));
            }
        }
        public bool IsMapCompletelyCovered(int i, int j)
        {
            // empty maps - return "not covered"
            if((Maps[i].overlap == 0.0) || (Maps[j].overlap == 0.0)) { return false; }

            // checking if map i is within j, so do not need to be plotted
            // limit to screen size only
            // swap Y, to have correct comparion againt ScreenY
            int i_scrX1 = (Maps[i].scrX1 >= 0 ? Maps[i].scrX1 : 0);
            int i_scrX2 = (Maps[i].scrX2 <= ScreenX ? Maps[i].scrX2 : ScreenX);
            int i_scrY1 = (Maps[i].scrY2 >= 0 ? Maps[i].scrY2 : 0);
            int i_scrY2 = (Maps[i].scrY1 <= ScreenY ? Maps[i].scrY1 : ScreenY);

            int j_scrX1 = (Maps[j].scrX1 >= 0 ? Maps[j].scrX1 : 0);
            int j_scrX2 = (Maps[j].scrX2 <= ScreenX ? Maps[j].scrX2 : ScreenX);
            int j_scrY1 = (Maps[j].scrY2 >= 0 ? Maps[j].scrY2 : 0);
            int j_scrY2 = (Maps[j].scrY1 <= ScreenY ? Maps[j].scrY1 : ScreenY);

            // now compare - if i is within j
            if ((i_scrX1 >= j_scrX1) && (i_scrX2 <= j_scrX2) && (i_scrY1 >= j_scrY1) && (i_scrY2 <= j_scrY2)) { return true; }

            return false;
        }
        public bool AreAnyCompletelyCoveredMapRemoved()
        {
            bool map_removed = false;
            for (int i = (NumBitmaps - 1); i >= 0; i--)
            {
                if (Maps[i].was_removed || Maps[i].overlap == 0.0) { continue; }

                // check if map is completely covered by a map before
                bool completely_covered = false;
                for (int j = 0; j < i; j++)
                {
                    if (IsMapCompletelyCovered(i, j))
                    {
                        completely_covered = true;
                        break;
                    }
                }
                // if yes - remove it (set a flag)
                if (completely_covered) 
                { 
                    Maps[i].was_removed = true;
                    map_removed = true; 
                }
            }
            return map_removed;
        }
        public bool AreSomeOsmMapsNotExist()
        {
            // use this in OsmTilesMode only (as in this case we do not know if tile exist or not) 
            // and if we are NOT downloading from web (as we cannot get it from web)
            if ((OsmTilesMode == true) && (OsmTilesWebDownload == 0))
            {
                bool not_exist = false;
                for (int i = 0; i < NumBitmaps; i++)
                {
                    if (Maps[i].was_removed || Maps[i].overlap == 0.0) { continue; }

                    // bitmap is null (i.e. we want to load it) - but it does not exist
                    if ((Maps[i].bmp == null) && (File.Exists(Maps[i].fname) == false))
                    {
                        Maps[i].was_removed = true;
                        not_exist = true;
                    }
                }
                return not_exist;
            }
            // otherwise return false (i.e. all exist or can be downloaded
            return false;
        }
        public void DrawJpeg(Graphics g)
        {
            RemoveBitmaps(NumBitmaps); // removes unused bitmaps

            // DEBUG - save into file map info after they are sorted
            // PrintMapInfo();

            MapErrors = __MapNoError;
            for(int i = (NumBitmaps-1); i >= 0; i--)
            {
                if (Maps[i].overlap == 0.0) { continue; }
                if (Maps[i].was_removed) { continue; }

                // load the map, if it is null
                if ((Maps[i].bmp == null))
                {
                    Cursor.Current = Cursors.WaitCursor;
                    // download map from web, if no problems with download
                    if ((OsmTilesWebDownload != 0) && !File.Exists(Maps[i].fname) && (MapErrors == __MapNoError))
                    {
                        DownloadOsmTile(Maps[i].fname);
                    }
                    if (File.Exists(Maps[i].fname))
                    {
                        try
                        {
                            Maps[i].bmp = new Bitmap(Maps[i].fname);
                        }
                        // if the size of the picture file is too large, an out of memory exception will occur
                        catch (System.OutOfMemoryException)
                        {
                            Maps[i].bmp = null;
                            MapErrors = __MapErrorOutOfMemory;
                        }
                        catch (Exception e)
                        {
                            Utils.log.Error(" DrawJpeg - new Bitmap", e);
                            Maps[i].bmp = null;
                            MapErrors = __MapErrorReading;
                        }
                    }
                    Cursor.Current = Cursors.Default;
                }
                // check that it was loaded OK
                if (Maps[i].bmp == null) { continue; }

                int bX1 = Maps[i].scrX1;
                int bX2 = Maps[i].scrX2;
                int bY1 = Maps[i].scrY1;
                int bY2 = Maps[i].scrY2;

                // if picture is smaller than the screen - map while picture to screen
                if (((bX2 - bX1) < ScreenX) || ((bY1 - bY2) < ScreenY))
                {
                    Rectangle src_rec = new Rectangle(0, 0, Maps[i].sizeX, Maps[i].sizeY);
                    Rectangle dest_rec = new Rectangle(bX1, bY2, bX2 - bX1, bY1 - bY2);

                    g.DrawImage(Maps[i].bmp, dest_rec, src_rec, GraphicsUnit.Pixel);
                }
                else // else need to take required portion of the whole picture, otherwise it is very slow to map
                {
                    double scalex = (double)Maps[i].sizeX / (bX2 - bX1);
                    double scaley = (double)Maps[i].sizeY / (bY1 - bY2);

                    Rectangle src_rec = new Rectangle((int)( - bX1 * scalex), (int)( - bY2 * scaley),
                                                      (int)(ScreenX * scalex), (int)(ScreenY * scaley));
                    Rectangle dest_rec = new Rectangle(0, 0, ScreenX, ScreenY);

                    g.DrawImage(Maps[i].bmp, dest_rec, src_rec, GraphicsUnit.Pixel);
                }
            }
        }
        public void DrawMovingImage(Graphics g, Bitmap BackBuffer, int dx, int dy)
        {
            ScreenX = BackBuffer.Width; ScreenY = BackBuffer.Height;
            // draw back buffer on screen, take into account the shift
            if (dx > 0)
            {
                if (dy > 0)
                {
                    Rectangle src_rec = new Rectangle(0, 0, ScreenX - dx, ScreenY - dy);
                    g.DrawImage(BackBuffer, dx, dy, src_rec, GraphicsUnit.Pixel);
                }
                else
                {
                    Rectangle src_rec = new Rectangle(0, -dy, ScreenX - dx, ScreenY + dy);
                    g.DrawImage(BackBuffer, dx, 0, src_rec, GraphicsUnit.Pixel);
                }
            }
            else
            {
                if (dy > 0)
                {
                    Rectangle src_rec = new Rectangle(-dx, 0, ScreenX + dx, ScreenY - dy);
                    g.DrawImage(BackBuffer, 0, dy, src_rec, GraphicsUnit.Pixel);
                }
                else
                {
                    Rectangle src_rec = new Rectangle(-dx, -dy, ScreenX + dx, ScreenY + dy);
                    g.DrawImage(BackBuffer, 0, 0, src_rec, GraphicsUnit.Pixel);
                }
            }

            // draw blank parts (as image is shifted). Compute what part to draw (for speed)
            SolidBrush b = new SolidBrush(Back_Color);
            if (dx > 0) //KB
            {
                Rectangle rec = new Rectangle(0, 0, dx, ScreenY);
                g.FillRectangle(b, rec);
            }
            else if(dx < 0)
            {
                Rectangle rec = new Rectangle(ScreenX + dx, 0, -dx, ScreenY);
                g.FillRectangle(b, rec);
            }

            if (dy > 0)
            {
                Rectangle rec = new Rectangle(0, 0, ScreenX, dy);
                g.FillRectangle(b, rec);
            }
            else if(dy < 0)
            {
                Rectangle rec = new Rectangle(0, ScreenY + dy, ScreenX, -dy);
                g.FillRectangle(b, rec);
            }
        }

        private int ToScreenX(double x)
        {
            return (ScreenShiftX + (int)((x - DataLongMin) * Data2Screen * ZoomValue));
        }
        private double ToDataX(int scr_x)
        {
            return (DataLongMin + (double)(scr_x - ScreenShiftX) / (Data2Screen * ZoomValue));
        }
        private int ToScreenY(double y)
        {
            return (ScreenShiftY - (int)((y - DataLatMax) * RatioMeterLatLong * Data2Screen * ZoomValue));
        }
        private double ToDataY(int scr_y)
        {
            return (DataLatMax + (double)(ScreenShiftY - scr_y) / (RatioMeterLatLong * Data2Screen * ZoomValue));
        }
        private void DrawCurrentPoint(Graphics g, double Long, double Lat, int size, Color col)
        {
            int x_point = ToScreenX(Long);
            int y_point = ToScreenY(Lat);

            SolidBrush drawBrush = new SolidBrush(col);
            g.FillEllipse(drawBrush, x_point - size / 2, y_point - size / 2, size, size);
        }
        //KB draw arrow   (size = radius)
        private void DrawArrow(Graphics g, double Long, double Lat, int heading_int, int size, Color col)
        {
            int x0 = ToScreenX(Long);
            int y0 = ToScreenY(Lat);
            
            SolidBrush br = new SolidBrush(col);

            // draw heading - 4 points arrow
            // needle
            Point[] pa = new Point[3];
            pa[0].X = (int)(x0 + size * Math.Cos(Math.PI / 2.0 - (heading_int * Math.PI / 180.0)));
            pa[0].Y = (int)(y0 - size * Math.Sin(Math.PI / 2.0 - (heading_int * Math.PI / 180.0)));

            // point just opposite (signs inverted)
            pa[1].X = (int)(x0 - size * 2 / 3 * Math.Cos(Math.PI / 2.0 - (heading_int * Math.PI / 180.0)));
            pa[1].Y = (int)(y0 + size * 2 / 3 * Math.Sin(Math.PI / 2.0 - (heading_int * Math.PI / 180.0)));

            // point A - at +150 deg from current
            pa[2].X = (int)(x0 + size * Math.Cos(Math.PI / 2.0 - ((heading_int + 150) * Math.PI / 180.0)));
            pa[2].Y = (int)(y0 - size * Math.Sin(Math.PI / 2.0 - ((heading_int + 150) * Math.PI / 180.0)));
            if (heading_int == 720) br.Color = Color.DimGray;
            g.FillPolygon(br, pa);

            // point B - at +210 deg from current
            pa[2].X = (int)(x0 + size * Math.Cos(Math.PI / 2.0 - ((heading_int + 210) * Math.PI / 180.0)));
            pa[2].Y = (int)(y0 - size * Math.Sin(Math.PI / 2.0 - ((heading_int + 210) * Math.PI / 180.0)));
            br.Color = modifyColor(br.Color, +100);
            g.FillPolygon(br, pa);
        }

        Color modifyColor(Color c, int mod)     //adds mod to color components
        {
            int r, g, b;
            r = c.R + mod; if (r > 255) r = 255; if (r < 0) r = 0;
            g = c.G + mod; if (g > 255) g = 255; if (g < 0) g = 0;
            b = c.B + mod; if (b > 255) b = 255; if (b < 0) b = 0;
            return Color.FromArgb(r, g, b);
        }

/* original
        private void DrawTrackLine(Graphics g, Pen p, float[] PlotLong, float[] PlotLat, int PlotSize, bool plot_dots)
        {
            // idea is to draw max 128 points (as it is slow),
            // so first select only points with are withing the map
            // reduce further size of the points to max 128
            int begin = Environment.TickCount;

            const int max_plot_size = 128;
            int max_points = PlotSize;

            SolidBrush drawBrush = new SolidBrush(p.Color); // brush to draw points
            int pen_size = (int) p.Width;

            int[] xmap = new int[PlotSize];  // x, y points on the bitmap
            int[] ymap = new int[PlotSize];
            int[] is_in = new int[PlotSize];  // point status: 1-in map, 0-out of map
            int[] kill = new int[PlotSize];   // set to 1 if point to be deleted

            // fill data
            for (int i = 0; i < max_points; i++)
            {
                xmap[i] = ToScreenX(PlotLong[i]);
                ymap[i] = ToScreenY(PlotLat[i]);

                if ((xmap[i] >= 0) && (xmap[i] < ScreenX) && (ymap[i] >= 0) && (ymap[i] < ScreenY)) { is_in[i] = 1; }
                else { is_in[i] = 0; }

                kill[i] = 0;
            }

            // pass 1 - kill all out points surrounded by out points
            for (int i = 1; i < max_points - 1; i++)
            {
                if ((is_in[i - 1]) == 0 && (is_in[i] == 0) && (is_in[i + 1] == 0)) { kill[i] = 1; }
            }

            // kill first and last, if we have 2 "out" at the ends
            if (max_points >= 2)
            {
                if ((is_in[0]) == 0 && (is_in[1] == 0)) { kill[0] = 1; }
                if ((is_in[max_points - 1]) == 0 && (is_in[max_points - 2] == 0)) { kill[max_points - 1] = 1; }
            }

            // copy good points
            int point_counter = 0;
            for (int i = 0; i < max_points; i++)
            {
                if (kill[i] == 1) { continue; }  // skip point marked to be removed
                if (point_counter != i)     // copy data, but not into itself
                {
                    xmap[point_counter] = xmap[i];
                    ymap[point_counter] = ymap[i];
                    is_in[point_counter] = is_in[i];
                    kill[point_counter] = kill[i];
                }
                point_counter++;
            }
            max_points = point_counter;

            // pass 2 - repeat: kill an "in" points surrounded by in points, before required num pointe reached
            int repeat_counter = 0;
            while (max_points > max_plot_size)
            {
                for (int i = 1; i < max_points - 1; i++)
                {
                    if ((kill[i - 1]) == 1) { continue; }  // prev point already marked for deletion - skip
                    if ((is_in[i - 1]) == 1 && (is_in[i] == 1) && (is_in[i + 1] == 1)) { kill[i] = 1; }
                }
                // copy good points
                point_counter = 0;
                for (int i = 0; i < max_points; i++)
                {
                    if (kill[i] == 1) { continue; }  // skip point marked to be removed
                    if (point_counter != i)     // copy data, but not into itself
                    {
                        xmap[point_counter] = xmap[i];
                        ymap[point_counter] = ymap[i];
                        is_in[point_counter] = is_in[i];
                        kill[point_counter] = kill[i];
                    }
                    point_counter++;
                }
                max_points = point_counter;

                repeat_counter++;
                if (repeat_counter > 7) { break; }
            }

            // OK, all done ... fill array and draw lines between "out" points. OR: draw as dots
            int first_plot_point = 0;
            int num_to_plot = 0;
            for (int i = 0; i < max_points; i++)
            {
                // we found an out point - draw the segment
                num_to_plot = i - first_plot_point + 1;
                if ((is_in[i] == 0) && (num_to_plot > 1))
                {
                    Point[] points = new Point[num_to_plot];
                    for (int k = 0; k < num_to_plot; k++)
                    {
                        points[k].X = xmap[first_plot_point + k];
                        points[k].Y = ymap[first_plot_point + k];

                        if (plot_dots)
                        { g.FillEllipse(drawBrush, points[k].X - pen_size / 2, points[k].Y - pen_size / 2, pen_size, pen_size); }
                    }

                    if(plot_dots == false) 
                        { g.DrawLines(p, points); }

                    first_plot_point = i + 1;
                }
            }
            // the last segment
            num_to_plot = max_points - first_plot_point;
            if (num_to_plot > 1)
            {
                Point[] points = new Point[num_to_plot];
                for (int k = 0; k < num_to_plot; k++)
                {
                    points[k].X = xmap[first_plot_point + k];
                    points[k].Y = ymap[first_plot_point + k];

                    if (plot_dots)
                    { g.FillEllipse(drawBrush, points[k].X - pen_size / 2, points[k].Y - pen_size / 2, pen_size, pen_size); }
                }
                if (plot_dots == false)
                    { g.DrawLines(p, points); }
            }
            int end = Environment.TickCount;


            Font drawFont = new Font("Arial", 8, FontStyle.Regular);
            //g.DrawString("Length=" + points.Length, drawFont, drawBrush, 0, 20);
            //g.DrawString("DPoints="+DrawPoints, drawFont, drawBrush, 0, 40);
            g.DrawString("clock=" + (end-begin), drawFont, drawBrush, 0, 60);
        }
        */



        // KB-Version2
        private void DrawTrackLine(Graphics g, Pen p, float[] PlotLong, float[] PlotLat, int PlotSize, bool plot_dots, float CurLong, float CurLat, bool ShowDistance, double unit_cff, string unit_name)
        {
            // idea is to draw max 128 points (as it is slow),
            // so first select only points which are within the map
            // reduce further size of the points to max 128
            //int begin = Environment.TickCount;
            //String str = "";// = new String("");

            // Current position on the screen
            int CurrentX = 0; 
            int CurrentY = 0;
            if (ShowDistance == true)
            {
                CurrentX = ToScreenX(CurLong);
                CurrentY = ToScreenY(CurLat);
            }
            // variables to find the point on the track with the minimum distance to the current position
            int MinDistance = int.MaxValue;
            int MinDistanceX = 0;
            int MinDistanceY = 0;
			int MinDistanceIndex = 0;

            SolidBrush drawBrush = new SolidBrush(p.Color); // brush to draw points
            int pen_size = (int)p.Width;
            const int SegmentSize = 64;
            Point[] points = new Point[SegmentSize];

            // fill in data
            bool LineStarted = false;
            int i = 0;      //0..Plotsize
            int d = 0;      //number of points to draw in a segment
            while (i < PlotSize)
            {
                bool PointInMap = false;
                bool IgnorePoint = false;
                //Point point = new Point(ToScreenX(PlotLong[i]), ToScreenY(PlotLat[i]));
                points[d].X = ToScreenX(PlotLong[i]);
                points[d].Y = ToScreenY(PlotLat[i]);

                if ((points[d].X >= 0) && (points[d].X < ScreenX) && (points[d].Y >= 0) && (points[d].Y < ScreenY))    //only points within screen
                { 
                    PointInMap = true; 
                    LineStarted = true; 
                }

                if (ShowDistance == true)
                {
                    // Find Point on track with minimum Distance to current position
                    int xDist = points[d].X - CurrentX;
                    int yDist = points[d].Y - CurrentY;
                    int Dist = xDist * xDist + yDist * yDist;		// We do not need to use sqrt, reduce CPU load

                    if( MinDistance > Dist )
                    {
                        MinDistance = Dist;
                        MinDistanceX = points[d].X;
                        MinDistanceY = points[d].Y;
						MinDistanceIndex = i;
                    }
                }

                i++;

                if (d > 0)  //erster punkt auf jeden Fall
                {
                    if (PointInMap && (Math.Abs(points[d].X - points[d - 1].X) < 5) && (Math.Abs(points[d].Y - points[d - 1].Y) < 5))  //only points which differ significant from previous
                    { IgnorePoint = true; }

                    if (!LineStarted)   //&& !PointInMap (implizit)
                    {
                        points[d - 1] = points[d];    //overwrite first point (outside screen)
                        IgnorePoint = true;
                    }
                }
                if (!IgnorePoint)
                { d++; }   //validate point; d= number of points
                if ((LineStarted && !PointInMap && !IgnorePoint) || d >= SegmentSize || i >= PlotSize)
                {
                    //str += d; str += " ";
                    //Draw Line segment
                    if (plot_dots)
                    {
                        for (int j = 0; j < d; j++)
                        {
                            g.FillEllipse(drawBrush, points[j].X - pen_size / 2, points[j].Y - pen_size / 2, pen_size, pen_size);
                        }
                    }
                    else
                    {
                        while (d < SegmentSize)     //DrawLines draws always complete array -> fill with last point
                        {
                            points[d] = points[d-1];
                            d++;
                        }
                        g.DrawLines(p, points);
                    }

                    points[0] = points[d-1];    //connect segments
                    d = 1;
                    LineStarted = false;
                }
            }

            if (ShowDistance == true)
            {
                // If the Current Position is outside of the Screen or the nearest point on the track is outside
                // of the screen, show a line between current position and track to indicate the direction to follow
                if (CurrentX <= 0 || CurrentX >= ScreenX || CurrentY <= 0 || CurrentY >= ScreenY ||
                     MinDistanceX <= 0 || MinDistanceX >= ScreenX || MinDistanceY <= 0 || MinDistanceY >= ScreenY)    //only points within screen
                {
                    //float OldPenWidth = p.Width;
                    //Color OldPenColor = p.Color;
                    Font drawFont = new Font("Arial", 8, FontStyle.Regular);

                    // Calculate the distance between track and current position
					double xCurPos, yCurPos, xMinPos, yMinPos;
                    utmUtil.getXY(CurLat, CurLong, out xCurPos, out yCurPos);
					utmUtil.getXY(PlotLat[MinDistanceIndex], PlotLong[MinDistanceIndex], out xMinPos, out yMinPos);
                    double deltaS = Math.Sqrt((xCurPos - xMinPos) * (xCurPos - xMinPos) + (yCurPos - yMinPos) * (yCurPos - yMinPos));
					deltaS = deltaS * unit_cff;

                    string strDistance = "Distance to T2F: " + deltaS.ToString("0.0") + unit_name;
                    SizeF TextSize = g.MeasureString(strDistance, drawFont);

                    // We draw the line between current position and T2F with half of the T2F width and somewhat lighter
                    p.Width = Math.Max( 2, p.Width / 2 );
                    p.Color = modifyColor(p.Color, +100);
                    // not supported by .NET CF??? p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawLine( p, CurrentX, CurrentY, MinDistanceX, MinDistanceY );
                    
                    // Draw a black box to show distance between current position and track2follow
                    p.Color = Color.Gray;
                    const int LineWidth = 1;
                    p.Width = LineWidth;
                    int TextBoxHeigth = (int) (TextSize.Height + 0.5);
                    drawBrush.Color = Color.Black;
                    g.FillRectangle(drawBrush, 0, ScreenY - TextBoxHeigth - LineWidth, ScreenX, ScreenY);
                    g.DrawLine(p, 0, ScreenY - TextBoxHeigth - LineWidth, ScreenX, ScreenY - TextBoxHeigth - LineWidth);

                    // Draw Distance to Track information
                    drawBrush.Color = Color.White;
                    g.DrawString(strDistance, drawFont, drawBrush, 2, ScreenY - TextBoxHeigth);

                    // restore old pen settings for further use
                    //p.Width = OldPenWidth;
                    //p.Color = OldPenColor;
                }
            }


            //int end = Environment.TickCount;

            //Font drawFont = new Font("Arial", 8, FontStyle.Regular);
            //g.DrawString("Length=" + PlotSize, drawFont, drawBrush, 0, 20);
            //g.DrawString("d=" + str, drawFont, drawBrush, 0, 40);
            //g.DrawString("ticks=" + (end - begin), drawFont, drawBrush, 0, 60);
        }



        private void DrawTickLabel(Graphics g, Pen p, int tick_dist_screen, double tick_dist_units, string unit_name)
        {
            // draw text: Create font and brush.
            Font drawFont = new Font("Arial", 8, FontStyle.Regular);
            SolidBrush drawBrush = new SolidBrush(p.Color);

            // draw map info, check the text size to draw in the right corner
            string str_map = GetBestMapName();
            SizeF size = g.MeasureString(str_map, drawFont);
            g.DrawString(str_map, drawFont, drawBrush, ScreenX - size.Width - 2, 0);

            // tick distance
            int x_point1 = 5;
            int x_point2 = x_point1 + tick_dist_screen;

            int y_point = (int) size.Height;

            p.Width = 1;
            g.DrawLine(p, x_point1, y_point - 5, x_point1, y_point + 5);
            g.DrawLine(p, x_point2, y_point - 5, x_point2, y_point + 5);

            p.Width = 3;
            g.DrawLine(p, x_point1, y_point, x_point2, y_point);

            string text = tick_dist_units.ToString() + unit_name;

            g.DrawString(text, drawFont, drawBrush, x_point1 + 2, 0);
        }
        public string GetBestMapName()
        {
            string str_map = "no map";

            if((NumMaps != 0) && (Maps[0].overlap != 0.0))
            {
                if (OsmTilesMode)
                {
                    string str_map_x = Path.GetFileNameWithoutExtension(Maps[0].fname);
                    string str_map_y = Path.GetFileName(Path.GetDirectoryName(Maps[0].fname));
                    string str_map_zm = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Maps[0].fname)));
                    str_map = str_map_zm + "/" + str_map_y + "/" + str_map_x;
                }
                else
                {
                    str_map = Path.GetFileNameWithoutExtension(Maps[0].fname);
                }
            }

            if      (MapErrors == __MapErrorReading)  
            {
                str_map += "\nRead Error";
                Utils.log.Debug ("Map ERROR " + str_map);
            }
            else if (MapErrors == __MapErrorOutOfMemory)
            {
                str_map += "\nOut of Memory Error\n(Filesize too large)";
            }
            else if (MapErrors == __MapErrorDownload) 
            { 
                str_map += "\nDownload Error";
                Utils.log.Debug ("Map ERROR " + str_map);
            }

            return str_map;
        }
        private double TickMark(double x, int nx)
        {
            double num, mult, mant;
            int inum;

            if (nx == 0) nx = 1;
            num = Math.Log10(Math.Abs((double)(x / nx)));
            inum = (int)Math.Floor(num);
            mult = Math.Pow(10.0, inum);
            mant = Math.Pow(10.0, num - inum);
            if (mant > 7.5) mant = 10.0;
            else if (mant > 3.5) mant = 5.0;
            else if (mant > 1.5) mant = 2.0;
            else mant = 1.0;
            return (mant * mult);
        }
        private void SetAutoScale(float[] PlotLong, float[] PlotLat, int PlotSize, bool lifeview)
        {
            // compute difference in Lat/Long scale
            utmUtil.setReferencePoint(PlotLat[PlotSize - 1], PlotLong[PlotSize - 1]);

            utmUtil.getLatLong(100.0, 100.0, out Meter2Lat, out Meter2Long);
            Meter2Lat = Math.Abs(Meter2Lat - PlotLat[PlotSize - 1]) / 100.0;
            Meter2Long = Math.Abs(Meter2Long - PlotLong[PlotSize - 1]) / 100.0;
            RatioMeterLatLong = Meter2Long / Meter2Lat;

            // during liveview (logging), set a fixed scale with current point in the middle
            if (lifeview || (PlotSize <= 1))
            {
                double last_x = PlotLong[PlotSize - 1];
                double last_y = PlotLat[PlotSize - 1];
                DataLongMin = last_x - Meter2Long * DefaultZoomRadius;
                DataLongMax = last_x + Meter2Long * DefaultZoomRadius;
                DataLatMin = last_y - Meter2Lat * DefaultZoomRadius;
                DataLatMax = last_y + Meter2Lat * DefaultZoomRadius;
            }
            else
            {
                // compute data limits
                DataLongMin = 1.0E9; DataLongMax = -1.0E9; DataLatMin = 1.0E9; DataLatMax = -1.0E9;
                for (int i = 0; i < PlotSize; i++)
                {
                    if (PlotLong[i] < DataLongMin) { DataLongMin = PlotLong[i]; }
                    if (PlotLong[i] > DataLongMax) { DataLongMax = PlotLong[i]; }
                    if (PlotLat[i] < DataLatMin) { DataLatMin = PlotLat[i]; }
                    if (PlotLat[i] > DataLatMax) { DataLatMax = PlotLat[i]; }
                }
            }

            // set plot scale, must be equal for both axis to plot map
            double xsize = DataLongMax - DataLongMin;
            double ysize = DataLatMax - DataLatMin;

            // check that size not 0
            const double delta = 0.001;
            if (xsize <= delta)
            { xsize += delta; DataLongMax += delta / 2; DataLongMin -= delta / 2; }
            if (ysize <= delta)
            { ysize += delta; DataLatMax += delta / 2; DataLatMin -= delta / 2; }

            double xscale = (double)ScreenX / xsize;
            double yscale = (double)ScreenY / (ysize * RatioMeterLatLong);
            Data2Screen = (yscale > xscale ? xscale : yscale);
        }

        // draw map and 2 lines (main and "to follow"). Shift is the x/y shift of the second line origin.
        public void DrawMaps(Graphics g, Bitmap BackBuffer, Graphics BackBufferGraphics, 
                             bool MouseMoving, bool lifeview, int MapMode, 
                             double unit_cff, string unit_name,
                             float[] PlotLong, float[] PlotLat, int PlotSize, Color line_color, int line_width, bool plot_dots,
                             float[] PlotLong2, float[] PlotLat2, int PlotSize2, Color line_color2, int line_width2, bool plot_dots2,
                             float[] CurLong, float[] CurLat, int heading, Color CurrentGpsLedColor)
        {
            /*
            if ((PlotSize == 0) && (PlotSize2 == 0)) // nothing to draw     KB: draw last position
            {
                g.Clear(Back_Color);

                // print some info
                Font tmpFont = new Font("Arial", 8, FontStyle.Regular);
                SolidBrush tmpBrush = new SolidBrush(Fore_Color);

                string tmpStr = "No data to plot:";
                SizeF size = g.MeasureString(tmpStr, tmpFont);
                g.DrawString(tmpStr, tmpFont, tmpBrush, 3, 5);
                tmpStr = "If logging, wait for the first sample.";
                g.DrawString(tmpStr, tmpFont, tmpBrush, 3, 5 + size.Height);
                tmpStr = "If viewing file, make sure it is not empty.";
                g.DrawString(tmpStr, tmpFont, tmpBrush, 3, 5 + size.Height*2);
                return;
            }*/

            // if back buffer exists and picture is moving - draw existing picture
            if (MouseMoving && (BackBuffer != null))
            {
                DrawMovingImage(g, BackBuffer, ScreenShiftX - ScreenShiftSaveX, ScreenShiftY - ScreenShiftSaveY);
                return;
            }

            // store current drawing screen size and set scale from "main" or the "track-to-follow" (if main not exist)
            ScreenX = BackBuffer.Width; ScreenY = BackBuffer.Height;
            if (ShowTrackToFollowMode == ShowTrackToFollow.T2FStart && PlotSize2 != 0) 
            { 
                // Show start position of track to follow
                SetAutoScale(PlotLong2, PlotLat2, 1, false); }
            else if (ShowTrackToFollowMode == ShowTrackToFollow.T2FEnd && PlotSize2 != 0)
            {
                // Show end position of track to follow
                float[] Long = { PlotLong2[PlotSize2 - 1] };
                float[] Lat = { PlotLat2[PlotSize2 - 1] };
                SetAutoScale( Long, Lat, 1, false);
            }
            else if (lifeview) 
            { 
                // Show current GPS position (last position)
                SetAutoScale(CurLong, CurLat, 1, lifeview); 
            }
            else if (PlotSize != 0) 
            { 
                // Show all points of loaded track
                SetAutoScale(PlotLong, PlotLat, PlotSize, lifeview); 
            }
            else if (PlotSize2 != 0) 
            { 
                // Show all points of track to follow
                SetAutoScale(PlotLong2, PlotLat2, PlotSize2, false); 
            }
            else 
            { 
                // Show last position
                SetAutoScale(CurLong, CurLat, 1, false); 
            }

            // need to draw the picture first into back buffer
            BackBufferGraphics.Clear(Back_Color);

            // in OSM tile mode, required map array is created based on the current screen coordinates
            if (OsmTilesMode) { FillOsmTiles(); }

            // Update screen coordinates for all maps
            for (int i = 0; i < NumMaps; i++)
            {
                Maps[i].scrX1 = ToScreenX(Maps[i].lon1);
                Maps[i].scrX2 = ToScreenX(Maps[i].lon2);
                Maps[i].scrY1 = ToScreenY(Maps[i].lat1);
                Maps[i].scrY2 = ToScreenY(Maps[i].lat2);
            }

            // select the best map and draw it
            SelectBestMap(MapMode);
            DrawJpeg(BackBufferGraphics);

            Pen pen = new Pen(Color.LightGray, 1);

            // draw the track-to-follow line
            if (PlotSize2 != 0)
            {
                pen.Color = line_color2;
                pen.Width = line_width2;
                DrawTrackLine(BackBufferGraphics, pen, PlotLong2, PlotLat2, PlotSize2, plot_dots2, CurLong[0], CurLat[0], true, unit_cff, unit_name);
                DrawCurrentPoint(BackBufferGraphics, PlotLong2[PlotSize2 - 1], PlotLat2[PlotSize2 - 1], line_width2, line_color2);
            }

            // draw the main track line
            if (PlotSize != 0)
            {
                pen.Color = line_color;
                pen.Width = line_width;
                DrawTrackLine(BackBufferGraphics, pen, PlotLong, PlotLat, PlotSize, plot_dots, 0, 0, false, unit_cff, unit_name);
                // draw last point larger by 5 points
                DrawCurrentPoint(BackBufferGraphics, PlotLong[PlotSize - 1], PlotLat[PlotSize - 1], line_width + 5, line_color);
            }
            if (lifeview)
            {
                DrawArrow(BackBufferGraphics, CurLong[0], CurLat[0], heading, line_width * 2 + 5, line_color);
                // draw gps led point
                //DrawCurrentPoint(BackBufferGraphics, CurLong[0], CurLat[0], line_width, CurrentGpsLedColor);
            }

            // draw tick label and map name
            double screen_width_units = (ScreenX / (Data2Screen * ZoomValue)) * unit_cff / Meter2Long;
            // tick distance in "units" (i.e. km or miles)
            double tick_distance_units = TickMark(screen_width_units, 4);
            // tick distance in screen pixels
            int tick_distance_screen = (int)(tick_distance_units * (Data2Screen * ZoomValue) / unit_cff * Meter2Long);

            pen.Color = line_color;
            DrawTickLabel(BackBufferGraphics, pen, tick_distance_screen, tick_distance_units, unit_name);

            // draw back buffer on screen
            g.DrawImage(BackBuffer, 0, 0);
        }

        // make sure that the central point is stationary after zoom in / zoom out
        public void ZoomIn()
        {
            ScreenShiftX -= (ScreenX / 2 - ScreenShiftX) / 2;
            ScreenShiftY -= (ScreenY / 2 - ScreenShiftY) / 2;
            ZoomValue *= 1.5;
        }
        public void ZoomOut()
        {
            ScreenShiftX += (int)((ScreenX / 2 - ScreenShiftX) * (1.0 - 1.0 / 1.5));
            ScreenShiftY += (int)((ScreenY / 2 - ScreenShiftY) * (1.0 - 1.0 / 1.5));
            ZoomValue /= 1.5;
        }

        // DEBUG printout - called in DrawJpeg (make sure it is commented out in release).
        private void PrintMapInfo()
        {
            try
            {
                Utils.log.Debug("--------------------------------------------------------------------------------------------------------------------");

                for (int i = 0; i < NumMaps; i++)
                {
                    string str_map = "";
                    string str_map_x = Path.GetFileNameWithoutExtension(Maps[i].fname);
                    string str_map_y = Path.GetFileName(Path.GetDirectoryName(Maps[i].fname));
                    string str_map_zm = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Maps[i].fname)));
                    str_map = str_map_zm + "/" + str_map_y + "/" + str_map_x;
                    while (str_map.Length < 30) { str_map += " "; }

                    Utils.log.Debug(str_map +
                                 " scr_sizeX = " + (Maps[i].scrX2 - Maps[i].scrX1).ToString("0000000") +
                                 " scr_sizeY = " + (Maps[i].scrY2 - Maps[i].scrY1).ToString("0000000") +
                                 " lat1 = " + Maps[i].lat1.ToString("00.000000") + " lon1 = " + Maps[i].lon1.ToString("00.000000") +
                                 " lat2 = " + Maps[i].lat2.ToString("00.000000") + "lon2 = " + Maps[i].lon2.ToString("00.000000") +
                                 " zoom_level = " + Maps[i].zoom_level.ToString("0000.000") +
                                 " q_factor = " + Maps[i].qfactor.ToString("0.0000") +
                                 " overlap = " + Maps[i].overlap.ToString("0.000") +
                                 " bitmap==null = " + (Maps[i].bmp == null ? " no  " : " yes ") +
                                (Maps[i].was_removed ? " <-removed " : "  "));
                }
            }
            catch (Exception e)
            {
                Utils.log.Error (" PrintMapInfo ", e);
            }
            finally
            {
                Utils.log.Debug("--------------------------------------------------------------------------------------------------------------------");
            }
        }
    }
}
