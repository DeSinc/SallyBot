using System;
using System.Collections.Generic;
using System.Linq;

namespace SallyBot.Chat
{
    /// <summary>
    /// Agnostic in-memory chat history store.
    /// Supports both text-completion string serialisation and chat-API message lists.
    /// </summary>
    public sealed class ChatHistory
    {
        private readonly List<ChatMessage> _messages = new();

        public IReadOnlyList<ChatMessage> Messages => _messages;

        // ── Mutations ──────────────────────────────────────────────────────────

        public void Add(ChatMessage message) => _messages.Add(message);

        public void Clear() => _messages.Clear();

        /// <summary>Remove the oldest message.</summary>
        public void RemoveFirst()
        {
            if (_messages.Count > 0)
                _messages.RemoveAt(0);
        }

        /// <summary>Replace text inside every message that contains <paramref name="oldText"/>.</summary>
        public void ReplaceInAll(string oldText, string newText)
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                var m = _messages[i];
                if (m.Content.Contains(oldText))
                    _messages[i] = new ChatMessage(
                        m.Author,
                        m.Content.Replace(oldText, newText),
                        m.IsBot,
                        m.IsSystem);
            }
        }

        /// <summary>Remove all messages that match <paramref name="predicate"/>.</summary>
        public void RemoveWhere(Predicate<ChatMessage> predicate) =>
            _messages.RemoveAll(predicate);

        // ── Queries ────────────────────────────────────────────────────────────

        /// <summary>Returns the last <paramref name="count"/> messages.</summary>
        public IReadOnlyList<ChatMessage> GetLast(int count) =>
            _messages.TakeLast(Math.Max(0, count)).ToList();

        // ── Text-completion helpers ────────────────────────────────────────────

        /// <summary>
        /// Serialises every message as "[Author]: Content\n[Author]: Content\n…"
        /// This is the format used by text-completion (Oobabooga, Dalai, etc.).
        /// </summary>
        public string ToCompletionString() =>
            string.Join("\n", _messages.Select(m => m.CompletionLine));

        /// <summary>Current character length of the text-completion representation.</summary>
        public int CompletionLength => ToCompletionString().Length;

        /// <summary>
        /// Drops the oldest messages until the text-completion string is at or
        /// under <paramref name="maxLength"/> characters (always keeps at least 1 message).
        /// </summary>
        public void TrimToMaxLength(int maxLength)
        {
            while (_messages.Count > 1 && CompletionLength > maxLength)
                _messages.RemoveAt(0);
        }

        // ── Chat-API helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns an anonymous-object list suitable for JSON serialisation as
        /// the OpenAI chat-completions "messages" array.
        /// The <paramref name="systemPrompt"/> is prepended as a system role message
        /// when provided.
        /// </summary>
        public List<object> ToChatApiMessages(string? systemPrompt = null)
        {
            var result = new List<object>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                result.Add(new { role = "system", content = systemPrompt });
            foreach (var m in _messages)
                result.Add(new { role = m.Role, content = m.Content });
            return result;
        }
    }
}
