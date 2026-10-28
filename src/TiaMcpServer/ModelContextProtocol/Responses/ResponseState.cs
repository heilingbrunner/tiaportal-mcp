using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseState : ResponseMessage
    {
        public int? PortalId { get; set; }
        public bool? IsConnected { get; set; }
        public string? Project { get; set; }
        public string? Session { get; set; }

        /// <summary>
        /// Whether the server was started with '--allow-write'. When false the project-mutating
        /// tools are not registered at all, so a client can tell why they are missing.
        /// </summary>
        public bool? AllowWrite { get; set; }

        /// <summary>
        /// The tool arguments preset on the command line ('--software-path', '--export-path',
        /// '--preserve-path'); a call may omit them. Null when no preset is configured.
        /// </summary>
        public System.Text.Json.Nodes.JsonObject? Presets { get; set; }

        /// <summary>
        /// The presets a call cannot override ('--force-presets'); they are not tool arguments.
        /// Null when nothing is forced.
        /// </summary>
        public string[]? ForcedPresets { get; set; }
    }
}
