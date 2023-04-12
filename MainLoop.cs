using System;
using System.Threading.Tasks;

namespace SallyBot
{
    internal static class MainLoop
    {
        internal static Task StartLoop()
        {
            MainGlobal.Server = MainGlobal.Client.GetGuild(MainGlobal.guildId);
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
                var delayVar = Task.Delay(1200);
                delayVar.Wait();
                Console.WriteLine($"| Waiting for connection to be established by Discord...");
            }

            Program.botUserId = MainGlobal.Client.CurrentUser.Id; // <--- bot's user ID is detected and filled in automatically

            if (MainGlobal.Server.GetUser(Program.botUserId).Nickname != null) // check if there is a nickname to set
            Program.botName = MainGlobal.Server.GetUser(Program.botUserId).Nickname;
            else                                                               // otherwise, just use username if there is no nickname
                Program.botName = MainGlobal.Server.GetUser(Program.botUserId).Username;

            return Task.CompletedTask;
        }
    }
}
