namespace SallyBot.Backends
{
    /// <summary>
    /// OpenRouter backend (openrouter.ai).
    /// OpenAI-compatible chat-completions API with access to many models.
    /// </summary>
    public sealed class OpenRouterBackend : OpenAiCompatibleBackend
    {
        public override string Name => "OpenRouter";

        /// <param name="apiKey">Your OpenRouter API key.</param>
        /// <param name="model">Model identifier, e.g. "openai/gpt-4o-mini".</param>
        public OpenRouterBackend(string apiKey, string model = "openai/gpt-4o-mini")
            : base("https://openrouter.ai/api", model, apiKey) { }
    }
}
