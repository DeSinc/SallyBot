using System.Collections.Generic;
using SallyBot.Chat;

namespace SallyBot.Backends
{
    /// <summary>
    /// Carries all parameters needed by any LLM backend to generate a response.
    /// Text-completion backends use <see cref="CompletionSuffix"/> to trigger the
    /// bot turn; chat-completion backends ignore it.
    /// </summary>
    public sealed class LlmRequest
    {
        /// <summary>
        /// Character / system instructions.
        /// Chat backends: sent as the "system" role message.
        /// Text backends: prepended to the prompt.
        /// </summary>
        public string SystemPrompt { get; init; } = string.Empty;

        /// <summary>Conversation history to include in the request.</summary>
        public IReadOnlyList<ChatMessage> Messages { get; init; } = new ChatMessage[0];

        /// <summary>
        /// Text appended after the message history for text-completion backends to
        /// trigger the bot's turn, e.g. <c>"### Response:\n[Sally]:"</c>.
        /// Ignored by chat-completion and Gemini backends.
        /// </summary>
        public string CompletionSuffix { get; init; } = string.Empty;

        public int MaxTokens { get; init; } = 280;
        public double Temperature { get; init; } = 1.0;

        public IReadOnlyList<string> StopSequences { get; init; } = DefaultStopSequences;

        /// <summary>
        /// Shared default stop sequences that work well across most local LLMs.
        /// </summary>
        public static readonly string[] DefaultStopSequences = new[]
        {
            "\n[", "\n>", "\n#", "\n##", "\n###",
            "# ", "## ", "### ",
            "</s>", "</p>", "</div>", "<br>",
            "<|endoftemplate|>", "<noinput>", "<nooutput>",
            "000000000000", "0123456789",
        };
    }
}
