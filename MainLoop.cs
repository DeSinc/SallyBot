using System;
using System.Threading.Tasks;

namespace SallyBot
{
    internal static class MainLoop
    {
        internal static Task StartLoop()
        {
            MainGlobal.Server = MainGlobal.Client.GetGuild(PUT_YOUR_SERVER_ID_HERE_OTHERWISE_KNOWN_AS_GUILD_ID);
            if (MainGlobal.Server != null)
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

            return Task.CompletedTask;
        }
    }
}
