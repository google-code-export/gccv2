using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Text;

namespace GpsUtils
{
    public class Gps
    {
        // handle to the gps device
        IntPtr gpsHandle = IntPtr.Zero;

        /// True: The GPS device has been opened. False: It has not been opened
        public bool Opened
        {
            get { return gpsHandle != IntPtr.Zero; }
        }

        public Gps() {}

        ~Gps()
        {
            Close();
        }

        public void Open()
        {
            if (!Opened)
            {
                gpsHandle = GPSOpenDevice(IntPtr.Zero, IntPtr.Zero, null, 0);
            }
        }

        public void Close()
        {
            if (Opened)
            {
                GPSCloseDevice(gpsHandle);
                gpsHandle = IntPtr.Zero;
            }
        }

        public GpsPosition GetPosition()
        {
            GpsPosition gpsPosition = null;
            if (Opened)
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
    }
}
