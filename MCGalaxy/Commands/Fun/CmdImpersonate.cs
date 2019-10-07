using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MCGalaxy.Commands.Fun
{
    public sealed class CmdImpersonate : Command2
    {
        public override string name { get { return "Impersonate"; } }
        public override string shortcut { get { return "imp"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Nobody; } }

        public override void Use(Player p, string message, CommandData data)
        {
            string[] args = message.SplitSpaces(2);
            if (args.Length == 1) { Help(p); return; }
            
            Player who = PlayerInfo.FindMatches(p, args[0]);

            if (who == null) { Help(p); return; }
            if (who.muted) { Player.Message(p, "Cannot impersonate a muted player"); return; }

            if (CheckRank(p, data, who, "impersonate", false))
            {
                Chat.MessageChat(who, "λFULL: &f" + args[2], null, true);
            }
        }

        public override void Help(Player p)
        {
            Player.Message(p, "%T/Impersonate [player] [message]");
            Player.Message(p, "%HSends a message as if it came from [player]");
        }
    }
}
