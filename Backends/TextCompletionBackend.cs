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
    /// Base class for backends that speak the OpenAI-compatible
    /// <c>POST /v1/completions</c> (text completion) API.
    /// Used by Oobabooga and LM Studio in text-completion mode.
    /// </summary>
    public abstract class TextCompletionBackend : ILlmBackend
    {
        protected readonly HttpClient Http;
        protected readonly string BaseUrl;

        public abstract string Name { get; }

        protected TextCompletionBackend(string baseUrl)
        {
            BaseUrl = baseUrl.TrimEnd('/');
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        // ── ILlmBackend ────────────────────────────────────────────────────────

        public virtual async Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default)
        {
            var prompt = BuildPrompt(request);
            var payload = BuildPayload(prompt, request.MaxTokens, request.Temperature, request.StopSequences);

            var json = new StringContent(
                JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync($"{BaseUrl}/v1/completions", json, ct);
            resp.EnsureSuccessStatusCode();

            var body = JObject.Parse(await resp.Content.ReadAsStringAsync(ct));
            return body["choices"]?[0]?["text"]?.Value<string>()?.Trim();
        }

        public virtual async Task<bool> ShouldReplyAsync(
            string botName,
            IReadOnlyList<ChatMessage> recentMessages,
            CancellationToken ct = default)
        {
            var recentText = string.Join("\n", recentMessages.Select(m => m.CompletionLine));
            var prompt =
                $"### Context: You are a chat-bot named {botName}. Below is an excerpt from a Discord " +
                $"chat room with many users in it. The users are talking amongst themselves. " +
                $"{botName} speaks sometimes.\n\n{recentText}\n" +
                $"### Instruction: Analyse the last line of chat in the above chat log. " +
                $"Is {botName} being spoken to directly, or mentioned by name such that a reply " +
                $"is warranted? Answer YES if so.\n### Verdict:";

            var payload = BuildPayload(prompt, maxTokens: 5, temperature: 0.1,
                LlmRequest.DefaultStopSequences);

            try
            {
                var json = new StringContent(
                    JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var resp = await Http.PostAsync($"{BaseUrl}/v1/completions", json, ct);
                resp.EnsureSuccessStatusCode();

                var body = JObject.Parse(await resp.Content.ReadAsStringAsync(ct));
                var answer = body["choices"]?[0]?["text"]?.Value<string>() ?? "";
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

        /// <summary>
        /// Assembles the full text prompt:
        /// SystemPrompt → message lines → CompletionSuffix.
        /// </summary>
        protected virtual string BuildPrompt(LlmRequest request)
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

        /// <summary>
        /// Returns an anonymous object representing the Oobabooga / OpenAI-compatible
        /// completions request body, including sensible defaults for local LLMs.
        /// </summary>
        protected virtual object BuildPayload(
            string prompt,
            int maxTokens,
            double temperature,
            IReadOnlyList<string> stop)
        {
            return new
            {
                prompt,
                max_tokens = maxTokens,
                temperature,
                stop,
                n = 1,
                stream = false,
                presence_penalty = 0,
                top_p = 1,
                min_p = 0,
                top_k = 0,
                repetition_penalty = 1,
                repetition_penalty_range = 512,
                typical_p = 1,
                tfs = 1,
                top_a = 0,
                seed = -1,
                do_sample = true,
                num_beams = 1,
                add_bos_token = true,
                skip_special_tokens = true,
                ban_eos_token = false,
            };
        }
    }
}
