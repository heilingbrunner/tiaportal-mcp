using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Multiuser;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TiaMcpServer.Siemens
{
    // Several TIA Portal instances can run on one machine, and the MCP protocol is stateless: no
    // request may rely on "the instance an earlier request connected to". Each attached instance
    // therefore gets a PortalContext keyed by its process id, and a request names its instance
    // with 'portalId' - or leaves it out when exactly one instance is attached. The registry only
    // saves re-attaching; it never picks between instances on its own.
    //
    // Callers: McpServer (Connect, GetPortals, the call-tool filter in PortalSelection) and the
    // MSTest suite through ConnectPortal(). Affected API: ConnectPortal() now fails instead of
    // attaching to an arbitrary instance when several are running. Reads/writes no data files.
    public partial class Portal
    {
        private readonly ConcurrentDictionary<int, PortalContext> _contexts = new();

        private readonly AsyncLocal<PortalContext?> _scoped = new();

        private readonly object _attachLock = new();

        public static readonly TimeSpan DefaultGateTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long a request waits for another request on the same instance to finish. A
        /// modal dialog in TIA Portal can block an Openness call indefinitely; past this time
        /// the waiting request fails with "busy" instead of hanging silently.
        /// </summary>
        public TimeSpan GateTimeout { get; set; } = DefaultGateTimeout;

        // Outside a request scope (unit tests calling Portal directly) the single attached
        // instance is used. With several attached and no scope there is no right answer, so it
        // fails loudly rather than reading null and silently dropping project assignments.
        private PortalContext? Current
        {
            get
            {
                var scoped = _scoped.Value;

                if (scoped != null)
                {
                    return scoped;
                }

                if (_contexts.Count > 1)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"{_contexts.Count} TIA Portal instances are attached and this call selects none of them. Pass 'portalId' to choose one.",
                        _contexts.Keys.Select(k => k.ToString()));
                }

                return _contexts.Values.FirstOrDefault();
            }
        }

        // Kept under their former field names so the partials (Blocks, Devices, Software, ...)
        // read the project of the current request without change.
        private TiaPortal? _portal => Current?.TiaPortal;

        private ProjectBase? _project
        {
            get => Current?.Project;
            set
            {
                var context = Current;
                if (context != null)
                {
                    context.Project = value;
                }
            }
        }

        private LocalSession? _session
        {
            get => Current?.Session;
            set
            {
                var context = Current;
                if (context != null)
                {
                    context.Session = value;
                }
            }
        }

        public int? CurrentPortalId => Current?.Id;

        #region instances

        public List<PortalInstance> GetPortals()
        {
            _logger?.LogInformation("Listing running TIA Portal instances...");

            return TiaPortal.GetProcesses()
                .Select(p => new PortalInstance
                {
                    PortalId = p.Id,
                    ProjectPath = p.ProjectPath?.ToString(),
                    Mode = p.Mode.ToString(),
                    IsAttached = _contexts.TryGetValue(p.Id, out var context) && IsAlive(context)
                })
                .ToList();
        }

        /// <summary>
        /// Attaches to the TIA Portal instance with the given process id. Without an id it
        /// attaches to the only running instance, or starts one when none is running; with
        /// several running it refuses rather than guessing.
        /// </summary>
        public int AttachPortal(int? portalId)
        {
            return AttachPortal(portalId, out _);
        }

        /// <param name="wasAttached">true when the instance was already attached before this call</param>
        public int AttachPortal(int? portalId, out bool wasAttached)
        {
            _logger?.LogInformation("Attaching to TIA Portal {PortalId}...", portalId?.ToString() ?? "(any)");

            // Connect is not scoped: without this lock two concurrent calls could both attach,
            // and the second Register would dispose the instance the first one returned.
            lock (_attachLock)
            {
                return AttachPortalLocked(portalId, out wasAttached);
            }
        }

        private int AttachPortalLocked(int? portalId, out bool wasAttached)
        {
            wasAttached = false;

            if (portalId is int known && _contexts.TryGetValue(known, out var attached) && IsAlive(attached))
            {
                RefreshProject(attached);
                wasAttached = true;

                return known;
            }

            var processes = TiaPortal.GetProcesses().ToList();
            TiaPortalProcess? process;

            if (portalId is int wanted)
            {
                process = processes.FirstOrDefault(p => p.Id == wanted)
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"No running TIA Portal instance has process id {wanted}. Call 'GetPortals' to list the running instances.",
                        processes.Select(Describe));
            }
            else if (processes.Count > 1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"{processes.Count} TIA Portal instances are running. Pass 'portalId' to choose one; 'GetPortals' lists them.",
                    processes.Select(Describe));
            }
            else if (processes.Count == 1)
            {
                process = processes[0];

                if (_contexts.TryGetValue(process.Id, out var existing) && IsAlive(existing))
                {
                    RefreshProject(existing);
                    wasAttached = true;

                    return process.Id;
                }
            }
            else
            {
                var started = new TiaPortal(TiaPortalMode.WithUserInterface);

                return Register(started.GetCurrentProcess().Id, started);
            }

            return Register(process.Id, process.Attach());
        }

        /// <summary>
        /// Runs one request against the instance it names. The instance is resolved and locked
        /// before the action runs, and the scope ends with the request - nothing carries over to
        /// the next one.
        /// </summary>
        public async ValueTask<T> RunScopedAsync<T>(int? portalId, Func<ValueTask<T>> action, CancellationToken cancellationToken)
        {
            var context = Resolve(portalId);

            if (context == null)
            {
                // nothing attached yet: the tool reports "not connected" as before
                return await action().ConfigureAwait(false);
            }

            if (!await context.Gate.WaitAsync(GateTimeout, cancellationToken).ConfigureAwait(false))
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"TIA Portal instance {context.Id} has been busy with another request for more than " +
                    $"{GateTimeout.TotalSeconds:0} s - possibly a dialog is waiting in TIA Portal. Close it or retry later.");
            }

            try
            {
                // a request ahead in the queue may have disconnected this instance meanwhile
                if (context.IsDisposed)
                {
                    throw new PortalException(PortalErrorCode.InvalidState,
                        $"TIA Portal instance {context.Id} was disconnected. Call 'Connect' again.");
                }

                _scoped.Value = context;

                return await action().ConfigureAwait(false);
            }
            finally
            {
                _scoped.Value = null;
                context.Gate.Release();
            }
        }

        private PortalContext? Resolve(int? portalId)
        {
            if (portalId is int id)
            {
                if (!_contexts.TryGetValue(id, out var named))
                {
                    throw new PortalException(PortalErrorCode.NotFound,
                        $"TIA Portal instance {id} is not attached. Call 'Connect' with portalId {id} first.",
                        _contexts.Keys.Select(k => k.ToString()));
                }

                return EnsureAlive(named);
            }

            var all = _contexts.Values.ToList();

            if (all.Count > 1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"{all.Count} TIA Portal instances are attached. Pass 'portalId' to choose one.",
                    all.Select(c => c.Id.ToString()));
            }

            return all.Count == 1 ? EnsureAlive(all[0]) : null;
        }

        private PortalContext EnsureAlive(PortalContext context)
        {
            if (IsAlive(context))
            {
                return context;
            }

            Forget(context);

            throw new PortalException(PortalErrorCode.InvalidState,
                $"TIA Portal instance {context.Id} is no longer running. Call 'Connect' again.");
        }

        private static bool IsAlive(PortalContext context)
        {
            if (context.IsDisposed)
            {
                return false;
            }

            try
            {
                using (var process = global::System.Diagnostics.Process.GetProcessById(context.Id))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                // no process with this id
                return false;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                // e.g. TIA Portal runs elevated and HasExited is denied: trust the Disposed event
                return true;
            }
        }

        private int Register(int id, TiaPortal tiaPortal)
        {
            var context = new PortalContext(id, tiaPortal);

            RefreshProject(context);

            if (_contexts.TryRemove(id, out var stale))
            {
                stale.Dispose();
            }

            _contexts[id] = context;

            return id;
        }

        private void Forget(PortalContext context)
        {
            if (_contexts.TryGetValue(context.Id, out var registered) && ReferenceEquals(registered, context))
            {
                _contexts.TryRemove(context.Id, out _);
            }

            context.Dispose();
        }

        // Picks up a project or session opened in the TIA Portal UI. It only looks inside the
        // given instance and keeps a project that is still open there, so it never switches a
        // request over to another instance or away from a project opened through 'OpenProject'.
        private static void RefreshProject(PortalContext context)
        {
            var tiaPortal = context.TiaPortal;

            if (context.Session != null && tiaPortal.LocalSessions.Any(s => s.Equals(context.Session)))
            {
                return;
            }

            if (context.Session == null && context.Project != null && tiaPortal.Projects.Any(p => p.Equals(context.Project)))
            {
                return;
            }

            context.Session = tiaPortal.LocalSessions.FirstOrDefault();
            context.Project = context.Session != null
                ? context.Session.Project
                : tiaPortal.Projects.FirstOrDefault();
        }

        private static string Describe(TiaPortalProcess process)
        {
            var path = process.ProjectPath?.ToString();

            return $"{process.Id}: {(string.IsNullOrEmpty(path) ? "(no project)" : path)}";
        }

        #endregion
    }
}
