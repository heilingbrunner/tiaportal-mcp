using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class Attribute
    {
        public string? Name { get; set; }
        public object? Value { get; set; }
        public string? AccessMode { get; set; }
    }

    public class BlockGroupInfo
    {
        public string? Name { get; set; }
        public IEnumerable<BlockGroupInfo>? Groups { get; set; }
        public IEnumerable<ResponseBlockInfo>? Blocks { get; set; }
    }

    /// <summary>
    /// One row of a watch or force table. The three row kinds (watch entry, force entry and a
    /// plain comment row) share a composition in Openness, so they share one DTO here and the
    /// fields that do not apply to a given kind stay null.
    /// </summary>
    public class TableEntryInfo
    {
        /// <summary>"Watch", "Force" or "Comment".</summary>
        public string? Kind { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? DisplayFormat { get; set; }
        public string? MonitorTrigger { get; set; }

        // Watch rows only.
        public string? ModifyTrigger { get; set; }
        public string? ModifyValue { get; set; }
        public string? ModifyIntention { get; set; }

        // Force rows only.
        public string? ForceValue { get; set; }
        public string? ForceIntention { get; set; }
    }

    #region cross references

    /// <summary>
    /// An object that owns references. Children recurse, so the depth is capped by the caller -
    /// a full AllObjects dump of a real PLC is otherwise megabytes.
    /// </summary>
    public class CrossRefSource
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? Address { get; set; }
        public string? Device { get; set; }
        public string? TypeName { get; set; }
        public IEnumerable<CrossRefReference>? References { get; set; }
        public IEnumerable<CrossRefSource>? Children { get; set; }
    }

    public class CrossRefReference
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? Address { get; set; }
        public string? Device { get; set; }
        public string? TypeName { get; set; }

        /// <summary>Only populated at maxDepth 3.</summary>
        public IEnumerable<CrossRefLocation>? Locations { get; set; }
    }

    public class CrossRefLocation
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? TypeName { get; set; }
        public string? Access { get; set; }
        public string? ReferenceType { get; set; }
        public string? ReferenceLocation { get; set; }
        public string? ReferencedAsName { get; set; }
    }

    #endregion
}
