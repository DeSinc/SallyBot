using Discord.WebSocket;

namespace SallyBot
{
    internal static class MainGlobal
    {
        internal static SocketGuild Server { get; set; }
        internal static DiscordSocketClient Client { get; set; }

        internal static string conS = "Discord bot API token goes here";
        internal static ulong guildId = PASTE_YOUR_DISCORD_SERVER_ID_HERE;

        // optional if you want to use Google's Gemini Pro model
        internal static string googleApiKey = "Google API key goes here"; // OPTIONAL (you can leave this as-is if you don't want to use it)
    }
}