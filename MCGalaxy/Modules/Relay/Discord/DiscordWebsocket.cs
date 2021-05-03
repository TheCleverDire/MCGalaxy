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
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using MCGalaxy.Config;
using MCGalaxy.Network;
using MCGalaxy.Tasks;

namespace MCGalaxy.Modules.Relay.Discord {
    
    /// <summary> Implements a basic websocket for communicating with Discord's gateway </summary>
    /// <remarks> https://discord.com/developers/docs/topics/gateway </remarks>
    /// <remarks> https://i.imgur.com/Lwc5Wde.png </remarks>
    public sealed class DiscordWebsocket : ClientWebSocket {
        public string Token;
        public Action OnReady;
        public Action<JsonObject> Handler;
        public Func<string> GetStatus;
        
        readonly object sendLock = new object();
        SchedulerTask heartbeat;
        string lastSequence;
        TcpClient client;
        SslStream stream;
        
        const int OPCODE_DISPATCH        = 0;
        const int OPCODE_HEARTBEAT       = 1;
        const int OPCODE_IDENTIFY        = 2;
        const int OPCODE_STATUS_UPDATE   = 3;
        const int OPCODE_VOICE_STATE_UPDATE = 4;
        const int OPCODE_RESUME          = 6;
        const int OPCODE_REQUEST_SERVER_MEMBERS = 8;
        const int OPCODE_INVALID_SESSION = 9;
        const int OPCODE_HELLO           = 10;
        const int OPCODE_HEARTBEAT_ACK   = 11;
        
        
        public DiscordWebsocket() {
            path = "/?v=6&encoding=json";
        }
        
        const string host = "gateway.discord.gg";
        // stubs
        public override bool LowLatency { set { } }
        public override string IP { get { return ""; } }
        
        public void Connect() {
            client = new TcpClient();
            client.Connect(host, 443);

            stream   = HttpUtil.WrapSSLStream(client.GetStream(), host);
            protocol = this;
            Init();
        }
        
        protected override void WriteCustomHeaders() {
            WriteHeader("Authorization: Bot " + Token);
            WriteHeader("Host: " + host);
        }
        
        public override void Close() {
            Server.Background.Cancel(heartbeat);
            try {
                client.Close();
            } catch {
                // ignore errors when closing socket
            }
        }
        
        protected override void Disconnect(int reason) {
            try {
                base.Disconnect(reason);
            } catch {
                // try to cleanly close connection when possible
            }
            Close();
        }
        
        
        public void ReadLoop() {
            byte[] data = new byte[4096];
            for (;;) {
                int len = stream.Read(data, 0, 4096);
                if (len == 0) break; // disconnected
                HandleReceived(data, len);
            }
        }
        
        protected override void HandleData(byte[] data, int len) {
            string value   = Encoding.UTF8.GetString(data, 0, len);
            JsonReader ctx = new JsonReader(value);
            JsonObject obj = (JsonObject)ctx.Parse();
            
            Logger.Log(LogType.SystemActivity, value);
            if (obj == null) return;
            
            int opcode = int.Parse((string)obj["op"]);
            DispatchPacket(opcode, obj);
        }
        
        void DispatchPacket(int opcode, JsonObject obj) {
            if (opcode == OPCODE_DISPATCH) HandleDispatch(obj);
            if (opcode == OPCODE_HELLO)    HandleHello(obj);
        }
        
        
        void HandleHello(JsonObject obj) {
            JsonObject data = (JsonObject)obj["d"];
            string interval = (string)data["heartbeat_interval"];            
            int msInterval  = int.Parse(interval);
            
            heartbeat = Server.Background.QueueRepeat(SendHeartbeat, null, 
                                          TimeSpan.FromMilliseconds(msInterval));
            SendIdentify();
            OnReady();
        }
        
        void HandleDispatch(JsonObject obj) {
            // update last sequence number
            object sequence;
            if (obj.TryGetValue("s", out sequence)) 
                lastSequence = (string)sequence;
            
            Handler(obj);
        }
        
        
        public void SendMessage(int opcode, JsonObject data) {
            JsonObject obj = new JsonObject()
            {
                { "op", opcode },
                { "d",  data }
            };
            SendMessage(obj);
        }
        
        public void SendMessage(JsonObject obj) {
            string str = Json.SerialiseObject(obj);
            Send(Encoding.UTF8.GetBytes(str), SendFlags.None);
        }
        
        protected override void SendRaw(byte[] data, SendFlags flags) {
            lock (sendLock) stream.Write(data);
        }
        
        void SendHeartbeat(SchedulerTask task) {
            JsonObject obj = new JsonObject();
            obj["op"] = OPCODE_HEARTBEAT;
            
            if (lastSequence != null) {
                obj["d"] = int.Parse(lastSequence);
            } else {
                obj["d"] = null;
            }
            SendMessage(obj);
        }
        
        const int INTENT_GUILD_MESSAGES  = 1 << 9;
        const int INTENT_DIRECT_MESSAGES = 1 << 12;
        
        public void SendIdentify() {
            JsonObject props = new JsonObject()
            {
                { "$os",      "linux" },
                { "$browser", Server.SoftwareName },
                { "$device",  Server.SoftwareName }
            };
            
            JsonObject data = new JsonObject()
            {
                { "token",      Token },
                { "intents",    INTENT_GUILD_MESSAGES | INTENT_DIRECT_MESSAGES },
                { "properties", props },
                { "presence",   MakePresence() }
            };
            SendMessage(OPCODE_IDENTIFY, data);
        }
        
        public void SendUpdateStatus() {
            JsonObject data = MakePresence();
            SendMessage(OPCODE_STATUS_UPDATE, data);
        }
        
        JsonObject MakePresence() {
            JsonObject activity = new JsonObject()
            {
                { "name", GetStatus() },
                { "type", 0 }
            };
            JsonObject obj = new JsonObject()
            {
                { "since",      null },
                { "activities", new JsonArray() { activity } },
                { "status",     "online" },
                { "afk",        false }
            };
            return obj;
        }
    }
}
