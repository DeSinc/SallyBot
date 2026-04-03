using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SallyBot.Chat;

namespace SallyBot.Backends
{
    /// <summary>
    /// Abstraction over any LLM backend that SallyBot can talk to.
    /// </summary>
    public interface ILlmBackend
    {
        /// <summary>Human-readable name used in logs.</summary>
        string Name { get; }

        /// <summary>
        /// Generate a reply from the language model.
        /// Returns null on error or empty response.
        /// </summary>
        Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default);

        /// <summary>
        /// Determine whether the bot should proactively reply to the latest message
        /// in <paramref name="recentMessages"/>.
        /// </summary>
        Task<bool> ShouldReplyAsync(
            string botName,
            IReadOnlyList<ChatMessage> recentMessages,
            CancellationToken ct = default);

        /// <summary>Returns true when the backend can currently accept requests.</summary>
        Task<bool> IsAvailableAsync();
    }
}
