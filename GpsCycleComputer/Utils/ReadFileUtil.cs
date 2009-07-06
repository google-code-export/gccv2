#region Using directives

using System;
using System.Collections;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using System.Globalization;

#endregion

namespace GpsUtils
{
    // Utilities to read Gcc, Kml and Gpx file
    public class ReadFileUtil
    {
        public ReadFileUtil() {}

        // read GCC file
        public static bool LoadGcc(string filename, int vector_size,          // allocated vector size (for dataX/Y/T)
                                   ref float[] dataLat, ref float[] dataLong, ref  UInt16[] dataT, out int data_size)  // x and y realive to origin, in metres, t in sec ralative to start
        {
            int Counter = 0;
            double OriginShiftX = 0.0; 
            double OriginShiftY = 0.0;
            bool Status = false;

            UtmUtil utmUtil = new UtmUtil();

            data_size = 0;

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

                    // read lat/long for the starting point
                    double data_lat = rd.ReadDouble(); 
                    double data_long = rd.ReadDouble();
                    utmUtil.setReferencePoint(data_lat, data_long);

                    Int16 x_int = 0; Int16 y_int = 0; Int16 z_int = 0; UInt16 v_int = 0; UInt16 t_int = 0;

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
                        catch (Exception e) 
                        {
                            Utils.log.Error (" LoadGcc - get 5 short ints ", e);
                            break; 
                        }

                        // check if this is a special record
                        // battery: z_int = 1
                        if ((v_int == 0xFFFF) && (t_int == 0xFFFF) && (z_int == 1))
                        {
                        }
                        // origin shift: z_int = 0
                        else if ((v_int == 0xFFFF) && (t_int == 0xFFFF) && (z_int == 0))
                        {
                            OriginShiftX += x_int;
                            OriginShiftY += y_int;
                        }
                        // which GPS options were selected: z_int = 2
                        else if ((v_int == 0xFFFF) && (t_int == 0xFFFF) && (z_int == 2))
                        {
                        }
                        // "normal" record
                        else
                        {
                            // take into account the origin shift
                            double real_x = OriginShiftX + x_int;
                            double real_y = OriginShiftY + y_int;

                            // check if we need to decimate arrays
                            if (Counter >= vector_size)
                            {
                                for (int i = 0; i < vector_size / 2; i++)
                                {
                                    dataLat[i] = dataLat[i * 2];
                                    dataLong[i] = dataLong[i * 2];
                                    dataT[i] = dataT[i * 2];
                                }
                                Counter /= 2;
                            }

                            double out_lat;
                            double out_long;
                            utmUtil.getLatLong(real_x, real_y, out out_lat, out out_long);

                            dataLat[Counter] = (float)out_lat;
                            dataLong[Counter] = (float)out_long;
                            dataT[Counter] = t_int;
                            Counter++;

                            try 
                            { 
                                rd.PeekChar(); 
                            }
                            catch (Exception e)
                            {
                                Utils.log.Error (" LoadGcc -  PeekChar", e);
                                break; 
                            }
                        }
                    }

                    rd.Close();
                    fs.Close();

