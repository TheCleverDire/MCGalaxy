﻿/*
Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCGalaxy)
Dual-licensed under the Educational Community License, Version 2.0 and
the GNU General Public License, Version 3 (the "Licenses"); you may
not use this file except in compliance with the Licenses. You may
obtain a copy of the Licenses at
http://www.opensource.org/licenses/ecl2.php
http://www.gnu.org/licenses/gpl-3.0.html
Unless required by applicable law or agreed to in writing,
software distributed under the Licenses are distributed on an "AS IS"
BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
or implied. See the Licenses for the specific language governing
permissions and limitations under the Licenses.
*/
using System;
using System.Net;

namespace MCGalaxy.Network {
    /// <summary> Static class for assisting with making web requests. </summary>
    public static class HttpUtil {

        public static WebClient CreateWebClient() { return new CustomWebClient(); }
        
        public static HttpWebRequest CreateRequest(string uri) {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.ServicePoint.BindIPEndPointDelegate = BindIPEndPointCallback;
            req.UserAgent = Server.SoftwareNameVersioned;
            return req;
        }


        class CustomWebClient : WebClient {
            protected override WebRequest GetWebRequest(Uri address) {
                HttpWebRequest req = (HttpWebRequest)base.GetWebRequest(address);
                req.ServicePoint.BindIPEndPointDelegate = BindIPEndPointCallback;
                req.UserAgent = Server.SoftwareNameVersioned;
                return (WebRequest)req;
            }
        }
        
        static IPEndPoint BindIPEndPointCallback(ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount) {
            IPAddress localIP = null;
            if (Server.Listener != null) {
                localIP = Server.Listener.IP;
            } else if (!IPAddress.TryParse(Server.Config.ListenIP, out localIP)) {
                return null;
            }
            
            // can only use same family for local bind IP
            if (remoteEndPoint.AddressFamily != localIP.AddressFamily) return null;
            return new IPEndPoint(localIP, 0);
        }
        
        public static bool IsPrivateIP(string ip) {
            //range of 172.16.0.0 - 172.31.255.255
            if (ip.StartsWith("172.") && (int.Parse(ip.Split('.')[1]) >= 16 && int.Parse(ip.Split('.')[1]) <= 31))
                return true;
            return IPAddress.IsLoopback(IPAddress.Parse(ip)) || ip.StartsWith("192.168.") || ip.StartsWith("10.");
            //return IsLocalIpAddress(ip);
        }

        public static bool IsLocalIP(string ip) {
            try { // get host IP addresses
                IPAddress[] hostIPs = Dns.GetHostAddresses(ip);
                // get local IP addresses
                IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

                // test if any host IP equals to any local IP or to localhost
                foreach ( IPAddress hostIP in hostIPs ) {
                    // is localhost
                    if ( IPAddress.IsLoopback(hostIP) ) return true;
                    // is local address
                    foreach ( IPAddress localIP in localIPs ) {
                        if ( hostIP.Equals(localIP) ) return true;
                    }
                }
            }
            catch { }
            return false;
        }
        
        /// <summary> Prefixes a URL by http:// if needed, and converts dropbox webpages to direct links. </summary>
        public static void FilterURL(ref string url) {
            if (!url.CaselessStarts("http://") && !url.CaselessStarts("https://"))
                url = "http://" + url;
            
            // a lot of people try linking to the dropbox page instead of directly to file, so auto correct
            if (url.CaselessStarts("http://www.dropbox")) {
                url = "http://dl.dropbox" + url.Substring("http://www.dropbox".Length);
                url = url.Replace("?dl=0", "");
            } else if (url.CaselessStarts("https://www.dropbox")) {
                url = "https://dl.dropbox" + url.Substring("https://www.dropbox".Length);
                url = url.Replace("?dl=0", "");
            }
            
            url = url.Replace("dl.dropboxusercontent.com", "dl.dropbox.com");
        }
        
        public static byte[] DownloadData(string url, Player p) {
            FilterURL(ref url);
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) {
                p.Message("%W{0} is not a valid URL.", url); return null;
            }
            
            byte[] data = null;
            try {
                using (WebClient client = CreateWebClient()) {
                    p.Message("Downloading file from: &f" + url);
                    data = client.DownloadData(uri);
                }
                p.Message("Finished downloading.");
            } catch (Exception ex) {
                Logger.LogError("Error downloading", ex);
                p.Message("%WFailed to download from &f" + url);
                return null;
            }
            return data;
        }
        
        public static byte[] DownloadImage(string url, Player p) {
            byte[] data = DownloadData(url, p);
            if (data == null) p.Message("%WThe url may need to end with its extension (such as .jpg).");
            return data;
        }
    }
}