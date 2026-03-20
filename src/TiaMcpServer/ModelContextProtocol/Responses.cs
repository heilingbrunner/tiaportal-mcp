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
    public class ResponseModule
    {
        public string? Name { get; set; }
        public string? TypeIdentifier { get; set; }
        public int? PositionNumber { get; set; }
        public IEnumerable<Attribute>? Attributes { get; set; }
    }

    public class ResponseModules : ResponseMessage
    {
        public IEnumerable<ResponseModule>? Items { get; set; }
    }

    public class ResponseModuleInfo : ResponseMessage
    {
        public string? Name { get; set; }
        public string? TypeIdentifier { get; set; }
        public int? PositionNumber { get; set; }
        public IEnumerable<Attribute>? Attributes { get; set; }
    }

    public class ResponseSubnet
    {
        public string? Name { get; set; }
        public string? SubnetType { get; set; }
        public string? Id { get; set; }
    }

    public class ResponseSubnets : ResponseMessage
    {
        public IEnumerable<ResponseSubnet>? Items { get; set; }
    }

    public class ResponseNetworkInterface
    {
        public string? Name { get; set; }
        public string? InterfaceType { get; set; }
        public string? IpAddress { get; set; }
        public string? SubnetMask { get; set; }
    }

    public class ResponseNetworkInterfaces : ResponseMessage
    {
        public IEnumerable<ResponseNetworkInterface>? Items { get; set; }
    }

    public class ResponseAddress
    {
        public int? StartAddress { get; set; }
        public int? Length { get; set; }
        public string? IoType { get; set; }
    }

    public class ResponseAddresses : ResponseMessage
    {
        public IEnumerable<ResponseAddress>? Items { get; set; }
    }

    public class ResponseCreateDevice : ResponseMessage
    {
        public string? Name { get; set; }
        public string? TypeIdentifier { get; set; }
        public string? DevicePath { get; set; }
    }

    public class ResponseDeleteDevice : ResponseMessage
    {
    }

    public class ResponseCreateDeviceGroup : ResponseMessage
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
    public class ResponseExternalSourceInfo
    {
        public string? Name { get; set; }
        public string? Extension { get; set; }
    }

    public class ResponseExternalSources : ResponseMessage
    {
        public IEnumerable<ResponseExternalSourceInfo>? Items { get; set; }
    }

    public class ResponseImportExternalSource : ResponseMessage
    {
    }

    public class ResponseGenerateBlocksFromSource : ResponseMessage
    {
        public bool Success { get; set; }
        public string? SourceName { get; set; }
    }

    public class ResponseDeleteExternalSource : ResponseMessage
    {
    }

    public class ResponseExportExternalSource : ResponseMessage
    {
    }

    public class ResponseCrossReferenceInfo
    {
        public string? SourceObject { get; set; }
        public string? ReferencedObject { get; set; }
        public string? ReferenceType { get; set; }
        public string? Path { get; set; }
    }

    public class ResponseCrossReferences : ResponseMessage
    {
        public IEnumerable<ResponseCrossReferenceInfo>? Items { get; set; }
    }
    public class ResponseSetIpAddress : ResponseMessage
    {
    }

    public class ResponseConnectToSubnet : ResponseMessage
    {
    }

    public class ResponseImportGsdFile : ResponseMessage
    public class ResponseOnlineStatus : ResponseMessage
    {
        public bool IsOnline { get; set; }
        public string? DevicePath { get; set; }
        public string? Mode { get; set; }
    }

    public class ResponseDownloadResult : ResponseMessage
    {
        public bool Success { get; set; }
        public IEnumerable<string>? Warnings { get; set; }
        public IEnumerable<string>? Errors { get; set; }
    }

    public class ResponseCompareResult : ResponseMessage
    {
        public IEnumerable<ComparisonDifference>? Differences { get; set; }
    }

    public class ComparisonDifference
    {
        public string? ObjectPath { get; set; }
        public string? ChangeType { get; set; }
        public string? Details { get; set; }
    }

    public class ResponseLibrary : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public int MasterCopyCount { get; set; }
        public int TypeCount { get; set; }
    }

    public class ResponseLibraries : ResponseMessage
    {
        public IEnumerable<ResponseLibrary>? Items { get; set; }
    }

    public class ResponseMasterCopy : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? TypeIdentifier { get; set; }
    }

    public class ResponseLibraryType : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public IEnumerable<Attribute>? Attributes { get; set; }
    }

    public class ResponseLibraryContents : ResponseMessage
    {
        public IEnumerable<ResponseMasterCopy>? MasterCopies { get; set; }
        public IEnumerable<ResponseLibraryType>? Types { get; set; }
    }

    public class ResponseCreateProject : ResponseMessage
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public bool Success { get; set; }
    }

    public class ResponseMultiuserInfo : ResponseMessage
    {
        public bool IsMultiuser { get; set; }
        public string? ServerName { get; set; }
        public IEnumerable<string>? Users { get; set; }
    }

    public class ResponseOpenGlobalLibrary : ResponseMessage
    {
    }

    public class ResponseCopyToLibrary : ResponseMessage
    {
    }

    public class ResponseCopyFromLibrary : ResponseMessage
    {
    }
}
