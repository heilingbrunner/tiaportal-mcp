using Siemens.Engineering.SW.Blocks;
using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the export format settings in 'tia.json' and the decisions ExportPlcProgram
    /// takes from them. These tests do not connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test10ExportFormats
    {
        private const string SimaticSd = "SIMATIC SD(.s7dcl, .s7res)";

        private string _directory = string.Empty;

        [TestInitialize]
        public void ClassInit()
        {
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);

            _directory = Path.Combine(Path.GetTempPath(), "TiaMcpServer.Test10." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static ExportFormatSettings SettingsOf(params (string Key, string Value)[] entries)
        {
            var body = string.Join(", ", entries.Select(e => $"\"{e.Key}\": \"{e.Value}\""));

            return ExportFormatSettings.Parse($"{{ \"version control interface\": {{ {body} }} }}", "test.json");
        }

        private static PortalException Throws(Action action)
        {
            try
            {
                action();
            }
            catch (PortalException ex)
            {
                return ex;
            }

            Assert.Fail("a PortalException was expected");

            return null!;
        }

        [DataTestMethod]
        [DataRow("SIMATIC SD(.s7dcl, .s7res)", ExportFormatKind.SimaticSd)]
        [DataRow("simatic sd (.s7dcl, .s7res)", ExportFormatKind.SimaticSd)]
        [DataRow(".xml", ExportFormatKind.Xml)]
        [DataRow(".awl", ExportFormatKind.Source)]
        [DataRow(".scl", ExportFormatKind.Source)]
        [DataRow(".db", ExportFormatKind.Source)]
        [DataRow(".udt", ExportFormatKind.Source)]
        [DataRow("SIMATIC SD(.yml, .js)", ExportFormatKind.SimaticSd)]
        [DataRow(".txt", ExportFormatKind.Unknown)]
        [DataRow("", ExportFormatKind.Unknown)]
        public void Test_1000_ParseKind_ReadsTheTextTiaPortalShows(string value, ExportFormatKind expected)
        {
            Assert.AreEqual(expected, ExportFormats.ParseKind(value));
        }

        [TestMethod]
        public void Test_1001_ParseKind_NullIsUnknown()
        {
            Assert.AreEqual(ExportFormatKind.Unknown, ExportFormats.ParseKind(null));
        }

        [TestMethod]
        public void Test_1010_BlockKey_FollowsTheLanguageAndDataBlocks()
        {
            Assert.AreEqual("code blocks-LAD", ExportFormats.BlockKey(false, ProgrammingLanguage.LAD));
            Assert.AreEqual("code blocks-FBD", ExportFormats.BlockKey(false, ProgrammingLanguage.FBD));
            Assert.AreEqual("code blocks-SCL", ExportFormats.BlockKey(false, ProgrammingLanguage.SCL));
            Assert.AreEqual("code blocks-STL", ExportFormats.BlockKey(false, ProgrammingLanguage.STL));
            Assert.IsNull(ExportFormats.BlockKey(false, ProgrammingLanguage.GRAPH), "GRAPH has no entry in the settings");
            Assert.AreEqual("data blocks", ExportFormats.BlockKey(true, ProgrammingLanguage.LAD), "a data block is matched by type, not by language");
            Assert.IsNull(ExportFormats.BlockKey(false, (ProgrammingLanguage)(-1)));
        }

        [TestMethod]
        public void Test_1020_RecommendedTool_NamesTheToolThatWritesTheFormat()
        {
            Assert.AreEqual("ExportAsDocuments", ExportFormats.RecommendedTool("code blocks-LAD", ExportFormatKind.SimaticSd));
            Assert.AreEqual("ExportXmlBlock", ExportFormats.RecommendedTool("code blocks-FBD", ExportFormatKind.Xml));
            Assert.AreEqual("ExportSourceBlock", ExportFormats.RecommendedTool("code blocks-STL", ExportFormatKind.Source));
            Assert.AreEqual("ExportSourceBlock", ExportFormats.RecommendedTool("data blocks", ExportFormatKind.Source));
            Assert.AreEqual("ExportTypeAsDocuments", ExportFormats.RecommendedTool("PLC data types", ExportFormatKind.SimaticSd));
            Assert.AreEqual("ExportXmlType", ExportFormats.RecommendedTool("PLC data types", ExportFormatKind.Xml));
            Assert.AreEqual("ExportSourceType", ExportFormats.RecommendedTool("PLC data types", ExportFormatKind.Source));
            Assert.IsNull(ExportFormats.RecommendedTool("PLC tag tables", ExportFormatKind.Xml), "an object type without an export tool for it");
            Assert.IsNull(ExportFormats.RecommendedTool("code blocks-LAD", ExportFormatKind.Unknown));
        }

        [TestMethod]
        public void Test_1030_Parse_ReadsStringValuesAndIgnoresTheRest()
        {
            // Arrange
            var json = "{ \"Version Control Interface\": { \"code blocks-LAD\": \"" + SimaticSd + "\", \"count\": 3 }, \"other\": {} }";

            // Act
            var settings = ExportFormatSettings.Parse(json, "test.json");

            // Assert
            Assert.AreEqual(SimaticSd, settings.Get("CODE BLOCKS-lad"), "the group and the keys are matched without regard to case");
            Assert.IsNull(settings.Get("count"));
            Assert.AreEqual(1, settings.Values.Count);
        }

        [TestMethod]
        public void Test_1031_Parse_RejectsBrokenJsonAndAMissingGroup()
        {
            var broken = Throws(() => ExportFormatSettings.Parse("{ not json", "broken.json"));
            var noGroup = Throws(() => ExportFormatSettings.Parse("{ \"other\": {} }", "nogroup.json"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, broken.Code);
            StringAssert.Contains(broken.Message, "broken.json");
            Assert.AreEqual(PortalErrorCode.InvalidParams, noGroup.Code);
            StringAssert.Contains(noGroup.Message, "version control interface");
        }

        [TestMethod]
        public void Test_1040_Load_ReadsTheFileAtTheExportRoot()
        {
            // Arrange
            File.WriteAllText(
                Path.Combine(_directory, ExportFormats.SettingsFileName),
                "{ \"version control interface\": { \"code blocks-STL\": \".awl\" } }");

            // Act
            var settings = ExportFormatSettings.Load(_directory);

            // Assert
            Assert.AreEqual(".awl", settings.Get("code blocks-STL"));
            Assert.AreEqual(Path.Combine(_directory, ExportFormats.SettingsFileName), settings.File);
        }

        [TestMethod]
        public void Test_1041_Load_MissingFileIsCreatedWithTheDefaultsAndAHint()
        {
            // Arrange
            var file = Path.Combine(_directory, ExportFormats.SettingsFileName);

            // Act
            var settings = ExportFormatSettings.Load(_directory);

            // Assert
            Assert.IsTrue(settings.WasCreated);
            Assert.IsTrue(File.Exists(file));
            StringAssert.Contains(settings.Hint, file);
            StringAssert.Contains(settings.Hint, "check");
            Assert.AreEqual(".awl", settings.Get("code blocks-STL"));
            Assert.AreEqual(SimaticSd, settings.Get("PLC data types"));
            Assert.AreEqual(ExportFormats.Defaults.Count, settings.Values.Count);
        }

        [TestMethod]
        public void Test_1043_Load_CreatedFileIsValidJsonThatLoadsAgainWithoutAHint()
        {
            // Arrange
            var created = ExportFormatSettings.Load(_directory);

            // Act
            var reloaded = ExportFormatSettings.Load(_directory);

            // Assert
            Assert.IsTrue(created.WasCreated);
            Assert.IsFalse(reloaded.WasCreated);
            Assert.IsNull(reloaded.Hint);
            CollectionAssert.AreEquivalent(created.Values.ToList(), reloaded.Values.ToList());
            Assert.IsFalse(File.Exists(Path.Combine(_directory, ExportFormats.SettingsFileName + ".tmp")), "the temporary file is moved away");
            Assert.IsFalse(File.ReadAllBytes(Path.Combine(_directory, ExportFormats.SettingsFileName)).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "no BOM");
        }

        [TestMethod]
        public void Test_1044_Load_CreatesTheExportFolderWhenItDoesNotExistYet()
        {
            // Arrange
            var nested = Path.Combine(_directory, "export", "Program");

            // Act
            var settings = ExportFormatSettings.Load(nested);

            // Assert
            Assert.IsTrue(settings.WasCreated);
            Assert.IsTrue(File.Exists(Path.Combine(nested, ExportFormats.SettingsFileName)));
        }

        [TestMethod]
        public void Test_1045_Load_ExistingFileIsNeverOverwritten()
        {
            // Arrange
            var file = Path.Combine(_directory, ExportFormats.SettingsFileName);
            var own = "{ \"version control interface\": { \"code blocks-STL\": \".xml\" } }";
            File.WriteAllText(file, own);

            // Act
            var settings = ExportFormatSettings.Load(_directory);

            // Assert
            Assert.IsFalse(settings.WasCreated);
            Assert.AreEqual(own, File.ReadAllText(file));
            Assert.AreEqual(1, settings.Values.Count, "an entry the file lacks is not filled in from the defaults");
        }

        [TestMethod]
        public void Test_1046_Defaults_CoverEveryObjectTypeOfTheScreenshotWithAKnownFormat()
        {
            var keys = ExportFormats.Defaults.Select(d => d.Key).ToList();

            foreach (var key in new[]
            {
                "code blocks-LAD", "code blocks-FBD", "code blocks-SCL", "code blocks-STL",
                "data blocks", "safety data blocks (F-DBs)", "PLC data types", "F-compliant PLC data types (UDT)"
            })
            {
                CollectionAssert.Contains(keys, key);
            }

            Assert.AreEqual(8, keys.Count);
            Assert.IsTrue(ExportFormats.Defaults.All(d => ExportFormats.ParseKind(d.Value) != ExportFormatKind.Unknown));
            Assert.AreEqual(".awl", ExportFormats.Defaults.Single(d => d.Key == "code blocks-STL").Value);

            foreach (var removed in new[] { "hmi unified global scripts", "code blocks-GRAPH", "PLC tag tables", "technology objects" })
            {
                CollectionAssert.DoesNotContain(keys, removed);
            }
        }

        [TestMethod]
        public void Test_1042_Load_EmptyExportPathFails()
        {
            Assert.AreEqual(PortalErrorCode.InvalidParams, Throws(() => ExportFormatSettings.Load("  ")).Code);
        }

        [TestMethod]
        public void Test_1050_PlanBlock_FollowsTheConfiguredFormat()
        {
            // Arrange
            var settings = SettingsOf(
                ("code blocks-LAD", SimaticSd),
                ("code blocks-FBD", ".xml"),
                ("code blocks-STL", ".awl"),
                ("data blocks", ".db"));

            // Act
            var lad = PlcProgram.PlanBlock(false, ProgrammingLanguage.LAD, settings);
            var fbd = PlcProgram.PlanBlock(false, ProgrammingLanguage.FBD, settings);
            var stl = PlcProgram.PlanBlock(false, ProgrammingLanguage.STL, settings);
            var db = PlcProgram.PlanBlock(true, ProgrammingLanguage.DB, settings);

            // Assert
            Assert.AreEqual(ProgramBlockFormat.Document, lad.Format);
            Assert.AreEqual(".s7dcl", lad.Extension);
            Assert.AreEqual(ProgramBlockFormat.Xml, fbd.Format);
            Assert.AreEqual(ProgramBlockFormat.Source, stl.Format);
            Assert.AreEqual(".awl", stl.Extension);
            Assert.AreEqual(ProgramBlockFormat.Source, db.Format);
            Assert.AreEqual(".db", db.Extension);
        }

        [TestMethod]
        public void Test_1051_PlanBlock_SourceFormatForABlockWithoutSourceFormIsSkippedWithAReason()
        {
            // Arrange
            var settings = SettingsOf(("code blocks-LAD", ".awl"));

            // Act
            var plan = PlcProgram.PlanBlock(false, ProgrammingLanguage.LAD, settings);

            // Assert
            Assert.AreEqual(ProgramBlockFormat.None, plan.Format);
            StringAssert.Contains(plan.Reason, "no source form");
        }

        [TestMethod]
        public void Test_1052_PlanBlock_LanguageWithoutASettingIsSkipped()
        {
            var plan = PlcProgram.PlanBlock(false, (ProgrammingLanguage)(-1), SettingsOf(("code blocks-LAD", ".xml")));

            Assert.AreEqual(ProgramBlockFormat.None, plan.Format);
            StringAssert.Contains(plan.Reason, "export format settings");
        }

        [TestMethod]
        public void Test_1054_PlanBlock_GraphBlockHasNoSettingAndIsSkippedNotFailed()
        {
            var plan = PlcProgram.PlanBlock(false, ProgrammingLanguage.GRAPH, SettingsOf(("code blocks-LAD", ".xml")));

            Assert.AreEqual(ProgramBlockFormat.None, plan.Format);
            StringAssert.Contains(plan.Reason, "GRAPH");
            StringAssert.Contains(plan.Reason, "export format settings");
        }

        [TestMethod]
        public void Test_1053_PlanBlock_MissingEntryAndUnknownFormatFail()
        {
            var settings = SettingsOf(("code blocks-LAD", ".txt"));

            var missing = Throws(() => PlcProgram.PlanBlock(false, ProgrammingLanguage.SCL, settings));
            var unknown = Throws(() => PlcProgram.PlanBlock(false, ProgrammingLanguage.LAD, settings));

            Assert.AreEqual(PortalErrorCode.InvalidParams, missing.Code);
            StringAssert.Contains(missing.Message, "code blocks-SCL");
            Assert.AreEqual(PortalErrorCode.InvalidParams, unknown.Code);
            StringAssert.Contains(unknown.Message, ".txt");
        }

        [TestMethod]
        public void Test_1060_PlanType_ReadsThePlcDataTypeEntryAndFailsWithoutIt()
        {
            Assert.AreEqual(ExportFormatKind.SimaticSd, PlcProgram.PlanType(SettingsOf(("PLC data types", SimaticSd))));
            Assert.AreEqual(ExportFormatKind.Source, PlcProgram.PlanType(SettingsOf(("PLC data types", ".udt"))));

            var ex = Throws(() => PlcProgram.PlanType(SettingsOf(("code blocks-LAD", ".xml"))));

            Assert.AreEqual(PortalErrorCode.InvalidParams, ex.Code);
            StringAssert.Contains(ex.Message, "PLC data types");
        }

        [TestMethod]
        public void Test_1070_BuildTools_GetExportFormatsIsReadOnly()
        {
            var readOnly = Program.BuildTools(allowWrite: false).Select(t => t.ProtocolTool.Name).ToList();

            CollectionAssert.Contains(readOnly, "GetExportFormats");
        }
    }
}
