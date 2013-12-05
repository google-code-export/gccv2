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
        public string closingMessage = null;

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
                MessageBox.Show("Logger Creation Exception:\r\n" + filePath , "ERROR");
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
            try
            {
                if (fs.Length > 500000) // file size is in bytes (=500KB)
                {
                    fs.Close();
                    wr.Close();
                    DateTime now = System.DateTime.Now;
                    string fileArch = filePath + now.Year + now.Month + now.Day + "_" +
                                now.Hour + now.Minute + now.Second + now.Millisecond + ".log";
                    File.Move(filePath, fileArch);
                    closingMessage = "There is a remarkable number of error messages in " + fileArch;
                    fs = new FileStream(filePath, FileMode.Create);
                    wr = new StreamWriter(fs);
                }
            }
            catch (Exception /*e*/)
            {
                MessageBox.Show("Logger ReCreation Exception:\r\n" + filePath, "ERROR");
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
