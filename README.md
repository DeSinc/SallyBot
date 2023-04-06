# SallyBot
AI Chatbot that takes selfies for you with Stable Diffusion, using the Automatic1111 webui with --api flag set (download and edit the launch batch file and add --api to the batch file and then just double click to run it)

Text generation is set up atm using the Dalai Alpaca model (2 command setup, instructions on github)

Coded in Discord.net C#

## USAGE

Download all files and put them in a folder ideally called SallyBot or something, you can try rename it if you dare risk the visual studio glitches

Double click sallybot.csproj and open with Visual Studio

It should open up the whole project and make a .sln file etc.

If you don't have a bot already:

* Create a new Discord bot on their developer portal and make an API key (takes about 2 mins) https://discord.com/developers/applications
            
* Make sure you enable message intents and other intents for this bot or you won't see any message content etc. Use this guide: https://autocode.com/discord/threads/what-are-discord-privileged-intents-and-how-do-i-enable-them-tutorial-0c3f9977/
* Join the bot to your server, follow this guide if you don't know how: https://discordjs.guide/preparations/adding-your-bot-to-servers.html#bot-invite-links

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
        threads = 4,   <--(btw, change this to your thread count minus 2 for more speed)
                
etc..

If you're using another AI text generator, check its github page for instructions on how to format the data and change the format of the request to what it needs. You might also need to change the way it sends the request in, which could be a lot of code changes depending. This bot sends via SocketIO to Dalai Alpaca which is the easiest to set up imo and runs on anything with very good speed. I mean anything. It runs on a raspberry pi 4B.

## Automatic1111 webui with --api flag set

Google it and github download it, and then set the --api flag in the web-user.bat file by right click edit to open it in notepad. Then save and run the batch file and the api is now ready to receive requests right from the bot!
