using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SallyBot.Backends
{
    /// <summary>
    /// Probes a prioritised list of backends and selects the first one that
    /// reports as available.  This preserves the original auto-detection
    /// behaviour: local servers are tried before cloud APIs.
    /// </summary>
    public static class BackendDetector
    {
        /// <summary>
        /// Returns the first backend from <paramref name="candidates"/> that is
        /// currently reachable, or null if none are available.
        /// </summary>
        public static async Task<ILlmBackend?> DetectAsync(IEnumerable<ILlmBackend> candidates)
        {
            foreach (var backend in candidates)
            {
                bool available;
                try
                {
                    available = await backend.IsAvailableAsync();
                }
                catch
                {
                    available = false;
                }

                if (available)
                {
                    Console.WriteLine($"| Backend selected: {backend.Name}");
                    return backend;
                }
                else
                {
                    Console.WriteLine($"| Backend not available: {backend.Name}");
                }
            }

            Console.WriteLine("| No LLM backend found. The bot will not reply until a backend becomes available.");
            return null;
        }
    }
}
