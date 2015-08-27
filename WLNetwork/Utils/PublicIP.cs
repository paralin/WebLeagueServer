using System;
using System.IO;
using System.Net;
using System.Reflection;
using log4net;

namespace WLNetwork.Utils
{
    public static class PublicIP
    {
        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static string Fetch()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://icanhazip.com");

                request.UserAgent = "curl"; // this simulate curl linux command

                string publicIPAddress;

                request.Method = "GET";
                using (WebResponse response = request.GetResponse())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        publicIPAddress = reader.ReadToEnd();
                    }
                }
                publicIPAddress = publicIPAddress.Replace("\n", "");

                log.Info("Fetched public IP " + publicIPAddress);
                return publicIPAddress;
            }
            catch (Exception ex)
            {
                log.Error("Can't fetch public IP", ex);
                return null;
            }
        }
    }
}