using System;
using System.Collections;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace GpsCycleComputer
{
    class HeartBeat
    {
        /// <summary>

        /// The waveInOpen function opens the given waveform-audio input device for recording. Then returning the devide id.
        /// </summary>
        /// <param name="hWaveIn">
        /// Pointer to a buffer that receives a handle identifying the open waveform-audio input device. Use this
        /// handle to identify the device when calling other waveform-audio input functions. This parameter can be NULL if WAVE_FORMAT_QUERY
        /// is specified for dwFlags
        /// </param>
        /// <param name="deviceId">
        /// Identifier of the waveform-audio input device to open. It can be either a device identifier or a handle of an open waveform-audio
        /// input device. You can use the following flag instead of a device identifier.
        /// </param>
        /// <param name="wfx">
        /// Pointer to a WAVEFORMATEX structure that identifies the desired format for recording waveform-audio data.
        /// You can free this structure immediately after waveInOpen returns.
        /// </param>
        /// <param name="dwCallBack">
        /// Pointer to a fixed callback function, an event handle, a handle to a window, or the identifier of a thread to be called during
        /// waveform-audio recording to process messages related to the progress of recording. If no callback function is required, this
        /// value can be zero. For more information on the callback function, see waveInProc.
        /// </param>
        /// <param name="dwInstance">
        /// User-instance data passed to the callback mechanism. This parameter is not used with the window callback mechanism.
        /// </param>
        /// <param name="dwFlags">
        /// Flags for opening the device. The following values are defined.
        /// </param>
        /// <returns></returns>
        [DllImport("coredll.dll")]
        private static extern MMRESULT waveInOpen(ref IntPtr hWaveIn, uint deviceId, ref WAVEFORMATEX wfx, IntPtr dwCallBack, uint dwInstance, WaveInOpenFlags dwFlags);

        [DllImport("coredll.dll", SetLastError = true)]
        private static extern uint waveInGetNumDevs();

        [DllImport("coredll.dll", SetLastError = true)]
        private static extern MMRESULT waveInPrepareHeader(IntPtr hwi, IntPtr whdr, uint cbwh);

        [DllImport("coredll.dll", SetLastError = true)]
        private static extern MMRESULT waveInUnprepareHeader(IntPtr hwi, IntPtr whdr, uint cbwh);

        [DllImport("coredll.dll", SetLastError = true)]
        private static extern MMRESULT waveInAddBuffer(IntPtr hwi, IntPtr whdr, uint cbwh);

        [DllImport("coredll.dll", SetLastError = true)]
        private static extern MMRESULT waveInStart(IntPtr hwi);

        [DllImport("coredll.dll", SetLastError = true)]
        private static extern MMRESULT waveInReset(IntPtr hwi);

        [DllImport("coredll.dll", SetLastError = true)]
        private static extern MMRESULT waveInClose(IntPtr hwi);

        //delegate void waveInProc(IntPtr hwi, WIMMessages uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

        [DllImport("coredll.dll")]
        static extern void PowerPolicyNotify(UInt32 powermode, UInt32 flags);

        [DllImport("coredll.dll", SetLastError = true)]
        public static extern IntPtr SetPowerRequirement(string pvDevice, CedevicePowerState deviceState, uint deviceFlags, string pvSystemState, ulong stateFlags);

        [DllImport("coredll.dll", SetLastError = true)]
        public static extern int ReleasePowerRequirement(IntPtr hPowerReq);

        public const int PPN_UNATTENDEDMODE = 0x0003;
        public const int PPN_APPBUTTONPRESSED = 0x0006;
        public const int POWER_NAME = 0x00000001;
        public const int POWER_FORCE = 0x00001000;

        public enum CedevicePowerState : int
        {
            PwrDeviceUnspecified = -1,
            D0 = 0,
            D1,
            D2,
            D3,
            D4,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WAVEHDR
        {
            public IntPtr lpData; // pointer to locked data buffer
            public uint dwBufferLength; // length of data buffer in bytes
            public uint dwBytesRecorded; // used for input only
            public IntPtr dwUser; // for client's use
            public WaveHdrFlags dwFlags; // assorted flags (see defines)
            public uint dwLoops; // loop control counter
            public IntPtr lpNext; // PWaveHdr, reserved for driver
            public IntPtr reserved; // reserved for driver
        }
        const int lpData = 0;       //offsets for unmanaged waveheader structure
        const int dwBufferLength = 4;
        const int dwFlags = 16;
        const int whdrsize = 32;

        [Flags]
        private enum WaveHdrFlags : uint
        {
            WHDR_DONE = 1,
            WHDR_PREPARED = 2,
            WHDR_BEGINLOOP = 4,
            WHDR_ENDLOOP = 8,
            WHDR_INQUEUE = 16
        }
        [Flags]
        enum WaveInOpenFlags : uint
        {
            CALLBACK_NULL = 0,
            CALLBACK_FUNCTION = 0x30000,
            CALLBACK_EVENT = 0x50000,
            CALLBACK_WINDOW = 0x10000,
            CALLBACK_THREAD = 0x20000,
            WAVE_FORMAT_QUERY = 1,
            WAVE_MAPPED = 4,
            WAVE_FORMAT_DIRECT = 8
        }


        /// <summary>
        /// Prefered structure to use with API call waveInOpen.
        /// Needed to encapsulate wave format data.
        /// </summary>
        private struct WAVEFORMATEX
        {
            public short wFormatTag;
            public short nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;
        }

        private enum MMSYSERR : uint
        {
            // Add MMSYSERR's here!

            MMSYSERR_BASE = 0x0000,
            MMSYSERR_NOERROR = 0x0000
        }

        private enum MMRESULT : uint
        {
            MMSYSERR_NOERROR = 0,
            MMSYSERR_ERROR = 1,
            MMSYSERR_BADDEVICEID = 2,
            MMSYSERR_NOTENABLED = 3,
            MMSYSERR_ALLOCATED = 4,
            MMSYSERR_INVALHANDLE = 5,
            MMSYSERR_NODRIVER = 6,
            MMSYSERR_NOMEM = 7,
            MMSYSERR_NOTSUPPORTED = 8,
            MMSYSERR_BADERRNUM = 9,
            MMSYSERR_INVALFLAG = 10,
            MMSYSERR_INVALPARAM = 11,
            MMSYSERR_HANDLEBUSY = 12,
            MMSYSERR_INVALIDALIAS = 13,
            MMSYSERR_BADDB = 14,
            MMSYSERR_KEYNOTFOUND = 15,
            MMSYSERR_READERROR = 16,
            MMSYSERR_WRITEERROR = 17,
            MMSYSERR_DELETEERROR = 18,
            MMSYSERR_VALNOTFOUND = 19,
            MMSYSERR_NODRIVERCB = 20,
            WAVERR_BADFORMAT = 32,
            WAVERR_STILLPLAYING = 33,
            WAVERR_UNPREPARED = 34
        }
        private enum WIMMessages : int
        {
            MM_WIM_OPEN = 0x03BE,
            MM_WIM_CLOSE = 0x03BF,
            MM_WIM_DATA = 0x03C0
        }
        IntPtr[] whdr = new IntPtr[nBuffers];
        IntPtr[] buffer = new IntPtr[nBuffers];

        private WAVEFORMATEX waveFormat;
        private const uint WAVE_MAPPER = unchecked((uint)(-1));
        private const short WAVE_FORMAT_PCM = 0x0001;
        private const uint WAVE_FORMAT_FLAG = 0x00010000;
        private IntPtr hwWaveIn = IntPtr.Zero;
        private IntPtr dwCallBack = IntPtr.Zero;

        const int nSamplesPerSec = 16000;
        const int nSamples = 8000;  // 500ms per buffer
        const int nBuffers = 4;
        
        
        int cur_whdr = -1;       //current wave header
        int s_env = 0;      //sample envelope (rectified and averaged)
        int s_env_max = 0;
        int noise = 0;
        const int AVGs = 16; //average for samples (2^n for performance)
        const int AVGh = 5;     //average for hr
        int thresh = Int16.MaxValue;      //threshold for beep recognition
        int lastbeepidx = 0;
        int lastupdateidx = 0;
        int bpm = 0;
        int cur_hr_avg = 0;        //current heart rate in bpm * AVGh

        int sample0 = 0, sample1 = 0, sample2 = 0;
        int sample3 = 0, sample4 = 0, sample5 = 0;

        IntPtr wavPowerHandle = IntPtr.Zero;

        public int currentHeartRate
        {
            get { return (cur_hr_avg + AVGh/2) / AVGh; }
        }
        public int signalStrength
        {
            get { return s_env_max; }
        }


        public HeartBeat()      //constructor
        {
            // For documentation on the correct values to use, please refer to the MSDN library.

            waveFormat = new WAVEFORMATEX();
            waveFormat.wFormatTag = WAVE_FORMAT_PCM;
            waveFormat.nChannels = 1;
            waveFormat.nSamplesPerSec = nSamplesPerSec;
            waveFormat.nAvgBytesPerSec = nSamplesPerSec * 2;
            waveFormat.nBlockAlign = 2;
            waveFormat.wBitsPerSample = 16;
            waveFormat.cbSize = 0;

            MMRESULT res = waveInOpen(ref hwWaveIn, WAVE_MAPPER, ref waveFormat, dwCallBack, 0, WaveInOpenFlags.CALLBACK_NULL);
            if (res != MMRESULT.MMSYSERR_NOERROR)
                GpsUtils.Utils.log.Error("waveInOpen", null);
            //whdrsize = 2 * Marshal.SizeOf(Type.GetType("IntPtr")) + 6 * sizeof(Int32);
            for (int i = 0; i < nBuffers; i++)
            {
                buffer[i] = Marshal.AllocHGlobal(nSamples * 2);
                whdr[i] = Marshal.AllocHGlobal(whdrsize);
                Marshal.WriteInt32(whdr[i], lpData, (int)buffer[i]);            //lpData = buffer[i];
                Marshal.WriteInt32(whdr[i], dwBufferLength, nSamples * 2);      //dwBufferLength = nSamples * 2;
                Marshal.WriteInt32(whdr[i], dwFlags, 0);                        //dwFlags = 0;
                res = waveInPrepareHeader(hwWaveIn, whdr[i], whdrsize);
                if (res != MMRESULT.MMSYSERR_NOERROR)
                    GpsUtils.Utils.log.Error("waveInPrepareHeader", null);
                res = waveInAddBuffer(hwWaveIn, whdr[i], whdrsize);
                if (res != MMRESULT.MMSYSERR_NOERROR)
                    GpsUtils.Utils.log.Error("waveInAddBuffer", null);
            }
            cur_whdr = -1;
            res = waveInStart(hwWaveIn);
            if (res != MMRESULT.MMSYSERR_NOERROR)
                GpsUtils.Utils.log.Error("waveInStart", null);
            PowerPolicyNotify(PPN_UNATTENDEDMODE, 1);
            wavPowerHandle = SetPowerRequirement("wav1:", CedevicePowerState.D0, POWER_NAME, null, 0);  //HTC diamond don't works with POWER_FORCE
            thresh = Int16.MaxValue;
            lastbeepidx = -2 * nSamplesPerSec;
        }

        ~HeartBeat()
        {
            Close();
        }

        public void Close()         //I can not control when destuctor is called, so make this function
        {
            if (hwWaveIn == IntPtr.Zero)
                return;
            waveInReset(hwWaveIn);
            for (int i = 0; i < nBuffers; i++)
            {
                waveInUnprepareHeader(hwWaveIn, whdr[i], whdrsize);
                Marshal.FreeHGlobal(whdr[i]);
                Marshal.FreeHGlobal(buffer[i]);
            }
            waveInClose(hwWaveIn);
            hwWaveIn = IntPtr.Zero;
            PowerPolicyNotify(PPN_UNATTENDEDMODE, 0);
            ReleasePowerRequirement(wavPowerHandle);
        }


        public void Tick()      //should be called once a second
        {
            while (true)        //loop trough filled buffers
            {
                int next_whdr = cur_whdr + 1;
                if (next_whdr >= nBuffers)
                    next_whdr = 0;
                if (((WaveHdrFlags)Marshal.ReadInt32(whdr[next_whdr], dwFlags) & WaveHdrFlags.WHDR_DONE) == 0)
                    break;                              //buffer not filled
                cur_whdr = next_whdr;
                lastbeepidx -= nSamples;
                lastupdateidx -= nSamples;
                if (lastupdateidx < -100 * nSamplesPerSec)
                    lastupdateidx = -10 * nSamplesPerSec;       //prevent negative overflow
                int noiseaccu = 0;
                for (int i = 0; i < nSamples; i++)
                {
                    int rectsample = 0;
                    sample5 = sample4;
                    sample4 = sample3;
                    sample3 = sample2;
                    sample2 = sample1;
                    sample1 = sample0;
                    sample0 = Marshal.ReadInt16(buffer[cur_whdr], 2 * i);

                    rectsample = 2 * sample0 - sample1 - sample2 + 2 * sample3 - sample4 - sample5;     //5.3kHz BP
                    if (rectsample < 0) rectsample = -rectsample;
                    s_env = (s_env * (AVGs - 1) + rectsample) / AVGs;
                    noiseaccu += s_env;
                    if (s_env > s_env_max)
                        s_env_max = s_env;
                    if (i - lastupdateidx > 4 * nSamplesPerSec)           //here because of 'continue' it would never be tested otherwise
                    {                                                     //4s no pulse and no update -> reset hr
                        cur_hr_avg = 0;
                        lastupdateidx = i;
                        s_env_max = noise;
                    }
                    if (s_env > thresh)
                    {
                        int periodidx = i - lastbeepidx;
                        if (periodidx < 272 * nSamplesPerSec / 1000)       // <272ms     > 220bpm
                        {
                            continue;               //ignore remaining samples above thresh and short following pulses from coded belts
                        }
                        lastbeepidx = i;            //qualified beep
                        bpm = 60 * AVGh * nSamplesPerSec / periodidx;       //bpm multiplied with AVGh
                        if (cur_hr_avg == 0 || i - lastupdateidx > 3 * nSamplesPerSec)  // || 3s no update -> reset hr
                        {
                            if (bpm > 37 * AVGh)
                                cur_hr_avg = bpm;                       //set cur_hr_avg to new value
                            else
                                cur_hr_avg = 0;
                            lastupdateidx = i;
                            continue;
                        }
                        if (bpm > cur_hr_avg + 10 * AVGh)
                        {
                            continue;               //ignore, because more than 10bpm higher - possible false beep due to belt move (not indexed)
                        }
                        if (bpm < cur_hr_avg - 10 * AVGh)
                        {
                            continue;               //ignore, because more than 10bpm lower - possible missing beep
                        }

                        if (bpm < 38 * AVGh)
                        {
                            continue;
                        }
                        cur_hr_avg = (cur_hr_avg * (AVGh - 1) + bpm) / AVGh;     //average and update current hr
                        lastupdateidx = i;
                    }
                }
                noise = (noiseaccu / nSamples + 20) * 3;
                //thresh = (s_env_max - noise_avg * 4) / 8 + noise_avg * 4 + 100;
                //thresh = (s_env_max + 1 * noise) / 2;
                thresh = (s_env_max - noise) / 8 + noise;

                Debug.WriteLine(noise + "  " + thresh + "  " + s_env_max);

                MMRESULT res;
                res = waveInAddBuffer(hwWaveIn, whdr[cur_whdr], whdrsize);        //feed buffer in queue again
                if (res != MMRESULT.MMSYSERR_NOERROR)
                    GpsUtils.Utils.log.Error("waveInAddBuffer", null);
            }
            s_env_max = s_env_max * 3 / 4;
        }
    }
}
