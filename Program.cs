using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Newtonsoft.Json.Linq;

using SallyBot.Backends;
using SallyBot.Chat;
using SallyBot.Extras;

namespace SallyBot
{
    class Program
    {
        // ── Timer ──────────────────────────────────────────────────────────────
        private static System.Timers.Timer _loop;

        // ── Discord ────────────────────────────────────────────────────────────
        private DiscordSocketClient _client;

        // ── State flags ────────────────────────────────────────────────────────
        private static bool   _dalaiThinking      = false;
        private static int    _llmThinkingTicks    = 0;
        private static int    _loopCounts          = 0;
        private static bool   _newMsgReceived      = false;
        private static int    _checkTimeout        = 0;
        private static int    _checkTimeoutCount   = 0;
        private static bool   _longMsgWarningGiven = false;

        // ── Config ─────────────────────────────────────────────────────────────
        private static int _maxChatHistoryLength = 500;

        // ── Bot identity ───────────────────────────────────────────────────────
        internal static ulong  BotUserId = 0;
        internal static string BotName   = "sally03";

        // ── Prompt templates ───────────────────────────────────────────────────
        private string CharacterPrompt =>
            $"[INST]\r\nEnter chat mode. You shall reply to other users while staying in character. " +
            $"This is not a roleplay, do not include actions. " +
            $"Keep your replies short, natural, casual and realistic.\r\n" +
            $"# About {BotName}:\r\nName: {BotName}\r\n[/INST]";

        private string CompletionSuffix => $"### Response:\n[{BotName}]:";

        private static readonly string InputPromptStartPic =
            "### Instruction: Take the scene and create a single line comma separated list of " +
            "descriptions of the scene and keywords/tags to describe what things are visible.\r\n\r\n" +
            "Ignore things that are not visible, such as thoughts, feelings, speech or sound.\r\n\r\n" +
            "Minimum Requirements:\r\n" +
            "1. List of keywords for characters in the scene\r\n" +
            "2. List of keywords to describe the location\r\n\r\n" +
            "Use only the top 15 keywords in the list. Reply with nothing but the single line list.";

        private const string InputPromptEndPic = "### Response:";

        // ── Content filters ────────────────────────────────────────────────────
        private static readonly List<string> BannedWords = new()
        {
            "p0rnography", "h3ntai"
        };

        private const string BannedWordsExact    = @"\b(fuck|shit|cock)\b";
        private const string BannedWordsExactPic = @"\b(booty|erotic|naked|topless|butt|ass|tentacle|tentacles|nude|r34)\b";

        // ── Regex ──────────────────────────────────────────────────────────────
        private const string TakeAPicRegexStr =
            @"\b(take|post|paint|generate|make|draw|create|show|give|snap|capture|send|display|share|shoot|see|provide|another)\b.*(\S\s{0,10})?(image|picture|screenshot|screenie|painting|pic|photo|photograph|portrait|selfie)\b";

        private const string PromptEndDetectionRegexStr =
            @"(?:\r\n?)|(\n\[|\n#|\[end|<end|]:|>:|<nooutput|<noinput|\[human|\[chat|\[sally|\[cc|<chat|<cc|\[@chat|\[@cc|bot\]:|\.]|<@chat|<@cc|\[.*]: |<\/s>|\[.*] : |\[[^\]]+\]\s*:)";

        private const string PromptSpoofDetectionRegexStr = @"\[[^\]]+[\]:\\]\:|\:\]|\[^\]]";
        private const string ToneIndicatorDetector        = @"^(\[[^\]]+\]|\([^)]+\))";
        private const string LinkDetectionRegexStr        =
            @"[a-zA-Z0-9]((?i) dot |(?i) dotcom|(?i)dotcom|(?i)dotcom |\.|\. | \.| \. |\,)[a-zA-Z]*((?i) slash |(?i) slash|(?i)slash |(?i)slash|\/|\/ | \/| \/ ).+[a-zA-Z0-9]";
        public const string PingAndChannelTagDetectFilterRegexStr = @"<[@#]\d{15,}>";

        private string BotNameMatchRegexStr => @$"(?:{BotName}\?|{BotName},)";

        private readonly Regex _takeAPicRegex = new(TakeAPicRegexStr, RegexOptions.IgnoreCase);

        // ── Message buffer ─────────────────────────────────────────────────────
        internal static SocketUserMessage? BufferChatMsg;
        internal static string            BufferInputMsgFiltered = string.Empty;

