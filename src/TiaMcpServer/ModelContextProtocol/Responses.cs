using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseMessage
    {
        public string? Message { get; set; }
        public JsonObject? Meta { get; set; }
    }

    public class ResponseAttributes : ResponseMessage
    {
        public IEnumerable<Attribute>? Attributes { get; set; }
    }

    public class ResponseSoftwareInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseDeviceInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseDeviceItemInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseBlockInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? TypeName { get; set; }
        public string? Name { get; set; }
        public string? Namespace { get; set; }
        public string? ProgrammingLanguage { get; set; }
        public string? MemoryLayout { get; set; }
        public bool? IsConsistent { get; set; }
        public string? HeaderName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsKnowHowProtected { get; set; }
        public string? Description { get; set; }
    }
    public class ResponseBlocksWithHierarchy : ResponseMessage
    {
        public BlockGroupInfo? Root { get; set; }
    }

    public class ResponseTypeInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? Name { get; set; }
        public string? TypeName { get; set; }
        public string? Namespace { get; set; }
        public bool? IsConsistent { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsKnowHowProtected { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseProjectInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? Name { get; set; }
    }

    #region PLC tags, constants

    public class ResponseTagTableInfo : ResponseAttributes
    {
        /// <summary>Root-relative path, e.g. "TagGroup1/Table1". Feed back into the tag tools.</summary>
        public string? Path { get; set; }
        public string? Name { get; set; }
        public bool? IsDefault { get; set; }
        public DateTime? ModifiedTimeStamp { get; set; }
        public int? TagCount { get; set; }
        public int? UserConstantCount { get; set; }
        public int? SystemConstantCount { get; set; }
    }

    public class ResponseTagTables : ResponseMessage
    {
        public IEnumerable<ResponseTagTableInfo>? Items { get; set; }
    }

    public class ResponseTagInfo : ResponseAttributes
    {
        public string? Path { get; set; }
        public string? Name { get; set; }
        public string? TableName { get; set; }
        public string? DataTypeName { get; set; }
        public string? LogicalAddress { get; set; }
        public string? Comment { get; set; }
        public bool? ExternalAccessible { get; set; }
        public bool? ExternalVisible { get; set; }
        public bool? ExternalWritable { get; set; }
        public bool? IsSafety { get; set; }
    }

    public class ResponseTags : ResponseMessage
    {
        public IEnumerable<ResponseTagInfo>? Items { get; set; }
    }

    public class ResponseConstantInfo
    {
        public string? Name { get; set; }

        /// <summary>"User" or "System". System constants are read-only in Openness.</summary>
        public string? Kind { get; set; }
        public string? DataTypeName { get; set; }

        /// <summary>
        /// PlcConstant.Value is 'object'; it is stringified so the structured output schema
        /// stays stable across data types.
        /// </summary>
        public string? Value { get; set; }
        public string? Comment { get; set; }
    }

    public class ResponseConstants : ResponseMessage
    {
        public IEnumerable<ResponseConstantInfo>? Items { get; set; }
    }

    public class ResponseExportTagTable : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
    }

    #endregion

    #region watch and force tables

    public class ResponseWatchTableInfo : ResponseAttributes
    {
        /// <summary>Root-relative path, e.g. "WatchGroup1/WatchTable_1".</summary>
        public string? Path { get; set; }
        public string? Name { get; set; }

        /// <summary>"Watch" or "Force".</summary>
        public string? Kind { get; set; }
        public bool? IsConsistent { get; set; }
        public int? EntryCount { get; set; }

        /// <summary>Populated by GetWatchTableInfo / GetForceTable, not by the list tools.</summary>
        public IEnumerable<TableEntryInfo>? Entries { get; set; }
    }

    public class ResponseWatchTables : ResponseMessage
    {
        public IEnumerable<ResponseWatchTableInfo>? Items { get; set; }
    }

    public class ResponseExportWatchTable : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
    }

    #endregion

    #region external sources

    public class ResponseExternalSourceInfo : ResponseAttributes
    {
        /// <summary>Root-relative path, e.g. "SourceGroup1/Source_1".</summary>
        public string? Path { get; set; }
        public string? Name { get; set; }
    }

    public class ResponseExternalSources : ResponseMessage
    {
        public IEnumerable<ResponseExternalSourceInfo>? Items { get; set; }
    }

    #endregion

    #region cross references

    public class ResponseCrossReferences : ResponseMessage
    {
        public IEnumerable<CrossRefSource>? Sources { get; set; }
        public int? SourceCount { get; set; }
        public int? ReferenceCount { get; set; }

        /// <summary>
        /// True when maxDepth cut the result short, so the caller knows to re-query a specific
        /// object with a higher depth rather than assuming the tree was complete.
        /// </summary>
        public bool? Truncated { get; set; }
    }

    #endregion

    public class ResponseConnect : ResponseMessage
    {
    }

    public class ResponseDisconnect : ResponseMessage
    {
    }

    public class ResponseState : ResponseMessage
    {
        public bool? IsConnected { get; set; }
        public string? Project { get; set; }
        public string? Session { get; set; }

        /// <summary>
        /// Whether the server was started with '--allow-write'. When false the project-mutating
        /// tools are not registered at all, so a client can tell why they are missing.
        /// </summary>
        public bool? AllowWrite { get; set; }
    }

    public class ResponseTiaInstallation
    {
        public int MajorVersion { get; set; }
        public string? InstallPath { get; set; }
        public bool EngineeringExists { get; set; }
        public bool PortalExeExists { get; set; }
    }

    public class ResponseDoctor : ResponseMessage
    {
        public string? Report { get; set; }
        public bool? IsConnected { get; set; }
        public int? ActiveTiaMajorVersion { get; set; }
        public string? ProjectName { get; set; }
        public string? ProjectPath { get; set; }
        public bool? IsUserInGroup { get; set; }
        public bool? AllowWrite { get; set; }
        public IEnumerable<ResponseTiaInstallation>? Installations { get; set; }
    }

    public class ResponseGetProjects : ResponseMessage
    {
        public IEnumerable<ResponseProjectInfo>? Items { get; set; }
    }

    public class ResponseOpenProject : ResponseMessage
    {
    }

    public class ResponseSaveProject : ResponseMessage
    {
    }

    public class ResponseSaveAsProject : ResponseMessage
    {
    }

    public class ResponseCloseProject : ResponseMessage
    {
    }

    public class ResponseTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseProjectTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseSoftwareTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseDevices : ResponseMessage
    {
        public IEnumerable<ResponseDeviceInfo>? Items { get; set; }
    }
    
    public class ResponseCompileSoftware : ResponseMessage
    {
    }
    
    public class ResponseBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseExportBlock : ResponseMessage
    {
    }

    public class ResponseImportBlock : ResponseMessage
    {
    }

    public class ResponseExportBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
        public IEnumerable<ResponseBlockInfo>? Inconsistent { get; set; }
    }

    public class ResponseTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
    }

    public class ResponseExportType : ResponseMessage
    {
    }

    public class ResponseImportType : ResponseMessage
    {
    }

    public class ResponseExportTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
        public IEnumerable<ResponseTypeInfo>? Inconsistent { get; set; }
    }

    public class ResponseExportAsDocuments : ResponseMessage
    {
    }

    public class ResponseExportBlocksAsDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseImportFromDocuments : ResponseMessage
    {
    }

    public class ResponseImportBlocksFromDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }
}
