using System;

using System.Globalization;
using System.IO;
using System.Windows.Forms;
using GpsCycleComputer;
using GpsUtils;

namespace GpsSample.FileSupport
{
    class GpxSupport : IFileSupport
    {
        CultureInfo IC = CultureInfo.InvariantCulture;

        public bool Load(string filename, int vector_size, ref float[] dataLat, ref float[] dataLong, ref int[] dataT, out int data_size)
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
                        dataT[Counter] = (Int32)double_total_sec;

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
                Utils.log.Error(" LoadGpx ", e);
            }
            Cursor.Current = Cursors.Default;
            return Status;
        }

        public void Write(String gpx_file, int CheckPointCount,
            Form1.CheckPointInfo[] CheckPoints,
            CheckBox checkGpxRte, CheckBox checkGpxSpeedMs,
            float[] PlotLat, float[] PlotLong, int PlotCount,
            short[] PlotS, int[] PlotT, short[] PlotZ, DateTime StartTime,
            NumericUpDown numericGpxTimeShift,
            string dist_unit, string speed_unit, string alt_unit, string exstop_info,
                string dist, string speed_cur, string speed_avg, string speed_max, string run_time_label, string last_sample_time, string altitude, string battery
            )
        {
            FileStream fs = null;
            StreamWriter wr = null;
            try
            {
                fs = new FileStream(gpx_file, FileMode.Create);
                wr = new StreamWriter(fs);

                // write GPX header
                wr.WriteLine("<?xml version=\"1.0\"?>");
                wr.WriteLine("<gpx");
                wr.WriteLine("version=\"1.0\"");
                wr.WriteLine(" creator=\"GPSCycleComputer\"");
                wr.WriteLine(" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
                wr.WriteLine(" xmlns=\"http://www.topografix.com/GPX/1/0\"");
                wr.WriteLine(" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/0 http://www.topografix.com/GPX/1/0/gpx.xsd\">");
                wr.WriteLine("");

                if (CheckPointCount != 0)
                {
                    for (int chk = 0; chk < CheckPointCount; chk++)
                    {
                        // need to replave chars not supported by XML
                        string chk_name = CheckPoints[chk].name.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

                        // GPS Track Analyser expects a newline between wpt and name
                        wr.WriteLine("<wpt lat=\"" + CheckPoints[chk].lat.ToString("0.##########", IC)
                                    + "\" lon=\"" + CheckPoints[chk].lon.ToString("0.##########", IC)
                                    + "\" >");
                        wr.WriteLine("<name>" + chk_name + "</name>");
                        wr.WriteLine("</wpt>");
                    }
                }

                wr.WriteLine(checkGpxRte.Checked ? "<rte>" : "<trk>");
                wr.WriteLine("<name>" + StartTime + "</name>");
                // GPS Track Analyser expects a newline between desc and Data
                wr.WriteLine("<desc>");
                wr.WriteLine("<![CDATA[" + dist + " " + dist_unit + " " + run_time_label + " " + exstop_info
                               + " " + speed_cur + " " + speed_avg + " " + speed_max + " " + speed_unit
                               + " battery " + battery
                               + "]]>");
                wr.WriteLine("</desc>");

                if (checkGpxRte.Checked == false) { wr.WriteLine("<trkseg>"); }

                // here write coordinates
                for (int i = 0; i < PlotCount; i++)
                {
                    if (checkGpxRte.Checked)
                    {
                        wr.WriteLine("<rtept lat=\"" + PlotLat[i].ToString("0.##########", IC) +
                                     "\" lon=\"" + PlotLong[i].ToString("0.##########", IC) + "\">");
                    }
                    else
                    {
                        wr.WriteLine("<trkpt lat=\"" + PlotLat[i].ToString("0.##########", IC) +
                                     "\" lon=\"" + PlotLong[i].ToString("0.##########", IC) + "\">");
                    }
                    if (PlotZ[i] != Int16.MinValue)     //ignore invalid value
                    {
                        wr.WriteLine("<ele>" + PlotZ[i] + "</ele>");
                    }
                    TimeSpan run_time = new TimeSpan(Decimal.ToInt32(numericGpxTimeShift.Value), 0, PlotT[i]);
                    string run_time_str = (StartTime + run_time).ToString("u");
                    run_time_str = run_time_str.Replace(" ", "T");
                    wr.WriteLine("<time>" + run_time_str + "</time>");

                    if (PlotS[i] != Int16.MinValue)     //ignore invalid value
                    {
                        if (checkGpxSpeedMs.Checked) // speed in m/s instead of km/h
                        {
                            wr.WriteLine("<speed>" + (PlotS[i] * (0.1 / 3.6)).ToString("0.##########", IC) + "</speed>");
                        }
                        else
                        {
                            wr.WriteLine("<speed>" + (PlotS[i] * 0.1).ToString("0.##########", IC) + "</speed>");
                        }
                    }
                    wr.WriteLine(checkGpxRte.Checked ? "</rtept>" : "</trkpt>");
                }
                // write end of the GPX file
                if (checkGpxRte.Checked == false) { wr.WriteLine("</trkseg>"); }
                if (checkGpxRte.Checked) { wr.WriteLine("</rte>"); } else { wr.WriteLine("</trk>"); }

                wr.WriteLine("</gpx>");
            }
            catch (Exception ee)
            {
                Utils.log.Error(" buttonSaveGPX_Click ", ee);
            }
            finally
            {
                if (wr != null) wr.Close();
                if (fs != null) fs.Close();
            }
        }
    }
}
