using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Regex;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using SocketIOClient;
using Newtonsoft.Json.Linq;
using System.IO;
using RestSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Text.Json;

// NOTE - I'm not 100% sure if all of these are needed for SallyBot. Some of these might be for my own code that is not included in this file. Just check to make sure.

namespace SallyBot
{
    class Program
    {
        private static Timer Loop;

        private SocketIO Socket;
        private DiscordSocketClient Client;

        static internal bool dalaiConnected = false;
        static internal int thinking = 0;
        static internal int typing = 0;
        static internal int typingTicks = 0;

        static internal ulong botUserId = 0; // <-- this is your bot's client ID number inside discord (not the token) and gets set in MainLoop after initialisation

        static internal string botName = "SallyBot";

        static internal string oobaboogaChatHistory = string.Empty; // <-- chat history saves to this string over time
        static internal bool chatHistoryDownloaded = false; // Records if you have downloaded chat history before so it only downloads message history once.

        static internal string oobaboogaInputPromptStart = $"### Instruction:\r\n" +
                $"Write the next message in this Discord chat room as a girl.\r\n";
        static internal string oobaboogaInputPromptEnd = $"### Single message response:\r\n" +
                $"[{botName}]: ";

        static internal string oobaboogaInputPromptStartPic = $"\n### After describing the image, {botName} must then send a message talking about the picture she just took." + // or pick this prompt ending to request a picture
            $"\n### Write a short list of things in the photo followed by a new message from SallyBot on a new line talking about the image: ";

        static internal string inputPromptEnding = $"\n[{botName}]: ";
        static internal string inputPromptEndingPic = $"\nAfter describing the image she took, {botName} may reply." +
            $"\nNouns of things in the photo: ";

        static internal string botReply = string.Empty;

        static internal string token = string.Empty;

        static void Main()
                => new Program().AsyncMain().GetAwaiter().GetResult();

