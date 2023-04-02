using System.Collections.Generic;

using Discord.WebSocket;

namespace SallyBot
{
    internal static class MainGlobal
    {
        internal static SocketGuild Server { get; set; }
        internal static DiscordSocketClient Client { get; set; }
        
        internal static int ClientHash { get; set; } // you can use this to detect which idk I forgot mid sentence while I was typing why I put this here I think I use it to validate client's session so a subsequent run of the program can't access the db while the prior one is still going? idk
        
        internal static string conS = "BOT_TOKEN_HERE";
    }
}
