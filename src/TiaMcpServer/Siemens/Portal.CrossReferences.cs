using Siemens.Engineering;
using Siemens.Engineering.CrossReference;
using Siemens.Engineering.SW.Tags;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Cross references for PLC software objects.
    ///
    /// Callers: the GetCrossReferences tool in McpServer.CrossReferences.cs. Affected API: none
    /// existing - all members are new. Reads/writes no data files.
    ///
    /// CrossReferenceService lives in Siemens.Engineering.Base, not Step7, and is offered by
    /// software, blocks, types, their groups, tags, tag tables and constants - but NOT by watch
    /// tables, force tables or external sources. A null service therefore means "this object
    /// kind has no cross references", which is reported as NotSupported rather than NotFound.
    /// </summary>
    public partial class Portal
    {
        /// <param name="objectPath">Empty targets the whole PLC software.</param>
        /// <param name="objectKind">
        /// "auto" (default) resolves the path against blocks, then types, then tag tables, then
        /// tags, then block groups. Pass an explicit kind to skip the probing.
        /// </param>
        public CrossReferenceResult GetCrossReferences(
            string softwarePath,
            string objectPath = "",
            string objectKind = "auto",
            CrossReferenceFilter filter = CrossReferenceFilter.AllObjects)
        {
            return Operation.Run(_logger, nameof(GetCrossReferences), PortalErrorCode.NotFound,
                () =>
                {
                    var provider = ResolveCrossReferenceProvider(softwarePath, objectPath, objectKind);

                    var service = provider.GetService<CrossReferenceService>()
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{(string.IsNullOrEmpty(objectPath) ? softwarePath : objectPath)}' does not provide cross references. " +
                            "Watch tables, force tables and external sources have none; blocks, types, groups, tags and tag tables do.");

                    return service.GetCrossReferences(filter);
                },
                ("softwarePath", softwarePath), ("objectPath", objectPath), ("objectKind", objectKind), ("filter", filter));
        }

        /// <summary>
        /// Maps a path plus kind onto the Openness object that offers the cross reference
        /// service. Probing order matters: block names and type names can collide.
        /// </summary>
        private IEngineeringServiceProvider ResolveCrossReferenceProvider(string softwarePath, string objectPath, string objectKind)
        {
            var plcSoftware = GetPlcSoftwareOrThrow(softwarePath);

            if (string.IsNullOrWhiteSpace(objectPath))
            {
                return plcSoftware;
            }

            var kind = (objectKind ?? "auto").Trim().ToLowerInvariant();

            IEngineeringServiceProvider? provider;

            switch (kind)
            {
                case "block":
                    provider = GetBlock(softwarePath, objectPath);
                    break;
                case "type":
                    provider = GetType(softwarePath, objectPath);
                    break;
                case "tagtable":
                    provider = GetTagTable(softwarePath, objectPath);
                    break;
                case "tag":
                    provider = GetTag(softwarePath, objectPath);
                    break;
                case "blockgroup":
                    provider = GetPlcBlockGroupByPath(softwarePath, objectPath);
                    break;
                case "auto":
                    // Each candidate needs its own cast: the ?? operator has no common type
                    // across PlcBlock, PlcType, PlcTagTable, PlcTag and PlcBlockGroup.
                    provider = (IEngineeringServiceProvider?)GetBlock(softwarePath, objectPath)
                               ?? (IEngineeringServiceProvider?)GetType(softwarePath, objectPath)
                               ?? (IEngineeringServiceProvider?)GetTagTable(softwarePath, objectPath)
                               ?? (IEngineeringServiceProvider?)TryGetTag(softwarePath, objectPath)
                               ?? (IEngineeringServiceProvider?)GetPlcBlockGroupByPath(softwarePath, objectPath);
                    break;
                default:
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"Unknown objectKind '{objectKind}'. Allowed values are 'auto', 'block', 'type', 'tagTable', 'tag' and 'blockGroup'.");
            }

            return provider
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"No object found at '{objectPath}' in '{softwarePath}'. Use 'GetSoftwareTree' to discover valid paths.");
        }

        /// <summary>
        /// GetTag throws InvalidParams for a path without a table segment, which is a normal
        /// outcome while probing in "auto" mode rather than a failure.
        /// </summary>
        private PlcTag? TryGetTag(string softwarePath, string objectPath)
        {
            try
            {
                return GetTag(softwarePath, objectPath);
            }
            catch (PortalException)
            {
                return null;
            }
        }
    }
}
