using Microsoft.Extensions.Logging;
using Siemens.Engineering.Settings;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>One setting of the TIA Portal settings tree, as shown in Options > Settings.</summary>
    public sealed class SettingEntry
    {
        public SettingEntry(string path, string name, string? value)
        {
            Path = path;
            Name = name;
            Value = value;
        }

        /// <summary>The folders above the setting, joined with '/'.</summary>
        public string Path { get; }

        public string Name { get; }

        /// <summary>The value as text; null when TIA Portal gives none or it cannot be read.</summary>
        public string? Value { get; }
    }

    public partial class Portal
    {
        // Reads the global TIA Portal settings (Options > Settings) through TiaPortal.SettingsFolders,
        // the only settings access Openness offers: a tree of named folders with name/value
        // settings and no documented names. The tree is flattened so the caller can find a setting
        // by path and name; which ones exist depends on the installed TIA Portal version.
        //
        // Callers: the GetExportFormats tool in McpServer.ExportFormats.cs. Affected API: none
        // existing - the member is new. File I/O: none.

        /// <summary>
        /// Every setting of the settings tree, depth first. A setting whose value cannot be read is
        /// returned with a null value instead of failing the whole call.
        /// </summary>
        public List<SettingEntry> GetSettings()
        {
            return Operation.Run(_logger, nameof(GetSettings), PortalErrorCode.NotSupported,
                () =>
                {
                    if (_portal == null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState, "Not connected to TIA Portal. Call 'Connect' first");
                    }

                    var entries = new List<SettingEntry>();

                    foreach (var folder in _portal.SettingsFolders)
                    {
                        FlattenSettings(folder, string.Empty, entries);
                    }

                    return entries;
                });
        }

        private void FlattenSettings(TiaPortalSettingsFolder folder, string parentPath, List<SettingEntry> entries)
        {
            var path = parentPath.Length == 0 ? folder.Name : parentPath + "/" + folder.Name;

            foreach (var setting in folder.Settings)
            {
                entries.Add(new SettingEntry(path, setting.Name, ReadSettingValue(setting)));
            }

            foreach (var child in folder.Folders)
            {
                FlattenSettings(child, path, entries);
            }
        }

        private string? ReadSettingValue(TiaPortalSetting setting)
        {
            try
            {
                return setting.Value?.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Setting '{Setting}' could not be read", setting.Name);
                return null;
            }
        }
    }
}
