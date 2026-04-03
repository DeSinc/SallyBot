using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SallyBot.Chat;

namespace SallyBot.Backends
{
    /// <summary>
    /// Base class for backends that speak the OpenAI-compatible
    /// <c>POST /v1/chat/completions</c> API (OpenAI, OpenRouter,
    /// LM Studio chat mode, Oobabooga chat mode, etc.).
    /// </summary>
    public abstract class OpenAiCompatibleBackend : ILlmBackend
    {
        protected readonly HttpClient Http;
        protected readonly string BaseUrl;
        protected readonly string Model;

        public abstract string Name { get; }

        protected OpenAiCompatibleBackend(string baseUrl, string model, string? apiKey = null)
        {
            BaseUrl = baseUrl.TrimEnd('/');
            Model = model;
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            if (!string.IsNullOrWhiteSpace(apiKey))
                Http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);
        }

        // ── ILlmBackend ────────────────────────────────────────────────────────

        public virtual async Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default)
        {
            var messages = BuildMessages(request);

            var payload = new
            {
                model = Model,
                messages,
                max_tokens = request.MaxTokens,
                temperature = request.Temperature,
                stop = request.StopSequences,
            };

            var json = new StringContent(
                JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync($"{BaseUrl}/v1/chat/completions", json, ct);
            resp.EnsureSuccessStatusCode();

            var body = JObject.Parse(await resp.Content.ReadAsStringAsync(ct));
            return body["choices"]?[0]?["message"]?["content"]?.Value<string>()?.Trim();
        }

        public virtual async Task<bool> ShouldReplyAsync(
            string botName,
            IReadOnlyList<ChatMessage> recentMessages,
            CancellationToken ct = default)
        {
            var chatLog = string.Join("\n", recentMessages.Select(m => m.CompletionLine));

            var messages = new[]
            {
                new
                {
                    role = "system",
                    content = $"You are a decision engine for a Discord chatbot named {botName}. " +
                              $"Given a chat excerpt, determine if {botName} is being directly addressed " +
                              $"or mentioned in a way that warrants a reply. " +
                              $"Respond with only YES or NO."
                },
                new
                {
                    role = "user",
                    content = $"Chat excerpt:\n{chatLog}\n\nShould {botName} reply?"
                },
            };

            var payload = new
            {
                model = Model,
                messages,
                max_tokens = 5,
                temperature = 0.1,
            };

            try
            {
                var json = new StringContent(
                    JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var resp = await Http.PostAsync($"{BaseUrl}/v1/chat/completions", json, ct);
                resp.EnsureSuccessStatusCode();

                var body = JObject.Parse(await resp.Content.ReadAsStringAsync(ct));
                var answer = body["choices"]?[0]?["message"]?["content"]?.Value<string>() ?? "";
                return answer.TrimStart().StartsWith("y", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public virtual async Task<bool> IsAvailableAsync()
        {
            try
            {
                var resp = await Http.GetAsync($"{BaseUrl}/v1/models");
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        protected virtual List<object> BuildMessages(LlmRequest request)
        {
            var list = new List<object>();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                list.Add(new { role = "system", content = request.SystemPrompt });
            foreach (var m in request.Messages)
                list.Add(new { role = m.Role, content = m.Content });
            return list;
        }
    }
}
