#region Using directives

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Windows.Forms;
using System.Text;
using System.IO;
using System.Drawing;

#endregion

namespace GpsUtils
{

    #region Internal Native Structures
    [StructLayout(LayoutKind.Sequential)]
    internal struct _SYSTEM_POWER_STATUS_EX2
    {
        public Byte ACLineStatus;
        public Byte BatteryFlag;
        public Byte BatteryLifePercent;
        Byte Reserved1;
        UInt32 BatteryLifeTime;
        UInt32 BatteryFullLifeTime;
        Byte Reserved2;
        Byte BackupBatteryFlag;
        Byte BackupBatteryLifePercent;
        Byte Reserved3;
        UInt32 BackupBatteryLifeTime;
        UInt32 BackupBatteryFullLifeTime;
        UInt32 BatteryVoltage;
        UInt32 BatteryCurrent;
        UInt32 BatteryAverageCurrent;
        UInt32 BatteryAverageInterval;
        UInt32 BatterymAHourConsumed;
        UInt32 BatteryTemperature;
        UInt32 BackupBatteryVoltage;
        Byte BatteryChemistry;
    }
    #endregion

    public class Utils
    {
        public Utils() {}

        public static IntPtr LocalAlloc(int byteCount)
        {
            IntPtr ptr = Win32.LocalAlloc(Win32.LMEM_ZEROINIT, byteCount);
            if (ptr == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }

            return ptr;
        }

        public static void LocalFree(IntPtr hMem)
        {
            IntPtr ptr = Win32.LocalFree(hMem);
            if (ptr != IntPtr.Zero)
            {
                throw new ArgumentException();
            }
        }

        public static UInt32 SwitchBacklight()
        {
            return SetSystemPowerState(null, 0x00100000, 0);
        }
        public static Int32 GetBatteryStatus()
        {
            _SYSTEM_POWER_STATUS_EX2 pwrStat;

            // allocate the necessary memory on the native side.  We have a class (_SYSTEM_POWER_STATUS_EX2) that
            // has the same memory layout as its native counterpart
            IntPtr ptr = Utils.LocalAlloc(Marshal.SizeOf(typeof(_SYSTEM_POWER_STATUS_EX2)));

            // fill in the required fields
            pwrStat = new _SYSTEM_POWER_STATUS_EX2();
            UInt32 pwrStatSize = (UInt32)Marshal.SizeOf(typeof(_SYSTEM_POWER_STATUS_EX2));


            UInt32 result = GetSystemPowerStatusEx2(ptr, pwrStatSize, 0);

            if (result == 0)
            { return -3; } // Failure

            // native call succeeded, marshal native data to our managed data
            pwrStat = (_SYSTEM_POWER_STATUS_EX2)Marshal.PtrToStructure(ptr, typeof(_SYSTEM_POWER_STATUS_EX2));

            if (pwrStat.BatteryLifePercent == 0xFF)
            { return -2; } // failure - BATTERY_PERCENTAGE_UNKNOWN

            if (pwrStat.ACLineStatus == 0x01)
            { return -1; } // OK, but running from AC

            return (Int32)pwrStat.BatteryLifePercent;
        }

        #region PInvokes to coredll.dll

        [DllImport("coredll.dll")]
        static extern UInt32 SetSystemPowerState(StringBuilder psState, UInt32 StateFlags, UInt32 Options);

        [DllImport("coredll.dll")]
        static extern UInt32 GetSystemPowerStatusEx2(IntPtr ptrSystemPowerStatusEx2, UInt32 dwLen, Int32 fUpdate);
        /*
          look for "Platform Invoke Data Types" for type conversion:
          ms-help://MS.VSCC.v80/MS.MSDN.v80/MS.VisualStudio.v80.en/dv_fxinterop/html/16014d9f-d6bd-481e-83f0-df11377c550f.htm
        */
        #endregion


