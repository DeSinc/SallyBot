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
    /// LM Studio backend.
    /// Shares the same auto-detection logic as <see cref="OobaboogaBackend"/>
    /// but defaults to LM Studio's usual port (1234) and prefers the chat API.
    /// </summary>
    public sealed class LmStudioBackend : ILlmBackend
    {
        public string Name => "LM Studio";

        private readonly string _baseUrl;
        private readonly HttpClient _probeHttp;
        private ILlmBackend? _impl;

        public LmStudioBackend(string baseUrl = "http://127.0.0.1:1234")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _probeHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
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
            catch { }

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

        private sealed class ChatImpl : OpenAiCompatibleBackend
        {
            public override string Name => "LM Studio (chat)";
            public ChatImpl(string baseUrl) : base(baseUrl, string.Empty) { }
        }

        private sealed class TextImpl : TextCompletionBackend
        {
            public override string Name => "LM Studio (text)";
            public TextImpl(string baseUrl) : base(baseUrl) { }
        }
    }
}
