using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Shared response shapes for the project-mutating tools in McpServerWrite.
    ///
    /// Callers: every tool in McpServerWrite.cs. Affected API: additive only - new DTOs, no
    /// existing response changed. Data: serialized into MCP structuredContent; all fields are
    /// nullable strings or string collections, no dates. No file I/O.
    ///
    /// Three shapes cover roughly thirty tools deliberately: minting one DTO per operation would
    /// triple the output schema surface for no gain to the caller.
    /// </summary>
    public class ResponseCreated : ResponseMessage
    {
        /// <summary>What was created, e.g. "Tag table", "Block group", "FB".</summary>
        public string? Kind { get; set; }
        public string? Name { get; set; }

        /// <summary>Root-relative path of the new object, where one applies.</summary>
        public string? Path { get; set; }
    }

    public class ResponseDeleted : ResponseMessage
    {
        public string? Kind { get; set; }
        public string? Path { get; set; }
    }

    public class ResponseRenamed : ResponseMessage
    {
        public string? Kind { get; set; }
        public string? OldPath { get; set; }
        public string? NewName { get; set; }
        public string? NewPath { get; set; }
    }

    public class ResponseImported : ResponseMessage
    {
        public string? Kind { get; set; }
        public string? GroupPath { get; set; }
        public string? ImportPath { get; set; }
    }

    public class ResponseGenerateBlocks : ResponseMessage
    {
        public IEnumerable<string>? GeneratedNames { get; set; }
        public int? Count { get; set; }
    }
}
