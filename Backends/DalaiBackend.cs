using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SocketIOClient;
using SallyBot.Chat;

namespace SallyBot.Backends
{
    /// <summary>
    /// Dalai backend (https://github.com/cocktailpeanut/dalai).
    /// Communicates via Socket.IO using streaming token events.
    /// </summary>
    public sealed class DalaiBackend : ILlmBackend
    {
        public string Name => "Dalai";
        public bool IsConnected { get; private set; }

        private readonly string _serverUrl;
        private readonly string _model;
        private SocketIO? _socket;

        public DalaiBackend(string serverUrl = "http://localhost:3000", string model = "alpaca.7B")
        {
            _serverUrl = serverUrl;
            _model     = model;
        }

        // ── Connection management ─────────────────────────────────────────────

        /// <summary>Attempt to connect to the Dalai Socket.IO server.</summary>
        public async Task ConnectAsync()
        {
            try
            {
                _socket = new SocketIO(_serverUrl);
                _socket.OnConnected += (_, _) =>
                {
                    Console.WriteLine("| Connected to Dalai server.");
                    IsConnected = true;
                };
                _socket.On("disconnect", _ =>
                {
                    Console.WriteLine("| Dalai server disconnected.");
                    IsConnected = false;
                });
                await _socket.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"| Could not connect to Dalai at {_serverUrl}: {ex.Message}");
            }
        }

        // ── ILlmBackend ────────────────────────────────────────────────────────

        public async Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default)
        {
            if (_socket == null || !IsConnected)
                return null;

            var prompt = BuildPrompt(request);

            var dalaiRequest = new
            {
                seed = -1,
                threads = 4,
                n_predict = request.MaxTokens,
                top_k = 40,
                top_p = 0.9,
                temp = request.Temperature,
                repeat_last_n = 64,
                repeat_penalty = 1.1,
                debug = false,
                model = _model,
                prompt,
            };

            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var accumulated = new StringBuilder(); // all tokens seen so far
            var response = new StringBuilder();   // tokens after the prompt suffix
            bool listening = false;
            string suffix = request.CompletionSuffix;

            _socket.On("result", resultRaw =>
            {
                if (tcs.Task.IsCompleted) return;

                var token = ExtractToken(resultRaw.ToString());
                if (string.IsNullOrEmpty(token)) return;

                if (!listening)
                {
                    accumulated.Append(token);
                    var acc = accumulated.ToString();
                    if (!string.IsNullOrEmpty(suffix) && acc.Contains(suffix))
                    {
                        // Start collecting the actual reply
                        int end = acc.IndexOf(suffix, StringComparison.Ordinal) + suffix.Length;
                        response.Append(acc.Substring(end));
                        listening = true;
                    }
                    else if (string.IsNullOrEmpty(suffix))
                    {
                        // No suffix expected – start from the very first token
                        response.Append(token);
                        listening = true;
                    }
                }
                else
                {
                    response.Append(token);
                    var current = response.ToString();

                    // Check stop sequences
                    string? hitStop = null;
                    foreach (var s in request.StopSequences)
                    {
                        if (current.Contains(s)) { hitStop = s; break; }
                    }
                    bool tooLong = current.Length > 500;

                    if (hitStop != null || tooLong)
                    {
                        var final = current;
                        if (hitStop != null)
                        {
                            int idx = final.IndexOf(hitStop, StringComparison.Ordinal);
                            if (idx >= 0) final = final.Substring(0, idx);
                        }
                        _socket.Off("result");
                        _socket.EmitAsync("stop").ConfigureAwait(false);
                        tcs.TrySetResult(final.Trim());
                    }
                }
            });

            ct.Register(() =>
            {
                _socket?.Off("result");
                tcs.TrySetCanceled();
            });

            await _socket.EmitAsync("request", dalaiRequest);

            try
            {
                return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
            }
            catch (TimeoutException)
            {
                _socket?.Off("result");
                Console.WriteLine("| Dalai: generation timed out.");
                return null;
            }
        }

        /// <summary>
        /// Dalai does not have an efficient reply-check path.
        /// Returns false so the bot only replies when explicitly mentioned.
        /// </summary>
        public Task<bool> ShouldReplyAsync(
            string botName, IReadOnlyList<ChatMessage> _, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> IsAvailableAsync() =>
            Task.FromResult(IsConnected);

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string BuildPrompt(LlmRequest request)
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

        private static string ExtractToken(string raw)
        {
            int start = raw.IndexOf("\"response\":\"", StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            var token = raw.Substring(start + 12);
            int end = token.IndexOf("\",\"", StringComparison.Ordinal);
            if (end >= 0) token = token.Substring(0, end);
            return token
                .Replace("\\r", "")
                .Replace("\\n", "\n");
        }
    }
}
