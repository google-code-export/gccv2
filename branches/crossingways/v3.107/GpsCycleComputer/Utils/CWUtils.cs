using System;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.IO;
using System.Globalization;

namespace GpsCycleComputer
{
    class CWUtils
    {
        private static string ByteArrayToHexString(byte[] Bytes)
        {
            StringBuilder Result;
            string HexAlphabet = "0123456789ABCDEF";
            Result = new StringBuilder();
            foreach (byte B in Bytes)
            {
                Result.Append(HexAlphabet[(int) (B >> 4)]);
                Result.Append(HexAlphabet[(int) (B & 0xF)]);
            }
            return Result.ToString();
        }

        /// <summary>
        /// Creates a Hassh for a password, so it can be stored locally
        /// </summary>
        /// <param name="password">the password (UTF8 encoded)</param>
        /// <returns>the hash for the given password</returns>
        public static string HashPassword(string password)
        {
            HashAlgorithm sha = new SHA1CryptoServiceProvider();
            UTF8Encoding enc = new UTF8Encoding();
            byte[] pwhashb = sha.ComputeHash(enc.GetBytes(password));
            return CWUtils.ByteArrayToHexString(pwhashb);
        }

        /// <summary>
        /// Verifies the given credential on crossingways
        /// TODO: should be called async in order to prevent GUI-lockups
        /// </summary>
        /// <param name="username">username</param>
        /// <param name="password">password in cleartext</param>
        /// <returns>status (if it starts with 00 everyting is fine)</returns>
        public static string VerifyCredentialsOnCrossingways(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return "50 - Please enter username and pasword!";
            else
            {
                // Create a Passwordhash
                // Not ideal but better than sending the password in cleatext...
                string pwhash = CWUtils.HashPassword(password);
                bool valid = false;
                try
                {
                    GpsSample.CW.LiveTracking lt = new GpsSample.CW.LiveTracking();
                    // CWRocks2008 is always sent... 
                    valid = lt.VerifyCredentials(username, pwhash, "CWRocks2008");
                }
                catch
                {
                    return "90 - Could not establish a connection to the server!";
                }
                if (valid)
                    return "00 - Credentials verified!";
                else
                    return "50 - Invalid credentials!";
            }
        }

        /// <summary>
        /// Verifies the given credential on crossingways
        /// TODO: should be called async in order to prevent GUI-lockups
        /// </summary>
        /// <param name="username">username</param>
        /// <param name="password">password in cleartext</param>
        /// <returns>status (if it starts with 00 everyting is fine)</returns>
        public static string VerifyCredentialsOnCrossingwaysViaHTTP(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return "50 - Please enter username and pasword!";
            else
            {
                // Create a Passwordhash
                // Not ideal but better than sending the password in cleatext...
                string pwhash = CWUtils.HashPassword(password);

                string url = "http://www.crossingways.com/services/livetracking.asmx/VerifyCredentials";
                string payload = "";
                payload += "username=" + UrlEncode(username) + "&";
                payload += "passwordhash=" + pwhash + "&";
                payload += "control=" + "CWRocks2008";
                
                string result = "";
                bool valid = false;
                try
                {
                    WebResponse resp = doPost(url, payload);
                    Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                    StreamReader readStream = new StreamReader(resp.GetResponseStream(), encode);
                    result = readStream.ReadToEnd();
                    result = result.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "");
                    result = result.Replace("\r\n", "");
                    result = result.Replace("<boolean xmlns=\"http://www.crossingways.com/\">", "");
                    result = result.Replace("</boolean>", "");
                }
                catch
                {
                    return "90 - Could not establish a connection to the server!";
                }

                valid = bool.Parse(result);
                if (valid)
                    return "00 - Credentials verified!";
                else
                    return "50 - Invalid credentials!";

            }
        }

        /// <summary>
        /// Updates a single Position on Crossingways
        /// </summary>
        /// <param name="username">username</param>
        /// <param name="passwordhash">hashed password</param>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <param name="ele">Elevation</param>
        /// <param name="heading">Heading</param>
        /// <param name="messagetext">A Message can be sent along (max. length 160)</param>
        /// <returns>status as a string</returns>
        public static string UpdatePositionOnCrossingways(string username, string passwordhash, double lat, double lon, double ele, double heading, string messagetext)
        {
            string message = "";
            try
            {
                GpsSample.CW.LiveTracking lt = new GpsSample.CW.LiveTracking();
                message = lt.CurrentPosition(username, passwordhash, lat, lon, ele, heading, DateTime.Now, 0, 0, messagetext);
            }
            catch
            {
                message += DateTime.Now.ToShortTimeString() + "90 - Could not establish a connection to the server!\r\n";
            }
            message += DateTime.Now.ToShortTimeString() + " " + message + " \r\n";
            return message;
        }


        /// <summary>
        /// Uploads an entire GPX file to the Server
        /// </summary>
        /// <param name="username"></param>
        /// <param name="passwordhash"></param>
        /// <param name="trackname"></param>
        /// <param name="gpx">gpx file as a string</param>
        /// <returns></returns>
        public static string UploadGPX(string username, string passwordhash, string trackname, string gpx)
        {
            string message = "";
            try
            {
                GpsSample.CW.LiveTracking lt = new GpsSample.CW.LiveTracking();
                message = lt.UploadGPX(username, passwordhash, trackname, gpx);
            }
            catch
            {
                message += DateTime.Now.ToShortTimeString() + "90 - Could not establish a connection to the server!\r\n";
            }
            message += DateTime.Now.ToShortTimeString() + " " + message + " \r\n";
            return message;
        }

