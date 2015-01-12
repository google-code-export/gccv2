//#define debugNav

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
using System.Threading;
using GpsCycleComputer;

#endregion

namespace GpsUtils
{
    // Utilities to work with JPEG maps and plot a track
    public class MapUtil
    {
        public Form1 parent;
        public Color Back_Color = Color.White;
        public Color Fore_Color = Color.Black;
        private Bitmap hourglass;

        public MapUtil() 
        {
            
            for (int i = 0; i < MaxNumMaps; i++)
            {
                Maps[i] = new MapInfo();
                Maps[i].bmp = null;
                Maps[i].fname = "";
            }
            //hourglass = new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("GpsSample.Graphics.hourglass.png"));
            hourglass = Form1.LoadBitmap("hourglass.png");
            clearNav(false);
        }

        public class MapInfo            //class is 4 times faster in sorting than struct
        {
            public string fname;                   // filename in the current I/O folder
            public double lat1, lon1, lat2, lon2;  // lat/long of the bottom left and top right corners
            public int sizeX, sizeY;               // X/Y image size
            public double overlap;                 // overlap in % of the current view (to select best map)
            public double zoom_level;              // ratio of the map screen size to the original size
            public double qfactor;                 // quality function, based on map zoom level
            public bool was_removed;               // flag that map was completely covered and was removed
            public int scrX1_ix, scrX2_iz, scrY1_iy, scrY2; // current screen coordinates in pixels
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
        //private int osmZoom = -2;
        private string osmMapName = "";
        public MapInfo[] Maps = new MapInfo[MaxNumMaps];
        //public MapInfo[] Maps = new MapInfo[MaxNumMaps];
        public string MapsFilesDirectory;
        string mapCopyright = null;

        public enum ShowTrackToFollow
        {
            T2FOff,
            T2FStart,
            T2FEnd,
            T2FCurrent
        };
        //public enum Voice
        //{
        //    Off,
        //    On,
        //    OnEssential,
        //}
        //public Voice VoiceCommand = Voice.Off;

        // Show the current position on the map or the start/stop position of the track
        public ShowTrackToFollow ShowTrackToFollowMode = ShowTrackToFollow.T2FOff;

        public bool hideAllInfos = false;   //not permanent stored
        public bool hideTrack = false;
        public bool hideT2f = false;
        public bool hideMap = false;

        public bool autoHideButtons = false;
        public bool showMapLabel = true;
        public bool showNav = true;
        public bool show_nav_button = true;
        public bool doneVoiceCommand = false;
        public bool reDownloadMaps = false;

        private int ScreenX = 100;         // screen (Backbuffer) size in pixels
        private int ScreenY = 100;
        public double Long2Pixel = 1.0;  // Coefficient to convert from data to screen values
        public double Lat2Pixel = 1.0;
        public double ZoomValue = 1.0;    // zoom coefficient, to multiply Data2Screen
        public int DefaultZoomRadius = 200;    //default zoom in m (in either direction)
        private double DataLongMiddle = 0.0;
        private double DataLatMiddle = 0.0;
        private int ScreenTileRefX = 0;     //reference upper left corner of all tiles in screen coordinates
        private int ScreenTileRefY = 0;

        // vars required to support screen shifting
        public double LongShift = 0.0;      // (total) map shift (degree)
        public double LatShift = 0.0;
        public double LongShiftSave = 0.0;  // map shift as mouse move started (degree)
        public double LatShiftSave = 0.0;
        public double Lat2PixelSave;        // save Lat2Pixel because otherwise map jumps up and down while shifting

        // vars to work with OpenStreetMap tiles
        private bool OsmTilesMode = false;
        private const int OsmNumZoomLevels = 24;    //max zoom level is 23   -   most servers do not respond on zoom level 18 and higher
        private bool[] OsmZoomAvailable = new bool[OsmNumZoomLevels]; // if a directories for this zoom level found (true/false)
        private int[] OsmNumberOfTilesInZoom = new int[OsmNumZoomLevels];
        public string OsmServer = "http://";
        private string OsmFileExtension = "";
        private int OsmN = 1;       //number of tiles in x
        private int OsmNz = 1;      //inclusive tile zoom
        private int OsmZoom = 1;    //zoom level 0..23
        private int centerTile_x = 0;
        private int centerTile_y = 0;
        private double centerTile_yd = 0.0;
        private int tilezoom = 0;   // -1 = zoom out;   +1 = zoom in
        private int tilePixel = 256;    //pixel of one tile

        // map error codes
        private const int __MapNoError = 0;
        private const int __MapErrorReading = 1;
        private const int __MapErrorDownload = 2;
        private const int __MapErrorOutOfMemory = 3;
        private const int __MapErrorNotFound = 4;

        private int MapErrors = __MapNoError;
        public void ClearMapError()
        { MapErrors = __MapNoError; }

        public struct CornerInfo
        {
            public int Type;        //0=straight(invalid)  1=half turn   2=turn   3=sharp turn
            public float Long;      //Long of corner
            public float Lat;       //Lat of corner
            public int IndexT2F;
            public int processedIndex;   //last processed index (always valid (if!=0))
            public int dir1;        //direction before corner
            public int dir2;        //direction after corner
            public int angle;       //angle of corner (-180..180 degree; 90=right)
            public int direction;   //direction (-180..180 degree) from CurrentPos
            public int distance;    //distance in m from CurrentPos
            public bool voicePlayed;    //flag voice played
        } public  CornerInfo corner;

        public struct NavInfo
        {
            public bool backward;           //navigate backward
            public int ix;                 //index of point closest to CurrentPosition
            public double ixd_intersec;     //index of intersection with calculated fraction
            public int ix_next_drive;       //index of point next to overdrive
            public double MinDistance;     //CurPos to Track
            public double LongIntersection; //long of min distance (intersection) point
            public double LatIntersection;  //lat of min distance (intersection) point
            public double Distance2Dest;     //inclusive MinDistance
            public int Angle100mAhead;       //-180 .. 180 degree; related to Cur; 0=North
            public int ShortSearch;          //if != 0: only short search from ix until minDistance gets larger; decremented every second
            public bool voicePlayed_toRoute;
            public bool voicePlayed_dest;
            public string strCmd;
            public string strDistance2Dest;
            public Point[] Symbol;
            public Orientation orient;
            public bool SkyDirection;
        } public NavInfo nav;
        public void clearNav(bool backward)
        {
            nav.backward = backward;
            if (backward && parent.Plot2ndCount > 0)
                nav.ix = parent.Plot2ndCount - 1;
            else
                nav.ix = 0;
            nav.ixd_intersec = nav.ix;
            nav.ix_next_drive = nav.ix;
            nav.MinDistance = -1.0;
            nav.ShortSearch = -1;        //no full search of minDistance
            nav.strCmd = "";
            nav.strDistance2Dest = "";
            nav.Symbol = null;
            nav.SkyDirection = false;
            invalidateCorner();
        }
        //Navigation Symbols
        Point[] arrow_r = new Point[] {
            new Point(20, 100),
            new Point(20, 20),
            new Point(60, 20),
            new Point(60, 0),
            new Point(100, 30),
            new Point(60, 60),
            new Point(60, 40),
            new Point(40, 40),
            new Point(40, 100), };
        Point[] arrow_hr = new Point[] {
            new Point(20, 100),
            new Point(20, 50),
            new Point(48, 22),
            new Point(34, 8),
            new Point(84, 0),
            new Point(76, 50),
            new Point(62, 36),
            new Point(40, 58),
            new Point(40, 100), };
        Point[] arrow_sr = new Point[] {
            new Point(14, 100),
            new Point(14, 0),
            new Point(50, 0),
            new Point(78, 28),
            new Point(92, 14),
            new Point(100, 64),
            new Point(50, 56),
            new Point(64, 42),
            new Point(42, 20),
            new Point(34, 20),
            new Point(34, 100), };
        Point[] arrow_to = new Point[] {
            new Point(40, 100),
            new Point(40, 50),
            new Point(20, 50),
            new Point(50, 10),
            new Point(80, 50),
            new Point(60, 50),
            new Point(60, 100), };
        Point[] line_to = new Point[] {
            new Point(0, 0),
            new Point(100, 0),
            new Point(100, 8),
            new Point(0, 8), };
        Point[] arrow_turn = new Point[] {
            new Point(6, 100),
            new Point(6, 0),
            new Point(80, 0),
            new Point(80, 60),
            new Point(100, 60),
            new Point(70, 100),
            new Point(40, 60),
            new Point(60, 60),
            new Point(60, 20),
            new Point(26, 20),
            new Point(26, 100), };
        Point[] destination = new Point[] {
            new Point(16, 82),
            new Point(16, 44),
            new Point(6, 44),
            new Point(6, 10),
            new Point(94, 10),
            new Point(94, 44),
            new Point(84, 44),
            new Point(84, 82),
            new Point(78, 82),
            new Point(78, 44),
            new Point(22, 44),
            new Point(22, 82), };
        int NavSymbol_size = 100;
        public enum Orientation
        {
            normal,
            mirrorX,    //right-left
            mirrorY,    //up-down
            right,
            left
        };

        public void ScaleNavSymbols(int scx_p, int scx_q)
        {
            ScaleNavSymbol(arrow_r, scx_p, scx_q);
            ScaleNavSymbol(arrow_hr, scx_p, scx_q);
            ScaleNavSymbol(arrow_sr, scx_p, scx_q);
            ScaleNavSymbol(arrow_to, scx_p, scx_q);
            ScaleNavSymbol(line_to, scx_p, scx_q);
            ScaleNavSymbol(arrow_turn, scx_p, scx_q);
            ScaleNavSymbol(destination, scx_p, scx_q);
            NavSymbol_size = (NavSymbol_size * scx_p + scx_q / 2) / scx_q;
        }
        void ScaleNavSymbol(Point[] pa, int scx_p, int scx_q)
        {
            for (int i = 0; i < pa.Length; i++)
            {
                pa[i].X = (pa[i].X * scx_p + scx_q / 2) / scx_q;
                pa[i].Y = (pa[i].Y * scx_p + scx_q / 2) / scx_q;
            }
        }

