/*
    Copyright 2011 MCForge
        
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
using MCGalaxy.DB;

namespace MCGalaxy.Commands.Info
{
    public sealed class CmdConnection : Command2
    {
        public override string name { get { return "Connection"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override bool UseableWhenFrozen { get { return true; } }

        public override void Use(Player p, string message, CommandData data)
        {
            if (message.Length == 0) { Help(p); return; }

            int matches;
            Player pl = PlayerInfo.FindMatches(p, message, out matches);
            if (matches > 1) return;
            if (matches == 1)
            {
                Show(p, pl.ColoredName, pl.name, pl.ip, pl.FirstLogin, pl.LastLogin, pl.LastLogout);
                p.Message(pl.ColoredName + " %Sis currently online.");
                return;
            }

            p.Message("Searching PlayerDB..");
            PlayerData target = PlayerDB.Match(p, message);
            if (target == null) return;
            Show(p, target.Name, target.Name, target.IP, target.FirstLogin, target.LastLogin, target.LastLogout);
        }

        static void Show(Player p, string name, string realname, string ip, DateTime first, DateTime last, DateTime lastd)
        {
            string rname = realname;
            string ipmsg = ip;
            name = PlayerInfo.GetColoredName(p, name);
            p.Message("%aConnection info for {0}%a:", name);

            DateTime minval = DateTime.MinValue;

            p.Message("{0} %Sis connecting as {1}", name, rname);
            p.Message("{0} %Sis connecting from IP: {1}", name, ipmsg);
            p.Message("{0} %Sfirst connected at {1:H:mm} on {1:d}", name, first);
            p.Message("{0} %Slast connected at {1:H:mm} on {1:d}", name, last);
            if (lastd == minval) {
            	p.Message("{0} %Shasn't disconnected yet", name);
            }
            else {
            	p.Message("{0} %Slast disconnected at {1:H:mm} on {1:d}", name, lastd);
            }
            
        }

        public override void Help(Player p)
        {
            p.Message("%T/Connection [player]");
            p.Message("%HShows information about a players connection");
        }
    }
}
