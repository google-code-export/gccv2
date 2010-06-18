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
        private bool logFileLocked = false;

        public Logger (String path)
        {
            this.filePath = path;
            if (this.filePath != "\\") this.filePath += "\\";
            this.filePath += "GpsCycleComputer.log";
            try
            {
                fs = new FileStream(filePath, FileMode.Append);
                wr = new StreamWriter(fs);
            }
            catch (Exception /*e*/)
            {
                MessageBox.Show("Loger Creation Exception:\r\n" + filePath , "ERROR");
            }
            logFileLocked = false;
        }

        public void Debug(String message)
        {
            checkFileLengh();
            while (logFileLocked) { }
            logFileLocked = true;
            wr.WriteLine(now() + " DEBUG " + message);
            wr.Flush();
            logFileLocked = false;

        }
        public void Info(String message)
        {
            checkFileLengh();
            while (logFileLocked) { }
            logFileLocked = true;
            wr.WriteLine(now() + " INFO " + message);
            wr.Flush();
            logFileLocked = false;

        }
        public void Error(String message, Exception e)
        {
            checkFileLengh();
            while (logFileLocked) { }

            logFileLocked = true;
            wr.WriteLine(now() + " ERROR " + message);
            wr.WriteLine(e.ToString());
            wr.Flush();
            logFileLocked = false;

        }
        private  String now()
        {
            DateTime now = System.DateTime.Now;
            return (now.Year + "." + now.Month + "." + now.Day + "_" +
                now.Hour + ":" + now.Minute + ":" + now.Second + "::" +
                now.Millisecond);
        }
        private void checkFileLengh()
        {
            while (logFileLocked) { }
            logFileLocked = true;
            DateTime now = System.DateTime.Now;
            try
            {

                FileInfo fi = new FileInfo(filePath);
                if (fi.Length > 500000) // file size is in bytes (=500KB)
                {

                    fs.Close();
                    wr.Close();
                    File.Move(filePath, filePath + now.Year + now.Month + now.Day + "_" +
                                now.Hour + now.Minute + now.Second + now.Millisecond + ".log");
                    fs = new FileStream(filePath, FileMode.Create);
                    wr = new StreamWriter(fs);

                }
                fi = null;
            }
            catch (Exception /*e*/)
            {
                MessageBox.Show("Loger Creation Exception:\r\n" + filePath, "ERROR");
            }
            logFileLocked = false;
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