        // return true if only sub-directories with names "0".."18" exist in this folder
        public bool DetectOsmTiles()
        {
            for (int i = 0; i < OsmNumZoomLevels; i++) { OsmZoomAvailable[i] = false; }

            string[] zoom_dirs = Directory.GetDirectories(MapsFilesDirectory);

            // nothing found
            if (zoom_dirs.Length == 0)
            {
                if (File.Exists(MapsFilesDirectory + "\\server.txt"))
                {
                    OsmFileExtension = ".png";
                    return true;
                }
                else
                    return false;
            }

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
        public double OsmYTile2Lat(double n, double ytile)
        {
            double lat_rad = Math.Atan(__sinh(Math.PI * (1.0 - 2.0 * ytile / n)));
            return (lat_rad * 180.0 / Math.PI);
        }
        public int OsmLong2XTile(double n, double lon_deg)
        {
            return (int)((lon_deg + 180.0) / 360.0 * n);
        }
        public double OsmLat2YTile(double n, double lat_deg)       //cast return value to int to get number of tile (upper left corner)
        {
            double lat_rad = lat_deg * Math.PI / 180.0;
            return ((1.0 - Math.Log(Math.Tan(lat_rad) + (1 / Math.Cos(lat_rad))) / Math.PI) / 2.0 * n);
        }
        private void LoadOsmServer()
        {
            if (File.Exists(MapsFilesDirectory + "\\server.txt"))
            {
                FileStream fs = null;
                StreamReader sr = null;
                try
                {
                    fs = new FileStream(MapsFilesDirectory + "\\server.txt", FileMode.Open, FileAccess.Read);
                    sr = new StreamReader(fs);
                    string s = sr.ReadLine();
                    if (s == "1") parent.checkDownloadOsm.Checked = true;
                    else parent.checkDownloadOsm.Checked = false;
                    s = sr.ReadLine();
                    if (s != null)
                        OsmServer = s;
                    else
                    {
                        parent.checkDownloadOsm.Checked = false;
                        OsmServer = "";
                    }
                }
                catch (Exception e)
                {
                    Utils.log.Error(" Load server.txt ", e);
                    parent.checkDownloadOsm.Checked = false;
                    OsmServer = "";
                }
                finally
                {
                    if (sr != null) sr.Close();
                    if (fs != null) fs.Close();
                }
            }
            else
                parent.checkDownloadOsm.Checked = false;
        }
        public void SaveOsmServer(string server)
        {
            OsmServer = server;
            if (!OsmTilesMode && parent.checkDownloadOsm.Checked)
                LoadMaps(MapsFilesDirectory);            //set OsmTilesMode and initialize it
            FileStream fs = null;
            StreamWriter sw = null;
            try
            {
                fs = new FileStream(MapsFilesDirectory + "\\server.txt", FileMode.Create, FileAccess.Write);
                sw = new StreamWriter(fs);
                sw.WriteLine(parent.checkDownloadOsm.Checked ? "1" : "0");
                sw.WriteLine(server);
            }
            catch (Exception e)
            {
                Utils.log.Error(" Save server.txt ", e);
            }
            finally
            {
                if (sw != null) sw.Close();
                if (fs != null) fs.Close();
            }
        }

        class DownloadData
        {
            public string DLFilename;
            public HttpWebRequest httpRequest;
            public HttpWebResponse response;
            public const int BUFFER_SIZE = 4096;
            public byte[] readBuffer = new byte[BUFFER_SIZE];
            public Stream streamResponse;
            public FileStream fs;
            public BinaryWriter wr;

            public void Cleanup()
            {
                if (wr != null) wr.Close();
                if (fs != null) fs.Close();
                if (streamResponse != null) streamResponse.Close();
                if (response != null) response.Close();
            }
        }
        ArrayList DlList = new ArrayList();          //list of currently downloading tiles (DownloadState)
        private bool DlListContains(string filename)    //could not get to work DlList.Contains() with object subelement
        {
            for (int i = 0; i < DlList.Count; i++)
            {
                if (((DownloadData)DlList[i]).DLFilename == filename)
                    return true;
            }
            return false;
        }
        private bool DownloadOsmTile(string tile_name)
        {
            if (DlList.Count > 2)       //only 2 download threads allowed on osm
                return false;
            DownloadData dld = new DownloadData();
            try
            {
                String forecastAdress = tile_name.Replace(MapsFilesDirectory + "\\", OsmServer);
                forecastAdress = forecastAdress.Replace("\\", "/");

                dld.DLFilename = tile_name;
                dld.httpRequest = (HttpWebRequest)WebRequest.Create(forecastAdress);

                dld.httpRequest.Credentials = CredentialCache.DefaultCredentials;
                dld.httpRequest.Timeout = 30000;          // 10 sec timeouts
                dld.httpRequest.ReadWriteTimeout = 30000;
                dld.httpRequest.UserAgent = "GpsCycleComputer";

                dld.httpRequest.BeginGetResponse(GetResponseCallback, dld);
                DlList.Add(dld);
                return true;
            }
            catch(Exception e)
            {
                DownloadEnd(e, dld);
            }
            return false;
        }
        private void GetResponseCallback(IAsyncResult asynchronousResult)
        {
            DownloadData dld = (DownloadData)asynchronousResult.AsyncState;
            try
            {
                // End the operation
                dld.response = (HttpWebResponse)dld.httpRequest.EndGetResponse(asynchronousResult);
                if (dld.response.StatusCode != HttpStatusCode.OK)
                {
                    Utils.log.Debug ("Map download error - " + dld.httpRequest.RequestUri + " HTTPResponse = " + dld.response.StatusCode + " - " + dld.response.StatusDescription);
                    MapErrors = __MapErrorDownload;
                    DownloadEnd(new Exception(dld.response.StatusCode.ToString()), dld);
                    return;
                }
                dld.streamResponse = dld.response.GetResponseStream();
                Directory.CreateDirectory(Path.GetDirectoryName(dld.DLFilename));
                dld.fs = new FileStream(dld.DLFilename, FileMode.Create);
                dld.wr = new BinaryWriter(dld.fs, Encoding.UTF8);
                //streamResponse.ReadTimeout = 200;
                dld.streamResponse.BeginRead(dld.readBuffer, 0, DownloadData.BUFFER_SIZE, BeginReadCallback, dld);
                return;
            }
            catch (Exception e)
            {
                DownloadEnd(e, dld);
            }
        }
        private void BeginReadCallback(IAsyncResult asyncResult)
        {
            DownloadData dld = (DownloadData)asyncResult.AsyncState;
            try
            {
                int n = dld.streamResponse.EndRead(asyncResult);
                if (n > 0)
                {
                    dld.wr.Write(dld.readBuffer, 0, n);
                    //dld.streamResponse.BeginRead(dld.readBuffer, 0, DownloadData.BUFFER_SIZE, new AsyncCallback(BeginReadCallback), dld);
                    dld.streamResponse.BeginRead(dld.readBuffer, 0, DownloadData.BUFFER_SIZE, BeginReadCallback, dld);
                }
                else
                {   //all data are read
                    dld.Cleanup();          //normal end of download
                    DlList.Remove(dld);
                }
            }
            catch (Exception e)
            {
                DownloadEnd(e, dld);
            }
        }

        void DownloadEnd(Exception e, DownloadData dld)
        {
            Utils.log.Error (" DownloadOsmTile ", e);
            if (e.Message.IndexOf("404") != -1)
                MapErrors = __MapErrorNotFound;
            else
                MapErrors = __MapErrorDownload;
            try { File.Delete(dld.DLFilename); }
            catch { }
            DlList.Remove(dld);
            dld.Cleanup();
        }




/*
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
 * */
        /*
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
                if (!parent.checkDownloadOsm.Checked && !OsmZoomAvailable[iz]) { continue; }

                double n = (double)(1 << iz); // num tiles in this zoom level

                int xtile_min = OsmLong2XTile(n, longMin);
                int xtile_max = OsmLong2XTile(n, longMax);
                int ytile_min = OsmLat2YTile(n, latMax);
                int ytile_max = OsmLat2YTile(n, latMin);

                // do not load very small tiles - cannot plot them anyway
                int num_tiles_in_this_zoom = (ytile_max - ytile_min + 1) * (xtile_max - xtile_min + 1);
                if (num_tiles_in_this_zoom > 32) { break; }

                // do a quick check if a central tile exist when we have lots of times, - skip if not exists
                if ((num_tiles_in_this_zoom > 9) && !parent.checkDownloadOsm.Checked)
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
        }*/

        public void ShiftMap(int dx, int dy)
        {
            LongShift = LongShiftSave + dx / Long2Pixel;
            LatShift = LatShiftSave + dy / Lat2PixelSave;
            if (DataLongMiddle - LongShift < -180) LongShift = DataLongMiddle + 180;
            if (DataLongMiddle - LongShift > 179.999999999999) LongShift = DataLongMiddle - 179.999999999999;
            if (DataLatMiddle + LatShift > 85.051) LatShift = 85.051 - DataLatMiddle;      //85.051;
            if (DataLatMiddle + LatShift < -85.051) LatShift = -85.051 - DataLatMiddle;
            System.Diagnostics.Debug.WriteLine(LatShift);
        }
        
        public void FillOsmTiles(int MapMode)
        {
            for (int i = 0; i < NumMaps; i++)
            {
                //Maps[i].qfactor = 0.0;
                Maps[i].was_removed = true;     //mark all tiles as unnecessary
            }
            int n = OsmN;
            int iz = 0;
            while ((n >>= 1) > 0)
                iz++;
            OsmZoom = iz;
            tilezoom = 0;
            tilePixel = 256;
            if ((MapMode == 2 || MapMode == 3) && iz > 0)
            {
                iz--; tilezoom++; tilePixel *= 2;
                if (MapMode == 3 && iz > 0) { iz--; tilezoom++; tilePixel *= 2; }
            }
            else if (MapMode == 4 && iz < OsmNumZoomLevels - 1) { iz++; tilezoom = -1; tilePixel /= 2; }
            OsmNz = 1 << iz;
            double centerTile_xd = (DataLongMiddle - LongShift + 180.0) / 360.0 * OsmNz;
            centerTile_x = (int)centerTile_xd;
            ScreenTileRefX = centerTile_x * tilePixel - (int)(centerTile_xd * tilePixel) + ScreenX / 2;
            int xtile_min = ((int)(centerTile_xd * tilePixel) - ScreenX/2) / tilePixel;
            int xtile_max = ((int)(centerTile_xd * tilePixel) + ScreenX/2) / tilePixel;
            if (xtile_min < 0) xtile_min = 0;
            if (xtile_max >= OsmNz) xtile_max = OsmNz - 1;

            centerTile_yd = OsmLat2YTile(OsmNz, DataLatMiddle + LatShift);
            centerTile_y = (int)centerTile_yd;
            ScreenTileRefY = centerTile_y * tilePixel - (int)(centerTile_yd * tilePixel) + ScreenY / 2;
            int ytile_min = ((int)(centerTile_yd * tilePixel) - ScreenY/2) / tilePixel;
            int ytile_max = ((int)(centerTile_yd * tilePixel) + ScreenY/2) / tilePixel;
            if (ytile_min < 0) ytile_min = 0;
            if (ytile_max >= OsmNz) ytile_max = OsmNz - 1;

            /*int xtile_min = OsmLong2XTile(n, ToDataX(0));
            int xtile_max = OsmLong2XTile(n, ToDataX(ScreenX));
            int ytile_min = (int)OsmLat2YTile(n, ToDataY(0));
            int ytile_max = (int)OsmLat2YTile(n, ToDataY(ScreenY));
            int num_tiles_in_this_zoom = (ytile_max - ytile_min + 1) * (xtile_max - xtile_min + 1);
            centerTile_x = (xtile_min + xtile_max) / 2;
            centerTile_y = (ytile_min + ytile_max) / 2;
            ScreenTileRefX = ToScreenX(OsmXTile2Long(n, centerTile_x));
            ScreenTileRefY = ToScreenY(OsmYTile2Lat(n, centerTile_y));
            */
            for (int ix = xtile_min; ix <= xtile_max; ix++)
            {
                for (int iy = ytile_min; iy <= ytile_max; iy++)
                {
                    int dec = 0;
                    bool ok = LoadTileFromSD(iz, ix, iy);
                    if (!ok)
                    {
                        dec++;
                        while (iz >= dec && !LoadTileFromSD(iz - dec, ix >> dec, iy >> dec))    //load next lower zoom
                            dec++;
                    }
                    if (ix == centerTile_x && iy == centerTile_y)
                        osmMapName = iz.ToString() + ((dec > 0) ? (-dec).ToString() : "") + "\\" + (ix >> dec).ToString() + "\\" + (iy >> dec).ToString();

                    // hopefully having 512 tiles within THIS screen is enough
                    if (NumMaps >= MaxNumMaps) { return; }
                }
            }
            
            for (int i = 0; i < NumMaps; i++)       //delete unused maps
            {
                if (Maps[i].was_removed)
                {
                    if (Maps[i].bmp != null && Maps[i].bmp != hourglass)    //don't dispose hourglass!
                        Maps[i].bmp.Dispose();
                    Maps[i].bmp = null;
                    MapInfo tmp;
                    for (int j = i; j < NumMaps - 1; j++)
                    {
                        tmp = Maps[j];              // necessary with MapInfo as class, otherwise this MapInfo will be disposed by GC
                        Maps[j] = Maps[j + 1];
                        Maps[j + 1] = tmp;
                    }
                    NumMaps--;
                    i--;        //next map is now one index lower
                }
            }
        }

        bool LoadTileFromSD(int iz, int ix, int iy)
        {
            if (iz >= OsmNumZoomLevels || OsmZoomAvailable[iz] == false)
                return false;
            Maps[NumMaps].fname = MapsFilesDirectory + "\\" + iz.ToString() +
                                          "\\" + ix.ToString() + "\\" + iy.ToString() + OsmFileExtension;

            Maps[NumMaps].scrX1_ix = ix;
            Maps[NumMaps].scrY1_iy = iy;
            Maps[NumMaps].scrX2_iz = iz;
            // check that this record is not exist in the area with stored bitmaps
            bool record_exist_with_bitmaps = false;
            int k;
            for (k = 0; k < NumMaps; k++)
            {
                if ((Maps[NumMaps].scrX1_ix == Maps[k].scrX1_ix) &&
                    (Maps[NumMaps].scrY1_iy == Maps[k].scrY1_iy) &&
                    (Maps[NumMaps].scrX2_iz == Maps[k].scrX2_iz))
                {
                    record_exist_with_bitmaps = true;
                    break;
                }
            }
            if (record_exist_with_bitmaps)
            {
                Maps[k].was_removed = false;        //validate existing tile
            }
            else
            {
                bool error = false;
                if (File.Exists(Maps[NumMaps].fname))
                {
                    try
                    {
                        Maps[NumMaps].bmp = new Bitmap(Maps[NumMaps].fname);
                    }
                    catch
                    {
                        if (Maps[NumMaps].bmp != null && Maps[NumMaps].bmp != hourglass)
                            Maps[NumMaps].bmp.Dispose();
                        error = true;
                    }
                }
                else
                    error = true;
                if (error)
                {
                    Maps[NumMaps].bmp = null;
                    if (!parent.checkDownloadOsm.Checked)
                        return false;
                }
                Maps[NumMaps].overlap = 1.0;
                
                Maps[NumMaps].was_removed = false;
                NumMaps++;

                MapInfo tmp;        //sort tiles with decreasing zoom
                for (int i = NumMaps - 1; i > 0; i--)
                    if (Maps[i].scrX2_iz > Maps[i - 1].scrX2_iz)
                    {
                        tmp = Maps[i - 1];
                        Maps[i - 1] = Maps[i];
                        Maps[i] = tmp;
                    }
                    else break;
            }
            return true;
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

            string crFile = mapsFilesDirectory + "\\copyright.txt";
            if (File.Exists(crFile))
            {
                StreamReader sr = null;
                try
                {
                    sr = new StreamReader(crFile, Encoding.UTF7);
                    mapCopyright = sr.ReadLine();
                }
                catch
                {
                    mapCopyright = null;
                }
                if (sr != null) sr.Close();
            }
            else
                mapCopyright = null;

            OsmTilesMode = DetectOsmTiles();
            if (!OsmTilesMode && parent.checkDownloadOsm.Checked)
            {                            // if want to download - force OsmTilesMode and enable all zoom levels
                for (int allowed_i = 0; allowed_i < OsmNumZoomLevels; allowed_i++)
                {
                    OsmZoomAvailable[allowed_i] = true;
                }
                OsmFileExtension = ".png";
                OsmTilesMode = true;
            }
            if (OsmTilesMode)
            {
                // all OSM tiles are 256x256
                for (int i = 0; i < MaxNumMaps; i++)
                {
                    Maps[i].sizeX = 256;
                    Maps[i].sizeY = 256;
                }
                LoadOsmServer();
                return;
            }

            // loop over possible map names and load existing map info
            Cursor.Current = Cursors.WaitCursor;
            OsmZoom = 1;            //otherwise zoom out may be prevented
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

                        FileStream fs = new FileStream(kml_file, FileMode.Open, FileAccess.Read);
                        StreamReader sr = new StreamReader(fs);
                        string line = "";
                        while ((line = sr.ReadLine()) != null)
                        {
                            line = line.Trim();
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
                            UtmUtil utmUtil1 = new UtmUtil();

                            utmUtil1.setReferencePoint(center_lat, center_long);
                            utmUtil1.getLatLong(-sizex, 0.0, out tmp, out Maps[NumMaps].lon1);
                            utmUtil1.getLatLong( sizex, 0.0, out tmp, out Maps[NumMaps].lon2);
                            utmUtil1.getLatLong(0.0,-sizey, out Maps[NumMaps].lat1, out tmp);
                            utmUtil1.getLatLong(0.0, sizey, out Maps[NumMaps].lat2, out tmp);
                        }
                        else
                            { kml_has_problems = true; }
                    }
                    else if (File.Exists(txt_file)) // load text file (if KML does not exist)
                    {
                        FileStream fs = new FileStream(txt_file, FileMode.Open, FileAccess.Read);
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
                        FileStream fs = new FileStream(gmi_file, FileMode.Open, FileAccess.Read);
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
                                while ((line = sr.ReadLine()) != null)
                                {
                                    line = line.Trim();
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
                    if (Maps[i].bmp != hourglass)       //don't dispose hourglass
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
        /*public class BitmapExistComparer : IComparer
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
        }*/
        public class ZoomComparer : IComparer
        {
            int IComparer.Compare(Object x1, Object y1)
            {
                if ((x1 is MapInfo) && (y1 is MapInfo))
                {
                    MapInfo x = (MapInfo)x1;
                    MapInfo y = (MapInfo)y1;

                    if (x.zoom_level > y.zoom_level) { return -1; }
                    if (x.zoom_level < y.zoom_level) { return 1; }

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
            else if (MapMode == 4)  { NumBitmaps = 4; scaleCorrection = 2.0; }
            else    { NumBitmaps = 1; scaleCorrection = 1.0;}

            // compute overlap for each map and map qfactor (function from the zoom level)
            double screen_area = (double)ScreenX * (double)ScreenY;

            for (int i = 0; i < NumMaps; i++)
            {
                double overlap_area = (double)GetLineOverLap(Maps[i].scrX1_ix, Maps[i].scrX2_iz, 0, ScreenX) *
                                      (double)GetLineOverLap(Maps[i].scrY2, Maps[i].scrY1_iy, 0, ScreenY);

                Maps[i].overlap = overlap_area / screen_area;

                double zoom_level = scaleCorrection * (double)(Maps[i].scrX2_iz - Maps[i].scrX1_ix) / (double)Maps[i].sizeX;

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
                //KB
                /*
                Array.Sort(Maps, 0, NumMaps, comp_qual);
                AreAnyCompletelyCoveredMapRemoved();
                NumBitmaps = (ScreenX / (Maps[0].scrX2 - Maps[0].scrX1) + 1) * (ScreenY / (Maps[0].scrY1 - Maps[0].scrY2) + 1) + 7;
                NumBitmaps = Math.Min(NumMaps, NumBitmaps);
                */
                
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
            int i_scrX1 = (Maps[i].scrX1_ix >= 0 ? Maps[i].scrX1_ix : 0);
            int i_scrX2 = (Maps[i].scrX2_iz <= ScreenX ? Maps[i].scrX2_iz : ScreenX);
            int i_scrY1 = (Maps[i].scrY2 >= 0 ? Maps[i].scrY2 : 0);
            int i_scrY2 = (Maps[i].scrY1_iy <= ScreenY ? Maps[i].scrY1_iy : ScreenY);

            int j_scrX1 = (Maps[j].scrX1_ix >= 0 ? Maps[j].scrX1_ix : 0);
            int j_scrX2 = (Maps[j].scrX2_iz <= ScreenX ? Maps[j].scrX2_iz : ScreenX);
            int j_scrY1 = (Maps[j].scrY2 >= 0 ? Maps[j].scrY2 : 0);
            int j_scrY2 = (Maps[j].scrY1_iy <= ScreenY ? Maps[j].scrY1_iy : ScreenY);

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
                    // Covering the map is only possible if the map is not removed
                    if (Maps[j].was_removed == false && IsMapCompletelyCovered(i, j))
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
            if ((OsmTilesMode == true) && (!parent.checkDownloadOsm.Checked))
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
        int delayCount = 0;
        public void DrawJpeg(Graphics g)
        {
            // DEBUG - save into file map info after they are sorted
            // PrintMapInfo();

            if (MapErrors == __MapErrorNotFound)
            {
                if (delayCount == 0)
                {
                    MapErrors = __MapNoError;
                    delayCount = 60;                 //try again in 60s
                }
                else
                    delayCount--;
            }
            else
                MapErrors = __MapNoError;
            for(int i = (NumBitmaps-1); i >= 0; i--)
            {
                if (Maps[i].overlap == 0.0) { continue; }
                if (Maps[i].was_removed) { continue; }

                // load the map, if it is null
                if ((Maps[i].bmp == null) || Maps[i].bmp == hourglass || reDownloadMaps)
                {
                    if (DlListContains(Maps[i].fname))     // hold a variable (list) of currently downloading maps
                        goto displayTile;
                    // download map from web, if no problems with download
                    if (parent.checkDownloadOsm.Checked && !File.Exists(Maps[i].fname) 
                        && (MapErrors == __MapNoError) || reDownloadMaps)               //reDownload always
                    {
                        if (DownloadOsmTile(Maps[i].fname))
                            Maps[i].bmp = hourglass;
                        else
                            Maps[i].bmp = null;
                        goto displayTile;
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
                        //catch (System.IO.IOException)
                        //{ }
                        catch (Exception e)
                        {
                            Utils.log.Error(" DrawJpeg - new Bitmap", e);
                            Maps[i].bmp = null;
                            MapErrors = __MapErrorReading;
                            if (parent.checkDownloadOsm.Checked)
                            {
                                try { File.Delete(Maps[i].fname); }     //delete file with Read Error to initiate redownload
                                catch { }
                            }
                        }
                    }
                }
            displayTile:
                // check that it was loaded OK
                if (Maps[i].bmp == null) { continue; }

                if (OsmTilesMode)
                {
                    int z_red = OsmZoom - Maps[i].scrX2_iz;
                    if (z_red == 0 && tilezoom == 0)
                    {
                        int x = (Maps[i].scrX1_ix - centerTile_x) * 256 + ScreenTileRefX;
                        int y = (Maps[i].scrY1_iy - centerTile_y) * 256 + ScreenTileRefY;
                        g.DrawImage(Maps[i].bmp, x, y);
                    }
                    else
                    {
                        int x = ((Maps[i].scrX1_ix << z_red+1) - (centerTile_x << tilezoom+1)) * 128 + ScreenTileRefX;
                        int y = ((Maps[i].scrY1_iy << z_red+1) - (centerTile_y << tilezoom+1)) * 128 + ScreenTileRefY;
                        int factor = 1 << z_red+1;
                        Rectangle src_rec = new Rectangle(-x * 2 / factor, -y * 2 / factor, ScreenX * 2 / factor, ScreenY * 2 / factor);
                        Rectangle dest_rec = new Rectangle(0, 0, ScreenX, ScreenY);
                        g.DrawImage(Maps[i].bmp, dest_rec, src_rec, GraphicsUnit.Pixel);
                    }
                }
                else
                {
                    int bX1 = ToScreenX(Maps[i].lon1);
                    int bY2 = ToScreenY(Maps[i].lat2);
                    int bX2 = ToScreenX(Maps[i].lon2);
                    int bY1 = ToScreenY(Maps[i].lat1);

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

                        Rectangle src_rec = new Rectangle((int)(-bX1 * scalex), (int)(-bY2 * scaley),
                                                          (int)(ScreenX * scalex), (int)(ScreenY * scaley));
                        Rectangle dest_rec = new Rectangle(0, 0, ScreenX, ScreenY);

                        g.DrawImage(Maps[i].bmp, dest_rec, src_rec, GraphicsUnit.Pixel);
                    }
                }
            }
            reDownloadMaps = false;
        }
        public void DrawMovingImage(Graphics g, Bitmap BackBuffer, int dx, int dy)
        {
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

        public int ToScreenX(double x)
        {
            return (int)((x + LongShift - DataLongMiddle) * Long2Pixel) + ScreenX / 2;
        }
        public double ToDataX(int scr_x)
        {
            return (DataLongMiddle - LongShift + (double)(scr_x - ScreenX /2) / Long2Pixel);
        }
        public int ToScreenY(double y)
        {
            return (ScreenY / 2 - (int)((y - DataLatMiddle - LatShift) * Lat2Pixel));
        }
        public double ToDataY(int scr_y)
        {
            return (DataLatMiddle + LatShift + (double)(ScreenY / 2 - scr_y) / Lat2Pixel);
        }
        public double ToDataYexact(int scr_y)
        {
            if (OsmTilesMode)
            {
                return OsmYTile2Lat(OsmNz, centerTile_yd + (double)(scr_y - ScreenY / 2) / tilePixel);
            }
            else return ToDataY(scr_y);
        }
        private void DrawCurrentPoint(Graphics g, double Long, double Lat, int size, Color col)
        {
            int x_point = ToScreenX(Long);
            int y_point = ToScreenY(Lat);

            SolidBrush drawBrush = new SolidBrush(col);
            g.FillEllipse(drawBrush, x_point - size / 2, y_point - size / 2, size, size);
        }
        //KB draw arrow   (size = radius)
        public void DrawArrow(Graphics g, int x0, int y0, int heading_int, int size, Color col)
        {
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
            g.FillPolygon(br, pa);

            // point B - at +210 deg from current
            pa[2].X = (int)(x0 + size * Math.Cos(Math.PI / 2.0 - ((heading_int + 210) * Math.PI / 180.0)));
            pa[2].Y = (int)(y0 - size * Math.Sin(Math.PI / 2.0 - ((heading_int + 210) * Math.PI / 180.0)));
            br.Color = Utils.modifyColor(br.Color, +100);
            g.FillPolygon(br, pa);
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
        // 
        private void DrawTrackLine(Graphics g, Pen p, float[] PlotLong, float[] PlotLat, int PlotSize, bool plot_dots, double CurLong, double CurLat)
        {
            // idea is to draw not too much points (as it is slow),
            // so first select only points which are within the map
            // further remove points which are closer than 5 pixel to the last
            //int begin = Environment.TickCount;
            //String str = "";// = new String("");

            SolidBrush drawBrush = new SolidBrush(p.Color); // brush to draw points
            int pen_size = (int)p.Width;
            const int SegmentSize = 64;
            Point[] points = new Point[SegmentSize];

            // fill in data
            bool LineStarted = false;
            int i = 0;      //0..Plotsize
            int d = 0;      //number of points to draw in a segment
            int outxo = 0, outx = 0, outyo = 0, outy = 0;
            Point oldPoint = new Point(0, 0);
            while (i < PlotSize)
            {
                bool PointInMap = false;
                bool IgnorePoint = false;
                //Point point = new Point(ToScreenX(PlotLong[i]), ToScreenY(PlotLat[i]));
                points[d].X = ToScreenX(PlotLong[i]);
                points[d].Y = ToScreenY(PlotLat[i]);

                if (points[d].X < 0)
                    outx = -1;
                else if (points[d].X >= ScreenX)
                    outx = 1;
                else outx = 0;

                if (points[d].Y < 0)
                    outy = -1;
                else if (points[d].Y >= ScreenY)
                    outy = 1;
                else outy = 0;

                if (outx == 0 && outy == 0)    //only points within screen
                { 
                    PointInMap = true; 
                    LineStarted = true; 
                }
                i++;
                if (d > 0)  //first point never ignore
                {
                    if (PointInMap && (Math.Abs(points[d].X - points[d - 1].X) < 5) && (Math.Abs(points[d].Y - points[d - 1].Y) < 5))  //only points which differ significant from previous
                    { IgnorePoint = true; }
                    if (!PointInMap)
                    {
                        if (outxo * outx == -1 || outyo * outy == -1           //change from left outside to right outside
                            || (outxo != outx && outyo != outy))               //
                        {
                            if (d < SegmentSize - 1)
                            {
                                points[d + 1] = points[d];
                                points[d] = oldPoint;       //both points valid
                                ScreenClip(ref points[d], points[d + 1]);
                                d++;
                                LineStarted = true;
                            }               //if array full, only new point
                        }
                        else if (!LineStarted)
                        {
                            points[d - 1] = points[d];    //overwrite first point (outside screen)
                            IgnorePoint = true;
                        }
                        if (!IgnorePoint)
                            ScreenClip(ref points[d], points[d - 1]);
                    }
                }
                oldPoint = points[d];
                outxo = outx; outyo = outy;
                if (!IgnorePoint)
                { d++; }   //validate point; d= number of points

                if (LineStarted && ((!PointInMap && !IgnorePoint) || d >= SegmentSize || i >= PlotSize))
                {
                    //System.Diagnostics.Debug.WriteLine(d);
                    //str += d; str += " ";
                    //Draw Line segment
                    if (d > 1)
                        ScreenClip(ref points[0], points[1]);
                    else
                        ScreenClip(ref points[0], points[0]);
                    if (plot_dots)
                    {
                        for (int j = 0; j < d; j++)
                        {
                            g.FillEllipse(drawBrush, points[j].X - pen_size / 2, points[j].Y - pen_size / 2, pen_size, pen_size);
                        }
                    }
                    else
                    {
                        while (d < SegmentSize)     //DrawLines() draws always complete array -> fill with last point
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

            //int end = Environment.TickCount;

            //Font drawFont = new Font("Arial", 8, FontStyle.Regular);
            //g.DrawString("Length=" + PlotSize, drawFont, drawBrush, 0, 20);
            //g.DrawString("d=" + str, drawFont, drawBrush, 0, 40);
            //g.DrawString("ticks=" + (end - begin), drawFont, drawBrush, 0, 60);

            return;
        }

        // not used anymore because redundant to navigation string
        //private void DrawDistanceToTrack2Follow(Graphics g, Pen p, double unit_cff, string unit_name) 
        //{
        //    Font drawFont = new Font("Arial", 8, FontStyle.Regular);
        //    SolidBrush drawBrush = new SolidBrush(Back_Color); // brush to draw rectangle

        //    string strDistance = "Distance to T2F: " + (nav.MinDistance * unit_cff).ToString("0.000") + unit_name;
        //    SizeF TextSize = g.MeasureString(strDistance, drawFont);

        //    // Draw a black box to show distance between current position and track2follow
        //    p.Color = Fore_Color;
        //    const int LineWidth = 1;
        //    p.Width = LineWidth;
        //    int TextBoxHeight = (int) (TextSize.Height + 0.5);
        //    g.FillRectangle(drawBrush, 0, ScreenY - TextBoxHeight - LineWidth, ScreenX, TextBoxHeight + LineWidth);
        //    g.DrawLine(p, 0, ScreenY - TextBoxHeight - LineWidth, ScreenX, ScreenY - TextBoxHeight - LineWidth);    //separation line to map

        //    // Draw Distance to Track information
        //    drawBrush.Color = Fore_Color;
        //    g.DrawString(strDistance, drawFont, drawBrush, 2, ScreenY - TextBoxHeight);
        //}

        private void DrawWayPoints(Graphics g, Pen p, Form1.WayPointInfo WayPoints, Color col, int showWP)
        {
            int i = 0;      //0..Plotsize
            SolidBrush br = new SolidBrush(col);
            Font drawFont = new Font("Arial", 8 * parent.df, FontStyle.Regular);
            Point[] pa = new Point[3];

            while (i < WayPoints.Count)
            {
                int x = ToScreenX(WayPoints.lon[i]);
                int y = ToScreenY(WayPoints.lat[i]);

                if (x >= 0 && x < ScreenX && y >= 0 && y < ScreenY)    //only points within screen
                {
                    int MarkerSize;

                    // Size of Marker dependend on the screen size
                    if (ScreenX > 320)
                        MarkerSize = 36;
                    else
                        MarkerSize = 20;

                    p.Color = col;
                    p.Width = 2.0F;
                    g.DrawLine(p, x, y, x, y - MarkerSize);
                    pa[0].X = x + 1;
                    pa[0].Y = y - MarkerSize;
                    pa[1].X = x + 1 + MarkerSize / 2;
                    pa[1].Y = y - MarkerSize * 3 / 4;
                    pa[2].X = x + 1;
                    pa[2].Y = y - MarkerSize / 2;
                    br.Color = col;
                    g.FillPolygon(br, pa);

                    p.Width = 1.0F;
                    p.Color = Color.Black;
                    g.DrawLine(p, x + 2, y, x + 2, y - MarkerSize / 2);
                    g.DrawLine(p, x + 2, y - MarkerSize / 2, x + 2 + MarkerSize / 2, y - MarkerSize * 3 / 4);

                    if (showWP == 1)
                    {
                        SizeF TextSize = g.MeasureString(WayPoints.name[i], drawFont);
                        br.Color = Color.Black;
                        g.FillRectangle(br, x + MarkerSize / 3, y - MarkerSize / 2, (int)TextSize.Width + 3, (int)TextSize.Height);
                        br.Color = Color.White;
                        g.DrawString(WayPoints.name[i], drawFont, br, x + 1 + MarkerSize / 3, y - MarkerSize / 2);
                    }
                }
                i++;
            }
        }

        private void DrawTickLabel(Graphics g, Pen p, int tick_dist_screen, double tick_dist_units, string unit_name, string clickLatLon)
        {
            // draw text: Create font and brush.
            Font drawFont = new Font("Arial", 8 * parent.df, FontStyle.Bold);
            SolidBrush drawBrush = new SolidBrush(p.Color);

            // draw map info, check the text size to draw in the right corner
            string str_map = GetBestMapName();
            SizeF size = g.MeasureString(str_map, drawFont);
            g.DrawString(str_map, drawFont, drawBrush, ScreenX - size.Width - 2, 0);

            if (clickLatLon != null)
            {
                float offset = 0;
                if (mapCopyright != null)
                    offset = ScreenY / 20;
                SizeF size2 = g.MeasureString(clickLatLon, drawFont);
                g.DrawString(clickLatLon, drawFont, drawBrush, ScreenX - size2.Width - 2, size.Height + offset);
            }

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

        private void DrawMapValues(Graphics g, Color col)
        {
            int number;
            string[] arStrName = new string[3];
            string[] arStrValue = new string[3];
            string[] arStrUnit = new string[3];
            number = parent.GetValuesToDisplayInMap(ref arStrName, ref arStrValue, ref arStrUnit);
            if (number == 0) return;

            int x = ScreenX / (number * 2);
            int txtPoints;
            if (number > 2)
                txtPoints = 14;
            else
                txtPoints = 18;
            Font drawFont = new Font("Arial", txtPoints * parent.df, FontStyle.Bold);
            Font drawFont2 = new Font("Arial", txtPoints * 0.7f * parent.df, FontStyle.Bold);
            SolidBrush backBrush = new SolidBrush(Back_Color);
            SolidBrush txtBrush = new SolidBrush(col);
            SizeF sizeVal, sizeUnit;
            for (int i = 0; i < 3; i++)
            {
                if (arStrValue[i] == null) continue;
                sizeVal = g.MeasureString(arStrValue[i], drawFont);
                sizeUnit = g.MeasureString(arStrUnit[i], drawFont2);
                int dx = (int)(sizeVal.Width + sizeUnit.Width);
                int y = ScreenY - (int)sizeVal.Height;
                if (parent.mapValuesCleanBack)
                {
                    g.FillRectangle(backBrush, x - dx / 2, y, dx, (int)sizeVal.Height);
                }
                g.DrawString(arStrValue[i], drawFont, txtBrush, x - dx / 2, y);
                g.DrawString(arStrUnit[i], drawFont2, txtBrush, x - dx / 2 + sizeVal.Width, y + sizeVal.Height * 9 / 10 - sizeUnit.Height);
                if (parent.mapValuesShowName)
                {
                    sizeVal = g.MeasureString(arStrName[i], drawFont2);
                    g.DrawString(arStrName[i], drawFont2, txtBrush, x - (int)sizeVal.Width / 2, y - (int)sizeVal.Height);
                }
                x += ScreenX / number;
            }
        }

        public string GetBestMapName()
        {
            string str_map = "no map";

            if((NumMaps != 0) && (Maps[0].overlap != 0.0))
            {
                if (OsmTilesMode)
                {/*
                    string str_map_y = Path.GetFileNameWithoutExtension(Maps[0].fname);
                    string str_map_x = Path.GetFileName(Path.GetDirectoryName(Maps[0].fname));
                    string str_map_zm = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Maps[0].fname)));
                    str_map = str_map_zm + "/" + str_map_x + "/" + str_map_y;
                  * */
                    str_map = osmMapName;
                }
                else
                {
                    str_map = Path.GetFileNameWithoutExtension(Maps[0].fname);
                }
            }

            if (MapErrors == __MapErrorReading)
            {
                str_map += "\nRead Error";
                Utils.log.Debug("Map ERROR " + str_map);
            }
            else if (MapErrors == __MapErrorOutOfMemory)
            {
                str_map += "\nOut of Memory Error\n(Filesize too large)";
            }
            else if (MapErrors == __MapErrorDownload)
            {
                str_map += "\nDownload Error";
                Utils.log.Debug("Map ERROR " + str_map);
            }
            else if (MapErrors == __MapErrorNotFound)
                str_map += "\n404 Not Found";

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

        double DataLongMin, DataLongMax, DataLatMin, DataLatMax;
        private void SetAutoScale(UtmUtil utmUtil, float[] PlotLong, float[] PlotLat, int PlotSize, bool lifeview)
        {
            if (PlotSize > 0)
            {
                if (!utmUtil.referenceSet)
                {
                    utmUtil.setReferencePoint(PlotLat[PlotSize - 1], PlotLong[PlotSize - 1]);
                    utmUtil.referenceSet = false;       //better not fix to an old point
                }
                if (lifeview || (PlotSize == 1))   // during liveview (logging), set a fixed scale with current point in the middle
                {
                    double last_x = PlotLong[PlotSize - 1];
                    double last_y = PlotLat[PlotSize - 1];
                    double extend_x = 1.0;
                    double extend_y = 1.0;
                    if (!OsmTilesMode)
                        if (ScreenY > ScreenX)
                            extend_y = (double)ScreenY / (double)ScreenX;
                        else
                            extend_x = (double)ScreenX / (double)ScreenY;
                    DataLongMin = last_x - utmUtil.meter2longit * DefaultZoomRadius * extend_x;
                    DataLongMax = last_x + utmUtil.meter2longit * DefaultZoomRadius * extend_x;
                    DataLatMin = last_y - utmUtil.meter2lat * DefaultZoomRadius * extend_y;
                    DataLatMax = last_y + utmUtil.meter2lat * DefaultZoomRadius * extend_y;
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
                    double dx = (DataLongMax - DataLongMin) / 20;       //make ca. 10% larger
                    double dy = (DataLatMax - DataLatMin) / 20;
                    if (dx < 0.0001) dx = 0.0001;
                    if (dy < 0.0001) dy = 0.0001;
                    DataLongMin -= dx;
                    DataLongMax += dx;
                    DataLatMin -= dy;
                    DataLatMax += dy;
                }
            }
            double xscale = (double)ScreenX / (DataLongMax - DataLongMin);
            double yscale = (double)ScreenY / ((OsmLat2YTile(1, DataLatMin) - OsmLat2YTile(1, DataLatMax)) * 360);
            DataLongMiddle = (DataLongMin + DataLongMax) / 2;
            DataLatMiddle = (DataLatMin + DataLatMax) / 2;
            if (OsmTilesMode)
            {
                UInt32 xTiles = (UInt32)((yscale > xscale ? xscale : yscale) * 360 / 256);
                for (OsmN = 1 << 30; OsmN > 1; OsmN >>= 1)
                    if ((xTiles & OsmN) > 0)
                        break;                      //OsmN is next smaller power of 2
                OsmN = (int)(OsmN * ZoomValue);
                Long2Pixel = OsmN * 32 / 45.0;      //  OsmN * 256 / 360.0
                double yMiddleTile = OsmLat2YTile(OsmN, DataLatMiddle + LatShift);     //linearize at middle of display
                Lat2Pixel = 1 / (OsmYTile2Lat(OsmN, yMiddleTile - 1.0 / 512) - OsmYTile2Lat(OsmN, yMiddleTile + 1.0 / 512));    //linearize with +/- 0.5 pixel
            }
            else
            {
                Long2Pixel = (yscale > xscale ? xscale : yscale) * ZoomValue;
                Lat2Pixel = Long2Pixel * utmUtil.meter2longit / utmUtil.meter2lat;
            }
        }

        public bool drawWhileShift = true;
        // draw map and 2 lines (main and "to follow"). Shift is the x/y shift of the second line origin.
        public void DrawMaps(Graphics g, Bitmap BackBuffer, Graphics BackBufferGraphics, UtmUtil utmUtil,
                             bool MouseMoving, bool lifeview, int MapMode, 
                             double unit_cff, string unit_name,
                             float[] PlotLong, float[] PlotLat, int PlotSize, Color line_color, int line_width, bool plot_dots,
                             Form1.WayPointInfo WayPointsT, Form1.WayPointInfo WayPointsT2F, int ShowWaypoints,
                             float[] PlotLong2, float[] PlotLat2, int PlotSize2, Color line_color2, int line_width2, bool plot_dots2,
                             double CurrentLong, double CurrentLat, int heading, string clickLatLon, Color mapLabelColor)
        {
            ScreenX = BackBuffer.Width; ScreenY = BackBuffer.Height;
            // if picture is moving - draw existing picture
            if (MouseMoving && !drawWhileShift)
            {
                DrawMovingImage(g, BackBuffer, (int)((LongShift - LongShiftSave) * Long2Pixel), (int)((LatShift - LatShiftSave) * Lat2Pixel));
                return;
            }
            // set scale from "main" or the "track-to-follow" (if main not exist)
            float[] CurLongA = { (float)CurrentLong };
            float[] CurLatA = { (float)CurrentLat };
            if (ShowTrackToFollowMode == ShowTrackToFollow.T2FStart && PlotSize2 > 0)
            {
                // Show start position of track to follow
                SetAutoScale(utmUtil, PlotLong2, PlotLat2, 1, false);
            }
            else if (ShowTrackToFollowMode == ShowTrackToFollow.T2FEnd && PlotSize2 > 0)
            {
                // Show end position of track to follow
                float[] Long = { PlotLong2[PlotSize2 - 1] };
                float[] Lat = { PlotLat2[PlotSize2 - 1] };
                SetAutoScale(utmUtil, Long, Lat, 1, false);
            }
            else if (parent.trackEditMode != Form1.TrackEditMode.Off)
            {
                SetAutoScale(utmUtil, null, null, 0, false);           // in trackEditMode prevent autoscale; only update vars
            }
            else if (lifeview)
            {
                // Show current GPS position (last position)
                SetAutoScale(utmUtil, CurLongA, CurLatA, 1, lifeview);
            }
            else if (PlotSize > 0)
            {
                // Show all points of loaded track
                SetAutoScale(utmUtil, PlotLong, PlotLat, PlotSize, lifeview);
            }
            else if (PlotSize2 > 0 && ShowTrackToFollowMode != ShowTrackToFollow.T2FCurrent)
            {
                // Show all points of track to follow
                SetAutoScale(utmUtil, PlotLong2, PlotLat2, PlotSize2, false);
            }
            else
            {
                // Show last position
                SetAutoScale(utmUtil, CurLongA, CurLatA, 1, false);
            }

            // need to draw the picture first into back buffer
            BackBufferGraphics.Clear(Back_Color);

            if (!hideMap)
            {
                // in OSM tile mode, required map array is created based on the current screen coordinates
                if (OsmTilesMode)
                {
                    FillOsmTiles(MapMode);
                    NumBitmaps = NumMaps;

                    DrawJpeg(BackBufferGraphics);
                }
                else
                {
                    // Update screen coordinates for all maps
                    for (int i = 0; i < NumMaps; i++)
                    {
                        Maps[i].scrX1_ix = ToScreenX(Maps[i].lon1);
                        Maps[i].scrX2_iz = ToScreenX(Maps[i].lon2);
                        Maps[i].scrY1_iy = ToScreenY(Maps[i].lat1);
                        Maps[i].scrY2 = ToScreenY(Maps[i].lat2);
                    }

                    // select the best map and draw it
                    SelectBestMap(MapMode);
                    RemoveBitmaps(NumBitmaps); // removes unused bitmaps
                    DrawJpeg(BackBufferGraphics);
                }
                if (mapCopyright != null)
                {
                    Font drawFont = new Font("Arial", 8 * parent.df, FontStyle.Bold);
                    SolidBrush drawBrush = new SolidBrush(mapLabelColor);
                    SizeF size = BackBufferGraphics.MeasureString(mapCopyright, drawFont);
                    BackBufferGraphics.DrawString(mapCopyright, drawFont, drawBrush, ScreenX - size.Width - 2, size.Height);
                }
            }

            Pen pen = new Pen(Color.LightGray, 1);

            // draw the track-to-follow line
            if (PlotSize2 > 0 && hideT2f == false)
            {
                pen.Color = line_color2;
                pen.Width = line_width2;
                DrawTrackLine(BackBufferGraphics, pen, PlotLong2, PlotLat2, PlotSize2, plot_dots2, CurrentLong, CurrentLat);
                DrawCurrentPoint(BackBufferGraphics, PlotLong2[PlotSize2 - 1], PlotLat2[PlotSize2 - 1], line_width2, line_color2);
            }

            // draw the main track line
            if (PlotSize > 0 && hideTrack == false)
            {
                pen.Color = line_color;
                pen.Width = line_width;
                DrawTrackLine(BackBufferGraphics, pen, PlotLong, PlotLat, PlotSize, plot_dots, 0, 0);
                // draw last point larger by 5 points
                DrawCurrentPoint(BackBufferGraphics, PlotLong[PlotSize - 1], PlotLat[PlotSize - 1], line_width + 5, line_color);
            }

            if (ShowWaypoints > 0)
            {
                // Draw the Waypoints (on top of track2follow and track line)
                DrawWayPoints(BackBufferGraphics, pen, WayPointsT, Utils.modifyColor(line_color, +100), ShowWaypoints);
                DrawWayPoints(BackBufferGraphics, pen, WayPointsT2F, Utils.modifyColor(line_color2, +100), ShowWaypoints);
            }

            if (lifeview)
            {
                Point p0 = new Point(ToScreenX(CurrentLong), ToScreenY(CurrentLat));
                Color col = line_color, col2 = line_color2;
                bool outside0 = false;
                bool outside1 = false;
                if (heading == 720) { col = Color.DimGray; col2 = Color.DimGray; }

                if (PlotSize2 > 0 && (showNav || parent.comboNavCmd.SelectedIndex > 0))
                {
#if debugNav
                    GetNavigationData(utmUtil, PlotLong2, PlotLat2, parent.Plot2ndD, PlotSize2, CurrentLong, CurrentLat);
#endif
                    //DoVoiceCommand();
                    if (showNav && !hideAllInfos)
                    {
                        // We draw the line between current position and T2F with half of the T2F width and somewhat lighter
                        pen.Width = Math.Max(2, line_width2 / 2);
                        pen.Color = Utils.modifyColor(line_color2, +100);
                        Point p1 = new Point(ToScreenX(nav.LongIntersection), ToScreenY(nav.LatIntersection));
                        outside0 = ScreenClip(ref p0, p1);
                        outside1 = ScreenClip(ref p1, p0);
                        BackBufferGraphics.DrawLine(pen, p0.X, p0.Y, p1.X, p1.Y);
                        //if (outside0 || outside1)      // If the Current Position is outside of the Screen or the nearest point on the track is outside
                        //{                              // of the screen, show the min distance to track
                        //    DrawDistanceToTrack2Follow(BackBufferGraphics, pen, unit_cff, unit_name);
                        //}
                        if (!outside0)
                            DrawArrow(BackBufferGraphics, p0.X, p0.Y, nav.Angle100mAhead, line_width2 * 3 + 7, line_color2);                    //navigation arrow pointing at t2f 100m ahead
                        DrawArrow(BackBufferGraphics, ScreenX * 4 / 5, ScreenY / 5, nav.Angle100mAhead - heading, ScreenX / 5, col2);   //big navigation arrow in direction of travel
                        
                        Font drawFont = new Font("Arial", 22, FontStyle.Bold);
                        SolidBrush drawBrush = new SolidBrush(line_color2);
                        //if (nav.strCmd.Length > 8)
                        //{
                        //    int blank = nav.strCmd.IndexOf(" ", 5);
                        //    if (blank > 1)
                        //    {
                        //        string navstr2 = nav.strCmd.Remove(0, blank + 1);
                        //        nav.strCmd = nav.strCmd.Remove(blank, nav.strCmd.Length - blank);
                        //        BackBufferGraphics.DrawString(navstr2, drawFont, drawBrush, ScreenX / 40, ScreenY / 12 + (int)BackBufferGraphics.MeasureString(nav.strCmd, drawFont).Height);
                        //    }
                        //}
                        if (nav.Symbol != null)
                        {
                            DrawNavSymbol(BackBufferGraphics, drawBrush, ScreenX / 40, ScreenY / 12, nav.Symbol, nav.orient, nav.SkyDirection, false);
                            BackBufferGraphics.DrawString(nav.strCmd, drawFont, drawBrush, ScreenX * 130 / 480, ScreenY / 14);
                        }
                        if (corner.Type != 0)
                        {
                            //DrawCurrentPoint(BackBufferGraphics, PlotLong2[corner.IndexT2F], PlotLat2[corner.IndexT2F], 6, Color.Fuchsia);
                            DrawCurrentPoint(BackBufferGraphics, corner.Long, corner.Lat, line_width2, Color.Yellow);

                            //DrawCurrentPoint(BackBufferGraphics, PlotLong2[corner.IndexT2F - 2], PlotLat2[corner.IndexT2F - 2], 4, Color.Yellow);
                            //DrawCurrentPoint(BackBufferGraphics, PlotLong2[corner.IndexT2F - 1], PlotLat2[corner.IndexT2F - 1], 4, Color.Yellow);
                            //DrawCurrentPoint(BackBufferGraphics, PlotLong2[corner.IndexT2F + 1], PlotLat2[corner.IndexT2F + 1], 4, Color.Yellow);
                            //DrawCurrentPoint(BackBufferGraphics, PlotLong2[corner.IndexT2F + 2], PlotLat2[corner.IndexT2F + 2], 4, Color.Yellow);
                        }
                    }
                }
                if (!outside0)
                    DrawArrow(BackBufferGraphics, p0.X, p0.Y, heading, line_width * 2 + 5, col);                            //arrow showing direction of movement
                // draw gps led point
                //DrawCurrentPoint(BackBufferGraphics, CurLong[0], CurLat[0], line_width, CurrentGpsLedColor);
            }
            if (showMapLabel && !hideAllInfos || parent.trackEditMode == Form1.TrackEditMode.T2f)
            {
                // draw tick label and map name
                double screen_width_units = ScreenX / Long2Pixel * unit_cff / utmUtil.meter2longit;
                // tick distance in "units" (i.e. km or miles)
                double tick_distance_units = TickMark(screen_width_units, 4);
                // tick distance in screen pixels
                int tick_distance_screen = (int)(tick_distance_units * Long2Pixel / unit_cff * utmUtil.meter2longit);

                pen.Color = mapLabelColor;
                DrawTickLabel(BackBufferGraphics, pen, tick_distance_screen, tick_distance_units, unit_name, clickLatLon);
            }
            if (!hideAllInfos)
                DrawMapValues(BackBufferGraphics, mapLabelColor);
            // draw back buffer on screen
            g.DrawImage(BackBuffer, 0, 0);
        }

        bool ScreenClip(ref Point p, Point p0)       //clip lines because it is very slow if coordinates are big (millions) due to high zoom
        {                                            //p0 is unmodified point of line (within screen)
            bool clipped = false;
            const int b = 80;      //border -> when clipped exact, line at the edge could be visible (and arrow vanishes)
            if (p.X < -b)
            {
                int dx = p0.X - p.X;
                if (dx != 0)
                    p.Y = (int)(((long)p.Y - p0.Y) * (p0.X + b) / dx + p0.Y);
                p.X = -b;
                clipped = true;
            }
            if (p.X > ScreenX + b)
            {
                int dx = p.X - p0.X;
                if (dx != 0)
                    p.Y = (int)(((long)p.Y - p0.Y) * (ScreenX + b - p0.X) / dx + p0.Y);
                p.X = ScreenX + b;
                clipped = true;
            }
            if (p.Y < -b)
            {
                int dy = p.Y - p0.Y;
                if (dy != 0)
                    p.X = (int)(((long)p0.X - p.X) * (p0.Y + b) / dy + p0.X);
                p.Y = -b;
                clipped = true;
            }
            if (p.Y > ScreenY + b)
            {
                int dy = p.Y - p0.Y;
                if (dy != 0)
                    p.X = (int)(((long)p.X - p0.X) * (ScreenY + b - p0.Y) / dy + p0.X);
                p.Y = ScreenY + b;
                clipped = true;
            }
            return clipped;
        }

        public void DrawNavigate(Graphics g, Bitmap BackBuffer, Graphics BackBufferGraphics, float[] PlotLong2, float[] PlotLat2, int PlotSize2, float CurLong, float CurLat, int heading, Color col)
        {
            BackBufferGraphics.Clear(Back_Color);
            SolidBrush drawBrush = new SolidBrush(col);
            string str_nav;
            ScreenX = BackBuffer.Width; ScreenY = BackBuffer.Height;
            if (PlotSize2 > 0)
            {
                int size = Math.Min(ScreenX, ScreenY * 66 / 100) / 2;
                if (heading == 720) col = Color.DimGray;
                DrawArrow(BackBufferGraphics, ScreenX / 2, size, nav.Angle100mAhead - heading, size, col);

                if (nav.Symbol != null)
                {
                    DrawNavSymbol(BackBufferGraphics, drawBrush, ScreenX / 50, ScreenY * 78 / 100 - NavSymbol_size, nav.Symbol, nav.orient, nav.SkyDirection, false);
                    BackBufferGraphics.DrawString(nav.strCmd, new Font("Arial", 24 * parent.df, FontStyle.Bold), drawBrush, ScreenX * 28 / 100, ScreenY * 66 / 100);
                }
                str_nav = nav.strDistance2Dest;
            }
            else
            {
                str_nav = "no Track2F loaded";
            }
            //BackBufferGraphics.DrawString(str_nav, new Font("Arial", 18 * parent.df, FontStyle.Bold), drawBrush, BackBuffer.Width / 50, BackBuffer.Height * 88 / 100);
            DrawMapValues(BackBufferGraphics, col);
            g.DrawImage(BackBuffer, 0, 0); // draw back buffer on screen
        }


        public void GetNavigationData(UtmUtil utmUtil, float[] PlotLong2, float[] PlotLat2, int[] PlotD2, int PlotSize2, double CurLong, double CurLat)
        {
            //System.Diagnostics.Debug.WriteLine("GetNavigationData");
            double x, y;                                            //m  (related to CurPos)
            double dist2, MinDistance2 = double.MaxValue / 16;      //m2
            int j = 0, k = 0;
            const int ArSize = 14;
            int[] indexAr = new int[ArSize];
            double[] xAr = new double[ArSize];      //Array in m relative to intersection/MinDistance
            double[] yAr = new double[ArSize];

            int begin = 0, end = PlotSize2-1, increment = 1;    //GetNavigationData is only executed if PlotSize2 > 0
            if (nav.backward)
            { 
                begin = end;
                end = 0;
                increment = -1;
            }
            if (nav.ix >= PlotSize2)    //in case trackpoints were removed
            {
                nav.ix = PlotSize2 - 1;     //set parameters in case point is more than 500m away
                nav.ixd_intersec = nav.ix;
                nav.ix_next_drive = nav.ix;
                nav.LongIntersection = PlotLong2[nav.ix];
                nav.LatIntersection = PlotLat2[nav.ix];
            }
            if (nav.ShortSearch == 0) nav.ix = begin;
            int beginindex = nav.ix;
            int min_ix = beginindex;
            int min_ixb = beginindex;
            double ca2 = 0, cb2 = 0, t2 = 0, min_ca2 = 0, min_t2 = 0;
            int min_segType = 0;
            int FBSearch = 1;       //1=forward  -1=backward   other=stop
            int min_FBSearch = 1;
            int min_increment = 0;
            while (FBSearch == 1 || FBSearch == -1)
            {
                int increment2 = increment * FBSearch;
                int i = beginindex;
                x = (PlotLong2[i] - CurLong) * utmUtil.longit2meter;
                y = (PlotLat2[i] - CurLat) * utmUtil.lat2meter;
                ca2 = x * x + y * y;
                while (i < PlotSize2 && i >= 0)    //search segment and point with min distance
                {
                    int segType = 0;
                    dist2 = ca2;
                    int ib = i + increment2;
                    if (ib < PlotSize2 && ib >= 0)
                    {
                        while (true)
                        {
                            x = (PlotLong2[ib] - PlotLong2[i]) * utmUtil.longit2meter;
                            y = (PlotLat2[ib] - PlotLat2[i]) * utmUtil.lat2meter;
                            t2 = x * x + y * y;
                            if (t2 < 4 && ib + increment2 < PlotSize2 && ib + increment2 >= 0)    //next point closer than 2m
                                ib += increment2;
                            else break;
                        }
                        x = (PlotLong2[ib] - CurLong) * utmUtil.longit2meter;
                        y = (PlotLat2[ib] - CurLat) * utmUtil.lat2meter;
                        cb2 = x * x + y * y;
                        
                        if (t2 <= cb2 - ca2)            //before i
                        {
                            segType = 0;
                            dist2 = ca2;
                        }
                        else if (t2 <= ca2 - cb2)       //after i+1
                        {
                            segType = 1;
                            dist2 = cb2;
                        }
                        else
                        {                               //currentPos inbetween track segment
                            segType = 2;
                            dist2 = (2 * (ca2 * cb2 + cb2 * t2 + t2 * ca2) - (ca2 * ca2 + cb2 * cb2 + t2 * t2)) / (4 * t2); //Dreieck-Höhenformel (t2 cannot be 0 here)
                        }
                    }
                    if (dist2 < MinDistance2)
                    {
                        min_ix = i;
                        min_ixb = ib;
                        MinDistance2 = dist2;
                        min_ca2 = ca2;
                        min_t2 = t2;
                        min_segType = segType;
                        min_increment = increment2;
                        min_FBSearch = FBSearch;
                    }
                    if (nav.ShortSearch != 0 && dist2 > MinDistance2 * 4 + 10000)
                        break;

                    i = ib;
                    ca2 = cb2;
                }
                if (min_ix == beginindex && FBSearch == 1)
                    FBSearch = -1;                         //try also backward
                else
                    FBSearch = 0;
            }

            if (MinDistance2 < 500 * 500 || nav.ShortSearch == 0)  //use only if within 500m of track zone
            {
                nav.ix = min_ix;
                nav.MinDistance = Math.Sqrt(MinDistance2);
                nav.Distance2Dest = nav.MinDistance;
                if (min_segType == 0)
                {
                    nav.ix_next_drive = min_ix;
                    nav.ixd_intersec = min_ix;
                    nav.LongIntersection = PlotLong2[min_ix];
                    nav.LatIntersection = PlotLat2[min_ix];
                }
                else if (min_segType == 1)
                {
                    nav.ix_next_drive = min_ixb;
                    nav.ixd_intersec = min_ixb;
                    nav.LongIntersection = PlotLong2[min_ixb];
                    nav.LatIntersection = PlotLat2[min_ixb];
                }
                else
                {
                    double quot;
                    if (min_t2 > 1) quot = Math.Sqrt((min_ca2 - MinDistance2) / min_t2);
                    else quot = 0;
                    if (min_FBSearch == 1)
                        nav.ix_next_drive = min_ixb;
                    else
                    {
                        nav.ix = min_ixb;
                        nav.ix_next_drive = min_ix;
                    }
                    nav.ixd_intersec = min_ix + quot * min_increment; ;
                    nav.LongIntersection = PlotLong2[min_ix] + quot * (PlotLong2[min_ixb] - PlotLong2[min_ix]);
                    nav.LatIntersection = PlotLat2[min_ix] + quot * (PlotLat2[min_ixb] - PlotLat2[min_ix]);
                }
            }
            else
            {
                if (nav.MinDistance == -1.0)    //not initialized yet
                {
                    nav.LongIntersection = PlotLong2[nav.ix];
                    nav.LatIntersection = PlotLat2[nav.ix];
                }
                x = (nav.LongIntersection - CurLong) * utmUtil.longit2meter;
                y = (nav.LatIntersection - CurLat) * utmUtil.lat2meter;
                nav.MinDistance = Math.Sqrt(x * x + y * y);
                nav.Distance2Dest = nav.MinDistance;
            }

            nav.ShortSearch = -1;
            //if (nav.ShortSearch > 0)
            //    nav.ShortSearch--;
            //else
            //    nav.ShortSearch = 30;       //for 30s only shortSearch

            // ix_next_drive is now first index to drive over

#if debugNav            //debug   intersection point
            DrawCurrentPoint(parent.BackBufferGraphics, nav.LongIntersection, nav.LatIntersection, 6, Color.Violet);
#endif
            //accumulate Distance2Dest from LongOld (intersection or MinDistance)
            double accu = 0;        //use extra accu because Distance2Dest not always starts from 0
            double LongOld = nav.LongIntersection;     //point to begin accumulation of Distance2Dest
            double LatOld = nav.LatIntersection;
            xAr[0] = 0; yAr[0] = 0; j = 1;
            indexAr[0] = 0;
            x = 0; y = 0;

            for (int i = nav.ix_next_drive; i < PlotSize2 && i >= 0; i += increment)   
            {
                //accumulate distance to destination
                double xa = (PlotLong2[i] - LongOld) * utmUtil.longit2meter;
                double ya = (PlotLat2[i] - LatOld) * utmUtil.lat2meter;
                double ma = Math.Sqrt(xa * xa + ya * ya);
                if (i == nav.ix_next_drive)
                    nav.Distance2Dest += ma;    //Distance2Dest now: Current - Intersection - ix_next_drive
                int dist = (int)(accu + ma);
                while (j < ArSize && j * 20 < dist)        //fill array every 20m for corner search [0, 20, 40,.. 260]
                {
                    double quot = (j * 20 - accu) / ma;
                    xAr[j] = x + xa * quot;    // xOld + quot * (x - xOld);
                    yAr[j] = y + ya * quot;    //yOld + quot * (y - yOld);
                    indexAr[j] = nav.ix_next_drive;
                    j++;
                }
                accu += ma;
                LongOld = PlotLong2[i];
                LatOld = PlotLat2[i];
                if (j < ArSize)           //only necessary for array fill
                {
                    x = (LongOld - nav.LongIntersection) * utmUtil.longit2meter;    //can get inacurate for long tracks, therefore not used for Distance2Dest
                    y = (LatOld - nav.LatIntersection) * utmUtil.lat2meter;
                }
                else
                    break;
            }

            nav.Distance2Dest += (PlotD2[end] - PlotD2[nav.ix_next_drive]) * increment;
        

#if debugNav            //debug   260m line
            for (int n = 0; n < j; n++)
            {
                DrawCurrentPoint(parent.BackBufferGraphics, (xAr[n] * utmUtil.meter2longit + nav.LongIntersection), (yAr[n] * utmUtil.meter2lat + nav.LatIntersection), 2, Color.White);
            }
#endif
            //nav.Angle100mAhead = (int)(180.0 / Math.PI * Math.Atan2((PlotLong2[Index100mDistance] - CurLong) * utmUtil.longit2meter, (PlotLat2[Index100mDistance] - CurLat) * utmUtil.lat2meter));
            int jdx100 = Math.Min(5, j - 1);
            nav.Angle100mAhead = (int)(180.0 / Math.PI * Math.Atan2(xAr[jdx100] - (CurLong - nav.LongIntersection) * utmUtil.longit2meter, yAr[jdx100] - (CurLat - nav.LatIntersection) * utmUtil.lat2meter));

            if (corner.Type == 0)      //search corner
            {
                int dir1 = 0, dir2 = 0, angle = 0, angleAbs, angleAbsMax = -1, kc = 0;
                bool cornerfound = false;
                if (corner.processedIndex == -1)
                    corner.processedIndex = begin;

                for (k = 2; k < j - 2; k++)
                {
                    if (indexAr[k] != PlotSize2 && (corner.processedIndex - indexAr[k]) * increment > 2)       //little overlap (reprocess)
                        continue;                                                   //already processed - to reduce calc

                    dir1 = (int)(180 / Math.PI * Math.Atan2(xAr[k - 1] - xAr[k - 2], yAr[k - 1] - yAr[k - 2]));
                    dir2 = (int)(180 / Math.PI * Math.Atan2(xAr[k + 2] - xAr[k + 1], yAr[k + 2] - yAr[k + 1]));
                    angle = dir2 - dir1;
                    if (angle > 180) angle -= 360;
                    if (angle <= -180) angle += 360;
                    angleAbs = Math.Abs(angle);
                    corner.processedIndex = indexAr[k];
                    if (angleAbs > angleAbsMax)
                    {
                        angleAbsMax = angleAbs;
                        corner.IndexT2F = indexAr[k];
                        corner.dir1 = dir1;
                        corner.dir2 = dir2;
                        corner.angle = angle;
                        kc = k;
                    }
                    if (angleAbsMax >= 35 && angleAbs < angleAbsMax - 10)   //run over corner (angle gets smaller)
                    {
                        cornerfound = true;
                        break;
                    }
                }
                if (angleAbsMax >= 35 && j < ArSize && kc >= j - 4)     //corner at end of T2F
                    cornerfound = true;
                if (cornerfound)
                {
                    if (angleAbsMax < 65)
                        corner.Type = 1;     //half turn
                    else if (angleAbsMax < 115)
                        corner.Type = 2;     //turn
                    else
                        corner.Type = 3;     //sharp turn
                    corner.distance = int.MaxValue;    //to overcome invalidate algorithm

                    corner.Long = (float)(xAr[kc] * utmUtil.meter2longit + nav.LongIntersection);
                    corner.Lat = (float)(yAr[kc] * utmUtil.meter2lat + nav.LatIntersection);
                    corner.voicePlayed = false;

#if debugNav                    //debug
                    //DrawCurrentPoint(parent.BackBufferGraphics, PlotLong2[indexAr[kc - 2]], PlotLat2[indexAr[kc - 2]], 6, Color.LightBlue);
                    //DrawCurrentPoint(parent.BackBufferGraphics, PlotLong2[indexAr[kc - 1]], PlotLat2[indexAr[kc - 1]], 6, Color.LightBlue);
                    //DrawCurrentPoint(parent.BackBufferGraphics, PlotLong2[indexAr[kc - 0]], PlotLat2[indexAr[kc - 0]], 8, Color.Blue);
                    //DrawCurrentPoint(parent.BackBufferGraphics, PlotLong2[indexAr[kc + 1]], PlotLat2[indexAr[kc + 1]], 6, Color.LightBlue);
                    //DrawCurrentPoint(parent.BackBufferGraphics, PlotLong2[indexAr[kc + 2]], PlotLat2[indexAr[kc + 2]], 6, Color.LightBlue);
#endif
                }
                else
                    corner.Type = 0;     //straight
            }
            if (corner.Type != 0)      //update volatile corner details
            {
                x = (corner.Long - CurLong) * utmUtil.longit2meter;
                y = (corner.Lat - CurLat) * utmUtil.lat2meter;
                int cornerdistance = (int)Math.Sqrt(x * x + y * y) / 10 * 10;       //rounded to 10m
                if (cornerdistance > corner.distance || cornerdistance > 300)
                {
                    invalidateCorner();
                }
                else
                {
                    corner.distance = cornerdistance;
                    corner.direction = (int)(180.0 / Math.PI * Math.Atan2(x, y));
                }
            }


            double unit_cff = parent.GetUnitsConversionCff();
            string unit_name = parent.GetUnitsName();

            int dir = nav.Angle100mAhead;
            if (parent.Heading != 720)
                dir -= parent.Heading;
            while (dir < 0) dir += 360;
            nav.orient = Orientation.normal;
            nav.SkyDirection = false;
            if (nav.MinDistance > (double)parent.numericTrackTolerance.Value)
            {
                nav.Symbol = arrow_to;
                if (parent.Heading == 720)
                    nav.SkyDirection = true;
                if (dir < 45)
                    nav.orient = Orientation.normal;
                else if (dir < 135)
                    nav.orient = Orientation.right;
                else if (dir < 225)
                    nav.orient = Orientation.mirrorY;
                else if (dir < 315)
                    nav.orient = Orientation.left;
                //else
                //nav.orient = Orientation.normal;
                //if (nav.MinDistance >= 10000)
                //    str += " " + ((int)nav.MinDistance / 1000).ToString() + "km";
                //else
                //    str += " " + ((int)nav.MinDistance / 10 * 10).ToString() + "m";
                nav.strCmd = (nav.MinDistance * unit_cff).ToString("0.00") + unit_name;
            }
            else if (parent.Heading != 720 && dir > 135 && dir < 225 && parent.CurrentSpeed > 4.0)
            {
                nav.Symbol = arrow_turn;      //wenden
                nav.strCmd = "";
            }
            else if (corner.Type != 0)        //navigation command
            {
                switch (corner.Type)
                {
                    case 1: nav.Symbol = arrow_hr; break;
                    case 2: nav.Symbol = arrow_r; break;
                    case 3: nav.Symbol = arrow_sr; break;
                    default: nav.Symbol = null; break;
                }
                if (corner.angle < 0)
                    nav.orient = Orientation.mirrorX;   //left
                //str = corner.angle.ToString() + str;
                nav.strCmd = corner.distance.ToString() + "m";
            }
            else if (nav.Distance2Dest <= 200)
            {
                nav.Symbol = destination;
                nav.strCmd = (((int)nav.Distance2Dest / 10) * 10).ToString() + "m";
            }
            else
                nav.Symbol = null;

            nav.strDistance2Dest = (nav.Distance2Dest * unit_cff).ToString("0.00") + unit_name + " to destin.";

            return;
        }

        public void invalidateCorner()
        {
            corner.Type = 0;
            corner.processedIndex = -1;
        }

        double absAngle(double a)       // -360..360deg reduced to 0..180deg   (but in rad)
        {
            double ret = Math.Abs(a);
            if (ret > Math.PI)
                ret = Math.Abs(2 * Math.PI - ret);
            return ret;
        }

        public void DrawNavSymbol(Graphics g, SolidBrush sb, int x, int y, Point[] symbol, Orientation or, bool SkyDir, bool shrink)
        {
            Point[] pa = (Point[])symbol.Clone();
            int len = pa.Length;

            for (int i = 0; i < len; i++)
            {
                switch (or)
                {
                    case Orientation.mirrorX:
                        pa[i].X = NavSymbol_size - pa[i].X;
                        break;
                    case Orientation.mirrorY:
                        pa[i].Y = NavSymbol_size - pa[i].Y;
                        break;
                    case Orientation.right:
                        pa[i].Y = symbol[i].X;
                        pa[i].X = NavSymbol_size - symbol[i].Y;
                        break;
                    case Orientation.left:
                        pa[i].Y = NavSymbol_size - symbol[i].X;
                        pa[i].X = symbol[i].Y;
                        break;
                }
                if (SkyDir)
                    if (or == Orientation.right || or == Orientation.left)
                        pa[i].Y -= NavSymbol_size / 5;
                    else
                        pa[i].X -= NavSymbol_size / 5;
                if (shrink)
                {
                    pa[i].X /= 2;
                    pa[i].Y /= 2;
                }
                pa[i].X += x;
                pa[i].Y += y;
            }
            Pen pen = new Pen(Color.Black, NavSymbol_size / (shrink ? 24 : 12));
            g.DrawPolygon(pen, pa);
            g.FillPolygon(sb, pa);
            if (symbol == arrow_to)
            {
                DrawNavSymbol(g, sb, x, y, line_to, or, false, shrink);     //draw other parts of symbol
                if (SkyDir)
                {
                    string s;
                    int dx, dy;
                    float fontsize;
                    int quot;
                    switch (or)
                    {
                        case Orientation.normal: s = "N"; dx = 60; dy = 50; break;
                        case Orientation.left: s = "W"; dx = 50; dy = 50; break;
                        case Orientation.right: s = "E"; dx = 0; dy = 40; break;
                        case Orientation.mirrorY: s = "S"; dx = 60; dy = -10; break;
                        default: s = ""; dx = x; dy = y; break;
                    }
                    if (shrink)
                    { fontsize = 8; quot = 200; }
                    else
                    { fontsize = 20; quot = 100; }
                    Font font = new Font("Arial", fontsize * parent.df, FontStyle.Bold);
                    dx = dx * NavSymbol_size / quot + x;
                    dy = dy * NavSymbol_size / quot + y;
                    g.DrawString(s, font, sb, dx, dy);
                }
            }
        }

        int msTicks = Int16.MinValue;
        public void DoVoiceCommand()
        {
            if (parent.comboNavCmd.SelectedIndex <= 0)
                return;
            int clock = nav.Angle100mAhead - parent.Heading;
            while (clock < 0) clock += 360;
            if (nav.MinDistance > (double)parent.numericTrackTolerance.Value)
            {
                if (!nav.voicePlayed_toRoute)            // if (Environment.TickCount - msTicks > 15000)
                {
                    if (parent.comboNavCmd.SelectedIndex > 2)
                    {
                        Utils.buzzer(1000);
                    }
                    else
                    {
                        FileStream fs = null;
                        StreamReader sr = null;
                        try
                        {
                            if (threadRunning == true)
                            {
                                thr.Abort();
                                threadRunning = false;
                            }
                            fs = new FileStream(parent.LanguageDirectory + "\\seq_toRoute.txt", FileMode.Open, FileAccess.Read);
                            sr = new StreamReader(fs);
                            string word;
                            VoiceStrAr.Clear();
                            while (true)
                            {
                                try { word = sr.ReadLine(); }
                                catch (EndOfStreamException) { break; }
                                if (word == null) break;
                                switch (word)
                                {
                                    case "%Distance":
                                        if (nav.MinDistance > 13000)
                                            VoiceStrAr.Add("Many.wav");
                                        else if (nav.MinDistance > 11000)
                                            VoiceStrAr.Add("Twelve.wav");
                                        else if (nav.MinDistance > 9000)
                                            VoiceStrAr.Add("Ten.wav");
                                        else if (nav.MinDistance > 7000)
                                            VoiceStrAr.Add("Eight.wav");
                                        else if (nav.MinDistance > 5000)
                                            VoiceStrAr.Add("Six.wav");
                                        else if (nav.MinDistance > 3000)
                                            VoiceStrAr.Add("Four.wav");
                                        else if (nav.MinDistance > 1500)
                                            VoiceStrAr.Add("Two.wav");
                                        else if (nav.MinDistance > 750)
                                            VoiceStrAr.Add("One.wav");
                                        else if (nav.MinDistance > 300)
                                            VoiceStrAr.Add("Fivehundred.wav");
                                        else if (nav.MinDistance > 150)
                                            VoiceStrAr.Add("Twohundred.wav");
                                        else if (nav.MinDistance > 50)
                                            VoiceStrAr.Add("Onehundred.wav");
                                        break;
                                    case "%Unit":
                                        if (nav.MinDistance > 1500)
                                            VoiceStrAr.Add("Kilometers.wav");
                                        else if (nav.MinDistance > 750)
                                            VoiceStrAr.Add("Kilometer.wav");
                                        else
                                            VoiceStrAr.Add("Meters.wav");
                                        break;
                                    case "%Direction":
                                        if (parent.Heading != 720)
                                        {
                                            if (clock < 30)
                                                VoiceStrAr.Add("Twelve.wav");
                                            else if (clock < 90)
                                                VoiceStrAr.Add("Two.wav");
                                            else if (clock < 150)
                                                VoiceStrAr.Add("Four.wav");
                                            else if (clock < 210)
                                                VoiceStrAr.Add("Six.wav");
                                            else if (clock < 270)
                                                VoiceStrAr.Add("Eight.wav");
                                            else if (clock < 330)
                                                VoiceStrAr.Add("Ten.wav");
                                            else
                                                VoiceStrAr.Add("Twelve.wav");
                                        }
                                        else
                                        {
                                            int dir = nav.Angle100mAhead;
                                            if (dir < -135)
                                                VoiceStrAr.Add("South.wav");
                                            else if (dir < -45)
                                                VoiceStrAr.Add("West.wav");
                                            else if (dir < 45)
                                                VoiceStrAr.Add("North.wav");
                                            else if (dir < 135)
                                                VoiceStrAr.Add("East.wav");
                                            else
                                                VoiceStrAr.Add("South.wav");
                                        }
                                        break;
                                    case "%OClock":
                                        if (parent.Heading != 720)
                                            VoiceStrAr.Add("OClock.wav");
                                        break;
                                    default:
                                        if (word.Length > 0)
                                            VoiceStrAr.Add(word);
                                        break;
                                }
                            }
                            thr = new Thread(new ThreadStart(VoiceThreadProc));
                            thr.Start();
                        }
                        catch (Exception e)
                        {
                            Utils.log.Error(" seq_toRoute ", e);
                        }
                        if (sr != null) sr.Close();
                        if (fs != null) fs.Close();
                    }
                    nav.voicePlayed_toRoute = true;          //msTicks = Environment.TickCount;
                }
            }
            else if (parent.Heading != 720 && clock > 135 && clock < 225 && parent.CurrentSpeed > 4.0)
            {
                if (Environment.TickCount - msTicks > 30000)
                {
                    if (parent.comboNavCmd.SelectedIndex > 2)
                    {
                        Utils.buzzer(1000);
                    }
                    else
                    {
                        try
                        {
                            if (threadRunning == true)
                            {
                                thr.Abort();
                                threadRunning = false;
                            }
                            VoiceStrAr.Clear();
                            VoiceStrAr.Add("TurnOver.wav");
                            thr = new Thread(new ThreadStart(VoiceThreadProc));
                            thr.Start();
                        }
                        catch (Exception e)
                        {
                            Utils.log.Error(" turn over ", e);
                        }
                    }
                    msTicks = Environment.TickCount;
                }
            }
            else if (corner.Type > 0 && ((parent.comboNavCmd.SelectedIndex & 1) == 1))
            {
                string s_dist = null;
                switch (corner.distance)
                {
                    case 210:
                    case 200:
                    case 190: s_dist = "Twohundred.wav"; break;
                    case 110:
                    case 100:
                    case 90: s_dist = "Onehundred.wav"; break;
                    case 20:
                    case 10: s_dist = "Now.wav"; break;
                    default: corner.voicePlayed = false; break;
                }
                if (s_dist != null && !corner.voicePlayed)
                {
                    if (parent.comboNavCmd.SelectedIndex > 2)
                    {
                        Utils.buzzer(1000);
                    }
                    else
                    {
                        FileStream fs = null;
                        StreamReader sr = null;
                        try
                        {
                            if (threadRunning == true)
                            {
                                thr.Abort();
                                threadRunning = false;
                            }
                            fs = new FileStream(parent.LanguageDirectory + "\\seq_turn.txt", FileMode.Open, FileAccess.Read);
                            sr = new StreamReader(fs);
                            string word;
                            VoiceStrAr.Clear();
                            while (true)
                            {
                                try { word = sr.ReadLine(); }
                                catch (EndOfStreamException) { break; }
                                if (word == null) break;
                                switch (word)
                                {
                                    case "%In":
                                        if (corner.distance > 50)
                                            VoiceStrAr.Add("In.wav");
                                        break;
                                    case "%Distance":
                                        VoiceStrAr.Add(s_dist);
                                        break;
                                    case "%Unit":
                                        if (corner.distance > 50)
                                            VoiceStrAr.Add("Meters.wav");
                                        break;
                                    case "%Half":
                                        if (corner.Type == 1)
                                            VoiceStrAr.Add("Half.wav");
                                        else if (corner.Type == 3)
                                            VoiceStrAr.Add("Sharp.wav");
                                        break;
                                    case "%Left":
                                        if (corner.angle < 0)
                                            VoiceStrAr.Add("Left.wav");
                                        else
                                            VoiceStrAr.Add("Right.wav");
                                        break;
                                    default:
                                        if (word.Length > 0)
                                            VoiceStrAr.Add(word);
                                        break;
                                }
                            }
                            thr = new Thread(new ThreadStart(VoiceThreadProc));
                            thr.Start();
                        }
                        catch (Exception e)
                        {
                            Utils.log.Error(" seq_turn ", e);
                        }
                        if (sr != null) sr.Close();
                        if (fs != null) fs.Close();
                    }
                    corner.voicePlayed = true;
                }
            }//corner
            else if (nav.Distance2Dest <= 200)      //destination
            {
                string s_dist = null;
                int dist = (int)(nav.Distance2Dest + 5) / 10 * 10;
                switch (dist)
                {
                    case 210:
                    case 200:
                    case 190: s_dist = "Twohundred.wav"; break;
                    case 110:
                    case 100:
                    case 90: s_dist = "Onehundred.wav"; break;
                    case 20:
                    case 10: s_dist = "Now.wav"; break;
                    default: nav.voicePlayed_dest = false; break;
                }
                if (s_dist != null && !nav.voicePlayed_dest)
                {
                    if (parent.comboNavCmd.SelectedIndex > 2)
                    {
                        Utils.buzzer(1000);
                    }
                    else
                    {
                        FileStream fs = null;
                        StreamReader sr = null;
                        try
                        {
                            if (threadRunning == true)
                            {
                                thr.Abort();
                                threadRunning = false;
                            }
                            fs = new FileStream(parent.LanguageDirectory + "\\seq_destination.txt", FileMode.Open, FileAccess.Read);
                            sr = new StreamReader(fs);
                            string word;
                            VoiceStrAr.Clear();
                            while (true)
                            {
                                try { word = sr.ReadLine(); }
                                catch (EndOfStreamException) { break; }
                                if (word == null) break;
                                switch (word)
                                {
                                    case "%In":
                                        if (dist > 50)
                                            VoiceStrAr.Add("In.wav");
                                        break;
                                    case "%Distance":
                                        VoiceStrAr.Add(s_dist);
                                        break;
                                    case "%Unit":
                                        if (dist > 50)
                                            VoiceStrAr.Add("Meters.wav");
                                        break;
                                    default:
                                        if (word.Length > 0)
                                            VoiceStrAr.Add(word);
                                        break;
                                }
                            }
                            thr = new Thread(new ThreadStart(VoiceThreadProc));
                            thr.Start();
                        }
                        catch (Exception e)
                        {
                            Utils.log.Error(" seq_destination ", e);
                        }
                        if (sr != null) sr.Close();
                        if (fs != null) fs.Close();
                    }
                    nav.voicePlayed_dest = true;
                }
            }
            else
            {
                nav.voicePlayed_toRoute = false;
                nav.voicePlayed_dest = false;
            }

            doneVoiceCommand = true;
        }

        public void playVoiceTest()
        {
            if (parent.comboNavCmd.SelectedIndex > 2)
            {
                Utils.buzzer(1000);
            }
            else
            {
                FileStream fs = null;
                StreamReader sr = null;
                try
                {
                    if (threadRunning == true)
                    {
                        thr.Abort();
                        threadRunning = false;
                    }
                    fs = new FileStream(parent.LanguageDirectory + "\\seq_test.txt", FileMode.Open, FileAccess.Read);
                    sr = new StreamReader(fs);
                    string word;
                    VoiceStrAr.Clear();
                    while (true)
                    {
                        try { word = sr.ReadLine(); }
                        catch (EndOfStreamException) { break; }
                        if (word == null) break;
                        switch (word)
                        {
                            default:
                                if (word.Length > 0)
                                    VoiceStrAr.Add(word);
                                break;
                        }
                    }
                    thr = new Thread(new ThreadStart(VoiceThreadProc));
                    thr.Start();
                }
                catch (Exception e)
                {
                    Utils.log.Error(" seq_test ", e);
                }
                if (sr != null) sr.Close();
                if (fs != null) fs.Close();
            }
        }

        [DllImport("coredll.dll")]
        public static extern bool sndPlaySound(string fname, int flag);

        // these are the SoundFlags we are using here, check mmsystem.h for more
        const int SND_SYNC = 0x00000000;   // play synchronously (default)
        const int SND_ASYNC = 0x0001; // play asynchronously
        const int SND_FILENAME = 0x00020000; // use file name
        const int SND_PURGE = 0x0040; // purge non-static events
        const int SND_NOSTOP = 0x00000010;   // don't stop any currently playing sound
        const int SND_NOWAIT = 0x00002000;   // don't wait if the driver is busy

        
        ArrayList VoiceStrAr = new ArrayList();
        Thread thr;
        bool threadRunning = false;
        public void VoiceThreadProc()                 //use own thread and SYNC to chain several voice files
        {
            threadRunning = true;
            sndPlaySound(null, 0);              //first stop any running voice
            foreach (string str in VoiceStrAr)
            {
                sndPlaySound(parent.LanguageDirectory + "\\" + str, SND_SYNC | SND_FILENAME | SND_NOSTOP);
            }
            threadRunning = false;
        }

        // make sure that the central point is stationary after zoom in / zoom out
        public void ZoomIn()
        {
            if (OsmZoom >= OsmNumZoomLevels-1 || Long2Pixel > 5000000)
                return;
            //LatShift *= 2;
            ZoomValue *= 2;
        }
        public void ZoomOut()
        {
            if (OsmZoom <= 0 || Long2Pixel < 1)
                return;
            //LatShift /= 2;
            ZoomValue /= 2;
        }

        // DEBUG printout - called in DrawJpeg (make sure it is commented out in release).
        /*private void PrintMapInfo()
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
        }*/
    }
}
