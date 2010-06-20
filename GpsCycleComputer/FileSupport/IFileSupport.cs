using System;

using System.Collections.Generic;
using System.Text;

namespace GpsSample.FileSupport
{
    interface IFileSupport
    {
        bool Load(string filename, 
            // allocated vector size (for dataX/Y/T)
            int vector_size,          
            // x and y realive to origin, in metres, t in sec ralative to start
            ref float[] dataLat, ref float[] dataLong, ref  Int32[] dataT, 
            out int data_size);
    }
}
