using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseSourceTree : ResponseMessage
    {
        public string? Directory { get; set; }

        /// <summary>Objects written per area: blocks, types, tagTables, watchTables, forceTables.</summary>
        public IDictionary<string, int>? Written { get; set; }

        /// <summary>Format used per area: 'document' where TIA Portal supports it, else 'xml'.</summary>
        public IDictionary<string, string>? Formats { get; set; }

        public IEnumerable<string>? Skipped { get; set; }

        public IEnumerable<string>? Failures { get; set; }
    }
}
