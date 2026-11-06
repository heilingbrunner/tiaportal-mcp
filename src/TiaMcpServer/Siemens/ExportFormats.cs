using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
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
    /// { "version-control-interface": { "code-blocks-LAD": "SIMATIC SD(.s7dcl, .s7res)", "code-blocks-STL": ".awl" } }
    /// </code>
    ///
    /// Callers: PlcProgram.cs, Portal.Plc.cs (ExportPlcProgram) and the GetExportFormats tool.
    /// File I/O: reads '&lt;export root&gt;/tia.json'; writes it only when it does not exist yet,
    /// with the defaults of TIA Portal.
    /// </summary>
    public static class ExportFormats
    {
        public const string SettingsFileName = "tia.json";

        public const string GroupName = "version-control-interface";

        /// <summary>The section for other programs that work with the same project; the server never reads it.</summary>
        public const string ArgsGroupName = "args";

        public const string ArgsProjectKey = "project";
        public const string ArgsSoftwarePathKey = "software-path";
        public const string ArgsExportPathKey = "export-path";
        public const string ArgsKeepStructureKey = "keep-structure";

        /// <summary>The old name of <see cref="ArgsKeepStructureKey"/>; 'tia init' renames it in place.</summary>
        public const string ArgsPreservePathKeyDeprecated = "preserve-path";
        public const string ArgsWithUserInterfaceKey = "with-user-interface";

        public const string LadBlocksKey = "code-blocks-LAD";
        public const string FbdBlocksKey = "code-blocks-FBD";
        public const string SclBlocksKey = "code-blocks-SCL";
        public const string StlBlocksKey = "code-blocks-STL";

        /// <summary>
        /// The format of a LAD or FBD block that contains STL networks. TIA Portal refuses SIMATIC SD for
        /// such a block, so only ".xml" works; a file without this entry behaves like ".xml".
        /// </summary>
        public const string LadStlBlocksKey = "code-blocks-LAD-STL";
        public const string GraphBlocksKey = "code-blocks-GRAPH";
        public const string DataBlocksKey = "data-blocks";
        public const string PlcTypesKey = "PLC-data-types";

        /// <summary>
        /// PLC tag tables are only exported as XML, so the export tools do not read this entry: a file
        /// without it behaves the same as one that says ".xml". It is listed so 'tia init' and
        /// 'GetExportFormats' show every object type.
        /// </summary>
        public const string PlcTagTablesKey = "PLC-tag-tables";

        /// <summary>
        /// No tool exports technology objects yet, so nothing reads this entry: a file without it behaves
        /// the same as one that says ".xml". It is listed so 'tia init' and 'GetExportFormats' show every
        /// object type.
        /// </summary>
        public const string TechnologyObjectsKey = "technology-objects";

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
            new KeyValuePair<string, string>(LadStlBlocksKey, ".xml"),
            new KeyValuePair<string, string>(GraphBlocksKey, ".xml"),
            new KeyValuePair<string, string>(DataBlocksKey, SimaticSdText),
            new KeyValuePair<string, string>("safety-data-blocks-F-DBs", SimaticSdText),
            new KeyValuePair<string, string>(PlcTypesKey, SimaticSdText),
            new KeyValuePair<string, string>("F-compliant-PLC-data-types-UDT", SimaticSdText),
            new KeyValuePair<string, string>(PlcTagTablesKey, ".xml"),
            new KeyValuePair<string, string>(TechnologyObjectsKey, ".xml")
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
                case ProgrammingLanguage.F_LAD:
                case ProgrammingLanguage.LAD:
                    return LadBlocksKey;

                case ProgrammingLanguage.FBD:
                    return FbdBlocksKey;

                case ProgrammingLanguage.SCL:
                    return SclBlocksKey;

                case ProgrammingLanguage.STL:
                    return StlBlocksKey;

                case ProgrammingLanguage.GRAPH:
                    return GraphBlocksKey;

                default:
                    return null;
            }
        }

        /// <summary>
        /// The export tool for the object type, or null when there is none or the format is unknown.
        /// The format comes from 'tia.json', so one tool per object kind writes every format.
        /// </summary>
        public static string? RecommendedTool(string key, ExportFormatKind kind)
        {
            if (kind == ExportFormatKind.Unknown)
            {
                return null;
            }

            if (key.StartsWith(PlcTypesKey, StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("F-compliant-PLC-data-types", StringComparison.OrdinalIgnoreCase))
            {
                return "ExportType";
            }

            if (key.StartsWith(PlcTagTablesKey, StringComparison.OrdinalIgnoreCase))
            {
                return "ExportTagTable";
            }

            var isBlock = key.StartsWith("code-blocks", StringComparison.OrdinalIgnoreCase) ||
                          key.StartsWith(DataBlocksKey, StringComparison.OrdinalIgnoreCase) ||
                          key.StartsWith("safety-data-blocks", StringComparison.OrdinalIgnoreCase);

            return isBlock ? "ExportBlock" : null;
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

            WriteAtomically(file, new JsonObject { [ExportFormats.GroupName] = group });
        }

        /// <summary>
        /// Creates or completes 'tia.json' for 'tia init'. A missing file is created with the
        /// 'args' that were given (blank ones are left out) and the format defaults of TIA Portal.
        /// An existing file is merged, never overwritten: missing 'args' keys and missing format keys
        /// are added, every existing value and every other key stays as it is. A file that is not
        /// valid JSON is an error and stays untouched. Nothing is written when nothing changes.
        /// </summary>
        public static TiaJsonInitResult Init(string exportRoot, IReadOnlyDictionary<string, string> args)
        {
            if (string.IsNullOrWhiteSpace(exportRoot))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"The export path is empty, so '{ExportFormats.SettingsFileName}' cannot be written.");
            }

            var file = Path.Combine(exportRoot, ExportFormats.SettingsFileName);
            var created = !System.IO.File.Exists(file);
            var root = created ? new JsonObject() : ReadRoot(file);
            var result = new TiaJsonInitResult { File = file, Created = created };
            var given = args.Where(a => !string.IsNullOrWhiteSpace(a.Value)).ToList();

            if (!created && root.FirstOrDefault(p => string.Equals(p.Key, ExportFormats.ArgsGroupName, StringComparison.OrdinalIgnoreCase)).Value is JsonObject existingArgs)
            {
                RenameDeprecatedArg(existingArgs, result);
            }

            if (given.Count > 0)
            {
                var argsGroup = GetOrAddObject(root, ExportFormats.ArgsGroupName, file);

                AddMissing(argsGroup, given, result.AddedArgs, result.KeptArgs);
            }

            var formats = GetOrAddObject(root, ExportFormats.GroupName, file);

            AddMissing(formats, ExportFormats.Defaults, result.AddedFormats, null);

            result.Changed = created || result.AddedArgs.Count > 0 || result.RenamedArgs.Count > 0 || result.AddedFormats.Count > 0;

            if (result.Changed)
            {
                WriteAtomically(file, root);
            }

            return result;
        }

        /// <summary>
        /// Renames the deprecated 'preserve-path' key to 'keep-structure', keeping its value. When the
        /// file has both keys nothing is changed and the old one is reported as kept, so no value is lost.
        /// </summary>
        private static void RenameDeprecatedArg(JsonObject group, TiaJsonInitResult result)
        {
            var old = group.FirstOrDefault(p => string.Equals(p.Key, ExportFormats.ArgsPreservePathKeyDeprecated, StringComparison.OrdinalIgnoreCase));

            if (old.Key == null)
            {
                return;
            }

            if (group.Any(p => string.Equals(p.Key, ExportFormats.ArgsKeepStructureKey, StringComparison.OrdinalIgnoreCase)))
            {
                result.KeptArgs.Add(old.Key);

                return;
            }

            var value = old.Value?.DeepClone();

            group.Remove(old.Key);
            group[ExportFormats.ArgsKeepStructureKey] = value;
            result.RenamedArgs.Add($"{old.Key} -> {ExportFormats.ArgsKeepStructureKey}");
        }

        private static JsonObject ReadRoot(string file)
        {
            JsonNode? root;

            try
            {
                root = JsonNode.Parse(System.IO.File.ReadAllText(file));
            }
            catch (JsonException ex)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{file}' is not valid JSON: {ex.Message}", null, ex);
            }

            return root as JsonObject
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{file}' does not hold a JSON object at its top level.");
        }

        /// <summary>The object of that name (matched without regard to case), added when it does not exist yet.</summary>
        private static JsonObject GetOrAddObject(JsonObject parent, string name, string file)
        {
            var match = parent.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase));

            if (match.Key == null)
            {
                var added = new JsonObject();

                parent[name] = added;

                return added;
            }

            return match.Value as JsonObject
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{file}' has \"{match.Key}\", but it is not an object.");
        }

        /// <summary>Adds the entries the group does not have; an entry with another value is only reported as kept.</summary>
        private static void AddMissing(
            JsonObject group, IEnumerable<KeyValuePair<string, string>> entries, List<string> added, List<string>? kept)
        {
            foreach (var entry in entries)
            {
                var match = group.FirstOrDefault(p => string.Equals(p.Key, entry.Key, StringComparison.OrdinalIgnoreCase));

                if (match.Key == null)
                {
                    group[entry.Key] = entry.Value;
                    added.Add(entry.Key);
                }
                else if (kept != null && !(match.Value is JsonValue node && node.TryGetValue<string>(out var text) && text == entry.Value))
                {
                    kept.Add(entry.Key);
                }
            }
        }

        private static void TryDelete(string file)
        {
            try
            {
                System.IO.File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // the original error is the one worth reporting
            }
        }

        /// <summary>Writes through a temporary file, so a crash never leaves half a file behind.</summary>
        private static void WriteAtomically(string file, JsonObject root)
        {
            var temporary = file + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                System.IO.File.WriteAllText(
                    temporary,
                    root.ToJsonString(new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                // File.Move cannot overwrite on .NET Framework; File.Replace swaps in one step, so the
                // user's file is never gone while the new one is not in place yet.
                if (System.IO.File.Exists(file))
                {
                    System.IO.File.Replace(temporary, file, null);
                }
                else
                {
                    System.IO.File.Move(temporary, file);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(temporary);

                throw new PortalException(PortalErrorCode.ExportFailed,
                    $"'{file}' could not be written: {ex.Message}", null, ex);
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

    /// <summary>What 'tia init' did to 'tia.json'.</summary>
    public sealed class TiaJsonInitResult
    {
        /// <summary>The settings file that was created or completed.</summary>
        public string File { get; set; } = string.Empty;

        /// <summary>True when the file did not exist and was created.</summary>
        public bool Created { get; set; }

        /// <summary>False when the file already had everything and was not rewritten.</summary>
        public bool Changed { get; set; }

        /// <summary>The 'args' keys that were added.</summary>
        public List<string> AddedArgs { get; } = new List<string>();

        /// <summary>The 'args' keys whose different value in the file was kept (or a deprecated key that was left because the new key exists).</summary>
        public List<string> KeptArgs { get; } = new List<string>();

        /// <summary>The deprecated 'args' keys that were renamed in place, as 'old -> new'.</summary>
        public List<string> RenamedArgs { get; } = new List<string>();

        /// <summary>The export format keys that were added.</summary>
        public List<string> AddedFormats { get; } = new List<string>();
    }
}
