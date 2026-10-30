using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseGeneratedSources : ResponseMessage
    {
        public string? Directory { get; set; }

        /// <summary>Files written per area: blocks, types.</summary>
        public IDictionary<string, int>? Written { get; set; }

        public IEnumerable<ResponseGeneratedSourceItem>? Items { get; set; }

        /// <summary>Objects deliberately left out, with the reason.</summary>
        public IEnumerable<string>? Skipped { get; set; }

        /// <summary>Objects that failed to generate, with the reason.</summary>
        public IEnumerable<string>? Failures { get; set; }

        /// <summary>Things to check or know after the run, e.g. a created settings file. Null when there are none.</summary>
        public IEnumerable<string>? Notes { get; set; }
    }
}
