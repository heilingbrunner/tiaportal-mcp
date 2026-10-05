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

        [McpServerTool(Name = "CompileSoftware", Title = "Compile software", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile the PLC software and report every compiler message with the object it belongs to, so errors can be fixed without re-reading the whole PLC. Warnings are reported as a successful compile with detail; only errors fail the call")]
        public static ResponseCompileSoftware CompileSoftware(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("password: the password to access adminsitration, default: no password")] string password = "")
        {
            try
            {
                var result = Portal.CompileSoftware(softwarePath, password);

                if (result == null)
                {
                    throw new McpException(
                        $"Failed compiling software '{softwarePath}'. Check that the path names a PLC software " +
                        "('GetProjectTree' lists them) and, for a safety program, that 'password' is correct.");
                }

                var messages = FlattenCompilerMessages(result.Messages, 0).ToList();
                var state = result.State.ToString();
                var failed = result.State == CompilerResultState.Error;

                var response = new ResponseCompileSoftware
                {
                    Message = $"Compiling '{softwarePath}' finished with state '{state}': " +
                              $"{result.ErrorCount} error(s), {result.WarningCount} warning(s)",
                    State = state,
                    ErrorCount = result.ErrorCount,
                    WarningCount = result.WarningCount,
                    Messages = messages,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = !failed,
                        ["state"] = state,
                        ["errorCount"] = result.ErrorCount,
                        ["warningCount"] = result.WarningCount,
                        ["messageCount"] = messages.Count
                    }
                };

                if (failed)
                {
                    // The message text is what a client surfaces, so name the first few offenders
                    // rather than making the caller re-query to find out what broke.
                    var offenders = messages
                        .Where(m => string.Equals(m.State, nameof(CompilerResultState.Error), StringComparison.OrdinalIgnoreCase))
                        .Take(5)
                        .Select(m => $"{m.Path}: {m.Description}");

                    throw new McpException(
                        $"Compiling '{softwarePath}' failed with {result.ErrorCount} error(s). " +
                        string.Join(" | ", offenders));
                }

                return response;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error compiling software '{softwarePath}': {ex.Message}", ex);
            }
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
