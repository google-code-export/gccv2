using System;

using System.Globalization;
using System.IO;
using System.Windows.Forms;
using GpsUtils;

namespace GpsSample.FileSupport
{
    class GpxSupport : IFileSupport
    {
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
    }
}