        /* Functions to get JPEG image size
         * return 0 - if all OK, 1 - file not exists, 2 - not JPEG, 3 - width/height not found, 4 - other errors
         */
        public static int GetJpegSize(string fname, out int w, out int h)
        {
            w = 0; 
            h = 0;
            if (File.Exists(fname) == false) { return 1; }

            int return_status = 3;
            try
            {
                FileStream fs = new FileStream(fname, FileMode.Open);
                BinaryReader wr = new BinaryReader(fs,Encoding.ASCII);

                // check that this is correct file format
                Byte b1 = 0, b2 = 0;
                if (wr.PeekChar() != -1) { b1 = wr.ReadByte(); }
                if (wr.PeekChar() != -1) { b2 = wr.ReadByte(); }
                if ((b1 != 0xFF) || (b2 != 0xD8)) { return 2; } // 0xD8 SOI: Start Of Image (beginning of datastream)

                while (wr.PeekChar() != -1)
                {
                    // scroll to the next marker
                    Byte next_marker = 0;
                    // Find 0xFF byte; count and skip any non-FFs
                    next_marker = wr.ReadByte();
                    while ((next_marker != 0xFF) && (wr.PeekChar() != -1))
                    {
                        next_marker = 0;
                        next_marker = wr.ReadByte();
                    }
                    // Get marker code byte, swallowing any duplicate FF bytes.  Extra FFs
                    // are legal as pad bytes, so don't count them in discarded_bytes.
                    do
                    {
                        next_marker = 0;
                        next_marker = wr.ReadByte();
                    }
                    while ((next_marker == 0xFF) && (wr.PeekChar() != -1));


                    if(    (next_marker == 0xDA)    // M_SOS - Start Of Scan (begins compressed data)
                        || (next_marker == 0xD9))   // M_EOI - End Of Image (end of datastream)
                        { break; }

                    // check which marker we have
                    if(    (next_marker == 0xC0)    // different data format
                        || (next_marker == 0xC1)
                        || (next_marker == 0xC2)
                        || (next_marker == 0xC3)
                        || (next_marker == 0xC5)
                        || (next_marker == 0xC6)
                        || (next_marker == 0xC7)
                        || (next_marker == 0xC9)
                        || (next_marker == 0xCA)
                        || (next_marker == 0xCB)
                        || (next_marker == 0xCD)
                        || (next_marker == 0xCE)
                        || (next_marker == 0xCF))
                    {
                        // length 2 bytes
                        wr.ReadByte();
                        wr.ReadByte();

                        // data_precision, 1 byte
                        wr.ReadByte();

                        b1 = 0; b2 = 0;
                        b1 = wr.ReadByte();
                        b2 = wr.ReadByte();

                        UInt32 hgt  = (((UInt32) b1) << 8) + ((UInt32) b2);

                        b1 = 0; b2 = 0;
                        b1 = wr.ReadByte();
                        b2 = wr.ReadByte();

                        UInt32 wid  = (((UInt32) b1) << 8) + ((UInt32) b2);

                        w = (int) wid;
                        h = (int) hgt;
                        return_status = 0;

                        // image dimention found, break the loop
                        break;
                    }
                    
                    // skip variable length marker which we are not interested in   -----------
                    b1 = 0; b2 = 0;
                    b1 = wr.ReadByte();
                    b2 = wr.ReadByte();
                    UInt32 length  = (((UInt32) b1) << 8) + ((UInt32) b2);

                    // Length includes itself, so must be at least 2
                    if (length < 2) { break; }
                    length -= 2;
                    // Skip over the remaining bytes
                    while (length > 0) 
                    {
                        wr.ReadByte();
                        length--;
                    }
                }

                wr.Close();
                fs.Close();
            }
            catch (Exception /*e*/) { return 4; }

            return return_status;
        }

