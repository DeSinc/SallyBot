using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SallyBot.Chat;

namespace SallyBot.Backends
{
    /// <summary>
    /// Oobabooga text-generation-webui backend.
    /// Auto-detects whether to use the chat-completions or text-completions API
    /// by probing <c>/v1/chat/completions</c> on first use.
    /// Default base URL: <c>http://127.0.0.1:5000</c>.
    /// </summary>
    public sealed class OobaboogaBackend : ILlmBackend
    {
        public string Name => "Oobabooga";

        private readonly string _baseUrl;
        private ILlmBackend? _impl;   // resolved on first use

        // Static probe client: short timeout, safe to share across instances
        private static readonly HttpClient _probeHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public OobaboogaBackend(string baseUrl = "http://127.0.0.1:5000")
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        // ── ILlmBackend ────────────────────────────────────────────────────────

        public async Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default)
        {
            await EnsureImplAsync(ct);
            return _impl == null ? null : await _impl.GenerateAsync(request, ct);
        }

        public async Task<bool> ShouldReplyAsync(
            string botName, IReadOnlyList<ChatMessage> recent, CancellationToken ct = default)
        {
            await EnsureImplAsync(ct);
            return _impl != null && await _impl.ShouldReplyAsync(botName, recent, ct);
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var resp = await _probeHttp.GetAsync($"{_baseUrl}/v1/models");
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ── Private ────────────────────────────────────────────────────────────

        /// <summary>
        /// Probes the server once to decide whether it supports chat completions.
        /// Falls back to text completions when the chat endpoint is unavailable.
        /// </summary>
        private async Task EnsureImplAsync(CancellationToken ct)
        {
            if (_impl != null) return;

            bool chatOk = false;
            try
            {
                var testPayload = new
                {
                    model = string.Empty,
                    messages = new[] { new { role = "user", content = "hi" } },
                    max_tokens = 1,
                };
                var body = new StringContent(
                    JsonConvert.SerializeObject(testPayload), Encoding.UTF8, "application/json");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                var resp = await _probeHttp.PostAsync($"{_baseUrl}/v1/chat/completions", body, cts.Token);
                chatOk = resp.IsSuccessStatusCode;
            }
            catch { /* fall through to text */ }

            if (chatOk)
            {
                Console.WriteLine($"| {Name}: using chat-completions API.");
                _impl = new ChatImpl(_baseUrl);
            }
            else
            {
                Console.WriteLine($"| {Name}: using text-completions API.");
                _impl = new TextImpl(_baseUrl);
            }
        }

        // ── Inner wrappers ─────────────────────────────────────────────────────

        private sealed class ChatImpl : OpenAiCompatibleBackend
        {
            public override string Name => "Oobabooga (chat)";
            public ChatImpl(string baseUrl) : base(baseUrl, string.Empty) { }
        }

        private sealed class TextImpl : TextCompletionBackend
        {
            public override string Name => "Oobabooga (text)";
            public TextImpl(string baseUrl) : base(baseUrl) { }
        }
    }
}
