using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the rules behind ExportPlcProgram / OverwritePlcProgram: which format a block gets,
    /// what a file of the import tree is, and the retry rounds. These tests do not connect to TIA
    /// Portal and do not open a project.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test9PlcProgram
    {
        private const string BlockRoot = "Program blocks";
        private const string TypeRoot = "PLC data types";

        [TestInitialize]
        public void ClassInit()
        {
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ToolDefaults.Configure(new CliOptions());
        }

        [TestMethod]
        public void Test_920_ClassifyBlock_TextLanguagesAndDataBlocksAreSources()
        {
            // Act
            var scl = PlcProgram.ClassifyBlock(isDataBlock: false, ProgrammingLanguage.SCL);
            var stl = PlcProgram.ClassifyBlock(isDataBlock: false, ProgrammingLanguage.STL);
            var data = PlcProgram.ClassifyBlock(isDataBlock: true, ProgrammingLanguage.DB);

            // Assert
            Assert.AreEqual(ProgramBlockFormat.Source, scl.Format);
            Assert.AreEqual(".scl", scl.Extension);
            Assert.AreEqual(ProgramBlockFormat.Source, stl.Format);
            Assert.AreEqual(".awl", stl.Extension, "STL sources use .awl");
            Assert.AreEqual(ProgramBlockFormat.Source, data.Format);
            Assert.AreEqual(".db", data.Extension);
        }

        [TestMethod]
        public void Test_921_ClassifyBlock_LadGoesToXml()
        {
            // Act
            var plan = PlcProgram.ClassifyBlock(isDataBlock: false, ProgrammingLanguage.LAD);

            // Assert
            Assert.AreEqual(ProgramBlockFormat.Xml, plan.Format);
            Assert.AreEqual(".xml", plan.Extension);
            Assert.IsNull(plan.Reason);
        }

        [TestMethod]
        public void Test_922_ClassifyBlock_OtherLanguagesAreSkippedWithAReason()
        {
            foreach (var language in new[] { ProgrammingLanguage.FBD, ProgrammingLanguage.GRAPH })
            {
                // Act
                var plan = PlcProgram.ClassifyBlock(isDataBlock: false, language);

                // Assert
                Assert.AreEqual(ProgramBlockFormat.None, plan.Format, language.ToString());
                Assert.IsNull(plan.Extension);
                StringAssert.Contains(plan.Reason, language.ToString());
                StringAssert.Contains(plan.Reason, "ExportXmlBlock", "the reason names the tool to use instead");
            }
        }

        [TestMethod]
        public void Test_923_ClassifyFile_SourcesAndXmlBelowTheBlockFolderAreImported()
        {
            // Act / Assert
            Assert.AreEqual(ProgramFileKind.UdtSource, PlcProgram.ClassifyFile("PLC data types/Common", ".udt", BlockRoot, TypeRoot).Kind);
            Assert.AreEqual(ProgramFileKind.BlockSource, PlcProgram.ClassifyFile("Program blocks/Motors", ".scl", BlockRoot, TypeRoot).Kind);
            Assert.AreEqual(ProgramFileKind.BlockSource, PlcProgram.ClassifyFile("Program blocks", ".awl", BlockRoot, TypeRoot).Kind);
            Assert.AreEqual(ProgramFileKind.BlockSource, PlcProgram.ClassifyFile("Program blocks", ".DB", BlockRoot, TypeRoot).Kind, "the extension is case-insensitive");
            Assert.AreEqual(ProgramFileKind.XmlBlock, PlcProgram.ClassifyFile("Program blocks/Motors", ".xml", BlockRoot, TypeRoot).Kind);
            Assert.AreEqual(ProgramFileKind.XmlBlock, PlcProgram.ClassifyFile("program BLOCKS", ".xml", BlockRoot, TypeRoot).Kind, "the folder name is case-insensitive");
        }

        [TestMethod]
        public void Test_924_ClassifyFile_XmlOutsideTheBlockFolderAndStlAreIgnoredWithAReason()
        {
            // Act
            var tagTable = PlcProgram.ClassifyFile("PLC tags", ".xml", BlockRoot, TypeRoot);
            var atRoot = PlcProgram.ClassifyFile(string.Empty, ".xml", BlockRoot, TypeRoot);
            var stl = PlcProgram.ClassifyFile("Program blocks", ".stl", BlockRoot, TypeRoot);

            // Assert
            Assert.AreEqual(ProgramFileKind.Ignored, tagTable.Kind);
            StringAssert.Contains(tagTable.Reason, BlockRoot);
            Assert.AreEqual(ProgramFileKind.Ignored, atRoot.Kind);
            Assert.AreEqual(ProgramFileKind.Ignored, stl.Kind);
            StringAssert.Contains(stl.Reason, ".awl", "the reason says what STL blocks are written as");
        }

        [TestMethod]
        public void Test_925_ClassifyFile_OtherFilesAreIgnoredSilently()
        {
            // Act
            var plan = PlcProgram.ClassifyFile("Program blocks", ".txt", BlockRoot, TypeRoot);

            // Assert
            Assert.AreEqual(ProgramFileKind.Ignored, plan.Kind);
            Assert.IsNull(plan.Reason, "a readme next to the sources is not worth a message");
        }

        [TestMethod]
        public void Test_926_RunRounds_RetriesAnItemThatWaitsForALaterOne()
        {
            // Arrange: the LAD block needs the SCL block that comes after it in the list
            var imported = new List<string>();

            void Import(string item)
            {
                if (item == "LadBlock" && !imported.Contains("SclBlock"))
                {
                    throw new InvalidOperationException("SclBlock is missing");
                }

                imported.Add(item);
            }

            // Act
            var result = PlcProgram.RunRounds(new[] { "LadBlock", "SclBlock" }, Import);

            // Assert
            CollectionAssert.AreEqual(new[] { "SclBlock", "LadBlock" }, result.Done);
            Assert.AreEqual(0, result.Failed.Count);
            Assert.AreEqual(2, result.Rounds);
        }

        [TestMethod]
        public void Test_927_RunRounds_ReportsWhatNeverSucceedsWithItsLastError()
        {
            // Arrange
            var attempts = 0;

            void Import(string item)
            {
                if (item == "Broken")
                {
                    attempts++;

                    throw new InvalidOperationException($"attempt {attempts}");
                }
            }

            // Act
            var result = PlcProgram.RunRounds(new[] { "Broken", "Fine" }, Import);

            // Assert
            CollectionAssert.AreEqual(new[] { "Fine" }, result.Done);
            Assert.AreEqual(1, result.Failed.Count);
            Assert.AreEqual("Broken", result.Failed[0].Item);
            Assert.AreEqual("attempt 2", result.Failed[0].Error, "the error of the last attempt is kept");
            Assert.AreEqual(2, result.Rounds, "one round that made progress, one that did not");
        }

        [TestMethod]
        public void Test_928_RunRounds_StopsAfterOneRoundWhenNothingSucceeds()
        {
            // Act
            var result = PlcProgram.RunRounds(new[] { "A", "B" }, _ => throw new InvalidOperationException("no"));

            // Assert
            Assert.AreEqual(0, result.Done.Count);
            Assert.AreEqual(2, result.Failed.Count);
            Assert.AreEqual(1, result.Rounds, "no progress means no second pass, so this cannot loop forever");
        }

        [TestMethod]
        public void Test_929_RunRounds_WithNothingToImportMakesNoRound()
        {
            // Act
            var result = PlcProgram.RunRounds(Array.Empty<string>(), _ => throw new InvalidOperationException("not called"));

            // Assert
            Assert.AreEqual(0, result.Rounds);
            Assert.AreEqual(0, result.Done.Count);
            Assert.AreEqual(0, result.Failed.Count);
        }

        [TestMethod]
        public void Test_930_BuildTools_ImportNeedsWriteModeAndExportDoesNot()
        {
            // Act
            var readOnly = Program.BuildTools(allowWrite: false).Select(t => t.ProtocolTool.Name).ToList();
            var withWrite = Program.BuildTools(allowWrite: true).Select(t => t.ProtocolTool.Name).ToList();

            // Assert
            CollectionAssert.Contains(readOnly, "ExportPlcProgram");
            foreach (var name in new[] { "OverwritePlcProgram", "ImportPlcProgramChanges" })
            {
                CollectionAssert.DoesNotContain(readOnly, name, "overwriting changes the project, so it needs --allow-write");
                CollectionAssert.Contains(withWrite, name);
            }

            CollectionAssert.Contains(withWrite, "ExportPlcProgram");
        }

        [TestMethod]
        public void Test_931_ImportTools_PreviewByDefault()
        {
            foreach (var name in new[] { nameof(McpServer.OverwritePlcProgram), nameof(McpServer.ImportPlcProgramChanges) })
            {
                // Arrange
                var method = typeof(McpServer).GetMethod(name)!;

                // Act
                var dryRun = method.GetParameters().Single(p => p.Name == "dryRun");

                // Assert
                Assert.IsTrue(dryRun.HasDefaultValue, name);
                Assert.AreEqual(true, dryRun.DefaultValue, $"{name} must not change the project unless dryRun=false is passed");
            }
        }

        [TestMethod]
        public void Test_932_ImportDescriptions_TellTheAgentItReplacesAndToPreviewFirst()
        {
            foreach (var name in new[] { nameof(McpServer.OverwritePlcProgram), nameof(McpServer.ImportPlcProgramChanges) })
            {
                // Act
                var description = typeof(McpServer).GetMethod(name)!
                    .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
                    .Cast<System.ComponentModel.DescriptionAttribute>()
                    .Single().Description;

                // Assert
                StringAssert.Contains(description, "REPLACED", name);
                StringAssert.Contains(description, "dryRun", name);
                StringAssert.Contains(description, "yes, import", name);
                StringAssert.Contains(description, "save project", name);
            }
        }

        [TestMethod]
        public void Test_934_Descriptions_SteerEditedFilesToImportPlcProgramChanges()
        {
            // Arrange
            string Description(string name) => typeof(McpServer).GetMethod(name)!
                .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
                .Cast<System.ComponentModel.DescriptionAttribute>()
                .Single().Description;

            // Act
            var whole = Description(nameof(McpServer.OverwritePlcProgram));
            var changes = Description(nameof(McpServer.ImportPlcProgramChanges));

            // Assert
            StringAssert.Contains(whole, "ONLY when the user explicitly wants the WHOLE program", "the full-tree tool is not the default");
            StringAssert.Contains(whole, nameof(McpServer.ImportPlcProgramChanges), "it points to the tool for edited files");
            StringAssert.Contains(changes, "by default", "the changes tool is the default for edited files");
            StringAssert.Contains(changes, nameof(McpServer.OverwritePlcProgram), "it points to the tool for the whole folder");
        }

        [TestMethod]
        public void Test_933_ImportPrompts_DescribeThePreviewWorkflow()
        {
            foreach (var body in new[]
            {
                McpPrompts.OverwritePlcProgram("PLC_1", "C:\\export"),
                McpPrompts.ImportPlcProgramChanges("PLC_1", "C:\\export")
            })
            {
                // Assert
                StringAssert.Contains(body, "dryRun true");
                StringAssert.Contains(body, "yes, import", "the import needs an explicit confirmation");
                StringAssert.Contains(body, "save project", "saving needs an explicit request, not an automatic call");
                StringAssert.Contains(body, "- dryRun: true", "the prompt passes dryRun=true unless asked otherwise");
            }
        }
    }
}
