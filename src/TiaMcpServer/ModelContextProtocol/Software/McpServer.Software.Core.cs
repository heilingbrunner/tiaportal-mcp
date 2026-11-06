using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.Compiler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region plc software

        [McpServerTool(Name = "GetSoftwareInfo", Title = "Get software info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Show name, description and attributes of one PLC software")]
        public static ResponseSoftwareInfo GetSoftwareInfo(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath)
        {
            try
            {
                var software = Portal.GetPlcSoftware(softwarePath);
                if (software is not null)
                {

                    var attributes = Helper.GetAttributeList(software);

                    return new ResponseSoftwareInfo
                    {
                        Message = $"Software info retrieved from '{softwarePath}'",
                        Name = software.Name,
                        Attributes = attributes,
                        Description = software.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Software not found at '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software info from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "CompilePlcSoftware", Title = "Compile PLC software", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile the whole PLC software and report every compiler message with the object it belongs to, so errors can be fixed without re-reading the whole PLC. Warnings are reported as a successful compile with detail; errors fail the call, and its text holds the complete report with every message. Show the 'Report' to the user as tables, unchanged. To compile a single block use 'CompileCodeBlock'")]
        public static ResponseCompile CompilePlcSoftware(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("password: the password to access adminsitration, default: no password")] string password = "")
        {
            try
            {
                var result = Portal.CompilePlcSoftware(softwarePath, password);

                if (result == null)
                {
                    throw new McpException(
                        $"Failed compiling software '{softwarePath}'. Check that the path names a PLC software " +
                        "('GetProjectTree' lists them) and, for a safety program, that 'password' is correct.");
                }

                return BuildCompileResponse(result, softwarePath);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error compiling software '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "CompileCodeBlock", Title = "Compile code block", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile a single code block (OB, FC, FB or DB) and report its compiler messages with the object each belongs to. Warnings are reported as a successful compile with detail; errors fail the call, and its text holds the complete report with every message. Show the 'Report' to the user as tables, unchanged. To compile the whole PLC use 'CompilePlcSoftware'")]
        public static ResponseCompile CompileCodeBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block, e.g. '0_OBs/Main_1'")] string blockPath,
            [Description("password: the password to access adminsitration, default: no password")] string password = "")
        {
            try
            {
                var result = Portal.CompileCodeBlock(softwarePath, blockPath, password);

                if (result == null)
                {
                    throw new McpException(
                        $"Failed compiling block '{blockPath}' in '{softwarePath}'. Check that the block exists " +
                        "('GetBlocks' lists them) and, for a safety program, that 'password' is correct.");
                }

                return BuildCompileResponse(result, blockPath);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error compiling block '{blockPath}': {ex.Message}", ex);
            }
        }

        private static ResponseCompile BuildCompileResponse(CompilerResult result, string target)
        {
            var messages = FlattenCompilerMessages(result.Messages, 0).ToList();
            var state = result.State.ToString();
            var failed = result.State == CompilerResultState.Error;

            if (failed)
            {
                // The message text is what a client surfaces, so it carries the whole table:
                // every message, not just the first few, so nobody has to re-query.
                throw new McpException(
                    $"Compiling '{target}' failed with {result.ErrorCount} error(s). Show this report to the user as tables, unchanged.{Environment.NewLine}{Environment.NewLine}" +
                    ReportFormatter.Compile(target, state, result.ErrorCount, result.WarningCount, messages));
            }

            return new ResponseCompile
            {
                Message = $"Compiling '{target}' finished with state '{state}': " +
                          $"{result.ErrorCount} error(s), {result.WarningCount} warning(s). Show the 'Report' to the user as tables, unchanged.",
                State = state,
                ErrorCount = result.ErrorCount,
                WarningCount = result.WarningCount,
                Messages = messages,
                Report = ReportFormatter.Compile(target, state, result.ErrorCount, result.WarningCount, messages),
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["state"] = state,
                    ["errorCount"] = result.ErrorCount,
                    ["warningCount"] = result.WarningCount,
                    ["messageCount"] = messages.Count
                }
            };
        }

        /// <summary>
        /// Walks the recursive CompilerResultMessage tree depth-first. Openness nests messages
        /// per object and then per detail, and the previous implementation discarded all of it
        /// in favour of CompilerResult.ToString().
        /// </summary>
        private static IEnumerable<CompileMessage> FlattenCompilerMessages(CompilerResultMessageComposition? messages, int depth)
        {
            if (messages == null)
            {
                yield break;
            }

            foreach (var message in messages)
            {
                if (message == null)
                {
                    continue;
                }

                yield return new CompileMessage
                {
                    Path = message.Path,
                    State = message.State.ToString(),
                    Description = message.Description,
                    ErrorCount = message.ErrorCount,
                    WarningCount = message.WarningCount,
                    Depth = depth
                };

                foreach (var child in FlattenCompilerMessages(message.Messages, depth + 1))
                {
                    yield return child;
                }
            }
        }

        #endregion
    }
}
