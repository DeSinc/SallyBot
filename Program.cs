private async Task LlamaReply(SocketMessage message, SocketCommandContext context)
        {
            bool humanPrompted = true;

            var Msg = message as SocketUserMessage;
            var Context = new SocketCommandContext(Client, Msg);
            var user = Context.User as SocketGuildUser;

            var patreon1 = MainGlobal.Server.Roles.FirstOrDefault(x => x.Id == 373446230090448897);
            var youtube1 = MainGlobal.Server.Roles.FirstOrDefault(x => x.Id == 419038061762969600);
            var adminRole = (user as IGuildUser).Guild.Roles.FirstOrDefault(x => x.Id == 364221505971814400);

            bool allowedUser = false;

            typingTicks = 0;

            List<string> bannedWords = new List<string>
                {
                    // Add your list of banned words here
                    "butt", "bum", "booty", "bath", "bathtub", "nudity", "naked"
                };

            if (user.Roles.Contains(adminRole))
            {
                allowedUser = true;
            }
            string takeAPicRegexStr = @"\b(take|generate|show|give|snap|capture|send|display|share|shoot|see|provide|another)\b.*(\S\s{0,10})?(image|picture|pic|photo|selfie)\b";
            Regex takeAPicRegex = new Regex(takeAPicRegexStr, RegexOptions.IgnoreCase);

            string msgUsernameClean = Regex.Replace(Msg.Author.Username, "[^a-zA-Z0-9]+", "");

            //                                          (?:\\n|\\r|(?<=\S)\[)(?!\s|$)(?=[^\s.?!0-9])(?!\.[a-z]{2,6}(?=[^\w]|$))(?!\s*[""”])(\S*?)(?=[\s.?!]|$)|([\[\]<>]?\s*(\[end|<end|\[human|\[chat|\[cc|<chat|<cc|\[@chat|\[@cc|<@chat|<@cc))\s*
            Regex promptEndDetectionRegex = new Regex(@"\n([^\\.\n]{2})|\r\n([^\\.\n]{2})|\r([^\\.\n]{2})|(\[end|<end|\[human|\[chat|\[sally|\[cc|<chat|<cc|\[@chat|\[@cc|sallybot\]:|<@chat|<@cc)", RegexOptions.IgnoreCase);

            string inputMsg = Msg.Content
                .Replace("\n", "")
                .Replace("\\n", "")
                .Replace("{", "")
                .Replace("}", "")
                .Replace("\"", "'");

            inputMsg = Regex.Replace(inputMsg, @"(<[@|\/][^<>]+>)|\[[^\]]+[\]:\\]\:|\:\]|\[^\]]", "");

            bool takeAPicMatch = takeAPicRegex.IsMatch(inputMsg);

            DateTime currentTimeInJapan = GetCurrentTimeInJapan();
            string timeOfDayInNaturalLanguage = GetTimeOfDayInNaturalLanguage(currentTimeInJapan);

            string inputPrompt = $"[{msgUsernameClean}]: {inputMsg}";
            string inputPromptEnding = "\n[SallyBot]: ";
            string inputPromptEndingPic = $"\nAfter captioning the image, Sallybot may reply." +
            $"\nNouns of things in the photo: ";

            if (inputMsg.Length > 335)
            {
                inputMsg = inputMsg.Substring(0, 335);

                //inputPrompt = "### Error: User message was too long and got deleted. Inform the user." +
                //inputPromptEnding;
            }

            var referencedMsg = Msg.ReferencedMessage as SocketUserMessage;
            if (referencedMsg != null)
            {
                string replyUsernameClean = string.Empty;
                string truncatedReply = referencedMsg.Content;
                if (referencedMsg.Author.Id == 438634979862511616)
                {
                    replyUsernameClean = "SallyBot";
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

                var referencedMsg2 = referencedMsg.ReferencedMessage as SocketUserMessage;
                if (referencedMsg2 != null)
                {
                    Console.WriteLine("2nd level reply was detected and included in the prompt.");
                    string replyUsernameClean2 = string.Empty;
                    string truncatedReply2 = referencedMsg2.Content;
                    if (referencedMsg2.Author.Id == 438634979862511616)
                    {
                        replyUsernameClean2 = "SallyBot";
                    }
                    else
                    {
                        replyUsernameClean2 = Regex.Replace(referencedMsg2.Author.Username, "[^a-zA-Z0-9]+", "");
                    }
                    if (truncatedReply2.Length > 150)
                    {
                        truncatedReply2 = truncatedReply2.Substring(0, 150);
                    }
                    inputPrompt = $"[{replyUsernameClean2}]: {truncatedReply2}" +
                        $"\n{inputPrompt}";
                }
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
            else
            {
                Console.WriteLine("User's image prompt contains no banned words.");
            }

            if (takeAPicMatch
                && !allowedUser
                && !(Msg.Author == MainGlobal.Server.Owner
                ||      user.Roles.Contains(adminRole)))
            {
                inputPrompt = $"{msgUsernameClean} requested a photo of SallyBot.. Ew, gross! He hasn't paid! SallyBot denying this request..." +
                inputPromptEnding;
            }
            else if (takeAPicMatch && allowedUser)
            //&& (Msg.Author == MainGlobal.Server.Owner  // allow owner
            //||  user.Roles.Contains(adminRole)))      // allow admins (change admin role id # to your admin role ID)
            {
                inputPrompt = inputPrompt + 
                    inputPromptEndingPic;
            }
            else
            {
                inputPrompt = inputPrompt +
                    inputPromptEnding;
            }

            Console.WriteLine("User's prompt: " + inputMsg);
            var request = new
            {
                seed = -1,
                threads = 16,
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

            string token = string.Empty;
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

            Socket.EmitAsync("request", request);

            Socket.On("result", result =>
            {
                thinking = 2;

                //while (i < 1)  // you can uncomment this to see the raw format the LLM is sending the data back
                //{
                //    Console.WriteLine(result);   // log full response once to see the format that the LLM is sending the tokens in
                //    i++;                        // seriously only once because it's huge spam
                //}

                tokenStartIndex = result.ToString().IndexOf("\"response\":\"");
                token = result.ToString().Substring(tokenStartIndex + 12);
                tokenEndIndex = token.IndexOf("\",\"");
                token = token.Substring(0, tokenEndIndex);

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
                    Socket.EmitAsync("stop");
                    thinking = 0;
                    typing = 0;
                    Console.WriteLine();
                }

                if (listening && humanPrompted)
                {
                    llmFinalMsg = llmMsg;
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
                    || llmFinalMsg.Length > 400)
                    || typingTicks > 7) // 7 ticks passed while still typing? axe it.
                    {

                        if (llmFinalMsgUnescaped.Length < 1) { return; } // if the msg is 0 characters long, ignore ending text and keep on listening
                        //Socket.Off("result");

                        listening = false;
                        //humanPrompted = false; // nothing generated after this point is human prompted. IT'S A HALLUCINATION! DISCARD IT ALL!
                        Msg.ReplyAsync(llmFinalMsgUnescaped);
                        botMsgCount++;

                        if (botMsgCount >= 1)
                        {
                            Socket.EmitAsync("stop");
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
                    if (humanPrompted
                    && llmMsg.Contains(inputPromptEnding))
                    {
                        llmMsg = string.Empty;
                        listening = true;
                        Console.WriteLine();
                        Console.Write("Response: ");
                    }
                }

                if (imgListening)
                {
                    llmFinalMsg = llmMsg;
                    promptEndDetected = promptEndDetectionRegex.IsMatch(llmFinalMsg);

                    if (llmFinalMsg.Length > 2
                    //&& llmMsg.Contains($"[{msgUsernameClean}]:")
                    //&& llmMsg.ToLower().Contains($": ")
                    && (promptEndDetected || llmFinalMsg.Length > 1900))
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

                        TakeAPic(Msg, llmPrompt, timeOfDayInNaturalLanguage);
                        botImgCount++;
                        if (botImgCount >= 1) // you can raise this if you want the bot to be able to send up to x images
                        {
                        Socket.EmitAsync("stop"); // note: this only works on my custom code of the LLM.
                                                  // //the default LLM doesn't yet listen to stop emits..
                                                  // //I had to code that in myself into the server source code
                        typing = 0;
                        thinking = 0;
                        }
                    }
                }
                else
                {
                    if (takeAPicMatch
                    && allowedUser
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
            });
        }
