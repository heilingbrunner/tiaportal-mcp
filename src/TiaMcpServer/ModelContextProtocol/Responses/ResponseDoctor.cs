using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseDoctor : ResponseMessage
    {
        public string? Report { get; set; }

        /// <summary>Assembly version of TiaMcpServer.exe, e.g. "0.4.0.0".</summary>
        public string? ServerVersion { get; set; }
        public bool? IsConnected { get; set; }
        public int? ActiveTiaMajorVersion { get; set; }
        public string? ProjectName { get; set; }
        public string? ProjectPath { get; set; }
        public bool? IsUserInGroup { get; set; }
        public bool? AllowWrite { get; set; }

        /// <summary>All command line options (from the client's JSON config args) with their effective value.</summary>
        public System.Text.Json.Nodes.JsonObject? Options { get; set; }
        public IEnumerable<ResponseTiaInstallation>? Installations { get; set; }
    }
}