        private async Task AsyncMain()
        {
            //AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
            //{
            //    Console.WriteLine(eventArgs.Exception.ToString());
            //};

            try
            {
                Client = new DiscordSocketClient(new DiscordSocketConfig
                {
                    MessageCacheSize = 1200,
                    LogLevel = LogSeverity.Debug,
                    AlwaysDownloadUsers = true,
                    GatewayIntents =
                GatewayIntents.MessageContent |
                GatewayIntents.Guilds |
                GatewayIntents.GuildMessages
                });

                Client.Log += Client_Log;
                Client.Ready += MainLoop.StartLoop;
                Client.MessageReceived += Client_MessageReceived;

                await Client.LoginAsync(TokenType.Bot, MainGlobal.conS);

                await Client.StartAsync();

                Loop = new Timer()
                {
                    Interval = 5900,
                    AutoReset = true,
                    Enabled = true
                };
                Loop.Elapsed += Tick;

                Console.WriteLine($"|{DateTime.Now} | Main loop initialised");

                MainGlobal.Client = Client;

                // Connect to the LLM with SocketIO (fill in your particular LLM server details here)
                try
                {
                    // Initialize the Socket.IO connection
                    Socket = new SocketIO("http://localhost:3000");
                    Socket.OnConnected += async (sender, e) =>
                    {
                        Console.WriteLine("Connected to LLM server.");
                        dalaiConnected = true;
                    };

                    Socket.ConnectAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }

                await Task.Delay(-1);

                AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
                {
                    Exception ex = eventArgs.Exception;
                    Console.WriteLine($"\u001b[45;1m[  DISC  ]\u001b[41;1m[  ERR  ]\u001b[0m MSG: {ex.Message} \n WHERE: {ex.StackTrace} \n\n");
                };
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }

        private async Task Client_Log(LogMessage Msg)
        {
            if (!Msg.Message.ToString().Contains("PRESENCE_UPDATE")
              && !Msg.Message.ToString().Contains("TYPING_START")
              && !Msg.Message.ToString().Contains("MESSAGE_CREATE")
              && !Msg.Message.ToString().Contains("MESSAGE_DELETE")
              && !Msg.Message.ToString().Contains("MESSAGE_UPDATE")
              && !Msg.Message.ToString().Contains("CHANNEL_UPDATE")
              && !Msg.Message.ToString().Contains("GUILD_")
              && !Msg.Message.ToString().Contains("REACTION_")
              && !Msg.Message.ToString().Contains("VOICE_STATE_UPDATE")
              && !Msg.Message.ToString().Contains("DELETE channels/")
              && !Msg.Message.ToString().Contains("POST channels/")
              && !Msg.Message.ToString().Contains("Heartbeat")
              && !Msg.Message.ToString().Contains("GET ")
              && !Msg.Message.ToString().Contains("PUT ")
              && !Msg.Message.ToString().Contains("Latency = "))
                Console.WriteLine($"|{DateTime.Now} - {Msg.Source}| {Msg.Message}");
        }

        private static async void Tick(object sender, ElapsedEventArgs e)
        {
            if (typing > 0)
            {
                typing--;       // Lower typing tick over time until it's back to 0 - used below for sending "Is typing..." to discord.
                typingTicks++;  // increase tick by 1 per tick. Each tick it gets closer to the limit you choose, until it exceeds the limit where you can tell the bot to interrupt the code.
            }
            if (thinking > 0)
            {
                thinking--;     // this thinking value, while above 0, stops all other user commands from coming in. Lowers over time until 0 again, then accepting requests.
                if (thinking == 0)
                {
                    if (token == string.Empty)
                    {
                        dalaiConnected = false; // not sure if Dalai server is still connected at this stage, so we set this to false to try other LLM servers like Oobabooga.
                        Console.WriteLine();
                        Console.WriteLine("No data was detected from any Dalai server. Is it switched on?"); // bot is cleared for requests again.
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("Thinking timeout"); // bot is cleared for requests again.
                    }
                }
            }
        }

        private async Task Client_MessageReceived(SocketMessage MsgParam)  // this fires upon receiving a message in the discord
        {
            try
            {
                var Msg = MsgParam as SocketUserMessage;
                var Context = new SocketCommandContext(Client, Msg);
                var user = Context.User as SocketGuildUser;

                var contextChannel = Context.Channel as SocketGuildChannel; // used if you want to select a channel for the bot to ignore or to only pay attention to

                if (Msg.Author.IsBot) return; // don't listen to bot messages, including itself

                //if (Msg.MentionedUsers.Contains(MainGlobal.Server.GetUser(botUserId))) // only run the code if you mentioned the bot
                //                                                                       // && (contextChannel.Id == channel_id_here) // you can uncomment this if you want it to only see one channel. put in the channel ID there.
                //{
                //    thinking = 2; // set thinking to 2 to make sure no new requests come in while it is generating (it scrambles the outputs together)
                //    await LlamaReply(Msg, Context); // run the LlamaReply function to reply to the user's message
                //    await Context.Channel.SendMessageAsync($"{user.Mention} said {Msg.Content} in {contextChannel}! Hello!! Ping me to get a LLM response from your LLM! You can remove this line of code to stop me talking."); // remove this once you confirm the bot is working
                //}

                if (Msg.MentionedUsers.Contains(MainGlobal.Server.GetUser(botUserId))) // these are my specific test channels - remove these lines if they're still here, or replace with your channel IDs
                {
                    if (dalaiConnected)
                    {
                        try
                        {
                            if (thinking <= 0 && typing <= 0)
                                await DalaiReply(Msg); // dalai chat
                        }
                        catch
                        {
                            Console.WriteLine("Could not connect to Dalai server. Attempting to send an Oobaboga request...");
                        }
                    }
                    else
                    {
                        try
                        {
                            await OobaboogaReply(Msg); // run the OobaboogaReply function to reply to the user's message with an Oobabooga chat server message
                        }
                        catch
                        {
                            Console.WriteLine("Could not connect to Oobabooga server. Attempting Dalai request...");
                            if (thinking <= 0 && typing <= 0)
                                await DalaiReply(Msg); // run the DalaiReply function to reply to the user's message with a Dalai chat server message
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }

        private async Task OobaboogaReply(SocketMessage message)
        {
            var Msg = message as SocketUserMessage;

            List<string> bannedWords = new List<string>
                {
                    // Add your list of banned words here
                    "butt", "bum", "booty", "nudity", "naked"
                };

            string takeAPicRegexStr = @"\b(take|paint|generate|make|draw|create|show|give|snap|capture|send|display|share|shoot|see|provide|another)\b.*(\S\s{0,10})?(image|picture|painting|pic|photo|portrait|selfie)\b";
            Regex takeAPicRegex = new Regex(takeAPicRegexStr, RegexOptions.IgnoreCase);

            string msgUsernameClean = Regex.Replace(Msg.Author.Username, "[^a-zA-Z0-9]+", "");

            string promptEndDetectionRegexStr = @"[\n|\r|\r\n]([^\\.|^\\\-|^\\*|)\n]{2})|(\[end|<end|]:|>:|\[human|\[chat|\[sally|\[cc|<chat|<cc|\[@chat|\[@cc|bot\]:|<@chat|<@cc|\[.*]: |\[.*] : |\[[^\]]+\]\s*:)";
            Regex promptEndDetectionRegex = new Regex(promptEndDetectionRegexStr, RegexOptions.IgnoreCase);

            string inputMsg = Msg.Content
                .Replace("\n", "")
                .Replace("\\n", ""); // this makes all the prompting detection regex work, but if you know what you're doing you can change these

            // cut out attempts from users to manufacture fake prompt endings/chat formatting to confuse the bot
            inputMsg = Regex.Replace(inputMsg, @"(<[#|@|\/][^<>]+>)|\[[^\]]+[\]:\\]\:|\:\]|\[^\]]", "");

            // check if the user is requesting a picture or not
            bool takeAPicMatch = takeAPicRegex.IsMatch(inputMsg);

            // find the local time in japan right now to change the time of day in the selfie
            // (you can change this to another country if you understand the code)
            DateTime currentTimeInJapan = GetCurrentTimeInJapan();
            string timeOfDayInNaturalLanguage = GetTimeOfDayInNaturalLanguage(currentTimeInJapan);

            // Create the message line that the bot will see
            string inputPrompt = $"[{msgUsernameClean}]: {inputMsg}";

            //// you can use this if you want to trim the messages to below 500 characters each
            //// (prevents hacking the bot memory a little bit)
            //if (inputMsg.Length > 300)
            //{
            //    inputMsg = inputMsg.Substring(0, 300);
            //    Console.WriteLine("Input message was too long and was truncated.");
            //}

            // downloads recent chat messages and puts them into the bot's memory
            if (chatHistoryDownloaded == false)
            {
                chatHistoryDownloaded = true; // only do this once per program run to load msges into memory
                var last100Msges = await Msg.Channel.GetMessagesAsync(20).FlattenAsync();
                foreach (var msg in last100Msges)
                {
                    oobaboogaChatHistory += $"[{msg.Author.Username}]: {msg.Content}\n";
                }
            }
            string oobaBoogaChatHistoryDetectedWords = IsSimilarToBannedWords(oobaboogaChatHistory, bannedWords, 0);

            if (oobaBoogaChatHistoryDetectedWords.Length > 2) // Threshold set to 2
            {
                foreach (string word in oobaBoogaChatHistoryDetectedWords.Split(' '))
                {
                    string wordTrimmed = word.Trim();
                    if (wordTrimmed.Length > 2)
                    {
                        oobaBoogaChatHistoryDetectedWords = oobaBoogaChatHistoryDetectedWords.Replace(wordTrimmed, "");

                        if (oobaBoogaChatHistoryDetectedWords.Contains("  "))
                            oobaBoogaChatHistoryDetectedWords = oobaBoogaChatHistoryDetectedWords.Replace("  ", " ");
                    }
                }
                Console.WriteLine("Oobabooga chat history contained bad or similar to bad words and all have been removed.");
            }

            string inputPromptDetectedWords = IsSimilarToBannedWords(inputPrompt, bannedWords, 0);

            if (inputPromptDetectedWords.Length > 2) // Threshold set to 2
            {
                foreach (string word in inputPromptDetectedWords.Split(' '))
                {
                    string wordTrimmed = word.Trim();
                    if (wordTrimmed.Length > 2)
                    {
                        inputPrompt = inputPrompt.Replace(wordTrimmed, "");

                        if (inputPrompt.Contains("  "))
                            inputPrompt = inputPrompt.Replace("  ", " ");
                    }
                }
                Console.WriteLine("Message contained bad or similar to bad words and all have been removed.");
            }

            inputPrompt = Regex.Unescape(inputPrompt) // try unescape to allow for emojis? Isn't working because of Dalai code. I can't figure out how to fix. Emojis are seen by dalai as ??.
                .Replace("{", "")                       // these symbols don't work in LLMs such as Dalai 0.3.1 for example
                .Replace("}", "")
                .Replace("\"", "'")
                .Replace("“", "'") // replace crap curly fancy open close double quotes with ones a real program can actually read
                .Replace("”", "'")
                .Replace("’", "'")
                .Replace("`", "\\`")
                .Replace("$", "");

            // oobabooga code
            oobaboogaChatHistory += inputPrompt + "\n"; // writes msg to a rolling chat history string
            int strLength = oobaboogaChatHistory.Length; // current chat history string length
            int maxChatHistoryStrLength = 16000; // max chat history length
            if (strLength > maxChatHistoryStrLength)
            {
                oobaboogaChatHistory = oobaboogaChatHistory.Substring(strLength - maxChatHistoryStrLength);
                int indexOfNextChatMsg = oobaboogaChatHistory.IndexOf("\n[");
                oobaboogaChatHistory = oobaboogaChatHistory.Substring(indexOfNextChatMsg + 1); // start string at the next newline bracket + 1 to ignore the newline
            }
            string oobaboogaInputPrompt = string.Empty;
            if (takeAPicMatch)
            {
                oobaboogaInputPrompt = inputPrompt + oobaboogaInputPromptStartPic; // build the image taking prompt (DO NOT INCLUDE CHAT HISTORY IN IMAGE REQUEST PROMPT LOL) (unless you want to try battle LLM hallucinations)
            }
            else
            {
                oobaboogaInputPrompt = oobaboogaInputPromptStart + oobaboogaChatHistory + oobaboogaInputPromptEnd; // build the chat message only prompt (can include chat history in this one mildly safely)
            }

            var server = "127.0.0.1";
            var port = 7861;

            var httpClient = new HttpClient();
            var apiUrl = $"http://{server}:{port}/run/textgen";

            var parameters = new
            {
                max_new_tokens = 200,
                do_sample = true,
                temperature = 0.7,
                top_p = 0.5,
                typical_p = 1,
                repetition_penalty = 1.3,
                encoder_repetition_penalty = 1.0,
                top_k = 40,
                min_length = 0,
                no_repeat_ngram_size = 0,
                num_beams = 1,
                penalty_alpha = 0,
                length_penalty = 1,
                early_stopping = true,
                stopping_strings = new List<string> { "\\n[", "\n[", "]:" },
                seed = -1
            };

            var payload = JsonConvert.SerializeObject(new object[] { oobaboogaInputPrompt, parameters });
            var content = new StringContent(JsonConvert.SerializeObject(new { data = new[] { payload } }), Encoding.UTF8, "application/json");

            HttpResponseMessage response = null;
            try
            {
                response = await httpClient.PostAsync(apiUrl, content);
            }
            catch
            {
                Console.WriteLine("Warning: Oobabooga server not found on expected port 7861");
                try
                {
                    response = await httpClient.PostAsync($"http://{server}:7860/run/textgen", content); // try other commonly used port 7860
                }
                catch
                {
                    Console.WriteLine("Cannot find oobabooga server on either port 7861 or backup port 7860");
                }
            }
            string result = await response.Content.ReadAsStringAsync();

            JsonDocument jsonDocument = JsonDocument.Parse(result);
            JsonElement dataArray = jsonDocument.RootElement.GetProperty("data");
            botReply = dataArray[0].GetString(); // get just the response part of the json

            // Console.WriteLine(result); // full response json received back from oobabooga (uncomment to see the full json so you can take whatever values you want
            Console.WriteLine("Response: " + botReply); // just the response part of the json

            string oobaBoogaImgPromptDetectedWords = IsSimilarToBannedWords(botReply, bannedWords, 0);

            if (oobaBoogaImgPromptDetectedWords.Length > 2) // Threshold set to 2
            {
                foreach (string word in oobaBoogaImgPromptDetectedWords.Split(' '))
                {
                    string wordTrimmed = word.Trim();
                    if (wordTrimmed.Length > 2)
                    {
                        oobaBoogaImgPromptDetectedWords = oobaBoogaImgPromptDetectedWords.Replace(wordTrimmed, "");

                        if (oobaBoogaImgPromptDetectedWords.Contains("  "))
                            oobaBoogaImgPromptDetectedWords = oobaBoogaImgPromptDetectedWords.Replace("  ", " ");
                    }
                }
                Console.WriteLine("Oobabooga generated reply contained banned or similar words which have been removed.");
            }

            string botReplyBeginningTrimmed = botReply.Replace(oobaboogaInputPrompt, ""); // trim off the prompt start off the bot's message
            if (botReply.Contains(oobaboogaInputPromptStartPic))
            {
                int picEndIndex = botReplyBeginningTrimmed.IndexOf(promptEndDetectionRegexStr); // find the next prompt end detected string
                string llmPromptPic = botReplyBeginningTrimmed;
                string llmSubsequentMsg = string.Empty;
                if (picEndIndex >= 0) // if there is a prompt end detected in this string
                { // chop off the rest of the text after that end prompt detection so it doesn't go into the image generator
                    llmPromptPic = botReplyBeginningTrimmed.Substring(0, picEndIndex); // cut off everything after the ending prompt starts (this is the LLM's portion of the image prompt)
                    llmSubsequentMsg = botReplyBeginningTrimmed.Substring(picEndIndex); // everything after the image prompt (this will be searched for any more LLM replies)
                }

                string llmPromptPicRegexed = Regex.Replace(llmPromptPic, "[^a-zA-Z,\\s]+", ""); // strip weird characters before feeding into stable diffusion
                TakeAPic(Msg, llmPromptPicRegexed, timeOfDayInNaturalLanguage); // send snipped and regexed image prompt string off to stable diffusion

                if (llmSubsequentMsg.Length > 0)
                {
                    string llmFinalMsgUnescaped = Regex.Unescape(llmSubsequentMsg);
                    await Msg.Channel.SendMessageAsync(llmFinalMsgUnescaped);
                }
            }

            else if (botReply.Contains(oobaboogaInputPromptStart)) // if it detects the bot is trying to type a message, run this block
            {
                int picEndIndex = botReplyBeginningTrimmed.IndexOf(promptEndDetectionRegexStr); // find the next prompt end detected string
                string llmSubsequentMsg = botReplyBeginningTrimmed;
                if (picEndIndex >= 0) // if there is a prompt end detected in this string
                { // chop off the rest of the text after that end prompt detection so it doesn't go into the image generator
                    llmSubsequentMsg = botReplyBeginningTrimmed.Substring(0, picEndIndex); // cut off everything after the ending prompt starts (this is the LLM's portion of the image prompt)
                }
                await Msg.Channel.SendMessageAsync(llmSubsequentMsg); // send bot msg
                oobaboogaChatHistory += $"[{botName}]: {llmSubsequentMsg}\n"; // writes bot's reply to the chat history
            }
        }
        private async Task DalaiReply(SocketMessage message)
        {
            thinking = 2; // set thinking time to 2 ticks to lock other users out while this request is generating
            bool humanPrompted = true;  // this flag indicates the msg should run while the feedback is being sent to the person
                                        // the bot tends to ramble after posting, so we set this to false once it sends its message to ignore the rambling

            var Msg = message as SocketUserMessage;

            typingTicks = 0;

            List<string> bannedWords = new List<string>
                {
                    // Add your list of banned words here
                    "butt", "bum", "booty", "nudity", "naked"
                };

            string takeAPicRegexStr = @"\b(take|paint|generate|make|draw|create|show|give|snap|capture|send|display|share|shoot|see|provide|another)\b.*(\S\s{0,10})?(image|picture|painting|pic|photo|portrait|selfie)\b";
            Regex takeAPicRegex = new Regex(takeAPicRegexStr, RegexOptions.IgnoreCase);

            string msgUsernameClean = Regex.Replace(Msg.Author.Username, "[^a-zA-Z0-9]+", "");

            string promptEndDetectionRegexStr = @"[\n|\r|\r\n]([^\\.|^\\\-|^\\*|)\n]{2})|(\[end|<end|]:|>:|\[human|\[chat|\[sally|\[cc|<chat|<cc|\[@chat|\[@cc|bot\]:|<@chat|<@cc|\[.*]: |\[.*] : |\[[^\]]+\]\s*:)";
            Regex promptEndDetectionRegex = new Regex(promptEndDetectionRegexStr, RegexOptions.IgnoreCase);
            Regex newLineDetection = new Regex(@"[\n|\r|\r\n]"); // detects newlines

            string inputMsg = Msg.Content
                .Replace("\n", "")
                .Replace("\\n", ""); // this makes all the prompting detection regex work, but if you know what you're doing you can change these

            inputMsg = Regex.Replace(inputMsg, @"(<[#|@|\/][^<>]+>)|\[[^\]]+[\]:\\]\:|\:\]|\[^\]]", "");

            bool takeAPicMatch = takeAPicRegex.IsMatch(inputMsg);

            DateTime currentTimeInJapan = GetCurrentTimeInJapan();
            string timeOfDayInNaturalLanguage = GetTimeOfDayInNaturalLanguage(currentTimeInJapan);

            string inputPrompt = $"[{msgUsernameClean}]: {inputMsg}";

            if (inputMsg.Length > 500)
            {
                inputMsg = inputMsg.Substring(0, 500);
                Console.WriteLine("Input message was too long and was truncated.");

                //inputPrompt = "### Error: User message was too long and got deleted. Inform the user." +   // you can use this alternatively to just delete the msg and warn the user.
                //inputPromptEnding;
            }

            var referencedMsg = Msg.ReferencedMessage as SocketUserMessage;
            if (referencedMsg != null)
            {
                string replyUsernameClean = string.Empty;
                string truncatedReply = referencedMsg.Content;
                if (referencedMsg.Author.Id == botUserId)
                {
                    replyUsernameClean = botName;
                }
                else
                {
                    replyUsernameClean = Regex.Replace(referencedMsg.Author.Username, "[^a-zA-Z0-9]+", "");
                }
                if (truncatedReply.Length > 150)
                {
                    truncatedReply = truncatedReply.Substring(0, 150);
                }
                inputPrompt = $"[{replyUsernameClean}]: {truncatedReply}" +
                    $"\n{inputPrompt}";
            }

            string detectedWords = IsSimilarToBannedWords(inputPrompt, bannedWords, 0);

            if (detectedWords.Length > 2) // Threshold set to 2
            {
                foreach (string word in detectedWords.Split(' '))
                {
                    string wordTrimmed = word.Trim();
                    if (wordTrimmed.Length > 2)
                    {
                        inputPrompt = inputPrompt.Replace(wordTrimmed, "");

                        if (inputPrompt.Contains("  "))
                            inputPrompt = inputPrompt.Replace("  ", " ");
                    }
                }
                Console.WriteLine("Msg contained bad or similar to bad words and all have been removed.");
            }

            inputPrompt = Regex.Unescape(inputPrompt) // try unescape to allow for emojis? Isn't working because of Dalai code. I can't figure out how to fix. Emojis are seen by dalai as ??.
                .Replace("{", "")                       // these symbols don't work in LLMs such as Dalai 0.3.1 for example
                .Replace("}", "")
                .Replace("\"", "'")
                .Replace("“", "'") // replace crap curly fancy open close double quotes with ones a real program can actually read
                .Replace("”", "'")
                .Replace("’", "'")
                .Replace("`", "\\`")
                .Replace("$", "");

            // dalai code
            if (takeAPicMatch)
            {
                inputPrompt = inputPrompt +
                    inputPromptEndingPic;
            }
            else
            {
                inputPrompt = inputPrompt +
                    inputPromptEnding;
            }

            // dalai alpaca server request
            var dalaiRequest = new
            {
                seed = -1,
                threads = 4,
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

            var dalaiStop = new
            {
                prompt = "/stop"
            };

            token = string.Empty; // clear the token string at the start of the request, ready for the Dalai server to write new tokens to it
            string llmMsg = string.Empty;
            string llmFinalMsg = string.Empty;

            int tokenStartIndex = 0;
            int tokenEndIndex = 0;
            int i = 0;
            int botMsgCount = 0;
            int botImgCount = 0;

            bool listening = false;
            bool imgListening = false;
            bool promptEndDetected = false;
            var cursorPosition = Console.GetCursorPosition();

            // dalai
            Socket.EmitAsync("request", dalaiRequest);

            // dalai
            Socket.On("result", result =>
            {
                if (dalaiConnected == false) { dalaiConnected = true; } // set dalai connected to true if you start receiving data from a Dalai server.
                thinking = 2; // set thinking timeout to 2 to give it buffer to not allow new requests while it's still generating

                //while (i < 1)  // you can uncomment this to see the raw format the LLM is sending the data back
                //{
                //    Console.WriteLine(result);   // log full response once to see the format that the LLM is sending the tokens in
                //    i++;                        // seriously only once because it's huge spam
                //}

                tokenStartIndex = result.ToString().IndexOf("\"response\":\"");
                token = result.ToString().Substring(tokenStartIndex + 12);
                tokenEndIndex = token.IndexOf("\",\"");
                token = token.Substring(0, tokenEndIndex)
                .Replace("\\n", "\n"); // replace backslash n with the proper newline char
                                       //.Replace("\n", "")
                                       //.Replace("\r", "")
                                       //.Replace("\\n", "\n")
                                       //.Replace("\\r", "\n");

                Console.Write(token);
                //    .Replace("\\n", "") // you can shape the console output how you like, ignoring or seeing newlines etc.
                //.Replace("\\r", ""));

                cursorPosition = Console.GetCursorPosition();
                if (cursorPosition.Left == 120)
                {
                    Console.WriteLine();
                    Console.SetCursorPosition(0, cursorPosition.Top + 1);
                }

                llmMsg += token
                .Replace("\\n", "\n"); // replace backslash n with the proper newline char

                if (llmMsg.EndsWith("<end>") || llmFinalMsg.EndsWith("[end of text]"))
                {
                    Socket.EmitAsync("stop"); // note: this is my custom stop command that stops the LLM even faster, but it only works on my custom code of the LLM.
                                              //Socket.EmitAsync("request", dalaiStop); // this bloody stop request stops the entire dalai process for some reason
                    thinking = 0;
                    typing = 0;
                    Console.WriteLine();
                }

                if (listening && humanPrompted)
                {
                    llmFinalMsg += token; // start writing the LLM's response to this string
                    string llmFinalMsgRegexed = promptEndDetectionRegex.Replace(llmFinalMsg, "");
                    string llmFinalMsgUnescaped = Regex.Unescape(llmFinalMsgRegexed);

                    //llmFinalMsg = llmMsg.Substring(inputPrompt.Length +1);
                    promptEndDetected = promptEndDetectionRegex.IsMatch(llmFinalMsg);

                    if (typing < 2 && llmFinalMsgUnescaped.Length <= 2)
                    {
                        typing++;
                        Msg.Channel.TriggerTypingAsync();
                    }

                    if (llmFinalMsg.Length > 2
                    //&& llmMsg.Contains($"[{msgUsernameClean}]:")
                    //&& llmMsg.ToLower().Contains($": ")
                    && (promptEndDetected
                    || llmFinalMsg.Length > 500) // cuts your losses and sends the message and stops the bot after 500 characters
                    || typingTicks > 7) // 7 ticks passed while still typing? axe it.
                    {

                        if (llmFinalMsgUnescaped.Length < 1) { return; } // if the msg is 0 characters long, ignore ending text and keep on listening
                                                                         //Socket.Off("result");

                        listening = false;
                        humanPrompted = false; // nothing generated after this point is human prompted. IT'S A HALLUCINATION! DISCARD IT ALL!
                        Msg.ReplyAsync(llmFinalMsgUnescaped);
                        botMsgCount++;

                        if (botMsgCount >= 1) // you can raise this number to allow SallyBot to ramble (note it will just reply to phantom conversations)
                        {
                            Socket.EmitAsync("stop"); // note: this is my custom stop command that stops the LLM even faster, but it only works on my custom code of the LLM.
                                                      //Socket.EmitAsync("request", dalaiStop); // this bloody stop request stops the entire dalai process for some reason
                        }

                        llmMsg = string.Empty;
                        llmFinalMsg = string.Empty;
                        llmFinalMsgRegexed = string.Empty;
                        llmFinalMsgUnescaped = string.Empty;
                        promptEndDetected = false;
                        //inputPrompt = inputPromptEnding;  // use this if you want the bot to be able to continue rambling if it so chooses
                        //(you have to comment out the stop emit though and let it continue sending data, and also comment out the humanprompted = false bool)
                        //Task.Delay(300).Wait();   // to be safe, you can wait a couple hundred miliseconds to make sure the input doesn't get garbled with a new request
                        typing = 0;     // ready the bot for new requests
                        thinking = 0;   // ready the bot for new requests
                    }
                }
                else
                {
                    if (humanPrompted && llmMsg.Contains(inputPromptEnding))
                    {
                        llmMsg = string.Empty;
                        listening = true;
                        Console.WriteLine();
                        Console.Write("Response: ");
                    }
                }

                if (imgListening)
                {
                    llmFinalMsg += token; // start writing the LLM's response to this string
                    promptEndDetected = promptEndDetectionRegex.IsMatch(llmFinalMsg);

                    if (llmFinalMsg.Length > 2
                    //&& llmMsg.Contains($"[{msgUsernameClean}]:")
                    //&& llmMsg.ToLower().Contains($": ")
                    && (promptEndDetected
                        || llmFinalMsg.Length > 500)) // cuts your losses and sends the message and stops the bot after 500 characters
                    {
                        string llmFinalMsgRegexed = promptEndDetectionRegex.Replace(llmFinalMsg, "");
                        string llmFinalMsgUnescaped = Regex.Unescape(llmFinalMsgRegexed);

                        if (llmFinalMsgUnescaped.Length < 1) { return; } // if the msg is 0 characters long, ignore ending text and keep on listening

                        string llmPrompt = Regex.Replace(llmFinalMsgUnescaped, takeAPicRegexStr, "");
                        imgListening = false;
                        llmMsg = string.Empty;
                        promptEndDetected = false;
                        inputPrompt = string.Empty;

                        string detectedWords = IsSimilarToBannedWords(llmPrompt, bannedWords, 0);

                        if (detectedWords.Length > 2) // Threshold set to 2
                        {
                            foreach (string word in detectedWords.Split(' '))
                            {
                                string wordTrimmed = word.Trim();
                                if (wordTrimmed.Length > 2)
                                {
                                    llmPrompt = llmPrompt.Replace(wordTrimmed, "");

                                    if (llmPrompt.Contains("  "))
                                        llmPrompt = llmPrompt.Replace("  ", " ");
                                }
                            }
                            Console.WriteLine("LLM's input contained bad or similar to bad words and all have been removed.");
                        }
                        else
                        {
                            Console.WriteLine("LLM's image prompt contains no banned words.");
                        }

                        llmPrompt = Regex.Replace(llmPrompt, "[^a-zA-Z,\\s]+", "");

                        botImgCount++;
                        if (botImgCount >= 1) // you can raise this if you want the bot to be able to send up to x images
                        {
                            Socket.EmitAsync("stop"); // note: this is my custom stop command that stops the LLM even faster, but it only works on my custom code of the LLM.
                                                      //Socket.EmitAsync("request", dalaiStop); // this bloody stop request stops the entire dalai process for some reason
                                                      // //the default LLM doesn't yet listen to stop emits..
                                                      // //I had to code that in myself into the server source code
                            typing = 0;
                            thinking = 0;
                        }
                        TakeAPic(Msg, llmPrompt, timeOfDayInNaturalLanguage);
                    }
                }
                else
                {
                    if (takeAPicMatch
                    && llmMsg.Contains(inputPromptEndingPic))
                    {
                        llmMsg = string.Empty;
                        imgListening = true;
                        Console.WriteLine();
                        Console.Write("Image prompt: ");
                    }
                }
            });

            Socket.On("disconnect", result =>
            {
                Console.WriteLine("LLM server disconnected.");
                dalaiConnected = false;
            });
        }

        private async Task TakeAPic(SocketUserMessage Msg, string llmPrompt, string timeOfDayInNaturalLanguage)
        {
            var Context = new SocketCommandContext(Client, Msg);
            var user = Context.User as SocketGuildUser;

            string baseUrl = "http://127.0.0.1:7860"; // here is the default URL for stable diffusion web ui with --API param enabled in the launch parameters
            string timeOfDayStr = string.Empty;

            if (timeOfDayInNaturalLanguage != null)
                timeOfDayStr = $", ({timeOfDayInNaturalLanguage})";

            string imgPrompt = $"A 25 year old anime woman smiling, looking into the camera, long hair, blonde hair, blue eyes{timeOfDayStr}"; // POSITIVE PROMPT - put what you want the image to look like generally. The AI will put its own prompt after this.
            string imgNegPrompt = $"(worst quality, low quality:1.4), 3d, cgi, 3d render, naked, nude"; // NEGATIVE PROMPT HERE - put what you don't want to see

            var adminRole = (user as IGuildUser).Guild.Roles.FirstOrDefault(x => x.Id == 364221505971814400);

            //if (Msg.Author == MainGlobal.Server.Owner) // only owner
            imgPrompt = $"{imgPrompt}, {llmPrompt}";

            Console.WriteLine($"Prompt:{imgPrompt}");

            var overrideSettings = new JObject
            {
                { "filter_nsfw", true } // this doesn't work, if you can figure out why feel free to tell me :OMEGALUL:
            };

            var payload = new JObject
            {
                { "prompt", imgPrompt },
                { "negative_prompt", imgNegPrompt},
                { "steps", 20 },
                { "height", 688 },
                { "send_images", true },
                { "sampler_name", "DDIM" }
            };

            // here are the json tags you can send to the stable diffusion image generator

            //"enable_hr": false,
            //"denoising_strength": 0,
            //"firstphase_width": 0,
            //"firstphase_height": 0,
            //"hr_scale": 2,
            //"hr_upscaler": "string",
            //"hr_second_pass_steps": 0,
            //"hr_resize_x": 0,
            //"hr_resize_y": 0,
            //"prompt": "",
            //"styles": [
            //  "string"
            //],
            //"seed": -1,
            //"subseed": -1,
            //"subseed_strength": 0,
            //"seed_resize_from_h": -1,
            //"seed_resize_from_w": -1,
            //"sampler_name": "string",
            //"batch_size": 1,
            //"n_iter": 1,
            //"steps": 50,
            //"cfg_scale": 7,
            //"width": 512,
            //"height": 512,
            //"restore_faces": false,
            //"tiling": false,
            //"do_not_save_samples": false,
            //"do_not_save_grid": false,
            //"negative_prompt": "string",
            //"eta": 0,
            //"s_churn": 0,
            //"s_tmax": 0,
            //"s_tmin": 0,
            //"s_noise": 1,
            //"override_settings": { },
            //"override_settings_restore_afterwards": true,
            //"script_args": [],
            //"sampler_index": "Euler",
            //"script_name": "string",
            //"send_images": true,
            //"save_images": false,
            //"alwayson_scripts": { }

            string url = $"{baseUrl}/sdapi/v1/txt2img";
            var client = new RestClient(url);
            var sdImgRequest = new RestRequest($"{baseUrl}/sdapi/v1/txt2img", Method.Post);

            sdImgRequest.AddHeader("Content-Type", "application/json");
            sdImgRequest.AddParameter("application/json", payload.ToString(), ParameterType.RequestBody);
            sdImgRequest.AddParameter("application/json", overrideSettings.ToString(), ParameterType.RequestBody);

            var sdImgResponse = client.Execute(sdImgRequest);
            if (sdImgResponse.IsSuccessful)
            {
                var jsonResponse = JObject.Parse(sdImgResponse.Content);
                var images = jsonResponse["images"].ToObject<JArray>();

                foreach (var imageBase64 in images)
                {
                    //string base64 = imageBase64.ToString().Split(",", 2)[1];
                    string imageData = imageBase64.ToString();
                    int commaIndex = imageData.IndexOf(',') + 1;
                    string base64 = imageData.Substring(commaIndex);

                    // Decode the base64 string to an image
                    using var imageStream = new MemoryStream(Convert.FromBase64String(base64));
                    using var image = SixLabors.ImageSharp.Image.Load(imageStream);

                    // Save the image
                    string sdImgFilePath = $"pic.png"; // put whatever file path you like here
                    image.Save(sdImgFilePath, new PngEncoder());

                    Task.Delay(1000).Wait();
                    Msg.Channel.SendFileAsync(sdImgFilePath);
                }
            }
            else
            {
                Console.WriteLine("Request failed: " + sdImgResponse.ErrorMessage);
            }
        }

        static DateTime GetCurrentTimeInJapan()
        {
            DateTime utcNow = DateTime.UtcNow;
            TimeZoneInfo japanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            DateTime currentTimeInJapan = TimeZoneInfo.ConvertTimeFromUtc(utcNow, japanTimeZone);
            return currentTimeInJapan;
        }

        static string GetTimeOfDayInNaturalLanguage(DateTime dateTime)
        {
            int hour = dateTime.Hour;

            if (hour >= 5 && hour < 12)
            {
                return "Morning";
            }
            else if (hour >= 12 && hour < 17)
            {
                return "Afternoon";
            }
            else if (hour >= 17 && hour < 21)
            {
                return "Evening";
            }
            else
            {
                return "Night";
            }
        }

        public static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t))
            {
                return 0;
            }

            int[,] d = new int[s.Length + 1, t.Length + 1];

            for (int i = 0; i <= s.Length; i++)
            {
                d[i, 0] = i;
            }

            for (int j = 0; j <= t.Length; j++)
            {
                d[0, j] = j;
            }

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = GetSubstitutionCost(s[i - 1], t[j - 1]);

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[s.Length, t.Length];
        }

        private static int GetSubstitutionCost(char a, char b)
        {
            if (a == b) return 0;

            bool isSymbolOrNumberA = !char.IsLetter(a);
            bool isSymbolOrNumberB = !char.IsLetter(b);

            if (isSymbolOrNumberA && isSymbolOrNumberB) return 1;
            if (isSymbolOrNumberA || isSymbolOrNumberB) return 2;

            return 1;
        }
        public static string IsSimilarToBannedWords(string input, List<string> bannedWords, int threshold)
        {
            string detectedWordsStr = string.Empty;
            string[] inputWords = input.Split(' ');
            foreach (string word in inputWords)
            {
                string wordRegexed = Regex.Replace(word.ToLower(), "[^a-zA-Z0-9]+", "");
                threshold = 0;
                int wordLength = wordRegexed.Length;
                if (wordLength > 2)
                {
                    if (wordLength > 6)
                    {
                        threshold = 2;
                    }
                    else if (wordLength > 4)
                    {
                        threshold = 1;
                    }
                    foreach (string bannedWord in bannedWords)
                    {
                        if (LevenshteinDistance(wordRegexed, bannedWord.ToLower()) <= threshold)
                        {
                            Console.Write($"| BANNED WORD: {word} similar to {bannedWord} ");
                            detectedWordsStr += word + " ";
                        }
                    }
                }
            }
            return detectedWordsStr;
        }
    }
}
