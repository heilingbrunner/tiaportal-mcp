using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseCompile : ResponseMessage
    {
        /// <summary>Success, Information, Warning or Error, from CompilerResultState.</summary>
        public string? State { get; set; }

        public int? ErrorCount { get; set; }

        public int? WarningCount { get; set; }

        /// <summary>
        /// The compiler message tree flattened depth-first. Each entry names the object it
        /// belongs to, so a caller can go straight to what failed instead of re-reading the
        /// whole PLC.
        /// </summary>
        public IEnumerable<CompileMessage>? Messages { get; set; }

        /// <summary>The complete result as markdown tables (counts, every compiler message); show it to the user unchanged.</summary>
        public string? Report { get; set; }
    }
}
