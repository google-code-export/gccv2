#region Using directives

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Text;

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

            // allocate the necessary memory on the native side.  We have a class (GpsPosition) that
            // has the same memory layout as its native counterpart
            IntPtr ptr = Utils.LocalAlloc(Marshal.SizeOf(typeof(_SYSTEM_POWER_STATUS_EX2)));

            // fill in the required fields
            pwrStat = new _SYSTEM_POWER_STATUS_EX2();
            UInt32 pwrStatSize = (UInt32)Marshal.SizeOf(typeof(GpsPosition));


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
