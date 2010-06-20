using System;

using System.Globalization;
using System.IO;
using System.Windows.Forms;
using GpsUtils;

namespace GpsSample.FileSupport
{
    class KmlSupport : IFileSupport
    {
        public bool Load(string filename, int vector_size, ref float[] dataLat, ref float[] dataLong, ref int[] dataT, out int data_size)
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
                Utils.log.Error(" LoadKml ", e);
            }
            Cursor.Current = Cursors.Default;
            return Status;
        }

        // read one word
        public string LoadWord(ref StreamReader sr)
        {
            string word = "";
            char[] buf = new char[1];
            try
            {
                while (sr.Peek() != -1)
                {
                    sr.Read(buf, 0, 1);
                    if ((buf[0] != ' ') && (buf[0] != '\t') && (buf[0] != '\r') && (buf[0] != '\n'))
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
                Utils.log.Error(" LoadWord ", e);
            }

            return word;
        }
    }
}
