# SallyBot
AI Chatbot coded in Discord.net C#

## USAGE

Download all files and put them in a folder ideally called SallyBot or something, you can try rename it if you dare risk the visual studio glitches

Double click sallybot.csproj and open with Visual Studio

It should open up the whole project and make a .sln file etc.

If you (don't have a bot already)

{

* Follow a quick youtube tutorial on creating a new Discord bot in their developer portal and make an API key (only takes about 5-10 mins)

* Make sure you enable message intents and other intents for this bot or you won't see any message content etc. Use this guide: https://autocode.com/discord/threads/what-are-discord-privileged-intents-and-how-do-i-enable-them-tutorial-0c3f9977/

* Join the bot to your server, follow this guide if you don't know how: https://discordjs.guide/preparations/adding-your-bot-to-servers.html#bot-invite-links

}

Put your bot API key in the MainGlobal.cs file

Press F5 to build it and run and see what happens (it should work first try)

It has a line of code to reply to any message it sees, feel free to remove this after testing the bot is up and running

If it doesn't idk lmao. Google will be your greatest ally in this battle. have fun


## AI text generator

This bot doesn't generate the AI text but just sends requests off to a language learning model of your choice. At the moment I made the requests send out in the format for Dalai Alpaca 7B because that's easy to set up, but you can change the format to other LLMs in the bot code.

It's this line:

var request = new
            {
                seed = -1,
                threads = 4, <--(btw you can change this to your thread count minus 2 for more speed)
                
etc..
