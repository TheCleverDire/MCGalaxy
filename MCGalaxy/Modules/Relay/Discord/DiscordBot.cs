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
using System.Text;
using System.Threading;
using MCGalaxy.Config;
using MCGalaxy.Events.GroupEvents;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Events.ServerEvents;

namespace MCGalaxy.Modules.Relay.Discord {

    public sealed class DiscordBot : RelayBot {
        bool disconnected, disconnecting;
        string[] operatorIds;
        
        DiscordApiClient api;
        DiscordWebsocket socket;        

        public override string RelayName { get { return "Discord"; } }        
        public override bool Enabled     { get { return Config.Enabled; } }
        public override bool Connected   { get { return socket != null && !disconnected; } }
        public DiscordConfig Config;
        
        void TryReconnect() {
            try {
                Disconnect("Attempting reconnect");
                AutoReconnect();
            } catch (Exception ex) {
                Logger.LogError("Error reconnecting Discord relay", ex);
            }
        }
        
        void IOThread() {
            try {
                socket.Connect();
                socket.ReadLoop();
            } catch (Exception ex) {
                Logger.LogError("Discord relay error", ex);
                if (disconnecting) return;
                
                // try to recover from dropped connection
                TryReconnect();
            }
        }
        
        protected override void DoConnect() {
            // TODO implement properly
            socket = new DiscordWebsocket();
            disconnecting = false;
            disconnected  = false;
            
            Channels    = Config.Channels.SplitComma();
            OpChannels  = Config.OpChannels.SplitComma();
            operatorIds = Config.OperatorUsers.SplitComma();
            
            socket.Token     = Config.BotToken;
            socket.Handler   = HandleEvent;
            socket.GetStatus = GetStatus;
            socket.OnReady   = OnReady;
                
            Thread worker = new Thread(IOThread);
            worker.Name = "DiscordRelayBot";
            worker.IsBackground = true;
            worker.Start();
        }
        
        protected override void DoDisconnect(string reason) {
            disconnecting = true;
            try {
                if (api != null) api.StopAsync();
                socket.Disconnect();
            } finally {
                disconnected = true;
                UnregisterEvents();
            }
        }
        
        
        void HandleEvent(JsonObject obj) {
            // actually handle the event
            string eventName = (string)obj["t"];
            if (eventName == "MESSAGE_CREATE") HandleMessageEvent(obj);
        }
        
        string ParseMessage(string input) {
            StringBuilder sb = new StringBuilder(input);
            SimplifyCharacters(sb);
            
            // remove variant selector character used with some emotes
            sb.Replace("\uFE0F", "");
            return sb.ToString();
        }
        
        string GetNick(JsonObject data) {
            if (!Config.UseNicks) return null;
            object raw;
            if (!data.TryGetValue("member", out raw)) return null;
            
            // Make sure this is really a member object first
            JsonObject member = raw as JsonObject;
            if (member == null) return null;
            
            member.TryGetValue("nick", out raw);
            return raw as string;
        }
        
        void HandleMessageEvent(JsonObject obj) {
            JsonObject data   = (JsonObject)obj["d"];
            JsonObject author = (JsonObject)data["author"];
            string channel    = (string)data["channel_id"];
            string message    = (string)data["content"];
            
            RelayUser user;
            message = ParseMessage(message);
            user    = new RelayUser();
            
            user.Nick   = GetNick(data) ?? (string)author["username"];
            user.UserID =                  (string)author["id"];            
            HandleChannelMessage(user, channel, message);
        }
        
        string GetStatus() {
            string online = PlayerInfo.NonHiddenCount().ToString();
            return Config.Status.Replace("{PLAYERS}", online);
        }        
        
        void OnReady() {
            api = new DiscordApiClient();
            api.Token = Config.BotToken;
            
            api.RunAsync();
            RegisterEvents();
        }
        
        
        void RegisterEvents() {
            OnPlayerConnectEvent.Register(HandlePlayerConnect, Priority.Low);
            OnPlayerDisconnectEvent.Register(HandlePlayerDisconnect, Priority.Low);
            OnPlayerActionEvent.Register(HandlePlayerAction, Priority.Low);
            HookEvents();
        }
        
        void UnregisterEvents() {
            OnPlayerConnectEvent.Unregister(HandlePlayerConnect);
            OnPlayerDisconnectEvent.Unregister(HandlePlayerDisconnect);
            OnPlayerActionEvent.Unregister(HandlePlayerAction);
            UnhookEvents();
        }
        
        void HandlePlayerConnect(Player p) { socket.SendUpdateStatus(); }
        void HandlePlayerDisconnect(Player p, string reason) { socket.SendUpdateStatus(); }
        
        
        public override void MessageChannel(string channel, string message) {
            message = EmotesHandler.Replace(message);
            message = ChatTokens.ApplyCustom(message);
            message = Colors.StripUsed(message);
            api.SendMessage(channel, message);
        }
        
        public override void MessageUser(RelayUser user, string message) {
            // TODO: implement this
        }
        
        void HandlePlayerAction(Player p, PlayerAction action, string message, bool stealth) {
            if (action != PlayerAction.Hide && action != PlayerAction.Unhide) return;
            socket.SendUpdateStatus();
        } 
        
        protected override bool CanUseCommands(RelayUser user, string cmdName, out string error) {
            error = null;
            return user.UserID != null && 
                operatorIds.CaselessContains(user.UserID);
        }
    }
}
