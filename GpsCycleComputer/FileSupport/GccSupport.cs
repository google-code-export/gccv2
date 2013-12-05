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
            int vector_size, ref float[] dataLat, ref float[] dataLong, ref Int16[] dataZ, ref Int32[] dataT, ref Int32[] dataD, ref Form1.TrackSummary ts, out int data_size)
        {
            int Counter = 0;
            int DecimateCount = 0, Decimation = 1;
            double OriginShiftX = 0.0;
            double OriginShiftY = 0.0;
            bool Status = false;
            ts.Clear();
            Int16 ReferenceAlt = Int16.MaxValue;

            UtmUtil utmUtil = new UtmUtil();

            data_size = 0;
            WayPoints.Count = 0;

            Cursor.Current = Cursors.WaitCursor;
            ts.filename = Path.GetFileName(filename);

            do
            {
                try
                {
                    FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                    BinaryReader rd = new BinaryReader(fs, System.Text.Encoding.Unicode);

                    // load header "GCC1" (1 is version in binary)
                    if (rd.ReadByte() != 'G') break; if (rd.ReadByte() != 'C') break;
                    if (rd.ReadByte() != 'C') break; if (rd.ReadByte() != 1) break;

                    // read time as 6 bytes: year, month...
                    int tyear = (int)rd.ReadByte(); tyear += 2000;
                    int tmonth = (int)rd.ReadByte(); int tday = (int)rd.ReadByte();
                    int thour = (int)rd.ReadByte(); int tmin = (int)rd.ReadByte();
                    int tsec = (int)rd.ReadByte();
                    ts.StartTime = new DateTime(tyear, tmonth, tday, thour, tmin, tsec);

                    // read lat/long for the starting point
                    double data_lat = rd.ReadDouble();
                    double data_long = rd.ReadDouble();
                    utmUtil.setReferencePoint(data_lat, data_long);

                    Int16 x_int = 0; Int16 y_int = 0; Int16 z_int = 0; Int16 s_int = 0;
                    UInt16 t_16 = 0; UInt16 t_16last = 0; Int32 t_high = 0;
                    double out_lat = 0.0, out_long = 0.0;
                    double OldX = 0.0; double OldY = 0.0;
                    UInt64 recordError = 0UL;

                    bool loop = true;
                    while (loop)    //break with EndOfStreamException
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
                                case 3: // waypoint
                                    // read waypoint name, if not blank
                                    string name = "";
                                    for (int i = 0; i < x_int; i++)
                                    {
                                        name += (char)(rd.ReadUInt16());
                                    }
                                    // store new waypoint
                                    if (WayPoints.Count < WayPoints.DataSize)
                                    {
                                        WayPoints.name[WayPoints.Count] = name;
                                        WayPoints.lat[WayPoints.Count] = (float)out_lat;
                                        WayPoints.lon[WayPoints.Count] = (float)out_long;
                                        WayPoints.Count++;
                                    }
                                    break;
                                case 4: // heart rate not supported in T2F
                                    break;

                                case 32: // name
                                    ts.name = rd.ReadString();
                                    break;
                                case 33: // desc
                                    ts.desc = rd.ReadString();
                                    break;
                                default:
                                    if ((1UL << z_int & recordError) == 0)
                                    {
                                        if (MessageBox.Show("unknown special record " + z_int + " at " + Counter + "\ntry to continue load anyway?", "Load Error",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1)
                                            == DialogResult.Cancel)
                                            loop = false; ;
                                        recordError |= 1UL << z_int;
                                    }
                                    if (loop && z_int >= 32)
                                        rd.ReadString();   //read unknown string in order to have correct record bounds
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
                                    dataZ[i] = dataZ[i * 2];
                                    dataT[i] = dataT[i * 2];
                                    dataD[i] = dataD[i * 2];
                                }
                                Counter /= 2;
                                Decimation *= 2;
                            }

                            // take into account the origin shift
                            double real_x = OriginShiftX + x_int;
                            double real_y = OriginShiftY + y_int;

                            double deltax = real_x - OldX;
                            double deltay = real_y - OldY;
                            ts.Distance += Math.Sqrt(deltax * deltax + deltay * deltay);
                            OldX = real_x; OldY = real_y;

                            // compute elevation gain
                            if (z_int != Int16.MinValue)        //MinValue = invalid
                            {
                                //if (ts.AltitudeStart == Int16.MinValue)
                                //    ts.AltitudeStart = z_int;
                                if (z_int > ReferenceAlt)
                                {
                                    ts.AltitudeGain += z_int - ReferenceAlt;
                                    ReferenceAlt = z_int;
                                }
                                else if (z_int < ReferenceAlt - (short)Form1.AltThreshold)
                                {
                                    ReferenceAlt = z_int;
                                }
                                if (z_int > (short)ts.AltitudeMax) ts.AltitudeMax = z_int;
                                if (z_int < (short)ts.AltitudeMin) ts.AltitudeMin = z_int;
                            }

                            if (DecimateCount == 0)    //when decimating, add only first sample, ignore rest of decimation
                            {                          //but calculate distance and elevation from all points
                                utmUtil.getLatLong(real_x, real_y, out out_lat, out out_long);
                                dataLat[Counter] = (float)out_lat;
                                dataLong[Counter] = (float)out_long;
                                dataZ[Counter] = z_int;
                                if (t_16 < t_16last)        // handle overflow
                                    t_high += 65536;
                                t_16last = t_16;
                                dataT[Counter] = t_high + t_16;
                                dataD[Counter] = (int)ts.Distance;
                                Counter++;
                            }
                            DecimateCount++;
                            if (DecimateCount >= Decimation)
                                DecimateCount = 0;
                        }
                    }

                    rd.Close();
                    fs.Close();
                    Status = true;
                }
                catch (Exception e)
                {
                    Utils.log.Error(" LoadGcc ", e);
                }
            } while (false);
            data_size = Counter;
            Cursor.Current = Cursors.Default;

            return Status;
        }
    }
}