        /// <summary>
        /// Uploads an entire GPX file to the Server
        /// </summary>
        /// <param name="username"></param>
        /// <param name="passwordhash"></param>
        /// <param name="trackname"></param>
        /// <param name="gpx">gpx file as a string</param>
        /// <returns></returns>
        public static string UploadGPXViaHTTP(string username, string passwordhash, string trackname, string gpx)
        {
            string url = "http://www.crossingways.com/services/livetracking.asmx/UploadGPX";
            string payload = "";
            payload += "username=" + UrlEncode(username) + "&";
            payload += "password=" + passwordhash + "&";
            payload += "trackname=" + UrlEncode(trackname) + "&";
            payload += "gpx=" + UrlEncode(gpx);
            string result = "";
            try
            {
                WebResponse resp = doPost(url, payload);
                Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                StreamReader readStream = new StreamReader(resp.GetResponseStream(), encode);
                result = readStream.ReadToEnd();
                result = result.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "");
                result = result.Replace("\r\n", "");
                result = result.Replace("<string xmlns=\"http://www.crossingways.com/\">", "");
                result = result.Replace("</string>", "");
            }
            catch
            {
                return "90 - Could not establish a connection to the server!";
            }
            return result;
        }

        /// <summary>
        /// Updates a single Position on Crossingways via an HTTP Get request
        /// </summary>
        /// <param name="username">username</param>
        /// <param name="passwordhash">hashed password</param>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <param name="ele">Elevation</param>
        /// <param name="heading">Heading</param>
        /// <param name="messagetext">A Message can be sent along (max. length 160)</param>
        /// <returns>status as a string</returns>
        public static string UpdatePositionOnCrossingwaysViaHTTP(string username, string passwordhash, double lat, double lon, double ele, double heading, string messagetext)
        {
            string url = "http://www.crossingways.com/services/livetracking.asmx/CurrentPosition"; 
            string payload= "";
            payload+="username="+ UrlEncode(username) + "&";
            payload += "password=" + passwordhash + "&";
            payload += "lat=" + lat.ToString(CultureInfo.InvariantCulture) + "&";
            payload += "lon=" + lon.ToString(CultureInfo.InvariantCulture) + "&";
            payload += "alt=" + ele.ToString(CultureInfo.InvariantCulture) + "&";
            payload += "heading=" + heading.ToString(CultureInfo.InvariantCulture) + "&";
            payload += "timestamp=" + UrlEncode(DateTime.Now.ToUniversalTime().ToString(CultureInfo.InvariantCulture)) + "&";
            payload += "trackid=" + 0 + "&";
            payload += "tracktypeid=" + 0 + "&";
            payload += "message=" + UrlEncode(messagetext) ;

            string result = "";
            try
            {
                WebResponse resp = doPost(url, payload);
                Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                StreamReader readStream = new StreamReader(resp.GetResponseStream(), encode);
                result = readStream.ReadToEnd();
                result = result.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "");
                result = result.Replace("\r\n", "");
                result = result.Replace("<string xmlns=\"http://www.crossingways.com/\">", "");
                result = result.Replace("</string>", "");
            }
            catch (Exception /*e*/)
            {
                result = "Failed. Will try again!";
            }
            return result;
        }

        public static WebResponse doPost(String url, String payload)
        {
            HttpWebRequest req = (HttpWebRequest) WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded; charset=utf-8 ";

            System.Net.ServicePointManager.Expect100Continue = false;
            // Encode the data
            byte[] encodedBytes = Encoding.UTF8.GetBytes(payload);
            req.ContentLength = encodedBytes.Length;
            req.AllowWriteStreamBuffering = true;

            // Write encoded data into request stream
            Stream requestStream = req.GetRequestStream();
            requestStream.Write(encodedBytes, 0, encodedBytes.Length);
            requestStream.Flush();
            requestStream.Close();

            WebResponse result = req.GetResponse();
            return result;

        }
        /// <summary>
        /// Replacement for HttpUtility.UrlEncode
        /// </summary>

        public static string UrlEncode(string instring)
        {
            StringReader strRdr = new StringReader(instring);
            StringWriter strWtr = new StringWriter();
            int charValue = strRdr.Read();
            while (charValue != -1)
            {
                if (((charValue >= 48) && (charValue <= 57)) // 0-9
                    || ((charValue >= 65) && (charValue <= 90)) // A-Z
                    || ((charValue >= 97) && (charValue <= 122))) // a-z
                    strWtr.Write((char) charValue);
                else if (charValue == 32)    // Space
                    strWtr.Write('+');
                else
                    strWtr.Write("%{0:x2}", charValue);

                charValue = strRdr.Read();
            }

            return strWtr.ToString();
        }


        /// <summary>
        /// Updates multiple positions on Crossingways
        /// </summary>
        /// <param name="username">username</param>
        /// <param name="passwordhash">hashed password</param>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <param name="timestamp">DateTime of the Coordinate</param>
        /// <param name="ele">Elevation</param>
        /// <param name="heading">Heading</param>
        /// <param name="messagetext">A Message can be sent along (max. length 160)</param>
        /// <returns>status as a string</returns>
        public static string UpdatePositionsOnCrossingways(string username, string passwordhash, double[] lat, double[] lon, DateTime[] timestamp, double[] ele, double[] heading, string messagetext)
        {
            string message = "";
            try
            {
                GpsSample.CW.LiveTracking lt = new GpsSample.CW.LiveTracking();
                message = lt.LogPositions(username, passwordhash, lat, lon, ele, heading, timestamp, 0, messagetext);
            }
            catch
            {
                message += DateTime.Now.ToShortTimeString() + "90 - Could not establish a connection to the server!\r\n";
            }
            message += DateTime.Now.ToShortTimeString() + " " + message + " \r\n";
            return message;
        }

    }
}
