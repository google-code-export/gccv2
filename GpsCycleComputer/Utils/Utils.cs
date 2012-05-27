#region Using directives

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Windows.Forms;
using System.Text;
using System.IO;
using System.Drawing;
using System.Net;
using System.Xml;

using Log;
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
        public UInt32 BatteryCurrent;
        public UInt32 BatteryAverageCurrent;
        public UInt32 BatteryAverageInterval;
        UInt32 BatterymAHourConsumed;
        UInt32 BatteryTemperature;
        UInt32 BackupBatteryVoltage;
        Byte BatteryChemistry;
    }
    #endregion

    public class Utils
    {
        public static Logger log = new Logger (Path.GetDirectoryName (System.Reflection.Assembly.GetExecutingAssembly ().GetName ().CodeBase)); // current dir

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
            return SetSystemPowerState(null, 0x00100000, 0);    //screenoff
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
            { return -300; } // Failure

            // native call succeeded, marshal native data to our managed data
            pwrStat = (_SYSTEM_POWER_STATUS_EX2)Marshal.PtrToStructure(ptr, typeof(_SYSTEM_POWER_STATUS_EX2));

            if (pwrStat.BatteryLifePercent == 0xFF)
            { return -255; } // failure - BATTERY_PERCENTAGE_UNKNOWN

            if (pwrStat.ACLineStatus == 0x01)
            { return -(Int32)pwrStat.BatteryLifePercent; } // OK, but running from AC

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
         * return 0 - if all OK, 1 - file not exists, 2 - not JPEG, 3 - width/height not found, 4 - other errors (EOF)
         */
        public static int GetJpegSize(string fname, out int w, out int h)
        {
            w = 0; 
            h = 0;
            if (File.Exists(fname) == false) { return 1; }

            FileStream fs = null;
            BinaryReader wr = null;
            int return_status = 3;
            try
            {
                fs = new FileStream(fname, FileMode.Open, FileAccess.Read);
                wr = new BinaryReader(fs, Encoding.ASCII);

                // check that this is correct file format
                Byte b1 = 0, b2 = 0;
                b1 = wr.ReadByte();
                b2 = wr.ReadByte();
                if ((b1 != 0xFF) || (b2 != 0xD8))    // 0xD8 SOI: Start Of Image (beginning of datastream)
                {
                    return_status = 2;
                    throw new Exception("not jpeg");
                }

                while (true)
                {
                    // scroll to the next marker
                    Byte next_marker = 0;
                    // Find 0xFF byte; count and skip any non-FFs
                    next_marker = wr.ReadByte();
                    while (next_marker != 0xFF)
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
                    while (next_marker == 0xFF);


                    if ((next_marker == 0xDA)    // M_SOS - Start Of Scan (begins compressed data)
                        || (next_marker == 0xD9))   // M_EOI - End Of Image (end of datastream)
                    { break; }

                    // check which marker we have
                    if ((next_marker == 0xC0)    // different data format
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

                        UInt32 hgt = (((UInt32)b1) << 8) + ((UInt32)b2);

                        b1 = 0; b2 = 0;
                        b1 = wr.ReadByte();
                        b2 = wr.ReadByte();

                        UInt32 wid = (((UInt32)b1) << 8) + ((UInt32)b2);

                        w = (int)wid;
                        h = (int)hgt;
                        return_status = 0;

                        // image dimension found, break the loop
                        break;
                    }

                    // skip variable length marker which we are not interested in   -----------
                    b1 = 0; b2 = 0;
                    b1 = wr.ReadByte();
                    b2 = wr.ReadByte();
                    UInt32 length = (((UInt32)b1) << 8) + ((UInt32)b2);

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
            }
            catch (EndOfStreamException e)
            {
                return_status = 4;
                log.Error(" GetJpegSize ", e);
            }
            catch (Exception e)
            {
                log.Error(" GetJpegSize ", e);
            }
            finally
            {
                if(wr != null) wr.Close();
                if (fs != null) fs.Close();
            }
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
        public static void DrawCompass(Graphics g, Color col, int x0, int y0, int size, int heading_int, bool compass_north)
        {
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
            if (compass_north && heading_int != 720)
                heading_int = -heading_int;

            // draw heading - 4 points arrow
            // needle
            Point[] pa = new Point[3];
            pa[0].X = (int)(x0 + (rad - 1 - tick / 2) * Math.Cos(Math.PI / 2.0 - (heading_int * Math.PI / 180.0)));
            pa[0].Y = (int)(y0 - (rad - 1 - tick / 2) * Math.Sin(Math.PI / 2.0 - (heading_int * Math.PI / 180.0)));

            // point just opposite (signs inverted)
            pa[1].X = (int)(x0 - (rad - 1 - tick / 2) * 2 / 3 * Math.Cos(Math.PI / 2.0 - (heading_int * Math.PI / 180.0)));
            pa[1].Y = (int)(y0 + (rad - 1 - tick / 2) * 2 / 3 * Math.Sin(Math.PI / 2.0 - (heading_int * Math.PI / 180.0)));

            // point A - at +150 deg from current
            pa[2].X = (int)(x0 + (rad - 1 - tick / 2) * Math.Cos(Math.PI / 2.0 - ((heading_int + 150) * Math.PI / 180.0)));
            pa[2].Y = (int)(y0 - (rad - 1 - tick / 2) * Math.Sin(Math.PI / 2.0 - ((heading_int + 150) * Math.PI / 180.0)));

            br.Color = heading_int == 720 ? Color.DimGray : Color.FromArgb(30, 86, 169);
            g.FillPolygon(br, pa);

            br.Color = heading_int == 720 ? Color.Gray : Color.FromArgb(91, 133, 209);
            // point B - at +210 deg from current
            pa[2].X = (int)(x0 + (rad - 1 - tick / 2) * Math.Cos(Math.PI / 2.0 - ((heading_int + 210) * Math.PI / 180.0)));
            pa[2].Y = (int)(y0 - (rad - 1 - tick / 2) * Math.Sin(Math.PI / 2.0 - ((heading_int + 210) * Math.PI / 180.0)));
            g.FillPolygon(br, pa);

            if (compass_north)
            {
                Pen p = new Pen(col, size / 32);
                int d = size / 10;
                Point[] points = new Point[] { new Point(x0 - d, y0 + d), new Point(x0 - d, y0 - d), new Point(x0 + d, y0 + d), new Point(x0 + d, y0 - d) };
                g.DrawLines(p, points);
            }
        }


        public static void update(string Revision)
        {
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                WebRequest request = WebRequest.Create("http://gccv2.googlecode.com/files/GpsCycleComputer.xml");
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response != null && "OK" == response.StatusDescription)
                {
                    Stream DataStream = response.GetResponseStream();
                    StreamReader Sreader = new StreamReader(DataStream);
                    string responseFromServer = Sreader.ReadToEnd();
                    if (responseFromServer.IndexOf("403", StringComparison.CurrentCulture) < 3)
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(responseFromServer);
                        XmlNode element = doc.SelectSingleNode("/application");
                        XmlNodeList cnode = element.ChildNodes;
                        int up2date = 0;
                        string link = "", webVersion;
                        foreach (XmlNode child in cnode)
                        {
                            switch (child.Name)
                            {
                                case "version":
                                    webVersion = child.InnerText.ToString();
                                    if (Revision2Number(webVersion) > Revision2Number(Revision))
                                        up2date = 1;
                                    else
                                        up2date = -1;
                                    break;
                                case "caburl":
                                    link = child.InnerText.ToString();
                                    break;
                            }
                        }
                        if (up2date == 1)
                            MessageBox.Show("The current version is up to date", "Update");
                        else if (up2date == -1)
                            System.Diagnostics.Process.Start(link, null);
                        else
                            MessageBox.Show("Error: No version found", "Update");
                    }
                    Sreader.Close();
                    DataStream.Close();
                }
                response.Close();
            }
            catch (Exception e)
            {
                Utils.log.Error(" Update ", e);
                MessageBox.Show(e.Message, "Update");
            }
            Cursor.Current = Cursors.Default;
        }
        private static int Revision2Number(string revision)
        {
            int number = 0;
            string str;
            int idx;
            str = RemoveNonDigit(revision);
            number = 1000000 * Convert.ToInt32(str);
            if ((idx = revision.IndexOf('.')) > -1)
            {
                str = revision.Remove(0, idx + 1);
                str = RemoveNonDigit(str);
                number += 10000 * Convert.ToInt32(str);
            }
            if ((idx = revision.ToUpper().IndexOf("SP")) > -1)
            {
                str = revision.Remove(0, idx + 2);
                str = RemoveNonDigit(str);
                number += 100 * Convert.ToInt32(str);
            }
            if ((idx = revision.ToUpper().IndexOf("BETA")) > -1)
            {
                str = revision.Remove(0, idx + 4);
                str = RemoveNonDigit(str);
                number += Convert.ToInt32(str);
            }
            else
                number += 99;       //non-beta is highest 'beta'
            return number;
        }
        private static string RemoveNonDigit(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (Char.IsDigit(str[i]))
                    continue;
                else
                    return str.Remove(i, str.Length - i);
            }
            return str;
        }


        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            return InputBoxRec(title, promptText, ref value, null);
        }

        public static DialogResult InputBoxRec(string title, string promptText, ref string value, string RecFile)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            int w = form.Width, h = form.Height;
            label.Bounds = new Rectangle(w *10/480, h *40/588, w *460/480, h *36/588);      //5, 20, 230, 18);
            textBox.Bounds = new Rectangle(w *10/480, h *80/588, w *460/480, h *36/588);    //5, 40, 230, 18);
            buttonOk.Bounds = new Rectangle(w *165/480, h *140/588, w *150/480, h *80/588);   //80, 70, 75, 24);
            buttonCancel.Bounds = new Rectangle(w *325/480, h *140/588, w *150/480, h *80/588);    //160, 70, 75, 24);

            //label.AutoSize = true;
            //textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            //buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            //buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            //form.ClientSize = new Size(240, 100);
            form.Controls.Add(label);
            form.Controls.Add(textBox);
            form.Controls.Add(buttonOk);
            form.Controls.Add(buttonCancel);

            if (RecFile != null)
            {
                Button buttonRec = new Button();
                buttonRec.Text = "Audio";
                buttonRec.Bounds = new Rectangle(w * 5/480, h * 140/588, w * 150/480, h * 80/588);
                form.Controls.Add(buttonRec);
                buttonRec.Click += buttonRec_Click; //buttonRec_Click;
                if(vr == null) vr = new VoiceRecorder();
                vr.Position(w * 5/480, h * 280/588);
                vr.RecFile = RecFile;
            }
            
            form.BackColor = GpsCycleComputer.Form1.bkColor;
            form.ForeColor = GpsCycleComputer.Form1.foColor;
            label.BackColor = GpsCycleComputer.Form1.bkColor;
            label.ForeColor = GpsCycleComputer.Form1.foColor;
            textBox.BackColor = GpsCycleComputer.Form1.bkColor;
            textBox.ForeColor = GpsCycleComputer.Form1.foColor;
            //textBox.BorderStyle = BorderStyle.FixedSingle;
            
            //form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            //form.FormBorderStyle = FormBorderStyle.FixedDialog;
            //form.StartPosition = FormStartPosition.CenterScreen;
            //form.MinimizeBox = false;
            //form.MaximizeBox = false;
            //form.AcceptButton = buttonOk;
            //form.CancelButton = buttonCancel;
            Microsoft.WindowsCE.Forms.InputPanel inputPanel = new Microsoft.WindowsCE.Forms.InputPanel();
            inputPanel.Enabled = true;
            DialogResult dialogResult = form.ShowDialog();
            inputPanel.Enabled = false;
            value = textBox.Text;
            return dialogResult;
        }

        static void buttonRec_Click(object sender, EventArgs e)
        {
            vr.ShowAndRecord();
        }

        static VoiceRecorder vr = null;

    }

    public class Win32
    {
        public const int LMEM_ZEROINIT = 0x40;
        [System.Runtime.InteropServices.DllImport("coredll.dll", EntryPoint = "#33", SetLastError = true)]
        public static extern IntPtr LocalAlloc(int flags, int byteCount);

        [System.Runtime.InteropServices.DllImport("coredll.dll", EntryPoint = "#36", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);
    }


    public class VoiceRecorder
    {
        #region API prototypes
        [DllImport("voicectl.dll")]
        private static extern IntPtr VoiceRecorder_Create(ref CM_VOICE_RECORDER voicerec);
        [DllImport("coredll.dll", EntryPoint = "SendMessage")]
        static extern IntPtr SendMessage(IntPtr hWnd, VR_MSG Msg, IntPtr wParam, IntPtr lParam);
        //end api delcare
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        protected struct CM_VOICE_RECORDER
        {
            public int cb;
            public wndStyle dwStyle;
            public int xPos, yPos;
            public IntPtr hwndParent;
            public int id;
            public IntPtr lpszRecordFileName;
        }
        public enum wndStyle : uint
        {
            VRS_NO_OKCANCEL = 0x0001, // No OK/CANCLE displayed
            VRS_NO_NOTIFY = 0x0002, // No parent Notifcation
            VRS_MODAL = 0x0004, // Control is Modal    
            VRS_NO_OK = 0x0008, // No OK displayed
            VRS_NO_RECORD = 0x0010, // No RECORD button displayed
            VRS_PLAY_MODE = 0x0020, // Immediately play supplied file when launched
            VRS_NO_MOVE = 0x0040, // Grip is removed and cannot be moved around by the user
            VRS_RECORD_MODE = 0x0080, // Immediately record when launched (don't works on WM)
            VRS_STOP_DISMISS = 0x0100 // Dismiss control when stopped (don't works on WM)
        }
        public enum VR_MSG : uint
        {
            VRM_RECORD = 0x1900,
            VRM_PLAY,
            VRM_STOP,
            VRM_CANCEL,
            VRM_OK,
        }

        private CM_VOICE_RECORDER voicerec;
        private IntPtr handle;
        public string RecFile
        {
            get { return Marshal.PtrToStringBSTR(voicerec.lpszRecordFileName); }
            set { voicerec.lpszRecordFileName = Marshal.StringToBSTR(value); }
        }

        public VoiceRecorder()
        {
            voicerec = new CM_VOICE_RECORDER();
            handle = new IntPtr();
            voicerec.cb = (int)Marshal.SizeOf(voicerec);  //26
            voicerec.dwStyle = wndStyle.VRS_NO_OK | wndStyle.VRS_NO_MOVE;
            voicerec.xPos = -1;
            voicerec.yPos = -1;
            voicerec.lpszRecordFileName = Marshal.StringToBSTR("\\myFile.wav");
        }
        public void ShowAndRecord()
        {
            handle = VoiceRecorder_Create(ref voicerec);            //show the voice recorder
            SendMessage(handle, VR_MSG.VRM_RECORD, IntPtr.Zero, IntPtr.Zero);
        }
        public void Position(int x, int y)
        {
            voicerec.xPos = x;
            voicerec.yPos = y;
        }
    }

}
