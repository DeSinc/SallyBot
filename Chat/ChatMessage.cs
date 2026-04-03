namespace SallyBot.Chat
{
    /// <summary>
    /// Represents a single message in a chat conversation.
    /// Carries both text-completion formatting ([Author]: Content)
    /// and chat-API role information (user / assistant).
    /// </summary>
    public sealed class ChatMessage
    {
        public ChatMessage(string author, string content, bool isBot = false, bool isSystem = false)
        {
            Author = author;
            Content = content;
            IsBot = isBot;
            IsSystem = isSystem;
        }

        /// <summary>Display name of the sender.</summary>
        public string Author { get; }

        /// <summary>Raw message text.</summary>
        public string Content { get; }

        /// <summary>True when this message was sent by the bot.</summary>
        public bool IsBot { get; }

        /// <summary>True for synthetic system annotations (e.g. "[System]: user attached an image").</summary>
        public bool IsSystem { get; }

        /// <summary>Text-completion prompt line: "[Author]: Content"</summary>
        public string CompletionLine => $"[{Author}]: {Content}";

        /// <summary>Chat-API role: "assistant" for bot messages, "user" for everything else.</summary>
        public string Role => IsBot ? "assistant" : "user";
    }
}
