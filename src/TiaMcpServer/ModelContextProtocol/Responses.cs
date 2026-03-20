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

    public class ResponseTagTableInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public int TagCount { get; set; }
    }

    public class ResponseTagInfo
    {
        public string? Name { get; set; }
        public string? DataTypeName { get; set; }
        public string? LogicalAddress { get; set; }
        public string? Comment { get; set; }
    }

    public class ResponseTagTables : ResponseMessage
    {
        public IEnumerable<ResponseTagTableInfo>? Items { get; set; }
    }

    public class ResponseTagTable : ResponseMessage
    {
        public string? Name { get; set; }
        public IEnumerable<ResponseTagInfo>? Tags { get; set; }
    }

    public class ResponseTags : ResponseMessage
    {
        public IEnumerable<ResponseTagInfo>? Items { get; set; }
    }

    public class ResponseExportTagTable : ResponseMessage
    {
    }

    public class ResponseImportTagTable : ResponseMessage
    {
    }

    public class ResponseCreateTag : ResponseMessage
    {
        public ResponseTagInfo? Tag { get; set; }
    }

    public class ResponseDeleteTag : ResponseMessage
    {
    }

    public class ResponseWatchTableInfo : ResponseAttributes
    {
        public string? Name { get; set; }
    }

    public class ResponseWatchTables : ResponseMessage
    {
        public IEnumerable<ResponseWatchTableInfo>? Items { get; set; }
    }

    public class ResponseExportWatchTable : ResponseMessage
    {
    }

    public class ResponseImportWatchTable : ResponseMessage
    {
    }

    public class ResponseDeleteResult : ResponseMessage
    {
        public bool Success { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
    }

    public class ResponseCopyBlock : ResponseMessage
    {
    }

    public class ResponseMoveBlock : ResponseMessage
    {
    }

    public class ResponseBlockGroupInfo : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public int BlockCount { get; set; }
        public int SubGroupCount { get; set; }
    }

    public class ResponseCreateBlockGroup : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
    }

    public class ResponseTypeGroupInfo : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public int TypeCount { get; set; }
        public int SubGroupCount { get; set; }
    }

    public class ResponseCreateTypeGroup : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
    }
}