        // ── Chat history ───────────────────────────────────────────────────────
        private static readonly ChatHistory _history           = new();
        private static          bool        _historyDownloaded = false;

        // ── Reply state ────────────────────────────────────────────────────────
        public  static bool   BotWillReply = false;
        private static string _botLastReply = "<noinput>";

        private static readonly CancellationTokenSource _appCts = new();

        // ======================================================================
        //  Entry point
        // ======================================================================

        public static async Task Main() => await new Program().AsyncMain();

        private async Task AsyncMain()
        {
            // Validate required config before doing anything else
            if (MainGlobal.DiscordToken.Contains("goes here") || MainGlobal.DiscordToken.Length < 10)
            {
                Console.WriteLine("[CONFIG] Set MainGlobal.DiscordToken to your Discord bot token.");
                return;
            }
            if (MainGlobal.GuildId == 0)
            {
                Console.WriteLine("[CONFIG] Set MainGlobal.GuildId to your Discord server (guild) ID.");
                return;
            }

            try
            {
                _client = new DiscordSocketClient(new DiscordSocketConfig
                {
                    MessageCacheSize    = 1200,
                    LogLevel            = LogSeverity.Debug,
                    AlwaysDownloadUsers = true,
                    GatewayIntents      =
                        GatewayIntents.MessageContent |
                        GatewayIntents.Guilds         |
                        GatewayIntents.GuildMessages  |
                        GatewayIntents.GuildMembers,
                });

                _client.Log               += Client_Log;
                _client.Ready             += OnReady;
                _client.MessageReceived   += Client_MessageReceived;
                _client.GuildMemberUpdated += Client_GuildMemberUpdated;

                MainGlobal.Client = _client;

                await _client.LoginAsync(TokenType.Bot, MainGlobal.DiscordToken);
                await _client.StartAsync();

                // Attempt Dalai connection (non-fatal)
                MainGlobal.DalaiBackend = new DalaiBackend(MainGlobal.DalaiUrl, MainGlobal.DalaiModel);
                await MainGlobal.DalaiBackend.ConnectAsync();

                // Auto-detect first available backend
                MainGlobal.ActiveBackend =
                    await BackendDetector.DetectAsync(BuildBackendCandidates());

                _loop = new System.Timers.Timer { Interval = 5500, AutoReset = true, Enabled = true };
                _loop.Elapsed += Tick;

                Console.WriteLine($"|{DateTime.Now} | Main loop initialised");

                AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
                {
                    var ex = args.Exception;
                    Console.WriteLine(
                        $"\u001b[45;1m[  DISC  ]\u001b[41;1m[  ERR  ]\u001b[0m " +
                        $"MSG: {ex.Message}\n WHERE: {ex.StackTrace}\n");
                };

                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        // ── Backend candidate list ─────────────────────────────────────────────

        private static IEnumerable<ILlmBackend> BuildBackendCandidates()
        {
            if (MainGlobal.DalaiBackend.IsConnected)
                yield return MainGlobal.DalaiBackend;

            yield return new OobaboogaBackend(MainGlobal.OobaboogaUrl);
            yield return new LmStudioBackend(MainGlobal.LmStudioUrl);

            if (!string.IsNullOrWhiteSpace(MainGlobal.OpenAiApiKey))
                yield return new OpenAiBackend(MainGlobal.OpenAiApiKey, MainGlobal.OpenAiModel);

            if (!string.IsNullOrWhiteSpace(MainGlobal.OpenRouterApiKey))
                yield return new OpenRouterBackend(MainGlobal.OpenRouterApiKey, MainGlobal.OpenRouterModel);

            if (!string.IsNullOrWhiteSpace(MainGlobal.GoogleApiKey))
                yield return new GeminiBackend(MainGlobal.GoogleApiKey, MainGlobal.GeminiModel);
        }

        // ======================================================================
        //  Discord callbacks
        // ======================================================================

        private Task OnReady() => MainLoop.StartLoop();

        private static readonly string[] _logBlacklist = new[]
        {
            "PRESENCE_UPDATE", "TYPING_START", "MESSAGE_CREATE", "MESSAGE_DELETE",
            "MESSAGE_UPDATE", "CHANNEL_UPDATE", "GUILD_", "REACTION_",
            "VOICE_STATE_UPDATE", "DELETE channels/", "POST channels/",
            "Heartbeat", "GET ", "PUT ", "Latency = ", "handler is blocking the",
        };

        private Task Client_Log(LogMessage msg)
        {
            if (msg.Message != null && _logBlacklist.All(x => !msg.Message.Contains(x)))
                Console.WriteLine($"|{DateTime.Now} - {msg.Source}| {msg.Message}");
            else if (msg.Exception != null)
                Console.WriteLine($"|{DateTime.Now} - {msg.Source}| {msg.Exception}");
            return Task.CompletedTask;
        }

        private Task Client_GuildMemberUpdated(
            Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
        {
            if (before.Value?.Id != _client.CurrentUser.Id) return Task.CompletedTask;

            if (after.DisplayName != null && before.Value?.DisplayName != after.DisplayName)
                BotName = after.DisplayName;
            else if (after.Nickname != null && before.Value?.Nickname != after.Nickname)
                BotName = after.Nickname;
            else if (before.Value?.Username != after.Username)
                BotName = after.Username;

            return Task.CompletedTask;
        }

        // ── Message received ───────────────────────────────────────────────────

        private async Task Client_MessageReceived(SocketMessage msgParam)
        {
            try
            {
                var msg = msgParam as SocketUserMessage;
                if (msg == null) return;

                var ctx  = new SocketCommandContext(_client, msg);
                var user = ctx.User as SocketGuildUser;

                if (msg.Author == MainGlobal.Server.Owner)
                    if (await HandleOwnerCommand(msg)) return;

                if (msg.Author.Id == _client.CurrentUser.Id) return;

                // One-time download of recent history
                if (!_historyDownloaded && !MainGlobal.DalaiBackend.IsConnected)
                {
                    _historyDownloaded = true;
                    await DownloadChatHistory(msg);
                }

                // Resolve display name
                string userName;
                if      (user?.DisplayName != null) userName = user.DisplayName;
                else if (user?.Nickname     != null) userName = user.Nickname;
                else                                 userName = msg.Author.Username;

                string userNameClean = Regex.Replace(userName, "[^a-zA-Z0-9]+", "");
                if (userNameClean.Length < 1) userNameClean = "User";

                string imagePresent = string.Empty;
                if (msg.Attachments.Count > 0)
                    imagePresent = $"\n[System]: {userName} attached an image";

                // Sanitise incoming message
                string inputMsg = Functions.FilterPingsAndChannelTags(msg.Content);
                inputMsg        = Regex.Replace(inputMsg, @"\n", " ");
                inputMsg        = Regex.Replace(inputMsg, PromptSpoofDetectionRegexStr, "");

                string inputMsgFiltered = $"[{userNameClean}]: {inputMsg.Trim()}";
                inputMsgFiltered = Regex.Replace(inputMsgFiltered, BannedWordsExact, "****");

                string detected = Functions.IsSimilarToBannedWords(inputMsgFiltered, BannedWords);
                if (detected.Length > 2)
                {
                    foreach (string w in detected.Split(' '))
                    {
                        string wt = w.Trim();
                        if (wt.Length > 2)
                        {
                            inputMsgFiltered = inputMsgFiltered.Replace(wt, "****");
                            if (inputMsgFiltered.Contains("  "))
                                inputMsgFiltered = inputMsgFiltered.Replace("  ", " ");
                        }
                    }
                    Console.WriteLine($"{inputMsgFiltered} <Banned or similar words removed.>{imagePresent}");
                }
                else if (!MainGlobal.DalaiBackend.IsConnected)
                {
                    Console.WriteLine($"{inputMsgFiltered}{imagePresent}");
                }

                // Add to history
                string safeContent = Regex.Replace(inputMsg.Trim(), LinkDetectionRegexStr, "<url>");
                _history.Add(new ChatMessage(userNameClean, safeContent));
                if (!string.IsNullOrEmpty(imagePresent))
                    _history.Add(new ChatMessage("System",
                        $"{userName} attached an image", isSystem: true));

                if (msg.Author.IsBot) return;

                // Wait for current generation
                int timeout = 45;
                while (_llmThinkingTicks > 0 && timeout-- > 0)
                    await Task.Delay(1000, _appCts.Token);

                _newMsgReceived = true;

                if (msg.MentionedUsers.Contains(MainGlobal.Server.GetUser(BotUserId))
                    && msg.Content.Length > 22)
                {
                    BotWillReply    = true;
                    _newMsgReceived = false;
                }

                BufferChatMsg          = msg;
                BufferInputMsgFiltered = inputMsgFiltered;

                if (BotWillReply)
                {
                    BotWillReply       = false;
                    _llmThinkingTicks  = 10;
                    _history.TrimToMaxLength(_maxChatHistoryLength);
                    _ = Task.Run(() => Reply(msg, inputMsgFiltered), _appCts.Token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MessageReceived error: {ex.Message}");
            }
        }

        // ── Owner runtime commands ─────────────────────────────────────────────

        private static async Task<bool> HandleOwnerCommand(SocketUserMessage msg)
        {
            string lower = msg.Content.Trim().ToLower();

            // "use backend <name>" – switch to a specific backend
            if (lower.StartsWith("use backend "))
            {
                string wanted = msg.Content.Trim().Substring("use backend ".Length).Trim();
                foreach (var b in BuildBackendCandidates())
                {
                    if (b.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)
                        && await b.IsAvailableAsync())
                    {
                        MainGlobal.ActiveBackend = b;
                        Console.WriteLine($"| Owner switched backend to: {b.Name}");
                        return true;
                    }
                }
                Console.WriteLine($"| Could not switch to backend '{wanted}'.");
                return true;
            }

            // Legacy Gemini toggles
            if (lower == "enable gemini" && !string.IsNullOrWhiteSpace(MainGlobal.GoogleApiKey))
            {
                MainGlobal.ActiveBackend =
                    new GeminiBackend(MainGlobal.GoogleApiKey, MainGlobal.GeminiModel);
                Console.WriteLine("| Switched to Google Gemini backend.");
                return true;
            }
            if (lower == "disable gemini")
            {
                var fallback = BuildBackendCandidates().Where(b => b is not GeminiBackend);
                MainGlobal.ActiveBackend = await BackendDetector.DetectAsync(fallback);
                return true;
            }

            return false;
        }

        // ======================================================================
        //  Timer Tick
        // ======================================================================

        private async void Tick(object? sender, ElapsedEventArgs e)
        {
            if (_llmThinkingTicks > 0) _llmThinkingTicks--;

            if (_newMsgReceived && _checkTimeoutCount <= 0 && _llmThinkingTicks <= 0)
            {
                _newMsgReceived = false;
                if (BufferChatMsg != null)
                    await ReplyCheck(BufferChatMsg, BufferInputMsgFiltered);
            }
            else if (_checkTimeoutCount > 0)
            {
                _checkTimeoutCount--;
            }
        }

        // ======================================================================
        //  Chat-history download
        // ======================================================================

        private async Task DownloadChatHistory(SocketUserMessage triggerMsg)
        {
            var downloaded = await triggerMsg.Channel.GetMessagesAsync(10).FlattenAsync();
            IGuild guild   = MainGlobal.Server;
            string removedNote = string.Empty;

            foreach (var dlMsg in downloaded)
            {
                if (dlMsg == null || dlMsg.Id == triggerMsg.Id) continue;

                var dlUser = await guild.GetUserAsync(dlMsg.Author.Id);
                string dlName = dlUser?.DisplayName ?? dlUser?.Nickname ?? dlUser?.Username
                                ?? dlMsg.Author.Username;
                string dlClean = Regex.Replace(dlName, "[^a-zA-Z0-9]+", "");
                if (dlClean.Length < 1) dlClean = "User";

                string dlContent = Regex.Replace(dlMsg.Content, PromptSpoofDetectionRegexStr, "");
                dlContent = Functions.FilterPingsAndChannelTags(dlContent);
                dlContent = Regex.Replace(dlContent, LinkDetectionRegexStr, "<url>");
                dlContent = Regex.Replace(dlContent, BannedWordsExact, "****");

                _history.Add(new ChatMessage(dlClean, dlContent,
                    isBot: dlMsg.Author.Id == BotUserId));

                if (dlMsg.Attachments.Count > 0)
                    _history.Add(new ChatMessage("System",
                        $"{dlName} attached an image", isSystem: true));
            }

            // Fuzzy ban sweep
            string allText = _history.ToCompletionString();
            string histBanned = Functions.IsSimilarToBannedWords(allText, BannedWords);
            if (histBanned.Length > 2)
            {
                foreach (string w in histBanned.Split(' '))
                {
                    string wt = w.Trim();
                    if (wt.Length > 2) _history.ReplaceInAll(wt, "****");
                }
                removedNote = " Removed all banned or similar words.";
            }

            Console.WriteLine(_history.ToCompletionString().Trim());
            Console.WriteLine($"   <Downloaded chat history successfully.{removedNote}>");
        }

        // ======================================================================
        //  Reply-check (should the bot proactively reply?)
        // ======================================================================

        private async Task ReplyCheck(SocketUserMessage msg, string inputMsgFiltered)
        {
            var backend = MainGlobal.ActiveBackend;
            if (backend == null)
            {
                MainGlobal.ActiveBackend = await BackendDetector.DetectAsync(BuildBackendCandidates());
                backend = MainGlobal.ActiveBackend;
                if (backend == null) return;
            }

            var recent = _history.GetLast(10).ToList();

            // Inject referenced-message context
            var referencedMsg = msg.ReferencedMessage as SocketUserMessage;
            if (referencedMsg != null)
            {
                string refName = referencedMsg.Author.Id == BotUserId
                    ? BotName
                    : Regex.Replace(referencedMsg.Author.Username, "[^a-zA-Z0-9]+", "");
                if (recent.Count > 0)
                    recent.Insert(recent.Count - 1, new ChatMessage(refName, referencedMsg.Content));
            }

            bool shouldReply;
            try
            {
                shouldReply = await backend.ShouldReplyAsync(BotName, recent, _appCts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"| ReplyCheck error ({backend.Name}): {ex.Message}");
                await TryFallbackBackendAsync();
                return;
            }

            if (shouldReply)
            {
                _checkTimeout  = 0;
                _llmThinkingTicks = 10;
                _history.TrimToMaxLength(_maxChatHistoryLength);
                _ = Task.Run(() => Reply(msg, inputMsgFiltered), _appCts.Token);
            }
            else
            {
                if (_checkTimeout < 3)
                    _checkTimeoutCount = ++_checkTimeout;
            }
        }

        // ======================================================================
        //  Main reply generation
        // ======================================================================

        private async Task Reply(SocketUserMessage msg, string inputMsgFiltered)
        {
            inputMsgFiltered = inputMsgFiltered
                .Replace("\n",  " ")
                .Replace("\\n", " ");

            bool takeAPicMatch = _takeAPicRegex.IsMatch(inputMsgFiltered);

            var backend = MainGlobal.ActiveBackend;
            if (backend == null)
            {
                MainGlobal.ActiveBackend = await BackendDetector.DetectAsync(BuildBackendCandidates());
                backend = MainGlobal.ActiveBackend;
                if (backend == null) { _llmThinkingTicks = 0; return; }
            }

            _history.TrimToMaxLength(_maxChatHistoryLength);

            // Build message list with optional reply context
            var referencedMsg = msg.ReferencedMessage as SocketUserMessage;
            var messages = _history.Messages.ToList();

            if (referencedMsg != null)
            {
                string refName = referencedMsg.Author.Id == BotUserId
                    ? BotName
                    : Regex.Replace(referencedMsg.Author.Username, "[^a-zA-Z0-9]+", "");
                if (messages.Count > 0)
                    messages.Insert(messages.Count - 1,
                        new ChatMessage(refName, referencedMsg.Content));
            }

            // Build request
            LlmRequest request;
            if (takeAPicMatch)
            {
                string rawInput = msg.Content;
                if (referencedMsg != null)
                    rawInput = referencedMsg.Content + "\n" + rawInput;
                rawInput = Regex.Replace(rawInput, PingAndChannelTagDetectFilterRegexStr, "");

                // Filter image prompt
                string imgPrompt = rawInput;
                imgPrompt = Regex.Replace(imgPrompt, BannedWordsExact, "");
                imgPrompt = Regex.Replace(imgPrompt, BannedWordsExactPic, "");
                imgPrompt = Regex.Replace(imgPrompt, BotNameMatchRegexStr, "",
                    RegexOptions.IgnoreCase);
                imgPrompt = imgPrompt.Replace(BotName, "");

                string imgDetected = Functions.IsSimilarToBannedWords(imgPrompt, BannedWords);
                if (imgDetected.Length > 2)
                {
                    foreach (string w in imgDetected.Split(' '))
                    {
                        string wt = w.Trim();
                        if (wt.Length > 2)
                        {
                            imgPrompt = imgPrompt.Replace(wt, "");
                            if (imgPrompt.Contains("  ")) imgPrompt = imgPrompt.Replace("  ", " ");
                        }
                    }
                }

                request = new LlmRequest
                {
                    SystemPrompt     = InputPromptStartPic,
                    Messages         = new[] { new ChatMessage("User", $"### User request: {imgPrompt}") },
                    CompletionSuffix = InputPromptEndPic,
                    MaxTokens        = 140,
                    Temperature      = 1.0,
                };
            }
            else
            {
                request = new LlmRequest
                {
                    SystemPrompt     = CharacterPrompt,
                    Messages         = messages,
                    CompletionSuffix = CompletionSuffix,
                    MaxTokens        = 280,
                    Temperature      = 1.0,
                };
            }

            msg.Channel.TriggerTypingAsync();

            // Strip control characters (cures emoji psychosis in some models)
            // No-op for chat backends but harmless
            string? rawReply;
            try
            {
                rawReply = await backend.GenerateAsync(request, _appCts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"| Reply error ({backend.Name}): {ex.Message}");
                await TryFallbackBackendAsync();
                _llmThinkingTicks = 0;
                return;
            }

            if (string.IsNullOrWhiteSpace(rawReply))
            {
                Console.WriteLine("| Empty reply from backend.");
                _llmThinkingTicks = 0;
                return;
            }

            // Sanitise reply
            string replyBanned = Functions.IsSimilarToBannedWords(rawReply, BannedWords);
            if (replyBanned.Length > 2)
            {
                foreach (string w in replyBanned.Split(' '))
                {
                    string wt = w.Trim();
                    if (wt.Length > 2)
                    {
                        rawReply = rawReply.Replace(wt, "");
                        if (rawReply.Contains("  ")) rawReply = rawReply.Replace("  ", " ");
                    }
                }
                Console.WriteLine("| Removed banned or similar words from reply.");
            }

            // Safety-strip the completion suffix (some text backends echo it)
            string trimmed = rawReply.Replace(CompletionSuffix, "").Trim();
            var promptEndMatch = Regex.Match(trimmed, PromptEndDetectionRegexStr);

            if (takeAPicMatch)
                await HandleImageReply(msg, trimmed, promptEndMatch, inputMsgFiltered);
            else
                await HandleChatReply(msg, trimmed, promptEndMatch, inputMsgFiltered);

            _llmThinkingTicks = 0;
        }

        // ── Image reply ────────────────────────────────────────────────────────

        private async Task HandleImageReply(
            SocketUserMessage msg,
            string trimmed,
            Match promptEndMatch,
            string inputMsgFiltered)
        {
            string llmPromptPic  = trimmed;
            string llmSubsequent = string.Empty;

            int endIdx = promptEndMatch.Index;
            if (endIdx >= 3)
            {
                llmPromptPic  = trimmed.Substring(0, endIdx);
                llmSubsequent = trimmed.Substring(endIdx);
            }

            await Functions.TakeAPic(msg, llmPromptPic, inputMsgFiltered);

            string botImgLine =
                $"\n[System]: {BotName} attached an image: {BotName}, " +
                $"{Functions.imgFormatString}{llmPromptPic.Replace("\n", ", ")}\n";
            Functions.imgFormatString = string.Empty;

            _history.Add(new ChatMessage("System", botImgLine.Trim(), isSystem: true));
            Console.WriteLine(botImgLine.Trim());

            // Any LLM text following the image prompt
            if (llmSubsequent.Length > 0 && llmSubsequent.Contains(CompletionSuffix))
            {
                string afterSuffix = llmSubsequent
                    .Substring(llmSubsequent.IndexOf(CompletionSuffix) + CompletionSuffix.Length)
                    .Replace(CompletionSuffix, "")
                    .Trim();
                if (afterSuffix.Length > 0)
                    await msg.ReplyAsync(afterSuffix);
            }
        }

        // ── Chat reply ─────────────────────────────────────────────────────────

        private async Task HandleChatReply(
            SocketUserMessage msg,
            string trimmed,
            Match promptEndMatch,
            string inputMsgFiltered)
        {
            int    endIdx = promptEndMatch.Index;
            string llmMsg = endIdx > 0 ? trimmed.Substring(0, endIdx) : trimmed;

            // Remove tone indicators e.g. "(excited)" at start
            llmMsg = Regex.Replace(llmMsg, ToneIndicatorDetector, "").Trim();

            // Reject gibberish
            bool hasText = Regex.IsMatch(llmMsg, @"[a-zA-Z0-9]");
            if (!hasText || llmMsg == promptEndMatch.Value)
            {
                await HandleLoop(msg, inputMsgFiltered, llmMsg, isGibberish: true);
                return;
            }

            // Similarity-based loop detection
            bool   botLooping        = false;
            string loopLineToRemove  = string.Empty;

            foreach (var line in _history.Messages)
            {
                if (line.Content.Length > 0 &&
                    Functions.LevenshteinDistance(
                        Regex.Replace(llmMsg,        @"\s+", ""),
                        Regex.Replace(line.Content,  @"\s+", "")) < llmMsg.Length / 3)
                {
                    Console.WriteLine("| Loop prevention: bot said a very similar sentence.");
                    loopLineToRemove = line.Content;
                    botLooping       = true;
                    break;
                }
            }

            if (llmMsg == _botLastReply && _loopCounts < 2)
            {
                await HandleLoop(msg, inputMsgFiltered, llmMsg, isGibberish: false);
                return;
            }
            else if (llmMsg == _botLastReply && _loopCounts >= 2)
            {
                _loopCounts = 0;
                Console.WriteLine("| Giving up on loop prevention — sending anyway.");
                await msg.Channel.SendMessageAsync(llmMsg);
                return;
            }
            else if (botLooping && loopLineToRemove.Length > 0)
            {
                _history.RemoveWhere(m => m.Content == loopLineToRemove);
                _ = Task.Run(() => Reply(msg, inputMsgFiltered), _appCts.Token);
                return;
            }

            string finalMsg = TrimRepeatedEdges(llmMsg);

            if (!botLooping)
            {
                _history.Add(new ChatMessage(BotName, finalMsg, isBot: true));
                string botLine = Regex.Replace(
                    $"{CompletionSuffix}{finalMsg}\n", LinkDetectionRegexStr, "url removed");
                Console.WriteLine(botLine.Trim());
            }

            if (finalMsg.Trim().Length > 0)
            {
                _botLastReply = finalMsg;
                await msg.Channel.SendMessageAsync(finalMsg);

                float ratio = (float)trimmed.Length / Math.Max(1, llmMsg.Length);
                if (!_longMsgWarningGiven && ratio >= 1.5f)
                {
                    _longMsgWarningGiven = true;
                    Console.WriteLine(
                        $"| Warning: raw reply was {ratio:F1}x longer than used portion. " +
                        "Consider tightening your prompts.");
                }
            }

            _loopCounts = 0;
        }

        // ── Loop recovery ──────────────────────────────────────────────────────

        private async Task HandleLoop(
            SocketUserMessage msg, string inputMsgFiltered, string llmMsg, bool isGibberish)
        {
            _loopCounts++;
            if (!isGibberish)
            {
                var kept = _history.Messages.Skip(2).ToList();
                _history.Clear();
                foreach (var m in kept) _history.Add(m);
                _history.ReplaceInAll(_botLastReply, "");
                Console.WriteLine(
                    $"| Duplicate reply detected — cleared history and retrying.\n  Msg: {llmMsg}");
            }
            else
            {
                Console.WriteLine($"| Gibberish reply — retrying.\n  Msg: {llmMsg}");
            }
            _ = Task.Run(() => Reply(msg, inputMsgFiltered), _appCts.Token);
        }

        // ── Repeated-edge trim ─────────────────────────────────────────────────

        private string TrimRepeatedEdges(string msg)
        {
            if (string.IsNullOrEmpty(_botLastReply) || string.IsNullOrEmpty(msg))
                return msg;

            int prefixLen = 1;
            while (prefixLen < msg.Length && prefixLen < _botLastReply.Length &&
                   string.Compare(msg, 0, _botLastReply, 0, prefixLen,
                       StringComparison.OrdinalIgnoreCase) == 0)
                prefixLen++;

            return prefixLen > 3 ? msg[(prefixLen - 1)..] : msg;
        }

        // ── Fallback backend ───────────────────────────────────────────────────

        private static async Task TryFallbackBackendAsync()
        {
            Console.WriteLine("| Searching for fallback backend...");
            var next = await BackendDetector.DetectAsync(BuildBackendCandidates());
            if (next != null)
            {
                Console.WriteLine($"| Switched to fallback: {next.Name}");
                MainGlobal.ActiveBackend = next;
            }
        }
    }
}
