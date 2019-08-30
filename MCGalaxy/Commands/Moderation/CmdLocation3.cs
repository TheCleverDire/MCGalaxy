/*
    Copyright 2015 MCGalaxy team

    Dual-licensed under the    Educational Community License, Version 2.0 and
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
using MCGalaxy.Config;
using MCGalaxy.Network;

namespace MCGalaxy.Commands.Moderation {
    public class CmdLocation3 : Command2
    {
        public override string name { get { return "Location3"; } }
        public override string shortcut { get { return "loc3"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

        class GeoInfo
        {
            [ConfigString] public string summary;
            [ConfigString] public string ip;
            [ConfigString] public string proxy;
            [ConfigString] public string city;
            [ConfigString] public string subdivision;
            [ConfigString] public string country;
            [ConfigString] public string country_abbr;
            [ConfigString] public string continent;
            [ConfigString] public string continent_abbr;
            [ConfigString] public string timezone;
            [ConfigString] public string host;
        }
        static ConfigElement[] elems;

        public override void Use(Player p, string message, CommandData data)
        {
            if (message.Length == 0)
            {
                if (p.IsSuper) { SuperRequiresArgs(p, "player name or IP"); return; }
                message = p.name;
            }

            string name, ip = ModActionCmd.FindIP(p, message, "Location", out name);
            if (ip == null) return;

            if (HttpUtil.IsPrivateIP(ip))
            {
                p.Message("%WPlayer has an internal IP, cannot trace"); return;
            }

            JsonContext ctx = new JsonContext();
            using (WebClient client = HttpUtil.CreateWebClient())
            {
                ctx.Val = client.DownloadString("http://geoip.pw/api/" + ip);
            }

            JsonObject obj = (JsonObject)Json.ParseStream(ctx);
            GeoInfo info = new GeoInfo();
            if (obj == null || !ctx.Success)
            {
                p.Message("%WError parsing GeoIP info"); return;
            }

            if (elems == null) elems = ConfigElement.GetAll(typeof(GeoInfo));
            obj.Deserialise(elems, info);

            string target = name == null ? ip : "of " + PlayerInfo.GetColoredName(p, name);
            p.Message("The IP {0} %Shas been traced to: ", target);
            p.Message("  IP Address: &f{0}&S", info.ip);
            p.Message("  Summary: &f{0}&S", info.summary);
            p.Message("  Continent: &f{1}&S ({0})", info.continent_abbr, info.continent);
            p.Message("  Country: &f{1}&S ({0})", info.country_abbr, info.country);
            p.Message("  Region/State: &f{0}", info.subdivision);
            p.Message("  City: &f{0}", info.city);
            p.Message("  Time Zone: &f{0}", info.timezone);
            p.Message("  Hostname: &f{0}", info.host);
            p.Message("  Is using proxy: &f{0}", info.proxy);
            p.Message("Geoip information by: &9http://geoip.pw/");
        }

        public override void Help(Player p)
        {
            p.Message("%T/GeoIP [name/IP]");
            p.Message("%HProvides detailed output on a player or an IP a player is on.");
        }
    }
}