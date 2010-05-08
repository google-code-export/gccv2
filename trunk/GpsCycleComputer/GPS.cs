using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Text;
using System.IO;
using System.Reflection;

namespace GpsUtils
{
    public class Gps
    {

        // -----------------------------------------------------------
        // options: use GccGPS.dll or Windows gpsapi.dll
        private bool useGccDll = false;
        private int comPort = 4;
        private int baudRate = 4800;

        // -----------------------------------------------------------
        // handle to the gps device
        IntPtr gpsHandle = IntPtr.Zero;

        // start GPS service driver (it shall be ON anyway) - if dll exist
        public void startGpsService()
        {
            string gps_dll_name = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName) + "\\GccGPS.dll";
            if (File.Exists(gps_dll_name))
                { GccGpsStart(); }
        }

        // set the mode: use GccGPS.dll or Windows "Parsed" driver from gpsapi.dll
        public void setOptions(bool _useGccDll, int _comPort, int _baudRate)
        {
            //Close();

            useGccDll = _useGccDll;
            comPort = _comPort;
            baudRate = _baudRate;
        }

        /// True: The GPS device has been opened. False: It has not been opened
        public bool Opened
        {
            get
            {
                if(useGccDll) {  return (GccIsGpsOpened() == 1); }
                else          {  return (gpsHandle != IntPtr.Zero); }
            }
        }

        public bool Suspended = false;

        public bool OpenedOrSuspended
        {
            get {  return Opened || Suspended; }
        }

        public Gps() {}

        ~Gps()
        {
            Close();
        }

        public int Open()
        {
            Suspended = false;
            if (!Opened)
            {
                if(useGccDll) { return GccOpenGps(comPort, baudRate); }
                else          { gpsHandle = GPSOpenDevice(IntPtr.Zero, IntPtr.Zero, null, 0); }
            }
            return 1;
        }

        public void Close()
        {
            if (Opened)
            {
                if(useGccDll) { GccCloseGps(); }
                else          { GPSCloseDevice(gpsHandle); gpsHandle = IntPtr.Zero; }
            }
            Suspended = false;
        }

        public void Suspend()
        {
            Close();
            Suspended = true;
        }


