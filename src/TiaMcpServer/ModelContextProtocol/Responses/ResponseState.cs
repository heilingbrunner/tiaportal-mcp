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
        /// '--keep-structure'); every preset is binding, a call cannot change it. Null when no preset is configured.
        /// </summary>
        public System.Text.Json.Nodes.JsonObject? Presets { get; set; }

        /// <summary>
        /// The presets a call cannot override; they are not tool arguments. Every preset is binding,
        /// so this lists the same names as <see cref="Presets"/> (kept for older clients).
        /// Null when no preset is configured.
        /// </summary>
        public string[]? ForcedPresets { get; set; }
    }
}
