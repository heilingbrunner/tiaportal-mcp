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
        /// <summary>Root-relative path, e.g. "1_Tests/FC_Block_1". Feed back into the block tools.</summary>
        public string? Path { get; set; }
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
        /// <summary>Root-relative path, e.g. "Common/BtnTyp_X". Feed back into the type tools.</summary>
        public string? Path { get; set; }
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
        /// <summary>Full path of the project or session file on the server machine.</summary>
        public string? Path { get; set; }
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
    }

    public class CompileMessage
    {
        /// <summary>Object the message belongs to, as TIA Portal reports it.</summary>
        public string? Path { get; set; }

        /// <summary>Success, Information, Warning or Error.</summary>
        public string? State { get; set; }

        public string? Description { get; set; }

        public int? ErrorCount { get; set; }

        public int? WarningCount { get; set; }

        /// <summary>Nesting level in the original message tree; 0 is a direct child of the result.</summary>
        public int? Depth { get; set; }
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

    /// <summary>
    /// Files one source document export produced. The list comes from TIA Portal rather than
    /// from an assumed '.s7dcl' name, so the caller learns what is actually on disk.
    /// </summary>
    public class ResponseDocumentFiles
    {
        public string? Name { get; set; }

        public string? Directory { get; set; }

        public IEnumerable<string>? Files { get; set; }

        /// <summary>Openness messages, present when the export was not a plain success.</summary>
        public IEnumerable<string>? Messages { get; set; }

        /// <summary>Success, PartialSuccess or Failure.</summary>
        public string? State { get; set; }
    }

    public class ResponseExportTypeAsDocuments : ResponseMessage
    {
        public ResponseTypeInfo? Item { get; set; }

        public ResponseDocumentFiles? Documents { get; set; }
    }

    public class ResponseExportTypesAsDocuments : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }

        public IEnumerable<ResponseDocumentFiles>? Documents { get; set; }

        /// <summary>Types skipped because TIA Portal does not export inconsistent objects.</summary>
        public IEnumerable<ResponseTypeInfo>? Inconsistent { get; set; }

        /// <summary>Types that failed to export, one entry per type, with the reason.</summary>
        public IEnumerable<string>? Failures { get; set; }
    }

    public class ResponseImportTypeFromDocuments : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
    }

    public class ResponseImportTypesFromDocuments : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
    }

    #region lookup

    /// <summary>One object matched by ResolveObjectPath.</summary>
    public class ResponseObjectMatch
    {
        /// <summary>block, type, tag, tagTable, watchTable or source.</summary>
        public string? Kind { get; set; }

        public string? Name { get; set; }

        /// <summary>Root-relative path, ready to pass to the tools of that area.</summary>
        public string? Path { get; set; }
    }

    public class ResponseResolveObjectPath : ResponseMessage
    {
        public IEnumerable<ResponseObjectMatch>? Items { get; set; }
    }

    public class ResponseOpenTiaProject : ResponseMessage
    {
        public string? ProjectPath { get; set; }

        /// <summary>False when this call had to establish the TIA Portal connection itself.</summary>
        public bool? WasAlreadyConnected { get; set; }

        /// <summary>PLC software paths in the form the other tools accept.</summary>
        public IEnumerable<string>? SoftwarePaths { get; set; }

        /// <summary>The project tree, so no follow-up GetProjectTree call is needed.</summary>
        public string? Tree { get; set; }
    }
    #endregion


    #region source and insight

    public class ResponseSourceText : ResponseMessage
    {
        public string? Name { get; set; }

        public string? Path { get; set; }

        /// <summary>'document' or 'xml'. May differ from the request when the object has no source document.</summary>
        public string? Format { get; set; }

        public string? Text { get; set; }

        /// <summary>Length before truncation, so the caller knows how much was withheld.</summary>
        public int? TotalChars { get; set; }

        public bool? Truncated { get; set; }

        public IEnumerable<string>? FileNames { get; set; }
    }

    public class ResponseInterfaceMember
    {
        public string? Name { get; set; }

        public string? DataTypeName { get; set; }

        /// <summary>Every attribute Openness reports for the member, stringified.</summary>
        public IDictionary<string, string>? Attributes { get; set; }
    }

    public class ResponseBlockInterface : ResponseMessage
    {
        public string? Path { get; set; }

        public IEnumerable<ResponseInterfaceMember>? Items { get; set; }
    }

    /// <summary>What importing one file would do.</summary>
    public class ResponseImportPreviewItem
    {
        public string? Name { get; set; }

        /// <summary>'create', 'overwrite', 'conflict' or 'overwrite-elsewhere'.</summary>
        public string? Effect { get; set; }

        /// <summary>Where the import would put it.</summary>
        public string? TargetPath { get; set; }

        /// <summary>Where an object of that name already lives, when one does.</summary>
        public string? ExistingPath { get; set; }

        public string? Note { get; set; }
    }

    public class ResponseImportPreview : ResponseMessage
    {
        public IEnumerable<ResponseImportPreviewItem>? Items { get; set; }

        public int? CreateCount { get; set; }

        public int? OverwriteCount { get; set; }

        public int? ConflictCount { get; set; }
    }

    /// <summary>One line of source text that matched a code search.</summary>
    public class ResponseCodeMatch
    {
        /// <summary>Root-relative path, ready to pass to GetBlockSource or GetTypeSource.</summary>
        public string? ObjectPath { get; set; }

        /// <summary>'document' or 'xml' - which representation was searched.</summary>
        public string? Format { get; set; }

        /// <summary>1-based line number within that representation.</summary>
        public int? Line { get; set; }

        public string? Text { get; set; }
    }

    public class ResponseCodeSearch : ResponseMessage
    {
        public string? Pattern { get; set; }

        public IEnumerable<ResponseCodeMatch>? Items { get; set; }

        public int? ObjectsSearched { get; set; }

        /// <summary>Objects that could not be read, with the reason.</summary>
        public IEnumerable<string>? Unsearchable { get; set; }

        public bool? Truncated { get; set; }
    }

    public class ResponseSourceTree : ResponseMessage
    {
        public string? Directory { get; set; }

        /// <summary>Objects written per area: blocks, types, tagTables, watchTables.</summary>
        public IDictionary<string, int>? Written { get; set; }

        /// <summary>Format used per area: 'document' where TIA Portal supports it, else 'xml'.</summary>
        public IDictionary<string, string>? Formats { get; set; }

        public IEnumerable<string>? Skipped { get; set; }

        public IEnumerable<string>? Failures { get; set; }
    }

    public class ResponsePlcSummary : ResponseMessage
    {
        public string? Name { get; set; }

        public string? SoftwarePath { get; set; }

        public int? BlockCount { get; set; }

        public int? TypeCount { get; set; }

        public int? TagTableCount { get; set; }

        public int? TagCount { get; set; }

        public int? UserConstantCount { get; set; }

        public int? WatchTableCount { get; set; }

        public int? ExternalSourceCount { get; set; }

        /// <summary>Blocks per concrete kind: OB, FB, FC, InstanceDB, GlobalDB.</summary>
        public IDictionary<string, int>? BlocksByKind { get; set; }

        /// <summary>Blocks per programming language: LAD, SCL, STL, FBD, ...</summary>
        public IDictionary<string, int>? BlocksByLanguage { get; set; }

        /// <summary>Objects that refuse to export until the software is compiled.</summary>
        public IEnumerable<string>? InconsistentObjects { get; set; }

        /// <summary>Objects whose content is hidden, so source and interface reads will fail.</summary>
        public IEnumerable<string>? KnowHowProtectedObjects { get; set; }

        public DateTime? LastModified { get; set; }
    }

    /// <summary>One object that uses the object WhereUsed was asked about.</summary>
    public class ResponseUsage
    {
        public string? UsedBy { get; set; }

        public string? Path { get; set; }

        public string? Address { get; set; }

        public string? TypeName { get; set; }
    }

    public class ResponseWhereUsed : ResponseMessage
    {
        /// <summary>The resolved path of the object that was looked up.</summary>
        public string? Path { get; set; }

        public string? Kind { get; set; }

        public IEnumerable<ResponseUsage>? Items { get; set; }
    }

    #endregion

}
