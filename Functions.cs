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
using System.Text.Json;
using System.Linq;

namespace SallyBot.Extras
{
    public static class Functions
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

            if (hour >= 5 && hour < 11)
            {
                return "Morning";
            }

            else if (hour >= 11 && hour < 15)
            {
                return "Mid-day";
            }

            else if (hour >= 15 && hour < 17)
            {
                return "Afternoon";
            }

            else if (hour >= 17 && hour < 21)
            {
                return "Evening";
            }

            return "Night";
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

        //private static async Task textGenWebUiTextRequest(string parameters)
        //{
        //    string response = null;
        //    // try other commonly used ports - flip flop between them with each failed attempt till it finds the right one
        //    if (Program.textGenWebUiApiEndpoint == "/api/v1/generate") // new better API, use this with the textGenWebUi arg --extensions api
        //    {
        //        var content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8, "application/json");

        //        response = await httpClient.PostAsync(Program.apiUrl, content);
        //    }
        //    else if (Program.textGenWebUiApiEndpoint == "/run/textgen") // old default API (busted but it kinda works)
        //    {
        //        var payload = JsonConvert.SerializeObject(new object[] { inputPrompt, parameters });
        //        var content = new StringContent(JsonConvert.SerializeObject(new { data = new[] { payload } }), Encoding.UTF8, "application/json");

        //        response = await httpClient.PostAsync($"http://{Program.textGenWebUiServer}:{Program.textGenWebUiServerPort}/run/textgen", content); // try other commonly used port 7860
        //    }
        //    return response;
        //}

        public static async Task TakeAPic(SocketUserMessage Msg, string llmPrompt, string userPrompt)
        {
            Msg.Channel.TriggerTypingAsync();
            // find the local time in japan right now to change the time of day in the selfie
            // (you can change this to another country if you understand the code)
            DateTime currentTimeInJapan = GetCurrentTimeInJapan();
            string timeOfDayInNaturalLanguage = GetTimeOfDayInNaturalLanguage(currentTimeInJapan);
            string timeOfDayStr = string.Empty;

            // adds (Night) to the image prompt if it's night in japan, etc.
            if (timeOfDayInNaturalLanguage != null)
                timeOfDayStr = $", ({timeOfDayInNaturalLanguage})";

            // POSITIVE PROMPT - put what you want YOUR bot to look like when it takes a selfie or appears in the image. The AI will put its own prompt AFTER this.
            string characterPromptImage = "A 25 year old anime woman smiling, long hair, blonde hair, blue eyes";
            string imgPrompt = string.Empty; 
            
            // NEGATIVE prompt - write what you DON'T want to see in the image here
            string imgNegPrompt = $"(worst quality, low quality:1.4), (nsfw:1.5), (easynegative:1.2), (negative_hand-neg:0.9), (worst quality, low quality:1.4), 3 arms, extra arms, extra limbs";

            int width = 688;
            int height = 488;
            bool selfie = false;

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

            if (selfie && (Msg.Attachments == null || Msg.Attachments.Count == 0))
            {
                imgPrompt = characterPromptImage + imgPrompt;
                height = 768;
            }
            else
                width = 768;

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
                // below options are all for upscaling (much improved details like hands etc. at the cost of a lot of speed)
                { "enable_hr", false }, // set this to true to upscale your image to 2x size with significant quality improvements (better hands better details etc) at cost of significantly longer generation time
                { "denoising_strength", 0.3 },
                //{ "firstphase_width", width },
                //{ "firstphase_height", height },
                { "hr_scale", 2 },
                { "hr_resize_x", width*2 },
                { "hr_resize_y", height*2 },
                { "hr_upscaler", "R-ESRGAN 4x+ Anime6B" },
                { "hr_second_pass_steps", 4 },
                { "override_settings", overrideSettings }
            };

            string url = $"{stableDiffUrl}/sdapi/v1/txt2img";

            // if an attached image is present in the user's image request, it uses this image as the base for making the new image (img2img)
            if (Msg.Attachments != null && Msg.Attachments.Count > 0)
            {
                // prevent lag from users sending the bot very large 2k sized images and crashing your SD
                if (height > 768)
                    height = 768;
                if (width > 768)
                    width = 768;

                url = $"{stableDiffUrl}/sdapi/v1/img2img";
                byte[] imageData;
                using (var client3 = new System.Net.Http.HttpClient())
                {
                    imageData = await client3.GetByteArrayAsync(Msg.Attachments.FirstOrDefault().Url);
                }

                // Convert the image data to base64
                string img_base64 = Convert.ToBase64String(imageData);
                var denoiseStrength = 0.35;

                var payload2 = new
                {
                    init_images = new List<string> { img_base64 },
                    resize_mode = 0,
                    denoising_strength = denoiseStrength,
                    image_cfg_scale = 9,
                    //mask = "string",
                    //mask_blur = 4,
                    //inpainting_fill = 0,
                    //inpaint_full_res = true,
                    //inpaint_full_res_padding = 0,
                    //inpainting_mask_invert = 0,
                    //initial_noise_multiplier = 0,
                    prompt = imgPrompt,
                    //styles = new List<string> { "string" },
                    //seed = -1,
                    //subseed = -1,
                    //subseed_strength = 0,
                    //seed_resize_from_h = -1,
                    //seed_resize_from_w = -1,
                    sampler_name = "DPM++ SDE Karras",
                    batch_size = 1,
                    n_iter = 1,
                    steps = 20,
                    cfg_scale = 7,
                    width = Msg.Attachments.FirstOrDefault().Width,
                    height = Msg.Attachments.FirstOrDefault().Height,
                    restore_faces = false,
                    tiling = false,
                    //do_not_save_samples = false,
                    //do_not_save_grid = false,
                    negative_prompt = imgNegPrompt,
                    //eta = 0,
                    //s_churn = 0,
                    //s_tmax = 0,
                    //s_tmin = 0,
                    //s_noise = 1,
                    //override_settings = new Dictionary<string, object>(),
                    //override_settings_restore_afterwards = true,
                    //script_args = new List<string>() { "", "64", "ESRGAN_4x", "1.875"},
                    //sampler_index = "Euler",
                    include_init_images = false,
                    //script_name = "SD upscale",
                    //send_images = true,
                    //save_images = false,
                    //alwayson_scripts = new Dictionary<string, object>()
                };
                payload = JObject.FromObject(payload2);
            }

