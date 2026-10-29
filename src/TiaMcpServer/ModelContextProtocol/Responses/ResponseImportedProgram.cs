using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseImportedProgram : ResponseMessage
    {
        public string? Directory { get; set; }

        /// <summary>Objects imported per area (blocks, types) and per format (source, xml).</summary>
        public IDictionary<string, int>? Written { get; set; }

        public IEnumerable<ResponseImportedProgramItem>? Items { get; set; }

        /// <summary>Files that never imported, with the error of the last attempt.</summary>
        public IEnumerable<string>? Failures { get; set; }

        /// <summary>Files that look like part of the tree but are not imported, with the reason.</summary>
        public IEnumerable<string>? Ignored { get; set; }

        /// <summary>How many passes over the pending files were made; more than 1 means some objects waited for others.</summary>
        public int? Rounds { get; set; }
    }
}
