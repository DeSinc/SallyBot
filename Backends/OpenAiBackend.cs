namespace SallyBot.Backends
{
    /// <summary>
    /// OpenAI backend (api.openai.com).
    /// Uses the standard chat-completions API.
    /// </summary>
    public sealed class OpenAiBackend : OpenAiCompatibleBackend
    {
        public override string Name => "OpenAI";

        /// <param name="apiKey">Your OpenAI secret key.</param>
        /// <param name="model">Model name, e.g. "gpt-4o" or "gpt-4o-mini".</param>
        public OpenAiBackend(string apiKey, string model = "gpt-4o-mini")
            : base("https://api.openai.com", model, apiKey) { }
    }
}
