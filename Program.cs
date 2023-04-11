using System;
using System.Collections.Generic;
using System.Linq;

using Discord.WebSocket;

using System.Text.RegularExpressions;

namespace SallyBot.Extras
{
    class Functions
    {
        static internal string pingAndChannelTagDetectFilterRegexStr = @"<[@#]\d{15,}>";
        public static string FilterPingsAndChannelTags(string inputMsg)
        {
            Regex pingAndChannelTagDetectionRegex = new Regex(pingAndChannelTagDetectFilterRegexStr);
            // replace pings and channel tags with their actual names
            var matches = pingAndChannelTagDetectionRegex.Matches(inputMsg);
            // get only unique matches
            var uniqueMatches = matches
                                .OfType<Match>()
                                .Select(m => m.Value)
                                .Distinct()
                                .ToList();

            foreach (var match in uniqueMatches)
            {
                string matchedTag = match;
                ulong matchedId = ulong.Parse(Regex.Match(matchedTag, @"\d+").Value);

                SocketGuildUser matchedUser;
                SocketGuildChannel matchedChannel;

                if (matchedTag.Contains("@"))
                    try
                    {
                        matchedUser = MainGlobal.Server.GetUser(matchedId);
                        if (matchedUser != null)
                        {
                            inputMsg = inputMsg.Replace(matchedTag, $"@{matchedUser.Username}#{matchedUser.Discriminator}");
                        }
                    }
                    catch
                    {
                        break; // not a real ID, break here
                    }
                else if (matchedTag.Contains("#"))
                    try
                    {
                        matchedChannel = MainGlobal.Server.GetChannel(matchedId);
                        inputMsg = inputMsg.Replace(matchedTag, $"#{matchedChannel.Name}");
                    }
                    catch
                    {
                        break; // not a real ID, break here
                    }
                else
                    break; // you somehow escaped this function without matching either, so break now before the code breaks
            }
            return inputMsg;
        }

        public static string IsSimilarToBannedWords(string input, List<string> bannedWords)
        {
            int threshold = 0;
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
            if (detectedWordsStr.Length > 0)
                Console.WriteLine(); // finish on a new line ready for the next console message
            return detectedWordsStr;
        }
        public static DateTime GetCurrentTimeInJapan()
        {
            DateTime utcNow = DateTime.UtcNow;
            TimeZoneInfo japanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            DateTime currentTimeInJapan = TimeZoneInfo.ConvertTimeFromUtc(utcNow, japanTimeZone);
            return currentTimeInJapan;
        }

        public static string GetTimeOfDayInNaturalLanguage(DateTime dateTime)
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
    }
}
