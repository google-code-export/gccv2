using System;

using System.IO;
using System.Windows.Forms;
using GpsUtils;
using GpsCycleComputer;

namespace GpsSample.FileSupport
{
    class GccSupport : IFileSupport
    {                                                                   //load as T2F (WayPoints)
        public bool Load(string filename, ref Form1.WayPointInfo WayPoints,
            int vector_size, ref float[] dataLat, ref float[] dataLong, ref Int16[] dataZ, ref Int32[] dataT, ref Int32[] dataD, ref Form1.TrackStatistics ts, out int data_size)
        {
            int Counter = 0;
            double OriginShiftX = 0.0;
            double OriginShiftY = 0.0;
            bool Status = false;
            ts.Clear();
            Int16 ReferenceAlt = Int16.MaxValue;

            UtmUtil utmUtil = new UtmUtil();

            data_size = 0;
            WayPoints.WayPointCount = 0;

            Cursor.Current = Cursors.WaitCursor;

            do
            {
                try
                {
                    FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
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

                    Int16 x_int = 0; Int16 y_int = 0; Int16 z_int = 0; Int16 s_int = 0;
                    UInt16 t_16 = 0; UInt16 t_16last = 0; Int32 t_high = 0;
                    double out_lat = 0.0, out_long = 0.0;
                    double OldX = 0.0; double OldY = 0.0;
                    UInt32 recordError = 0;

                    while (true)    //break with EndOfStreamException
                    {
                        // get 5 short ints
                        try
                        {
                            x_int = rd.ReadInt16();
                            y_int = rd.ReadInt16();
                            z_int = rd.ReadInt16();
                            s_int = rd.ReadInt16();
                            t_16 = rd.ReadUInt16();
                        }
                        catch (EndOfStreamException) { break; }
                        catch (Exception e)
                        {
                            Utils.log.Error(" LoadGcc - get 5 short ints ", e);
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
                                    break;
                                case 2: // which GPS options were selected: z_int = 2
                                    break;
                                case 3: // checkpoint
                                    // read checkpoint name, if not blank
                                    string name = "";
                                    for (int i = 0; i < x_int; i++)
                                    {
                                        name += (char)(rd.ReadUInt16());
                                    }
                                    // store new checkpoint
                                    if (WayPoints.WayPointCount < (WayPoints.WayPointDataSize - 1))
                                    {
                                        WayPoints.name[WayPoints.WayPointCount] = name;
                                        WayPoints.lat[WayPoints.WayPointCount] = (float)out_lat;
                                        WayPoints.lon[WayPoints.WayPointCount] = (float)out_long;
                                        WayPoints.WayPointCount++;
                                    }
                                    break;
                                case 4: // heart rate
                                    break;
                                default:
                                    if ((1 << z_int & recordError) == 0)
                                    {
                                        if (MessageBox.Show("unknown special record " + z_int + "\ntry to load anyway?", "Load Error",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1)
                                            == DialogResult.Cancel)
                                            throw new ApplicationException();
                                        recordError |= 1U << z_int;
                                    }
                                    break;
                            }
                        }
                        else    // "normal" record
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

                            // take into account the origin shift
                            double real_x = OriginShiftX + x_int;
                            double real_y = OriginShiftY + y_int;

                            double deltax = real_x - OldX;
                            double deltay = real_y - OldY;
                            ts.Distance += Math.Sqrt(deltax * deltax + deltay * deltay);
                            OldX = real_x; OldY = real_y;

                            dataD[Counter] = (int)ts.Distance;
                            utmUtil.getLatLong(real_x, real_y, out out_lat, out out_long);
                            dataLat[Counter] = (float)out_lat;
                            dataLong[Counter] = (float)out_long;

                            dataZ[Counter] = z_int;
                            // compute elevation gain
                            if (z_int != Int16.MinValue)        //MinValue = invalid
                            {
                                if (ts.AltitudeStart == Int16.MinValue) ts.AltitudeStart = z_int;

                                if (z_int > ReferenceAlt)
                                {
                                    ts.AltitudeGain += z_int - ReferenceAlt;
                                    ReferenceAlt = z_int;
                                }
                                else if (z_int < ReferenceAlt - (short)Form1.AltThreshold)
                                {
                                    ReferenceAlt = z_int;
                                }
                                if (z_int > ts.AltitudeMax) ts.AltitudeMax = z_int;
                                if (z_int < ts.AltitudeMin) ts.AltitudeMin = z_int;
                            }

                            if (t_16 < t_16last)        // handle overflow
                                t_high += 65536;
                            t_16last = t_16;
                            dataT[Counter] = t_high + t_16;
                            Counter++;
                        }
                    }

                    rd.Close();
                    fs.Close();

                    data_size = Counter;
                    Status = true;
                }
                catch (Exception e)
                {
                    Utils.log.Error(" LoadGcc ", e);
                }
            } while (false);
            Cursor.Current = Cursors.Default;

            return Status;
        }
    }
}
