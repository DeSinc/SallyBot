using Discord.WebSocket;

namespace SallyBot
{
    internal static class MainGlobal
    {
        internal static SocketGuild Server { get; set; }
        internal static DiscordSocketClient Client { get; set; }

        internal static string conS = "Put your bot token here, between these double quotes"; PUT_YOUR_BOT_TOKEN_HERE_AND_REMOVE_THIS_WHITE_TEXT_AFTER_THE_SEMICOLON
        internal static ulong guildId = PUT_YOUR_SERVER_ID_HERE_OTHERWISE_KNOWN_AS_GUILD_ID;
    }
}
