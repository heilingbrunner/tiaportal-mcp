using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TiaMcpServer.Git
{
    /// <summary>A git call failed; the message says why in words a tool caller can act on.</summary>
    public sealed class GitException : Exception
    {
        public GitException(string message, int exitCode = 0, Exception? inner = null)
            : base(message, inner)
        {
            ExitCode = exitCode;
        }

        /// <summary>The exit code of the git process, or 0 when git did not run at all.</summary>
        public int ExitCode { get; }
    }

    public static partial class GitRepo
    {
        // Thin wrapper around the git command line, shared by the partial files of this class.
        //
        // Callers: Git.Changes.cs and, through it, the ImportPlcProgramChanges tool. Affected
        // API: none existing - the class is new. External effects: starts 'git' from PATH with
        // the given working directory; the tool only runs read-only commands (rev-parse, status).
        //
        // The command line is used instead of a library so the server gains no dependency; the
        // output formats chosen by the callers ('-z', porcelain) are stable across git versions.
        //
        // The call runs inside the exclusive access of a TIA Portal write tool, so a git that
        // never returns would freeze TIA Portal behind its "Exclusive access" box. Three things
        // keep that from happening:
        // - the timeout is applied to the process, not to reading its output, and a git that
        //   exceeds it is killed together with its children;
        // - git gets no stdin (the server's own stdin is the MCP channel) and its stdout and
        //   stderr are read by background pumps, so a child that keeps a pipe open (a detached
        //   fsmonitor daemon, an automatic housekeeping run) cannot hold the call;
        // - fsmonitor and the untracked cache are switched off for these calls, so a read-only
        //   look at the working tree never starts a daemon.

        private const int TimeoutMilliseconds = 30000;

        /// <summary>How long to wait for the pipes after the process ended, before using what was read so far.</summary>
        private const int PipeDrainMilliseconds = 2000;

        /// <summary>git exits with 128 on fatal errors such as "not a git repository".</summary>
        private const int FatalExitCode = 128;

        /// <summary>
        /// Runs git in <paramref name="workingDirectory"/> and returns what it wrote to stdout.
        /// Throws <see cref="GitException"/> when git cannot be started, exits with an error or
        /// does not finish in time.
        /// </summary>
        public static string Run(string workingDirectory, params string[] arguments)
        {
            var guarded = new[] { "-c", "core.fsmonitor=false", "-c", "core.untrackedCache=false" }.Concat(arguments).ToArray();

            return RunCommand("git", workingDirectory, guarded, TimeoutMilliseconds);
        }

        /// <summary>
        /// Runs any executable with the safeguards described above. <see cref="Run"/> uses it for
        /// git; it is public so the timeout can be tested with a command that is slow on purpose.
        /// </summary>
        public static string RunCommand(string fileName, string workingDirectory, string[] arguments, int timeoutMilliseconds)
        {
            if (!Directory.Exists(workingDirectory))
            {
                throw new GitException($"Directory '{workingDirectory}' does not exist.");
            }

            var display = fileName + " " + string.Join(" ", arguments);

            var info = new ProcessStartInfo(fileName, string.Join(" ", arguments.Select(Quote)))
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // A missing credential must fail the call, not wait for input nobody can type; and a
            // read-only status must not take the index lock that a running editor may want.
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            info.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";

            Process? process;

            try
            {
                process = Process.Start(info);
            }
            catch (Win32Exception ex)
            {
                throw new GitException($"{fileName} could not be started. Install it and make sure it is on PATH: " + ex.Message, 0, ex);
            }

            using (process)
            {
                if (process == null)
                {
                    throw new GitException($"{fileName} could not be started.");
                }

                process.StandardInput.Close();

                var output = new StreamPump(process.StandardOutput);
                var error = new StreamPump(process.StandardError);

                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    KillTree(process);

                    throw new GitException($"{display} did not finish within {timeoutMilliseconds / 1000.0:0.#} seconds and was stopped.");
                }

                var text = output.Text(PipeDrainMilliseconds);

                if (process.ExitCode != 0)
                {
                    throw new GitException(
                        $"{display} failed (exit code {process.ExitCode}): {error.Text(PipeDrainMilliseconds).Trim()}",
                        process.ExitCode);
                }

                return text;
            }
        }

        /// <summary>True when a failure means "this folder is not in a repository" rather than a broken git.</summary>
        private static bool IsNotARepository(GitException ex)
        {
            return ex.ExitCode == FatalExitCode;
        }

        private static string Quote(string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return argument;
            }

            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>Ends the process and its children; a child that outlives it could hold the pipes open.</summary>
        private static void KillTree(Process process)
        {
            try
            {
                // taskkill's output is redirected: an inherited stdout would be the MCP channel.
                var info = new ProcessStartInfo("taskkill", $"/PID {process.Id} /T /F")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var killer = Process.Start(info))
                {
                    killer?.StandardInput.Close();
                    killer?.StandardOutput.ReadToEnd();
                    killer?.WaitForExit(5000);
                }
            }
            catch (Exception)
            {
                // Fall through to the plain kill below.
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
                // The process ended on its own between the timeout and the kill.
            }
        }

        /// <summary>
        /// Reads a stream on a background task, so the caller can stop waiting for it. A plain
        /// ReadToEnd would block until every process holding the pipe has exited.
        /// </summary>
        private sealed class StreamPump
        {
            private readonly StringBuilder _text = new StringBuilder();
            private readonly Task _task;

            public StreamPump(StreamReader reader)
            {
                _task = Task.Run(() =>
                {
                    var buffer = new char[4096];
                    int count;

                    try
                    {
                        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            lock (_text)
                            {
                                _text.Append(buffer, 0, count);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // The pipe was closed under us (the process was killed); keep what was read.
                    }
                });
            }

            /// <summary>What was read so far, after giving the pump a short time to finish.</summary>
            public string Text(int waitMilliseconds)
            {
                _task.Wait(waitMilliseconds);

                lock (_text)
                {
                    return _text.ToString();
                }
            }
        }
    }
}
