using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MCGalaxy.Commands.Fun
{
    public sealed class CmdPlayerClone : Command2
    {
        public override string name { get { return "playerclone"; } }
        public override string shortcut { get { return "clone"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Nobody; } }

        public override void Use(Player p, string message, CommandData data)
        {
            string[] args = message.SplitSpaces(2);
            if (args.Length == 1) { Help(p); return; }

            Player who = PlayerInfo.FindMatches(p, args[0]);

            if (who == null) { Help(p); return; }
            if (who.muted) { Player.Message(p, "Cannot impersonate a muted player"); return; }

            if (CheckRank(p, data, who, "playerclone", false))
            {
                Boolean clone = false;
                if (clone == false) {
                    Command.Find("Skin").Use(who, args[1]);
                    Command.Find("Nick").Use(who, args[1]);
                    clone = true;
                }
                else {
                    Command.Find("Skin").Use(p, args[1]);
                    Command.Find("Nick").Use(p, args[1]);
                    clone = false;
                }
            }
        }

        public override void Help(Player p)
        {
            Player.Message(p, "%T/Impersonate [player] [message]");
            Player.Message(p, "%HSends a message as if it came from [player]");
        }
    }
}
