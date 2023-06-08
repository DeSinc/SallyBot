using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

// regex
using System.Text.RegularExpressions;

// Stable Diffusion required json and image manipulation packages
using Newtonsoft.Json.Linq;

using RestSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace SallyBot.Extras
{
    class Functions
    {
        // here is the default URL for stable diffusion web ui with --API param enabled in the launch parameters
        public static string stableDiffUrl = "http://127.0.0.1:7860";
        public static string imgFormatString = string.Empty;
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
        public static DateTime GetCurrentTimeInAustralia()
        {
            DateTime utcNow = DateTime.UtcNow;
            TimeZoneInfo auTimeZone = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
            DateTime currentTimeInAu = TimeZoneInfo.ConvertTimeFromUtc(utcNow, auTimeZone);
            return currentTimeInAu;
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

        //private static async Task OobaboogaTextRequest(string parameters)
        //{
        //    string response = null;
        //    // try other commonly used ports - flip flop between them with each failed attempt till it finds the right one
        //    if (Program.oobApiEndpoint == "/api/v1/generate") // new better API, use this with the oob arg --extensions api
        //    {
        //        var content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8, "application/json");

        //        response = await httpClient.PostAsync(Program.apiUrl, content);
        //    }
        //    else if (Program.oobApiEndpoint == "/run/textgen") // old default API (busted but it kinda works)
        //    {
        //        var payload = JsonConvert.SerializeObject(new object[] { oobaboogaInputPrompt, parameters });
        //        var content = new StringContent(JsonConvert.SerializeObject(new { data = new[] { payload } }), Encoding.UTF8, "application/json");

        //        response = await httpClient.PostAsync($"http://{Program.oobServer}:{Program.oobServerPort}/run/textgen", content); // try other commonly used port 7860
        //    }
        //    return response;
        //}
        public static async Task TakeAPic(SocketUserMessage Msg, string llmPrompt, string userPrompt)
        {
            var Context = new SocketCommandContext(MainGlobal.Client, Msg);
            var user = Context.User as SocketGuildUser;

            // find the local time in japan right now to change the time of day in the selfie
            // (you can change this to another country if you understand the code)
            DateTime currentTimeInJapan = Functions.GetCurrentTimeInJapan();
            string timeOfDayInNaturalLanguage = Functions.GetTimeOfDayInNaturalLanguage(currentTimeInJapan);
            string timeOfDayStr = string.Empty;

            // adds (Night) to the image prompt if it's night in japan, etc.
            if (timeOfDayInNaturalLanguage != null)
                timeOfDayStr = $", ({timeOfDayInNaturalLanguage})";

            // POSITIVE PROMPT - put what you want YOUR bot to look like when it takes a selfie or appears in the image. The AI will put its own prompt AFTER this.
            string characterPromptImage = "A 25 year old anime woman smiling, long hair, blonde hair, blue eyes";
            string imgPrompt = string.Empty; 
            
            // NEGATIVE prompt - write what you DON'T want to see in the image here
            string imgNegPrompt = $"negative_hand-neg:1, (nsfw, naked, nude:1.6), (easynegative:1.0), (negative_hand-neg:0.9), (worst quality, low quality:1.4), 3 arms, extra arms, extra limbs";

            int width = 688;
            int height = 488;
            bool selfie = false;
            //customdscode

            if (userPrompt.Length > 4
                && llmPrompt.Trim().Length > 2)
            {
                userPrompt = userPrompt.ToLower();

                if (userPrompt.Contains("selfie"))
                {
                    selfie = true;
                    imgFormatString = ", looking into the camera, ";
                }
                else if (userPrompt.Contains("person")
                        || userPrompt.Contains("you as")
                        || userPrompt.Contains("yourself as")
                        || userPrompt.Contains("you cosplaying")
                        || userPrompt.Contains("yourself cosplaying"))
                {
                    selfie = true;
                }
                else if (userPrompt.Contains(" of you"))
                {
                    selfie = true;
                }
                else if (userPrompt.Contains(" of you with "))
                {
                    selfie = true;
                    imgFormatString = ", she is with ";
                }
                else if (userPrompt.Contains(" at you"))
                {
                    selfie = true;
                }
                else if (userPrompt.Contains(" in you"))
                {
                    selfie = true;
                }
                else if (userPrompt.Contains(" for you"))
                {
                    selfie = true;
                }
                else if (userPrompt.Contains(" by you"))
                {
                    selfie = true;
                }
                else if (userPrompt.Contains(" and you"))
                {
                    selfie = true;
                }

                if (userPrompt.Contains("holding"))
                {
                    selfie = true;
                    imgFormatString += ", holding ";
                }
            }

            imgPrompt += imgFormatString;

            if (selfie)
            {
                imgPrompt = characterPromptImage + imgPrompt;
                width = 488;
                height = 688;
            }

            if (llmPrompt.Length > 0)
            {
                llmPrompt = $"(({llmPrompt}))"; // <-- emphasise the prompted thing in the image
            }

            //if (Msg.Author == MainGlobal.Server.Owner) // only owner
            imgPrompt = $"{imgPrompt} {llmPrompt}";

            var overrideSettings = new JObject
            {// uncomment this line to specify a particular image model you want to use
                //{ "sd_model_checkpoint", "AnythingV5_v5PrtRE.safetensors" }
            };

            var payload = new JObject
            {
                { "prompt", imgPrompt },
                { "negative_prompt", imgNegPrompt},
                { "steps", 20 },
                { "width", width },
                { "height", height },
                { "send_images", true },
                { "sampler_name", "DDIM" }, // set this to "DPM++ SDE Karras" if you want slightly higher quality images, but slower image generation
                { "override_settings", overrideSettings }
            };

            // Below are all the potential json tags you can send to the stable diffusion image generator.
            // Just add any of these you want to the payload section above.

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

            string url = $"{stableDiffUrl}/sdapi/v1/txt2img";
            RestClient client = new RestClient();
            RestRequest sdImgRequest = new RestRequest();
            try
            {
                client = new RestClient(url);
                sdImgRequest = new RestRequest(url, Method.Post);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No Stable Diffusion detected at {stableDiffUrl}. Run webui-user.bat with:\n" +
                "set COMMANDLINE_ARGS=--api\n" +
                "in the webui-user.bat file for Automatic1111 Stable Diffusion.\n" +
                "Stable Diffusion error message: " + ex);
            }

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

                    if (selfie)
                        sdImgFilePath = $"cutepic.png"; //

                    image.Save(sdImgFilePath, new PngEncoder());

                    Task.Delay(1000).Wait();

                    using var fileStream = new FileStream(sdImgFilePath, FileMode.Open, FileAccess.Read);
                    var file = new FileAttachment(fileStream, "pic.png");
                    if (Msg.Reference != null)
                        await Context.Channel.SendFileAsync(sdImgFilePath, null, false, null, null, false, null, Msg.Reference);
                    else
                    {
                        var messageReference = new MessageReference(Msg.Id);
                        await Context.Channel.SendFileAsync(sdImgFilePath, null, false, null, null, false, null, messageReference);
                    }
                }
            }
            else
            {
                Console.WriteLine("Request failed: " + sdImgResponse.ErrorMessage);
            }
        }
    }
}