        public GpsPosition GetPosition()
        {
            GpsPosition gpsPosition = null;

            if (Opened)
            {
                if(useGccDll)
                {
                    int hour; int min; int sec;
                    double latitude;  double longitude;
                    double hdop;      double altitude;
                    int    max_snr;   int    num_sat;
                    double speed;     double heading;

                    int status = GccReadGps(out hour, out min, out sec,
                                            out latitude, out longitude,
                                            out num_sat, out hdop, out altitude,
                                            out max_snr,
                                            out speed, out heading);

                    /*  defines from GccGPS
                        READ_NO_ERRORS 0x01
                        READ_HAS_DATA  0x02
                        READ_HAS_GPGSV 0x04
                        READ_HAS_GPGGA 0x08
                        READ_HAS_GPRMC 0x10
                    */

                    // no data read from GPS port
                    if((status & 0x01) == 0) { return gpsPosition; }

                    // has some data - create GpsPosition structure
                    if((status & 0x02) != 0)
                    {
                        gpsPosition = new GpsPosition();

                        // we set version 2 for our stuff. Use some of the fields in the structure for our use
                        gpsPosition.dwVersion = 2;

                        gpsPosition.dwValidFields = 0;

                        // GPGSV is OK
                        if((status & 0x04) != 0)
                        {
                            // use flVerticalDilutionOfPrecision field for SNR (do not want to fill SNR for all sats!)
                            gpsPosition.flVerticalDilutionOfPrecision = (float) max_snr;
                            gpsPosition.dwValidFields |= 0x00000400;

                            if(num_sat != 0)
                            {
                                gpsPosition.dwSatellitesInView = num_sat;
                                gpsPosition.dwValidFields |= 0x00002000;
                            }
                        }

                        // GPGGA is OK
                        if((status & 0x08) != 0)
                        {
                            // use dwFlags to fill today time (do not want to fill stUTCTime!)
                            if( (hour != 0) || (min != 0) || (sec != 0) )
                            {
                                // encode in 3 lower bytes
                                gpsPosition.dwFlags = 0;
                                gpsPosition.dwFlags |= (sec & 0xFF);
                                gpsPosition.dwFlags |= ((min & 0xFF) << 8);
                                gpsPosition.dwFlags |= ((hour & 0xFF) << 16);

                                gpsPosition.dwValidFields |= 0x00000001;
                            }

                            if( (latitude != 0.0) && (longitude != 0.0) )
                            {
                                gpsPosition.dblLatitude = latitude;
                                gpsPosition.dblLongitude = longitude;
                                gpsPosition.dwValidFields |= 0x00000002;
                                gpsPosition.dwValidFields |= 0x00000004;
                            }

                            if(hdop != 0.0)
                            {
                                gpsPosition.flHorizontalDilutionOfPrecision = (float) hdop;
                                gpsPosition.dwValidFields |= 0x00000200;
                            }

                            if(altitude != 0.0)
                            {
                                gpsPosition.flAltitudeWRTEllipsoid = (float) altitude;
                                gpsPosition.dwValidFields |= 0x00000080;
                            }
                        }

                        // GPRMC is OK
                        if((status & 0x10) != 0)
                        {
                            if(speed != -1.0)
                            {
                                gpsPosition.flSpeed = (float) speed;
                                gpsPosition.dwValidFields |= 0x00000008;
                            }

                            if(heading != -1.0)
                            {
                                gpsPosition.flHeading = (float) heading;
                                gpsPosition.dwValidFields |= 0x00000010;
                            }
                        }
                    }
                }
                else
                {
                    // allocate the necessary memory on the native side.  We have a class (GpsPosition) that
                    // has the same memory layout as its native counterpart
                    IntPtr ptr = Utils.LocalAlloc(Marshal.SizeOf(typeof(GpsPosition)));

                    // fill in the required fields
                    gpsPosition = new GpsPosition();
                    gpsPosition.dwVersion = 1;
                    gpsPosition.dwSize = Marshal.SizeOf(typeof(GpsPosition));

                    // Marshal our data to the native pointer we allocated.
                    Marshal.StructureToPtr(gpsPosition, ptr, false);

                    // call native method passing in our native buffer
                    int result = GPSGetPosition(gpsHandle, ptr, 500000, 0);
                    if (result == 0)
                    {
                        // native call succeeded, marshal native data to our managed data
                        gpsPosition = (GpsPosition)Marshal.PtrToStructure(ptr, typeof(GpsPosition));
                    }

                    // free our native memory
                    Utils.LocalFree(ptr);
                }
            }

            return gpsPosition;
        }

        #region PInvokes to gpsapi.dll
        [DllImport("gpsapi.dll")]
        static extern IntPtr GPSOpenDevice(IntPtr hNewLocationData, IntPtr hDeviceStateChange, string szDeviceName, int dwFlags);

        [DllImport("gpsapi.dll")]
        static extern int  GPSCloseDevice(IntPtr hGPSDevice);

        [DllImport("gpsapi.dll")]
        static extern int  GPSGetPosition(IntPtr hGPSDevice, IntPtr pGPSPosition, int dwMaximumAge, int dwFlags);

        #endregion

        #region PInvokes to GccGPS.dll
        [DllImport("GccGPS.dll")]
        static extern int GccGpsStart();
        [DllImport("GccGPS.dll")]
        static extern int GccGpsStop();
        [DllImport("GccGPS.dll")]
        static extern int GccGpsRefresh();
        [DllImport("GccGPS.dll")]
        static extern int GccGpsStatus();

        [DllImport("GccGPS.dll")]
        static extern int GccOpenGps(int com_port, int rate);
        [DllImport("GccGPS.dll")]
        static extern int GccCloseGps();
        [DllImport("GccGPS.dll")]
        static extern int GccIsGpsOpened();
        [DllImport("GccGPS.dll")]
        static extern int GccReadGps(out int hour, out int min, out int sec,
                                     out double latitude, out double longitude,
                                     out int num_sat, out double hdop, out double altitude,
                                     out int max_snr,
                                     out double speed, out double heading);

        [DllImport("GccGPS.dll")]
        static extern int GccReadGpsTest1();
        [DllImport("GccGPS.dll")]
        static extern int GccReadGpsTest2();

        #endregion


    }
}