                    data_size = Counter;
                    Status = true;
                }
                catch (Exception e) 
                {
                    Utils.log.Error (" LoadGcc ", e);
                }
            } while (false);
            Cursor.Current = Cursors.Default;

            return Status;
        }

        // read one word
        public static string LoadWord(ref StreamReader sr)
        {
            string word = "";
            char[] buf = new char[1];
            try
            {
                while (sr.Peek() != -1)
                {
                    sr.Read(buf, 0, 1);
                    if((buf[0] != ' ') && (buf[0] != '\t') && (buf[0] != '\r') && (buf[0] != '\n'))
                        { word += buf[0]; }
                    else 
                    {
                        // break if any chars has been loaded, otherwise simply skip the blanks
                        if (word != "") { break; }
                    }
                }
            }
            catch (Exception e)
            {
                Utils.log.Error (" LoadWord ", e);
            }

            return word;
        }

        // read KML file
        public static bool LoadKml(string filename, int vector_size,          // allocated vector size (for dataX/Y/T)
                                   ref float[] dataLat, ref float[] dataLong, ref  UInt16[] dataT, out int data_size)  // x and y realive to origin, in metres, t in sec ralative to start
        {
            int Counter = 0;
            bool Status = false;

            data_size = 0;

            // set "." as decimal separator for reading the map info
            NumberFormatInfo number_info = new NumberFormatInfo();
            number_info.NumberDecimalSeparator = ".";

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                FileStream fs = new FileStream(filename, FileMode.Open);
                StreamReader sr = new StreamReader(fs);
                string line = "";

                bool data_load_activated = false;
                bool coordinates_found = false;

                while (sr.Peek() != -1)
                {
                    // read a single word (if we detected the required section), or a whole line
                    if (data_load_activated)
                    {
                        line = LoadWord(ref sr);

                        // skip blank lines. Look for "<coordinates>" to trigger load values.
                        if (line == "") { continue; }
                        if (line.IndexOf("<coordinates>") >= 0) { coordinates_found = true; }
                        if (line.IndexOf("</coordinates>") >= 0) { data_load_activated = false; }

                        line = line.Replace("<coordinates>", "");
                        line = line.Replace("</coordinates>", "");
                        line = line.Trim();

                        // OK, read the values (lat and long)
                        if (coordinates_found && (line != ""))
                        {
                            string[] words = line.Split(new Char[] { ',' });
                            if (words.Length >= 2)
                            {
                                double tmp_long = Convert.ToDouble(words[0].Trim(), number_info);
                                double tmp_lat = Convert.ToDouble(words[1].Trim(), number_info);

                                // check if we need to decimate arrays
                                if (Counter >= vector_size)
                                {
                                    for (int i = 0; i < vector_size / 2; i++)
                                    {
                                        dataLat[i] = dataLat[i * 2];
                                        dataLong[i] = dataLong[i * 2];
                                    }
                                    Counter /= 2;
                                }

                                dataLat[Counter] = (float)tmp_lat;
                                dataLong[Counter] = (float)tmp_long;
                                dataT[Counter] = 0; // note time is not availble in KML
                                Counter++;
                            }
                        }
                    }
                    else
                    {
                        line = sr.ReadLine().Trim();

                        if (line == "</kml>") { break; }
                        else if ((line.StartsWith("<LineString>")) || (line.StartsWith("<Point>")))
                        {
                            data_load_activated = true; coordinates_found = false; continue;
                        }
                        else if ((line.StartsWith("</LineString>")) || (line.StartsWith("</Point>")))
                        {
                            data_load_activated = false; coordinates_found = false; continue;
                        }
                    }
                }
                sr.Close();
                fs.Close();

                data_size = Counter;
                Status = true;
            }
            catch (Exception e)
            {
                Utils.log.Error (" LoadKml ", e);
            }
            Cursor.Current = Cursors.Default;
            return Status;
        }

        // read GPX file
        public static bool LoadGpx(string filename, int vector_size,          // allocated vector size (for dataX/Y/T)
                                   ref float[] dataLat, ref float[] dataLong, ref  UInt16[] dataT, out int data_size)  // x and y realive to origin, in metres, t in sec ralative to start
        {
            int Counter = 0;
            bool Status = false;
            DateTime StartTime = DateTime.Now;

            data_size = 0;

            // set "." as decimal separator for reading the map info
            NumberFormatInfo number_info = new NumberFormatInfo();
            number_info.NumberDecimalSeparator = ".";

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                FileStream fs = new FileStream(filename, FileMode.Open);
                StreamReader sr = new StreamReader(fs);
                string line = "";

                double last_lat = 0.0;
                double last_long = 0.0;

                while (sr.Peek() != -1)
                {
                    line = sr.ReadLine().Trim();

                    // skip blank lines
                    if (line == "") { continue; }

                    // line contains coordinates - extract
                    if ((line.IndexOf(" lat=\"") >= 0) && (line.IndexOf(" lon=\"") >= 0))
                    {
                        last_lat = 0.0;
                        last_long = 0.0;

                        string[] words = line.Split(new Char[] { ' ' });
                        for (int i = 0; i < words.Length; i++)
                        {
                            if (words[i].IndexOf("lon=\"") >= 0)
                            {
                                words[i] = words[i].Replace("></trkpt>", "");
                                words[i] = words[i].Replace("lon=\"", "");
                                words[i] = words[i].Replace("\"", "");
                                words[i] = words[i].Replace(">", "");
                                last_long = Convert.ToDouble(words[i].Trim(), number_info);
                            }
                            else if (words[i].IndexOf("lat=\"") >= 0)
                            {
                                words[i] = words[i].Replace("></trkpt>", "");
                                words[i] = words[i].Replace("lat=\"", "");
                                words[i] = words[i].Replace("\"", "");
                                words[i] = words[i].Replace(">", "");
                                last_lat = Convert.ToDouble(words[i].Trim(), number_info);
                            }
                        }

                        // fix if file contains coordinates only (i.e. no time info), all on one line
                        if ((line.IndexOf("></trkpt>") >= 0) && (last_lat != 0.0) && (last_long != 0.0))
                        {
                            // check if we need to decimate arrays
                            if (Counter >= vector_size)
                            {
                                for (int i = 0; i < vector_size / 2; i++)
                                {
                                    dataLat[i] = dataLat[i * 2];
                                    dataLong[i] = dataLong[i * 2];
                                    dataT[i] = dataT[i * 2];
                                }
                                Counter /= 2;
                            }

                            dataLat[Counter] = (float)last_lat;
                            dataLong[Counter] = (float)last_long;
                            dataT[Counter] = 0; // no time in this file

                            Counter++;

                            last_lat = 0.0; last_long = 0.0;
                        }
                    }
                    // fix if file contains coordinates only (i.e. no time info), but on a separate lines
                    else if ((line.IndexOf("</trkpt>") == 0) && (last_lat != 0.0) && (last_long != 0.0))
                    {
                        // check if we need to decimate arrays
                        if (Counter >= vector_size)
                        {
                            for (int i = 0; i < vector_size / 2; i++)
                            {
                                dataLat[i] = dataLat[i * 2];
                                dataLong[i] = dataLong[i * 2];
                                dataT[i] = dataT[i * 2];
                            }
                            Counter /= 2;
                        }

                        dataLat[Counter] = (float)last_lat;
                        dataLong[Counter] = (float)last_long;
                        dataT[Counter] = 0; // no time in this file

                        Counter++;

                        last_lat = 0.0; last_long = 0.0;
                    }
                    // time follows lat/long, so make sure these are loaded
                    else if ((line.IndexOf("<time>") == 0) && (line.IndexOf("</time>") >= 0) && (line.Length == 33)
                             && (last_lat != 0.0) && (last_long != 0.0))
                    {
                        line = line.Replace("<time>", "");
                        line = line.Replace("</time>", "");
                        line = line.Trim();

                        // read time
                        DateTime tm = new DateTime(Convert.ToInt16(line.Substring(0, 4)),
                                                   Convert.ToInt16(line.Substring(5, 2)),
                                                   Convert.ToInt16(line.Substring(8, 2)),
                                                   Convert.ToInt16(line.Substring(11, 2)),
                                                   Convert.ToInt16(line.Substring(14, 2)),
                                                   Convert.ToInt16(line.Substring(17, 2)));

                        if (Counter == 0) // the first point loaded
                        {
                            StartTime = tm;
                        }

                        // check if we need to decimate arrays
                        if (Counter >= vector_size)
                        {
                            for (int i = 0; i < vector_size / 2; i++)
                            {
                                dataLat[i] = dataLat[i * 2];
                                dataLong[i] = dataLong[i * 2];
                                dataT[i] = dataT[i * 2];
                            }
                            Counter /= 2;
                        }

                        dataLat[Counter] = (float)last_lat;
                        dataLong[Counter] = (float)last_long;

                        TimeSpan run_time = tm - StartTime;
                        double double_total_sec = run_time.TotalSeconds; if (double_total_sec < 0.0) { double_total_sec = 0.0; }
                        dataT[Counter] = (UInt16)double_total_sec; 

                        Counter++;

                        last_lat = 0.0;
                        last_long = 0.0;
                    }
                }
                sr.Close();
                fs.Close();

                data_size = Counter;
                Status = true;
            }
            catch (Exception e)
            {
                Utils.log.Error (" LoadGpx ", e);
            }
            Cursor.Current = Cursors.Default;
            return Status;
        }
    }
}
