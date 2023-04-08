# SallyBot
AI Chatbot that uses locally-ran large language models to talk to you (not chat-GPT, offline running on your system) and [Stable Diffusion](https://github.com/AUTOMATIC1111/stable-diffusion-webui) to take real selfies

### Supported LLM interfaces:

[Oobabooga Text Generation Web UI](https://github.com/oobabooga/text-generation-webui)

[Dalai Alpaca](https://github.com/cocktailpeanut/dalai)

Coded in Discord.net C#  

Context: [I Made a Discord Chat Bot that Can Take Selfies](https://www.youtube.com/watch?v=KM4a7RGG270)  
[![](https://markdown-videos.deta.dev/youtube/KM4a7RGG270)](https://youtu.be/KM4a7RGG270)

### Examples

![image](https://user-images.githubusercontent.com/12345584/230606279-cb741c83-ebb9-4e4f-9754-67bee57d1540.png)

## USAGE

Clone the repo into a folder called SallyBot (changing the name risks issues with the Visual Studio environment)

Double click sallybot.csproj and open with Visual Studio

It should open up the whole project and make a .sln file, etc.

If you don't have a bot already:

* Create a new Discord bot on the Discord Developer Portal and make an API key (takes about 2 mins) https://discord.com/developers/applications
            
* Make sure you enable message intents and other intents for this bot or you won't see any message content etc.  
Use this guide: https://autocode.com/discord/threads/what-are-discord-privileged-intents-and-how-do-i-enable-them-tutorial-0c3f9977/
![image](https://user-images.githubusercontent.com/11000195/230468248-10b014c7-db1e-4c33-96ef-5305c24c7b27.png)

* Join the bot to your server, follow this guide if you don't know how: https://discordjs.guide/preparations/adding-your-bot-to-servers.html#bot-invite-links

Put your bot API key in the MainGlobal.cs file

Put your server's ID into the MainLoop.cs file

Press F5 to build it and run and see what happens (it should work first try)

There is a disabled line of code that you can enable which replies to any message the bot sees. This can be used to test if the bot works in your server by sending a message back to you. CTRL + F search for "Hello!!" to find that commented-out line of code, and use it/remove it/change it however you like.

If it does not repeat your message back to you, then message intents might not be enabled. See the steps above with pictures to fix that.

Feel free to use or remove this line of code entirely after testing the bot is up and running and able to read your messages.

## AI Text Generation with Dalai Alpaca

This bot doesn't generate the AI text but just sends requests off to a language learning model of your choice. At the moment I made the requests send out in the format for [Dalai Alpaca 7B](https://github.com/cocktailpeanut/dalai) because that's easy to set up, but you can change the format to other LLMs in the bot code.

It's this line:
```c#
    var request = new
    {
        seed = -1,
        threads = 4,   <--(btw, change this to your thread count minus 2 for more speed)
        n_predict = 200,
        top_k = 40,
        top_p = 0.9,
        temp = 0.8,
        repeat_last_n = 64,
        repeat_penalty = 1.1,
        debug = false,
        model = "alpaca.7B",

        prompt = inputPrompt
    };          
```

If you're using another AI text generator, check its github page for instructions on how to format the data and change the format of the request to what it needs. You might also need to change the way it sends the request in, which could be a lot of code changes depending. This bot sends via SocketIO to Dalai Alpaca which is the easiest to set up imo and runs on anything with very good speed. I mean anything. It runs on a raspberry pi 4B.

## Generate images with Stable Diffusion
Download [stable-diffusion-webui](https://github.com/AUTOMATIC1111/stable-diffusion-webui)  
Follow their installation steps, then find either `webui-user.bat` on Windows or `webui-user.sh` on Linux.  
Edit the file and modify the `COMMANDLINE_ARGS` line.

Windows:
```bat
set COMMANDLINE_ARGS="--api"
```
Linux:
```sh
export COMMANDLINE_ARGS="--api"
```
Save the file and run it.  The API is now ready to receive request right from Sally!

## Known Issues

1. Stable Diffusion needs to use an older version of Python.  Follow the steps in their repo and install Python 3.10 making sure to add it to your system PATH.  
Afterwards, assuming you did not change the default install location, modify the `PYTHON` line in your `webui-user.bat` file.  
```bat
set PYTHON="%LOCALAPPDATA%\Programs\Python\Python310\python.exe"
```

2. Dalai Alpaca needs to be run in Command Prompt (cmd.exe) and not PowerShell (powershell.exe).  
With Windows 11, Microsoft made PowerShell the default terminal, make sure to use Command Prompt to start it instead, an easy way to do that is `WIN + R` ``cmd.exe` and then use `cd` to navigate to the Dalai directory.

3. Dalai Alpaca tends to ramble even after your bot has already sent the message.

The reason for this is that there is no proper working stop command built into dalai. There is one they tried to make, but it crashes the dalai server every 2nd or 3rd time you run it so in my view it's not working.

I built my own stop command into the source code myself. Not only does it work every time without crashing, it's also much faster and stops the rambling instantly, compared to their weak stop command that lets it ramble for like 5+ whole seconds after you told it to stop.

It's just 5 easy steps to get it working on your system too!

1. download the dalai source code from that cocktailpeanut github linked above
2. unzip it to a folder somewhere
3. replace the index.js file with my special index.js file here: https://pastebin.com/A3bpWhTG
4. navigate a command prompt to this custom source dalai folder we just unzipped with the custom index.js file in it (by typing "cd" and then the custom folder. like this: ``cd c:\downloads\dalaiCustomSource``
5. run the ``npx dalai serve`` command from command prompt within this folder 

then it will work
