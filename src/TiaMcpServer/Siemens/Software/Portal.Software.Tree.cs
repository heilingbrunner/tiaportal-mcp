using Microsoft.Extensions.Logging;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.CrossReference;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.SW.WatchAndForceTables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        #region get software tree ...

        /// <param name="sections">
        /// Comma separated subset of "blocks,types,tags,watch,sources", or "all" (the default).
        /// Lets a client keep the output small on a large PLC.
        /// </param>
        public string GetSoftwareTree(string softwarePath, string sections = "all")
        {
            _logger?.LogInformation("Getting software tree for path: {SoftwarePath}", softwarePath);

            if (IsProjectNull())
            {
                return string.Empty;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    StringBuilder sb = new();
                    sb.AppendLine($"{plcSoftware.Name} [PLC Software]");

                    var selected = ParseTreeSections(sections);
                    var ancestorStates = new List<bool>();

                    // Every section takes an "is last" flag; that can only be decided once all
                    // present sections are known, so they are collected before being rendered.
                    var renderers = new List<Action<bool>>();

                    var blockGroup = plcSoftware.BlockGroup;
                    if (selected.Contains("blocks") && blockGroup != null)
                    {
                        renderers.Add(isLast => GetSoftwareTreeBlockGroup(sb, blockGroup, ancestorStates, "Program blocks", isLast));
                    }

                    var typeGroup = plcSoftware.TypeGroup;
                    if (selected.Contains("types") && typeGroup != null)
                    {
                        renderers.Add(isLast => GetSoftwareTreeTypeGroup(sb, typeGroup, ancestorStates, "PLC data types", isLast));
                    }

                    renderers.AddRange(BuildAdditionalTreeSections(plcSoftware, sb, ancestorStates, selected));

                    for (int i = 0; i < renderers.Count; i++)
                    {
                        renderers[i](i == renderers.Count - 1);
                    }

                    return sb.ToString();
                }
                else
                {
                    return $"No PLC software found at path: {softwarePath}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting software tree for {SoftwarePath}", softwarePath);
                return $"Error retrieving software tree: {ex.Message}";
            }
        }

        private void GetSoftwareTreeBlockGroup(StringBuilder sb, PlcBlockGroup blockGroup, List<bool> ancestorStates, string groupLabel, bool isLastSection)
        {
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastSection)}{groupLabel}"); // [Collection]
            var newAncestorStates = new List<bool>(ancestorStates) { isLastSection };
            
            // Get blocks in this group
            var blocks = blockGroup.Blocks.ToList();
            var subGroups = blockGroup.Groups.ToList();
            
            // First, add all blocks
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                // Block is last only if it's the last block AND there are no subgroups following
                var isLastBlock = (i == blocks.Count - 1) && (subGroups.Count == 0);

                var blockTypeName = new[] { "ArrayDB", "GlobalDB", "InstanceDB" }.Contains(block.GetType().Name)
                    ? "DB"
                    : block.GetType().Name;

                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastBlock)}{block.Name} [{blockTypeName}{block.Number}, {block.ProgrammingLanguage}]");
            }
            
            // Then, add all subgroups recursively
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                var isLastGroup = i == subGroups.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastGroup)}{subGroup.Name}"); // [Block Group]

                var groupAncestorStates = new List<bool>(newAncestorStates) { isLastGroup };
                GetSoftwareTreeBlockGroupRecursive(sb, subGroup, groupAncestorStates);
            }
        }

        private void GetSoftwareTreeBlockGroupRecursive(StringBuilder sb, PlcBlockGroup blockGroup, List<bool> ancestorStates)
        {
            // Get blocks in this group
            var blocks = blockGroup.Blocks.ToList();
            var subGroups = blockGroup.Groups.ToList();
            
            // First, add all blocks
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                // Block is last only if it's the last block AND there are no subgroups following
                var isLastBlock = (i == blocks.Count - 1) && (subGroups.Count == 0);

                var blockTypeName = new[] { "ArrayDB", "GlobalDB", "InstanceDB" }.Contains(block.GetType().Name)
                    ? "DB"
                    : block.GetType().Name;

                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastBlock)}{block.Name} [{blockTypeName}{block.Number}, {block.ProgrammingLanguage}]");
            }
            
            // Then, add all subgroups recursively
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                var isLastGroup = i == subGroups.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastGroup)}{subGroup.Name}"); // [Block Group]

                var groupAncestorStates = new List<bool>(ancestorStates) { isLastGroup };
                GetSoftwareTreeBlockGroupRecursive(sb, subGroup, groupAncestorStates);
            }
        }

        private void GetSoftwareTreeTypeGroup(StringBuilder sb, PlcTypeGroup typeGroup, List<bool> ancestorStates, string groupLabel, bool isLastSection)
        {
            
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastSection)}{groupLabel}"); // [Collection]
            var newAncestorStates = new List<bool>(ancestorStates) { isLastSection };
            
            // Get types in this group
            var types = typeGroup.Types.ToList();
            var subGroups = typeGroup.Groups.ToList();
            
            // First, add all types
            for (int i = 0; i < types.Count; i++)
            {
                var type = types[i];
                // Type is last only if it's the last type AND there are no subgroups following
                var isLastType = (i == types.Count - 1) && (subGroups.Count == 0);

                var typeTypeName = type.GetType().Name;
                typeTypeName = typeTypeName=="PlcStruct" ? "UDT": typeTypeName;

                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastType)}{type.Name} [{typeTypeName}]");
            }
            
            // Then, add all subgroups recursively
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                var isLastGroup = i == subGroups.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastGroup)}{subGroup.Name}"); // [Type Group]

                var groupAncestorStates = new List<bool>(newAncestorStates) { isLastGroup };
                GetSoftwareTreeTypeGroupRecursive(sb, subGroup, groupAncestorStates);
            }
        }

        private void GetSoftwareTreeTypeGroupRecursive(StringBuilder sb, PlcTypeGroup typeGroup, List<bool> ancestorStates)
        {
            // Get types in this group
            var types = typeGroup.Types.ToList();
            var subGroups = typeGroup.Groups.ToList();
            
            // First, add all types
            for (int i = 0; i < types.Count; i++)
            {
                var type = types[i];
                // Type is last only if it's the last type AND there are no subgroups following
                var isLastType = (i == types.Count - 1) && (subGroups.Count == 0);

                var typeTypeName = type.GetType().Name;
                typeTypeName = typeTypeName == "PlcStruct" ? "UDT" : typeTypeName;

                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastType)}{type.Name} [{typeTypeName}]");
            }
            
            // Then, add all subgroups recursively
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                var isLastGroup = i == subGroups.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastGroup)}{subGroup.Name}"); // [Type Group]

                var groupAncestorStates = new List<bool>(ancestorStates) { isLastGroup };
                GetSoftwareTreeTypeGroupRecursive(sb, subGroup, groupAncestorStates);
            }
        }

        #endregion

        // From the former Portal.Tree.cs:
        // The tag, watch/force table and external source sections of the software tree.
        //
        // Callers: GetSoftwareTree in this file. Affected API: none - these are private helpers;
        // GetSoftwareTree itself gains an optional 'sections' parameter. Reads/writes no data files.
        //
        // The block and type sections keep their hand-written renderers above (GetSoftwareTreeBlockGroup and friends); the three
        // sections added here share one generic renderer because their group hierarchies have the
        // same shape (a group holds items plus subgroups) despite having no common base type.

        #region tree

        /// <summary>Section keys accepted by GetSoftwareTree's 'sections' parameter.</summary>
        internal static readonly string[] SoftwareTreeSectionKeys =
            ["blocks", "types", "tags", "watch", "sources"];

        /// <summary>
        /// Parses the comma separated 'sections' argument. Empty, null or "all" selects
        /// everything; unknown keys are ignored so a client cannot break the call by guessing.
        /// </summary>
        private static HashSet<string> ParseTreeSections(string sections)
        {
            if (string.IsNullOrWhiteSpace(sections) || sections.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return new HashSet<string>(SoftwareTreeSectionKeys, StringComparer.OrdinalIgnoreCase);
            }

            var selected = sections
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => SoftwareTreeSectionKeys.Contains(s, StringComparer.OrdinalIgnoreCase));

            var set = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);

            // A request that selected nothing recognisable is treated as "all" rather than
            // silently returning a header with no body.
            return set.Count == 0
                ? new HashSet<string>(SoftwareTreeSectionKeys, StringComparer.OrdinalIgnoreCase)
                : set;
        }

        /// <summary>
        /// Renders one top-level section: a label line followed by the group tree beneath it.
        /// Item labels are produced as strings so a group with two item collections (watch plus
        /// force tables) can feed them both through the same renderer.
        /// </summary>
        private void RenderTreeSection<TGroup>(
            StringBuilder sb,
            TGroup rootGroup,
            List<bool> ancestorStates,
            string label,
            bool isLastSection,
            Func<TGroup, IEnumerable<string>> itemLabels,
            Func<TGroup, IEnumerable<TGroup>> subGroups,
            Func<TGroup, string> groupName)
            where TGroup : class
        {
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastSection)}{label}");

            RenderTreeSectionBody(
                sb,
                rootGroup,
                new List<bool>(ancestorStates) { isLastSection },
                itemLabels,
                subGroups,
                groupName);
        }

        private void RenderTreeSectionBody<TGroup>(
            StringBuilder sb,
            TGroup group,
            List<bool> ancestorStates,
            Func<TGroup, IEnumerable<string>> itemLabels,
            Func<TGroup, IEnumerable<TGroup>> subGroups,
            Func<TGroup, string> groupName)
            where TGroup : class
        {
            var labels = itemLabels(group).ToList();
            var groups = subGroups(group).ToList();

            for (var i = 0; i < labels.Count; i++)
            {
                // An item is only the last branch when no subgroups follow it.
                var isLastItem = i == labels.Count - 1 && groups.Count == 0;
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastItem)}{labels[i]}");
            }

            for (var i = 0; i < groups.Count; i++)
            {
                var isLastGroup = i == groups.Count - 1;

                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastGroup)}{groupName(groups[i])}");

                RenderTreeSectionBody(
                    sb,
                    groups[i],
                    new List<bool>(ancestorStates) { isLastGroup },
                    itemLabels,
                    subGroups,
                    groupName);
            }
        }

        #endregion

        #region section renderers

        private void GetSoftwareTreeTagTableGroup(
            StringBuilder sb,
            PlcTagTableGroup tagTableGroup,
            List<bool> ancestorStates,
            string groupLabel,
            bool isLastSection)
        {
            RenderTreeSection<PlcTagTableGroup>(
                sb,
                tagTableGroup,
                ancestorStates,
                groupLabel,
                isLastSection,
                g => g.TagTables.Select(t =>
                    $"{t.Name} [Tag table, {t.Tags.Count} tags, {t.UserConstants.Count} constants]"),
                g => g.Groups,
                g => g.Name);
        }

        private void GetSoftwareTreeWatchAndForceTableGroup(
            StringBuilder sb,
            PlcWatchAndForceTableGroup watchGroup,
            List<bool> ancestorStates,
            string groupLabel,
            bool isLastSection)
        {
            RenderTreeSection<PlcWatchAndForceTableGroup>(
                sb,
                watchGroup,
                ancestorStates,
                groupLabel,
                isLastSection,
                g => g.WatchTables.Select(w => $"{w.Name} [Watch table]")
                      .Concat(g.ForceTables.Select(f => $"{f.Name} [Force table]")),
                g => g.Groups,
                g => g.Name);
        }

        private void GetSoftwareTreeExternalSourceGroup(
            StringBuilder sb,
            PlcExternalSourceGroup sourceGroup,
            List<bool> ancestorStates,
            string groupLabel,
            bool isLastSection)
        {
            RenderTreeSection<PlcExternalSourceGroup>(
                sb,
                sourceGroup,
                ancestorStates,
                groupLabel,
                isLastSection,
                g => g.ExternalSources.Select(s => $"{s.Name} [External source]"),
                g => g.Groups,
                g => g.Name);
        }

        #endregion

        #region tree

        /// <summary>
        /// Builds the tag, watch/force and external source sections. Returns actions taking an
        /// "is last" flag so GetSoftwareTree can decide which section closes the tree.
        /// </summary>
        private List<Action<bool>> BuildAdditionalTreeSections(
            PlcSoftware plcSoftware,
            StringBuilder sb,
            List<bool> ancestorStates,
            HashSet<string> selected)
        {
            var sections = new List<Action<bool>>();

            var tagTableGroup = plcSoftware.TagTableGroup;
            if (selected.Contains("tags") && tagTableGroup != null)
            {
                sections.Add(isLast =>
                    GetSoftwareTreeTagTableGroup(sb, tagTableGroup, ancestorStates, "PLC tags", isLast));
            }

            var watchGroup = plcSoftware.WatchAndForceTableGroup;
            if (selected.Contains("watch") && watchGroup != null)
            {
                sections.Add(isLast =>
                    GetSoftwareTreeWatchAndForceTableGroup(sb, watchGroup, ancestorStates, "Watch and force tables", isLast));
            }

            var sourceGroup = plcSoftware.ExternalSourceGroup;
            if (selected.Contains("sources") && sourceGroup != null)
            {
                sections.Add(isLast =>
                    GetSoftwareTreeExternalSourceGroup(sb, sourceGroup, ancestorStates, "External source files", isLast));
            }

            return sections;
        }

        #endregion
    }
}
