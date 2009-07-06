using System;
using System.IO;

using System.Windows.Forms;

namespace Log
{
    public class Logger
    {
        private String filePath = "";
        private FileStream fs;
        private StreamWriter wr;

        public Logger (String path)
        {

            DateTime now = System.DateTime.Now.Date;
            this.filePath = path + "/gcc_" /*+
                now.Year + now.Month + now.Day + "_" +
                now.Hour + now.Minute + now.Second +
                now.Millisecond */+ ".log";
            try
            {
                fs = new FileStream (filePath, FileMode.Create);
                wr = new StreamWriter (fs);
            }
            catch (Exception e)
            {
                MessageBox.Show ("Loger Creation Exception:\r\n" + filePath + "\r\n" + e.ToString (), "ERROR");
            }


        }

        public void Debug (String message)
        {
            DateTime now = System.DateTime.Now;
            wr.WriteLine (now.Year + "." + now.Month + "." + now.Day + "_" +
                now.Hour + ":" + now.Minute + ":" + now.Second + "::" +
                now.Millisecond + " DEBUG " + message);
            wr.Flush ();
        }
        public void Info (String message)
        {
            DateTime now = System.DateTime.Now;
            wr.WriteLine (now.Year + "." + now.Month + "." + now.Day + "_" +
                now.Hour + ":" + now.Minute + ":" + now.Second + "::" +
                now.Millisecond + " INFO " + message);
            wr.Flush ();
        }
        public void Error (String message, Exception e)
        {
            DateTime now = System.DateTime.Now;
            wr.WriteLine (now.Year + "." + now.Month + "." + now.Day + "_" +
                now.Hour + ":" + now.Minute + ":" + now.Second + "::" +
                now.Millisecond + " DEBUG " + message);
            wr.WriteLine (e.ToString ());
            wr.Flush ();
        }
        ~Logger ()
        {
            try
            {
                wr.Close ();
                fs.Close ();
            }
            catch (Exception /*e*/)
            {
                /*really do nothing*/
            }
        }

    }
}
