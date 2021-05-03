﻿/*
    Copyright 2015 MCGalaxy
        
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using MCGalaxy.Config;
using MCGalaxy.Network;

namespace MCGalaxy.Modules.Relay.Discord {
    
    /// <summary> Implements a basic web client for communicating with Discord's API </summary>
    /// <remarks> https://discord.com/developers/docs/reference </remarks>
    /// <remarks> https://discord.com/developers/docs/resources/channel#create-message </remarks>
    public sealed class DiscordApiClient {
        public string Token;
        const string host = "https://discord.com/api";
        AutoResetEvent handle = new AutoResetEvent(false);
        volatile bool terminating;
        
        class Request { public string Path, Data; }
        Queue<Request> requests = new Queue<Request>();
        readonly object reqLock = new object();
            
        
        void HandleNext() {
            Request req = null;
            lock (reqLock) {
                if (requests.Count > 0) req = requests.Dequeue();
            }
            if (req == null) { handle.WaitOne(); return; }
            
            // TODO HttpWebRequest
            using (WebClient client = HttpUtil.CreateWebClient()) {
                client.Headers[HttpRequestHeader.ContentType]   = "application/json";
                client.Headers[HttpRequestHeader.Authorization] = "Bot " + Token;

                string resp = client.UploadString(host + req.Path, req.Data);
                Logger.Log(LogType.SystemActivity, resp);
            }
        }
        
        void SendLoop() {
            for (;;) {
                if (terminating) break;
                
                try {
                    HandleNext();
                } catch (Exception ex) {
                    Logger.LogError(ex);
                }
            }
            
            // cleanup state
            try { 
                lock (reqLock) requests.Clear();
                handle.Dispose(); 
            } catch {
            }
        }
        
        
        void WakeupWorker() {
            try {
                handle.Set();
            } catch (ObjectDisposedException) {
                // for very rare case where handle's already been destroyed
            }
        }
        
        public void RunAsync() {
            Thread worker = new Thread(SendLoop);
            worker.Name   = "Discord-ApiClient";
            worker.IsBackground = true;
            worker.Start();
        }
        
        public void StopAsync() {
            terminating = true;
            WakeupWorker();
        }
        
        
        public void MakeRequest(string path, JsonObject obj) {
            Request req = new Request();
            req.Path = path;
            req.Data = Json.SerialiseObject(obj);
            
            lock (reqLock) requests.Enqueue(req);
            WakeupWorker();
        }
        
        public void SendMessage(string channelID, string message) {
            JsonObject allowed = new JsonObject()
            {
                { "parse", new JsonArray() { "users", "roles" } }
            };
            JsonObject obj = new JsonObject()
            {
                { "content", message },
                { "allowed_mentions", allowed }
            };
            
            string path = "/channels/" + channelID + "/messages";
            MakeRequest(path, obj);
        }
    }
}