        // Util to draw clock on a given graphics
        public static void DrawClock(Graphics g, Color col, int x0, int y0, int size, float font_size)
        {
            DateTime tm = DateTime.Now;

            int rad = (size-2) / 2;
            int tick = rad / 10;
            if (tick < 2) { tick = 2; }

            // draw ticks
            Pen p = new Pen(col, 1);
            for (int i = 0; i < 12; i++)
            {
                int x1 = (int)(x0 + rad * Math.Cos(i * Math.PI / 6.0));
                int y1 = (int)(y0 - rad * Math.Sin(i * Math.PI / 6.0));
                int x2 = (int)(x0 + (rad-tick) * Math.Cos(i * Math.PI / 6.0));
                int y2 = (int)(y0 - (rad - tick) * Math.Sin(i * Math.PI / 6.0));
                g.DrawLine(p, x1, y1, x2, y2);
            }
            // draw sec
            int sec = tm.Second;
            int x3 = (int)(x0 + (rad-1 - tick / 2) * Math.Cos(Math.PI / 2.0 - (sec * Math.PI / 30.0)));
            int y3 = (int)(y0 - (rad-1 - tick / 2) * Math.Sin(Math.PI / 2.0 - (sec * Math.PI / 30.0)));

            // draw min/sec
            string str = tm.Hour.ToString("00") + ":" + tm.Minute.ToString("00");
            Font f = new Font("Arial", font_size, FontStyle.Regular);
            SizeF sz = g.MeasureString(str, f);
            SolidBrush br = new SolidBrush(col);
            g.DrawString(str, f, br, x0 - (int)sz.Width / 2, y0 - (int)sz.Height / 2);

            br.Color = Color.LightGreen;
            g.FillEllipse(br, x3 - tick / 2, y3 - tick / 2, tick, tick);
        }

        // Util to draw compass on a given graphics. Heading is in degrees, 0 is "north" == up
        public static void DrawCompass(Graphics g, Color col, int x0, int y0, int size, int heading_int)
        {
            // this control was made from clock, so convert 0..360 deg heading into 0..60 seconds range
            double h_as_sec = heading_int / 6.0;

            int rad = (size - 2) / 2;
            int tick = rad / 10;
            if (tick < 2) { tick = 2; }

            // draw ticks
            SolidBrush br = new SolidBrush(col);
            for (int i = 0; i < 8; i++)
            {
                int xt = (int)(x0 + (rad - 1 - tick / 2) * Math.Cos(i * Math.PI / 4.0));
                int yt = (int)(y0 - (rad - 1 - tick / 2) * Math.Sin(i * Math.PI / 4.0));
                g.FillEllipse(br, xt - tick / 2, yt - tick / 2, tick, tick);
            }

            // draw heading - 4 points arrow
            // needle
            Point[] pa = new Point[3];
            pa[0].X = (int)(x0 + (rad - 1 - tick / 2) * Math.Cos(Math.PI / 2.0 - (h_as_sec * Math.PI / 30.0)));
            pa[0].Y = (int)(y0 - (rad - 1 - tick / 2) * Math.Sin(Math.PI / 2.0 - (h_as_sec * Math.PI / 30.0)));

            // point just opposite (signs inverted)
            pa[1].X = (int)(x0 - (rad - 1 - tick / 2) * 2 / 3 * Math.Cos(Math.PI / 2.0 - (h_as_sec * Math.PI / 30.0)));
            pa[1].Y = (int)(y0 + (rad - 1 - tick / 2) * 2 / 3 * Math.Sin(Math.PI / 2.0 - (h_as_sec * Math.PI / 30.0)));

            // point A - at +25 "seconds" from current
            pa[2].X = (int)(x0 + (rad - 1 - tick / 2) * Math.Cos(Math.PI / 2.0 - ((h_as_sec + 25) * Math.PI / 30.0)));
            pa[2].Y = (int)(y0 - (rad - 1 - tick / 2) * Math.Sin(Math.PI / 2.0 - ((h_as_sec + 25) * Math.PI / 30.0)));

            br.Color = Color.FromArgb(30, 86, 169);
            g.FillPolygon(br, pa);

            br.Color = Color.FromArgb(91, 133, 209);
            // point B - at +35 "seconds" from current
            pa[2].X = (int)(x0 + (rad - 1 - tick / 2) * Math.Cos(Math.PI / 2.0 - ((h_as_sec + 35) * Math.PI / 30.0)));
            pa[2].Y = (int)(y0 - (rad - 1 - tick / 2) * Math.Sin(Math.PI / 2.0 - ((h_as_sec + 35) * Math.PI / 30.0)));
            g.FillPolygon(br, pa);
        }
    }

    public class Win32
    {
        public const int LMEM_ZEROINIT = 0x40;
        [System.Runtime.InteropServices.DllImport("coredll.dll", EntryPoint = "#33", SetLastError = true)]
        public static extern IntPtr LocalAlloc(int flags, int byteCount);

        [System.Runtime.InteropServices.DllImport("coredll.dll", EntryPoint = "#36", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);
    }

}
