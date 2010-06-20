#region Using directives

using System;
using System.Collections;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using System.Globalization;
using GpsSample.FileSupport;

#endregion

namespace GpsUtils
{
    public class ReadFileUtil
    {
        public static bool LoadGcc(string filename, int vector_size,          
            ref float[] dataLat, ref float[] dataLong, ref  Int32[] dataT, out int data_size)
        {
            return new GccSupport().Load(filename, vector_size, ref dataLat, ref dataLong, ref dataT, out data_size);
        }

        public static bool LoadKml(string filename, int vector_size,          
            ref float[] dataLat, ref float[] dataLong, ref  Int32[] dataT, out int data_size)  
        {
            return new KmlSupport().Load(filename, vector_size, ref dataLat, ref dataLong, ref dataT, out data_size);
        }

        public static bool LoadGpx(string filename, int vector_size,          
            ref float[] dataLat, ref float[] dataLong, ref  Int32[] dataT, 
            out int data_size)  // x and y realive to origin, in metres, t in sec ralative to start
        {
            return new GpxSupport().Load(filename, vector_size, ref dataLat, ref dataLong, ref dataT, out data_size);
        }
    }
}
