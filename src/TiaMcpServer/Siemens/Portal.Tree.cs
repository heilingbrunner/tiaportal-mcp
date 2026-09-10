using Siemens.Engineering.SW;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.WatchAndForceTables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The tag, watch/force table and external source sections of the software tree.
    ///
    /// Callers: GetSoftwareTree in Portal.cs. Affected API: none - these are private helpers;
    /// GetSoftwareTree itself gains an optional 'sections' parameter. Reads/writes no data files.
    ///
    /// The block and type sections keep their hand-written renderers in Portal.cs; the three
    /// sections added here share one generic renderer because their group hierarchies have the
    /// same shape (a group holds items plus subgroups) despite having no common base type.
    /// </summary>
    public partial class Portal
    {
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
    }
}