            // Below are all the potential json tags you can send to the stable diffusion image generator.
            // Just add any of these you want to the payload section above.

            //"enable_hr": false,
            //"denoising_strength": 0.3,
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

            RestClient client = new RestClient();
            RestRequest sdImgRequest = new RestRequest();
            try
            {
                var options = new RestClientOptions(url)
                {
                    ThrowOnAnyError = true,
                    MaxTimeout = 200000     // Max allowed timeout for the image request in miliseconds
                };

                client = new RestClient(options);
                sdImgRequest = new RestRequest(url, Method.Post);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No Stable Diffusion detected at {stableDiffUrl}. Run webui-user.bat with:\n" +
                "set COMMANDLINE_ARGS=--api\n" +
                "in the webui-user.bat file for Automatic1111 Stable Diffusion.\n" +
                "Stable Diffusion error message: " + ex);
                return;
            }
            
            sdImgRequest.AddHeader("Content-Type", "application/json");
            sdImgRequest.AddParameter("application/json", payload.ToString(), ParameterType.RequestBody);
            sdImgRequest.AddParameter("application/json", overrideSettings.ToString(), ParameterType.RequestBody);
            sdImgRequest.Timeout = 200000; // Set the actual timeout for the image generation here, in miliseconds

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
                    string sdImgFilePath = "pic.png"; // put whatever file path you like here

                    if (selfie)
                        sdImgFilePath = "cutepic.png";

                    image.Save(sdImgFilePath, new PngEncoder());

                    Task.Delay(1000).Wait();

                    using var fileStream = new FileStream(sdImgFilePath, FileMode.Open, FileAccess.Read);
                    var file = new FileAttachment(fileStream, "cutepic.png");
                    if (Msg.Reference != null)
                        await Msg.Channel.SendFileAsync(sdImgFilePath, null, false, null, null, false, null, Msg.Reference);
                    else
                    {
                        var messageReference = new MessageReference(Msg.Id);
                        await Msg.Channel.SendFileAsync(sdImgFilePath, null, false, null, null, false, null, messageReference);
                    }
                }
            }
            else
            {
                Console.WriteLine("Request failed: " + sdImgResponse.ErrorMessage);
            }
        }

        public static string FilterPingsAndChannelTags(string inputMsg)
        {
            Regex pingAndChannelTagDetectionRegex = new Regex(Program.pingAndChannelTagDetectFilterRegexStr);
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

                        if (matchedUser.Nickname != null
                            && matchedUser.Nickname.Length > 0)
                            inputMsg = inputMsg.Replace(matchedTag, $"{matchedUser.Nickname}");
                        else
                            inputMsg = inputMsg.Replace(matchedTag, $"{matchedUser.Username}");
                        //inputMsg = inputMsg.Replace(matchedTag, $"@{matchedUser.Username}#{matchedUser.Discriminator}");
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

        public static async Task CheckJsonResponse(string result, bool replyCheck)
        {
            // Parse the JSON output into a JsonDocument object
            JsonDocument doc = JsonDocument.Parse(result);

            // Get the root element of the JSON document
            JsonElement root = doc.RootElement;

            // Try to get the "candidates" property as an array
            bool foundCandidates = root.TryGetProperty("candidates", out JsonElement candidates);

            // Also check for a "promptFeedback" property as an array
            bool foundFeedback = root.TryGetProperty("promptFeedback", out JsonElement promptFeedback);
            if (foundCandidates)
            {
                // Get the first element of the array
                JsonElement first = candidates[0];

                // Get the "content" property as an object
                JsonElement jsonContent = first.GetProperty("content");

                // Get the "parts" property as an array
                JsonElement parts = jsonContent.GetProperty("parts");

                // Get the first element of the array
                JsonElement part = parts[0];

                // Get the "text" property as a string
                string text = part.GetProperty("text").GetString();

                Program.botReply = text;

                if (replyCheck && // if this is a reply check, not a full reply request
                    Program.botReply.ToLower().Trim()
                    .Contains('y')) // and the reply check came back with a "YES" or "Y" or other affirmative response
                {
                    // flag LLM to generate a reply on next tick or followthrough code
                    Program.botWillReply = true;

                    // clear botReply so the bot doesn't shout "YES" in the chat if the reply fails to populate this field later
                    Program.botReply = string.Empty;
                }
            }
            else if (foundFeedback)
            {
                //// Get the first element of the array
                //JsonElement first = promptFeedback[0];

                //// Get the "content" property as an object
                //JsonElement jsonContent = first.GetProperty("blockReason");

                //// Get the "parts" property as an array
                //JsonElement parts = jsonContent.GetProperty("parts");

                //// Get the first element of the array
                //JsonElement part = parts[0];

                //// Get the "text" property as a string
                //string text = part.GetProperty("text").GetString();

                Console.WriteLine(result.ToString());
                Console.WriteLine("Error: Response was blocked due to one of the above safeguards.");
            }
            else
            {
                Console.WriteLine(result.ToString());
                Console.WriteLine("Error: The above JSON format different from expected. The usual response text was not present in the reply from the API. This might be due to an API restriction or a rate limit.");
            }
        }
    }
}
