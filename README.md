# SallyBot
AI Chatbot that uses locally-ran large language models to talk to you (No Chat-GPT! Runs on your PC! It even runs on a raspberry pi 4B!)

It also utilises [Stable Diffusion](https://github.com/AUTOMATIC1111/stable-diffusion-webui) to take real selfies, running on your local GPU

Coded in Discord.net C#

Context: [I Made a Discord Chat Bot that Can Take Selfies](https://www.youtube.com/watch?v=KM4a7RGG270)  
[![](https://markdown-videos.deta.dev/youtube/KM4a7RGG270)](https://youtu.be/KM4a7RGG270)

### Supported LLM interfaces:

[Oobabooga Text Generation Web UI](https://github.com/oobabooga/text-generation-webui)  
[Dalai Alpaca](https://github.com/cocktailpeanut/dalai)

### Examples

![Image showing examples of SallyBot](https://user-images.githubusercontent.com/12345584/230606279-cb741c83-ebb9-4e4f-9754-67bee57d1540.png)

## USAGE

Either clone the repository using git (look up how to do so if unsure)

or

Click the green 'Code' drop-down at the top right corner of this webpage and select 'Download ZIP' and place the contents into a folder called SallyBot (changing the name risks issues with the Visual Studio environment)

Download and install Visual Studio Community WITH C# dotNET development modules ticked. Look up guides if you are confused - when installing Visual Studio Community, it will ask with big squares with tickboxes in the corners which types of coding packages you want to install. Pick the C#/Dot NET ones.

Double click on the sallybot.sln file and open with Visual Studio Community

(If that doesn't work try open sallybot.csproj with Visual Studio Community. It should open up the whole project and make a .sln file, etc.)

(If that doesn't work you likely need to install the C# dotNET packages with the Visual Studio Installer window. You can run this by typing Visual Studio Installer into the start menu and hitting enter.)

If you don't have a bot already:

* Create a new Discord bot on the Discord Developer Portal and make an API key (takes about 2 mins) https://discord.com/developers/applications

* Make sure you enable message intents and other intents for this bot or you won't see any message content etc.
Use this guide: https://autocode.com/discord/threads/what-are-discord-privileged-intents-and-how-do-i-enable-them-tutorial-0c3f9977/
![Image showing Discord app permissions](https://user-images.githubusercontent.com/11000195/230468248-10b014c7-db1e-4c33-96ef-5305c24c7b27.png)

* Join the bot to your server, follow this guide if you don't know how: https://discordjs.guide/preparations/adding-your-bot-to-servers.html#bot-invite-links

Put your bot API key and your server's ID in the MainGlobal.cs file (they will error out until you fill them in to show you where to put them)

Press F5 to build it and run and see what happens (it should work first try)

## Using Oobabooga Text Generation Webui -- Runs on the GPU - much faster - OR the CPU depending on what you choose in the installer

Warning: For the average 7B model, it requires a card with at least 6GB of VRAM. If you're running on CPU you need at least 6-8GB of ram.

If you're willing to run on the inferior smaller parameter count models like Pythia-2.8B-deduped or something then it'll work on less RAM/VRAM, but the output is untested and frankly likely to be bad.

Download and install [Oobabooga from their repo here](https://github.com/oobabooga/text-generation-webui). Their README has information on how to install it, with two methods: 1-click installer and manual. I heavily recommend the 1-click installer.

After downloading and unzipping the 1-click installer, run `start_windows.bat`. The installer is interactive and you will need to enter your GPU device when prompted, or select CPU mode. I do not recommend CPU mode.

Once the install script is installed, you will need to download a model.

Search for a language model yourself on HuggingFace. Once you have found one to your liking, copy the username/modelname.

Here's an example of what a username/modelname looks like on HuggingFace:

![Example of a model on HuggingFace](https://github.com/DeSinc/SallyBot/assets/12345584/987c2e91-4fd2-4f82-8eb1-fbf63db76abb)

You then download the model with the web interface:
* Open a browser and go to 127.0.0.1:7682 in the URL address bar and hit enter
* Click the 'Models' tab at the top of the page
* Paste the ``UserName/ModelName`` of the model to download from Huggingface in the Download Model box
* Hit 'Download' and wait for the model to finish downloading
* Click the little blue 'Refresh' icon next to the model selection drop-down box at the top left corner of the webpage
* Select the model from the drop-down list, as it should now be present

After the installing has finished you need to set up the arguements in the `webui.py` file to allow SallyBot to communicate with Oobabooga. It can be found near the top of the file.

![Showing where to input args](https://github.com/DeSinc/SallyBot/assets/36467674/a7c6e8b0-6644-4c73-878b-9b2cb44c1d3a)

Arguments to be added here include:

`--chat` sets the web interface to chat mode which is better. This arg isn't necessary it just makes the web interface look nicer if you choose to browse to it on localhost:7862 in your web browser.

`--model <folder_name_of_model>` specifies which model Oobabooga should use, replace `<folder_name_of_model>` it is the name of the folder in text-generation-webui/models.

`--api` tells Oobabooga to allow SallyBot to integrate together.

`--loader exllama_hf` uses the much updated ExLLAMA model loader which is literally nearly 2x faster than the previously used loader. Might already be default by the time you see and run this.

`--listen-port 7862` is set to 7862 to not overlap with stable diffusion. `--api` opens a separate port for sallybot to interface with which runs on port 5000. Port 7862 can still be used to view the web interface if you like.

`--xformers` is a very good optimiser that reduces your vram usage for free. This argument is not required but very encouraged. It needs to be installed into Oobabooga to use. Run `cmd_windows.bat` and type `pip install xformers`, when it is done you can type exit.

`--wbits 4` and `--groupsize 128` specify details about the model. If you know what you're doing you can remove whichever ones you don't need. `--groupsize 128` if you are using a non 128 groupsize model, or `--wbits 4` if you are not running a 4-bit quantized model, for instance. Most of the consumer running ones are 4bit quantized to run on normal amounts of vram, so you'll need this arg to run those models.

Example of args:
`--model TheBloke_Llama-2-7B-Chat-GGML --loader exllama_hf --api --listen-port 7862 --wbits 4 --groupsize 128`

![Image showing example of args](https://github.com/DeSinc/SallyBot/assets/117759431/23ed964c-13c3-4984-ad74-0f5e1657c67e)

If you'd like to modify the parameters for Oobabooga, it's this section here:
```c#
var parameters = new
{
    prompt = oobaboogaInputPrompt,
    max_new_tokens = tokenCount,
    do_sample = false,
    temperature = 0.5,
    top_p = 0.1,
    typical_p = 1,
    repetition_penalty = 1.18,
    encoder_repetition_penalty = 1,
    top_k = 40,
    num_beams = 1,
    penalty_alpha = 0,
    min_length = 0,
    length_penalty = 1,
    no_repeat_ngram_size = 0,
    early_stopping = true,
    stopping_strings = new string[] { "\n[", "\n>", "]:", "\n#", "\n##", "\n###", "##", "###", "000000000000", "1111111111", "0.0.0.0.", "1.1.1.1.", "2.2.2.2.", "3.3.3.3.", "4.4.4.4.", "5.5.5.5.", "6.6.6.6.", "7.7.7.7.", "8.8.8.8.", "9.9.9.9.", "22222222222222", "33333333333333", "4444444444444444", "5555555555555", "66666666666666", "77777777777777", "888888888888888", "999999999999999999", "01010101", "0123456789", "<noinput>", "<nooutput>" },
    seed = -1,
    add_bos_token = true,
    ban_eos_token = false,
    skip_special_tokens = true
};
```
(From Program.cs in the OobaboogaReply() function)

## AI Text Generation with Dalai Alpaca (Run on the CPU) -- Easier to install but has long-standing BUGS that are not getting fixed (project seems abandoned)

This bot doesn't generate the AI text but just sends requests off to a language learning model of your choice. At the moment I made the requests send out in the format for [Dalai Alpaca 7B](https://github.com/cocktailpeanut/dalai)

Just install [this](https://nodejs.org/en/download) first and then follow the quick and easy 2-step instructions on the Dalai github link above. The bot will automatically connect and start sending Dalai requests when you ping the bot.

If you wish to modify the LLM parameters, it's this section here for Dalai:
```c#
var dalaiRequest = new
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

## Other AI text generators as yet unsupported

If you're using another AI text generator, check its github page for instructions on how to format the data and change the format of the request to what it needs. You might also need to change the way it sends the request in, which could be a lot of code changes depending. This bot sends via SocketIO to Dalai Alpaca which is the easiest to set up imo and runs on anything with very good speed. I mean anything. It runs on a raspberry pi 4B. Some guy got it running on a texas instruments calculator I heard. You still need 4gb of ram for the model to load though.

## Generate images with Stable Diffusion (runs on the GPU and needs probably minimum 4GB vram, more like 6GB to not have to hack around getting it to work)
Download [stable-diffusion-webui](https://github.com/AUTOMATIC1111/stable-diffusion-webui)  
Follow their installation steps, then find either `webui-user.bat` on Windows or `webui-user.sh` on Linux.  
Edit the file and modify the `COMMANDLINE_ARGS` line.

Windows:
```bat
set COMMANDLINE_ARGS=--api --xformers
```
Linux:
```sh
export COMMANDLINE_ARGS=--api --xformers
```
Save the file and run it.  The API is now ready to receive request right from SallyBot. If you get an error with xformers just remove --xformers from the args and save the file and run it again.

It will send it to a default model of some kind, most likely it comes with Stable Diffusion 1.5 model which is a real-life images model.

Go to Civitai (warning: look out for NSFW imagery if you make an account) and then look for an anime model you like on the homepage.

Download that model, put it in \models\stablediffusion\ where the text file is that says "Place checkpoints here.txt"

Open the stable diffusion Web ui by going to localhost:7860 in your web browser url bar

Select the drop-down at the top left and pick the model you just downloaded, or the one you want to try out.

Now the next image request you send from SallyBot will be in that model.

## Known Issues

### Stable Diffusion needs to use an older version of Python.  Follow the steps in their repo and install Python 3.10 making sure to add it to your system PATH.

Afterwards, assuming you did not change the default install location, modify the `PYTHON` line in your `webui-user.bat` file.

```bat
set PYTHON="%LOCALAPPDATA%\Programs\Python\Python310\python.exe"
```

### Dalai Alpaca needs to be run in Command Prompt (cmd.exe) and not PowerShell (powershell.exe).

With Windows 11, Microsoft made PowerShell the default terminal, make sure to use Command Prompt to start it instead, an easy way to do that is `WIN + R` `cmd.exe` and then use `cd` to navigate to the Dalai directory.

### Emoji Psychosis / Hashtag Psychosis

Oobabooga has an issue at the moment with its default built-in API (this is why you use ``--extension api``) where for some reason any data that is sent in the request ends up not setting params correctly and somewhere somehow this is causing the bot to want to spam ever lengthening repeating twitter hashtags and long long strings of emojis incessantly.

The result of these params being set wrong is that the bot enters a state known only as Hashtag Psychosis. If the bot sees a single hashtag anywhere it will begin to put a few hashtags on the end of their msg.. then 4.. then 10... and it sees its own messages in the history and causes it to spiral and become actually unusable.

~~No known cure exists~~ Edit: the cure is to remove the leading space on your prompt since ooba seemed to add its own space and cause a double space, making it see emojis as the next likely character for some reason. probably already fixed nowadays though. this is old bug I just left here because it's funny

### Dalai tends to ramble even after your bot has already sent the message.

The reason for this is that there is no proper working stop command built into dalai. There is one they tried to make, but it crashes the dalai server every 2nd or 3rd time you run it so in my view it's not working.

I built my own stop command into the source code myself. Not only does it work every time without crashing, it's also much faster and stops the rambling instantly, compared to their weak stop command that lets it ramble for like 5+ whole seconds after you told it to stop.

It's just 5 easy steps to get it working on your system too!

1. download the dalai source code from that cocktailpeanut github linked above
2. unzip it to a folder somewhere ***ON C DRIVE ON C DRIVE ON C DRIVE IT DOES NOT WORK ON ANY OTHER DRIVE LETTER*** (I suggest putting it next to the c:\users\username\dalai folder. mine is C:\Users\Dean\dalaiDS)

![image](https://user-images.githubusercontent.com/12345584/230718163-c513acf9-e174-4941-a73b-c2c6dd0f87bb.png)

3. replace the index.js file with my special index.js file here: https://pastebin.com/A3bpWhTG
4. navigate a command prompt to this custom source dalai folder we just unzipped with the custom index.js file in it (by typing "cd" and then the custom folder. like this: ``cd c:\downloads\dalaiCustomSource``
5. run the ``npx dalai serve`` command from command prompt within this folder

NOTE: it will do this purple IndexTree thingie every time you start the server with my custom code:

![image](https://user-images.githubusercontent.com/12345584/230718248-72b5720a-99ec-4dcd-85a1-4e167363ea0a.png)

I don't know why but it just does. It takes like 20-40 seconds at worst. Small price to pay for a working stop command that actually stops the bot ramblin without crashing the server.

That's it! After it starts up, you are ready to run the bot and query some LLMs!
