using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SallyBot.Chat;

namespace SallyBot.Backends
{
    /// <summary>
    /// Google Gemini backend (generativelanguage.googleapis.com).
    /// Uses Google's own REST API format (not OpenAI-compatible).
    /// </summary>
    public sealed class GeminiBackend : ILlmBackend
    {
        public string Name => "Google Gemini";

        private readonly string _apiKey;
        private readonly string _model;
        private readonly HttpClient _http;

        public GeminiBackend(string apiKey, string model = "gemini-pro")
        {
            _apiKey = apiKey;
            _model = model;
            _http = new HttpClient
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com"),
                Timeout = TimeSpan.FromSeconds(60),
            };
        }

        // ── ILlmBackend ────────────────────────────────────────────────────────

        public async Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default)
        {
            var prompt = BuildPrompt(request);

            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } },
                },
                generation_config = new
                {
                    temperature = request.Temperature,
                    max_output_tokens = request.MaxTokens,
                    stop_sequences = request.StopSequences,
                },
                safety_settings = SafetySettings(),
            };

            var json = new StringContent(
                JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(
                $"/v1beta/models/{_model}:generateContent?key={_apiKey}", json, ct);

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"| Gemini: HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
                return null;
            }

            var body = JObject.Parse(await resp.Content.ReadAsStringAsync(ct));
            var text = body["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.Value<string>();

            if (text == null)
            {
                // Surface block reason when present
                var blockReason = body["promptFeedback"]?["blockReason"]?.Value<string>();
                if (blockReason != null)
                    Console.WriteLine($"| Gemini: response blocked – {blockReason}");
                else
                    Console.WriteLine($"| Gemini: unexpected response format.\n{body}");
            }

            return text?.Trim();
        }

        public async Task<bool> ShouldReplyAsync(
            string botName,
            IReadOnlyList<ChatMessage> recentMessages,
            CancellationToken ct = default)
        {
            var recentText = string.Join("\n", recentMessages.Select(m => m.CompletionLine));
            var prompt =
                $"### Context: You are a chat-bot named {botName}. Below is an excerpt from a Discord " +
                $"chat room with many users in it. The users are talking amongst themselves. " +
                $"{botName} speaks sometimes.\n\n{recentText}\n" +
                $"### Instruction: Analyse the last line. Is {botName} being spoken to directly, " +
                $"or mentioned by name such that a reply is warranted? Answer YES if so.\n### Verdict:";

            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generation_config = new { temperature = 0.1, max_output_tokens = 2 },
                safety_settings = SafetySettings(),
            };

            try
            {
                var json = new StringContent(
                    JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync(
                    $"/v1beta/models/{_model}:generateContent?key={_apiKey}", json, ct);
                if (!resp.IsSuccessStatusCode) return false;

                var body = JObject.Parse(await resp.Content.ReadAsStringAsync(ct));
                var answer =
                    body["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.Value<string>() ?? "";
                return answer.TrimStart().StartsWith("y", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gemini is considered available when a non-trivial API key is configured.
        /// No network probe is performed to avoid wasting quota.
        /// </summary>
        public Task<bool> IsAvailableAsync() =>
            Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey) && _apiKey.Length > 20);

        // ── Helpers ────────────────────────────────────────────────────────────

        private string BuildPrompt(LlmRequest request)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                sb.Append(request.SystemPrompt);
                if (!request.SystemPrompt.EndsWith("\n"))
                    sb.AppendLine();
            }
            foreach (var m in request.Messages)
                sb.AppendLine(m.CompletionLine);
            if (!string.IsNullOrWhiteSpace(request.CompletionSuffix))
                sb.Append(request.CompletionSuffix);
            return sb.ToString();
        }

        private static object[] SafetySettings() => new object[]
        {
            new { category = "HARM_CATEGORY_HARASSMENT",        threshold = "BLOCK_ONLY_HIGH" },
            new { category = "HARM_CATEGORY_HATE_SPEECH",       threshold = "BLOCK_ONLY_HIGH" },
            new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_ONLY_HIGH" },
            new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_ONLY_HIGH" },
        };
    }
}
