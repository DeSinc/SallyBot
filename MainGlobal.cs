using Discord.WebSocket;
using SallyBot.Backends;

namespace SallyBot
{
    internal static class MainGlobal
    {
        internal static SocketGuild Server { get; set; }
        internal static DiscordSocketClient Client { get; set; }

        // ── Discord ────────────────────────────────────────────────────────────
        internal static string DiscordToken = "Discord bot API token goes here";
        internal static ulong GuildId = 0; // PASTE_YOUR_DISCORD_SERVER_ID_HERE

        // ── Cloud API keys (leave blank to skip that backend) ──────────────────
        internal static string GoogleApiKey = "";       // Google Gemini
        internal static string OpenAiApiKey = "";       // OpenAI
        internal static string OpenRouterApiKey = "";   // OpenRouter

        // ── Local server base URLs ─────────────────────────────────────────────
        internal static string OobaboogaUrl = "http://127.0.0.1:5000";
        internal static string LmStudioUrl  = "http://127.0.0.1:1234";
        internal static string DalaiUrl     = "http://localhost:3000";

        // ── Model overrides (optional, leave empty for server default) ─────────
        internal static string OpenAiModel      = "gpt-4o-mini";
        internal static string OpenRouterModel  = "openai/gpt-4o-mini";
        internal static string GeminiModel      = "gemini-pro";

        // ── Active backend (resolved at startup by BackendDetector) ───────────
        internal static ILlmBackend? ActiveBackend { get; set; }

        // ── Dalai (SocketIO, managed separately) ──────────────────────────────
        internal static DalaiBackend DalaiBackend { get; set; } = new DalaiBackend();
    }
}