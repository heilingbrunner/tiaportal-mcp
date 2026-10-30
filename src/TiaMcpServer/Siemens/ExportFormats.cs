using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TiaMcpServer.Siemens
{
    /// <summary>How TIA Portal is configured to export one kind of object.</summary>
    public enum ExportFormatKind
    {
        /// <summary>Not a format this server knows.</summary>
        Unknown,

        /// <summary>SIMATIC SD documents: '.s7dcl' and '.s7res'.</summary>
        SimaticSd,

        /// <summary>SimaticML: '.xml'.</summary>
        Xml,

        /// <summary>External source: '.awl', '.scl', '.db' or '.udt'.</summary>
        Source
    }

    /// <summary>
    /// The export format settings of TIA Portal (Options > Settings > Version control interface >
    /// Export formats), free of TIA Portal so they can be tested without it. Openness cannot read
    /// them, so they live in 'tia.json' at the root of the export folder, written by the
    /// user:
    /// <code>
    /// { "version control interface": { "code blocks-LAD": "SIMATIC SD(.s7dcl, .s7res)", "code blocks-STL": ".awl" } }
    /// </code>
    ///
    /// Callers: PlcProgram.cs, Portal.Plc.cs (ExportPlcProgram) and the GetExportFormats tool.
    /// File I/O: reads '&lt;export root&gt;/tia.json'; writes it only when it does not exist yet,
    /// with the defaults of TIA Portal.
    /// </summary>
    public static class ExportFormats
    {
        public const string SettingsFileName = "tia.json";

        public const string GroupName = "version control interface";

        public const string LadBlocksKey = "code blocks-LAD";
        public const string FbdBlocksKey = "code blocks-FBD";
        public const string SclBlocksKey = "code blocks-SCL";
        public const string StlBlocksKey = "code blocks-STL";
        public const string DataBlocksKey = "data blocks";
        public const string PlcTypesKey = "PLC data types";

        private const string SimaticSdText = "SIMATIC SD(.s7dcl, .s7res)";

        /// <summary>
        /// The defaults TIA Portal offers for a new workspace (Options > Settings > Version control
        /// interface > Export formats), written to a missing 'tia.json'. The values of the
        /// user's installation may differ, which is why the file has to be checked.
        /// </summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Defaults = new[]
        {
            new KeyValuePair<string, string>(LadBlocksKey, SimaticSdText),
            new KeyValuePair<string, string>(FbdBlocksKey, SimaticSdText),
            new KeyValuePair<string, string>(SclBlocksKey, SimaticSdText),
            new KeyValuePair<string, string>(StlBlocksKey, ".awl"),
            new KeyValuePair<string, string>(DataBlocksKey, SimaticSdText),
            new KeyValuePair<string, string>("safety data blocks (F-DBs)", SimaticSdText),
            new KeyValuePair<string, string>(PlcTypesKey, SimaticSdText),
            new KeyValuePair<string, string>("F-compliant PLC data types (UDT)", SimaticSdText)
        };

        public const string Example =
            "{ \"" + GroupName + "\": { \"" + LadBlocksKey + "\": \"SIMATIC SD(.s7dcl, .s7res)\", \"" +
            StlBlocksKey + "\": \".awl\", \"" + DataBlocksKey + "\": \"SIMATIC SD(.s7dcl, .s7res)\", \"" +
            PlcTypesKey + "\": \"SIMATIC SD(.s7dcl, .s7res)\" } }";

        /// <summary>Reads the format from the text TIA Portal shows, e.g. 'SIMATIC SD(.s7dcl, .s7res)' or '.xml'.</summary>
        public static ExportFormatKind ParseKind(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ExportFormatKind.Unknown;
            }

            var text = value!.Trim();

            if (text.IndexOf("SIMATIC SD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf(".s7dcl", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ExportFormatKind.SimaticSd;
            }

            switch (text.ToLowerInvariant())
            {
                case ".xml":
                    return ExportFormatKind.Xml;

                case ".awl":
                case ".scl":
                case ".db":
                case ".udt":
                    return ExportFormatKind.Source;

                default:
                    return ExportFormatKind.Unknown;
            }
        }

        /// <summary>The settings key that decides the format of one block, or null for a language without a setting.</summary>
        public static string? BlockKey(bool isDataBlock, ProgrammingLanguage language)
        {
            if (isDataBlock)
            {
                return DataBlocksKey;
            }

            switch (language)
            {
                case ProgrammingLanguage.LAD:
                    return LadBlocksKey;

                case ProgrammingLanguage.FBD:
                    return FbdBlocksKey;

                case ProgrammingLanguage.SCL:
                    return SclBlocksKey;

                case ProgrammingLanguage.STL:
                    return StlBlocksKey;

                default:
                    return null;
            }
        }

        /// <summary>The export tool that writes the configured format of the object type, or null when there is none.</summary>
        public static string? RecommendedTool(string key, ExportFormatKind kind)
        {
            var isType = key.StartsWith(PlcTypesKey, StringComparison.OrdinalIgnoreCase) ||
                         key.StartsWith("F-compliant PLC data types", StringComparison.OrdinalIgnoreCase);

            var isBlock = key.StartsWith("code blocks", StringComparison.OrdinalIgnoreCase) ||
                          key.StartsWith(DataBlocksKey, StringComparison.OrdinalIgnoreCase) ||
                          key.StartsWith("safety data blocks", StringComparison.OrdinalIgnoreCase);

            switch (kind)
            {
                case ExportFormatKind.SimaticSd:
                    return isType ? "ExportTypeAsDocuments" : isBlock ? "ExportAsDocuments" : null;

                case ExportFormatKind.Xml:
                    return isType ? "ExportXmlType" : isBlock ? "ExportXmlBlock" : null;

                case ExportFormatKind.Source:
                    return isType ? "ExportSourceType" : isBlock ? "ExportSourceBlock" : null;

                default:
                    return null;
            }
        }
    }

    /// <summary>The content of 'tia.json': the configured format text per object type.</summary>
    public sealed class ExportFormatSettings
    {
        private readonly Dictionary<string, string> _values;

        private ExportFormatSettings(string file, Dictionary<string, string> values, bool wasCreated)
        {
            File = file;
            _values = values;
            WasCreated = wasCreated;
        }

        /// <summary>The settings file the values came from.</summary>
        public string File { get; }

        /// <summary>True when the file did not exist and was created with the defaults of TIA Portal.</summary>
        public bool WasCreated { get; }

        /// <summary>Asks the user to check a file that was just created; null when the file already existed.</summary>
        public string? Hint => WasCreated
            ? $"'{File}' did not exist and was created with the defaults TIA Portal offers for a new workspace. " +
              "Please check the formats against Options > Settings > Version control interface > Export formats " +
              "and correct the file if your settings differ; the next export reads it as it is."
            : null;

        /// <summary>Object type (settings key) and its format text.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Values => _values.ToList();

        /// <summary>The format text of one object type, or null when the file has no entry for it.</summary>
        public string? Get(string key)
        {
            return _values.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Reads 'tia.json' from the root of an export folder. A file that is missing is created
        /// with the defaults of TIA Portal and flagged through <see cref="WasCreated"/> so the caller
        /// can ask the user to check it; an existing file that is unusable is an error, because an
        /// export must not guess a format the user wrote down.
        /// </summary>
        public static ExportFormatSettings Load(string exportRoot)
        {
            if (string.IsNullOrWhiteSpace(exportRoot))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"The export path is empty, so '{ExportFormats.SettingsFileName}' cannot be found.");
            }

            var file = Path.Combine(exportRoot, ExportFormats.SettingsFileName);

            if (!System.IO.File.Exists(file))
            {
                CreateDefaultFile(file);

                return new ExportFormatSettings(
                    file,
                    new Dictionary<string, string>(ExportFormats.Defaults.ToDictionary(d => d.Key, d => d.Value), StringComparer.OrdinalIgnoreCase),
                    wasCreated: true);
            }

            return Parse(System.IO.File.ReadAllText(file), file);
        }

        /// <summary>Writes the defaults through a temporary file, so a crash never leaves half a file behind.</summary>
        private static void CreateDefaultFile(string file)
        {
            var group = new JsonObject();

            foreach (var entry in ExportFormats.Defaults)
            {
                group[entry.Key] = entry.Value;
            }

            var root = new JsonObject { [ExportFormats.GroupName] = group };
            var temporary = file + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                System.IO.File.WriteAllText(
                    temporary,
                    root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                System.IO.File.Move(temporary, file);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new PortalException(PortalErrorCode.ExportFailed,
                    $"'{file}' does not exist and could not be created: {ex.Message}", null, ex);
            }
        }

        public static ExportFormatSettings Parse(string json, string file)
        {
            JsonNode? root;

            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{file}' is not valid JSON: {ex.Message}", null, ex);
            }

            var group = (root as JsonObject)?
                .FirstOrDefault(p => string.Equals(p.Key, ExportFormats.GroupName, StringComparison.OrdinalIgnoreCase))
                .Value as JsonObject;

            if (group == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{file}' has no object \"{ExportFormats.GroupName}\". Example: {ExportFormats.Example}");
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in group)
            {
                if (property.Value is JsonValue node && node.TryGetValue<string>(out var text))
                {
                    values[property.Key] = text;
                }
            }

            return new ExportFormatSettings(file, values, wasCreated: false);
        }
    }
}
