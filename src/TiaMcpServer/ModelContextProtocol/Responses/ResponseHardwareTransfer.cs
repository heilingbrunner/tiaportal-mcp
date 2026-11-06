using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseHardwareTransfer : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public bool DryRun { get; set; }
        public string? State { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>The complete result as markdown tables (state, counts, every message); show it to the user unchanged.</summary>
        public string? Report { get; set; }
    }
}
