using System;
using System.Threading.Tasks;

namespace SallyBot
{
    internal static class MainLoop
    {
        internal static Task StartLoop()
        {
            MainGlobal.Server = MainGlobal.Client.GetGuild(MainGlobal.GuildId);
            if (MainGlobal.Server != null) // check if bot is in a server
            {
                Console.WriteLine("| Server detected: " + MainGlobal.Server.Name);
            }
            else
            {
                Console.WriteLine($"| A server has not yet been defined.");
            }

            while (MainGlobal.Server == null || MainGlobal.Server.Name == null || MainGlobal.Server.Name.Length < 1)
            {
                Console.WriteLine("| Waiting for connection to be established by Discord...");
                Task.Delay(1200);
            }

            Program.BotUserId = MainGlobal.Client.CurrentUser.Id; // <--- bot's user ID is detected and filled in automatically

            // check if there is a nickname to set
            Program.BotName = MainGlobal.Server.GetUser(Program.BotUserId).Nickname ??
                              // otherwise, just use username if there is no nickname
                              MainGlobal.Server.GetUser(Program.BotUserId).Username;

            return Task.CompletedTask;
        }
    }
}
