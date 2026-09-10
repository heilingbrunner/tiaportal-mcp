using Microsoft.Extensions.Logging;
using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The single exception-decoration point for Openness operations, as required by
    /// docs/error-model.md. Wrap every Portal operation body in <see cref="Run{T}"/> instead of
    /// repeating the try/catch/decorate/log/rethrow block per method.
    ///
    /// Callers: the Portal partial classes in this folder. Affected API: none existing - this
    /// adds a new internal helper and changes no public signature. Reads/writes no data files;
    /// its only output is via ILogger.
    /// </summary>
    internal static class Operation
    {
        /// <summary>
        /// Openness objects are not thread-safe and the MCP SDK may dispatch tool calls
        /// concurrently, so all Openness traffic is serialized here.
        /// Monitor - not SemaphoreSlim - because Portal methods call each other (ExportBlock
        /// calls GetBlock, ExportBlocks calls GetBlocks), and Monitor is reentrant on the
        /// same thread while SemaphoreSlim would self-deadlock.
        /// </summary>
        private static readonly object Gate = new object();

        /// <summary>
        /// Marker written into PortalException.Data so a nested Run does not log the same
        /// failure once per stack level.
        /// </summary>
        private const string LoggedKey = "__logged";

        internal static T Run<T>(
            ILogger? logger,
            string operation,
            PortalErrorCode failCode,
            Func<T> body,
            params (string Key, object? Value)[] context)
        {
            lock (Gate)
            {
                try
                {
                    return body();
                }
                catch (Exception ex)
                {
                    throw Decorate(logger, operation, failCode, ex, context);
                }
            }
        }

        internal static void Run(
            ILogger? logger,
            string operation,
            PortalErrorCode failCode,
            Action body,
            params (string Key, object? Value)[] context)
        {
            lock (Gate)
            {
                try
                {
                    body();
                }
                catch (Exception ex)
                {
                    throw Decorate(logger, operation, failCode, ex, context);
                }
            }
        }

        /// <summary>
        /// Bridge for the legacy bool-returning Portal methods that swallow their exceptions.
        /// Prefer <see cref="Run{T}"/> for new code; this exists so those methods can adopt the
        /// shared lock and logging without changing their signature.
        /// </summary>
        internal static bool TryRun(ILogger? logger, string operation, Action body)
        {
            lock (Gate)
            {
                try
                {
                    body();
                    return true;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "{Operation} failed", operation);
                    return false;
                }
            }
        }

        private static PortalException Decorate(
            ILogger? logger,
            string operation,
            PortalErrorCode failCode,
            Exception ex,
            (string Key, object? Value)[] context)
        {
            var pex = ex as PortalException
                      ?? new PortalException(failCode, $"{operation} failed", null, ex);

            // Inner frames win: an inner Run already recorded the most specific context.
            foreach (var (key, value) in context)
            {
                if (!pex.Data.Contains(key))
                {
                    pex.Data[key] = value;
                }
            }

            if (!pex.Data.Contains("operation"))
            {
                pex.Data["operation"] = operation;
            }

            if (!pex.Data.Contains(LoggedKey))
            {
                pex.Data[LoggedKey] = true;
                logger?.LogError(pex, "{Operation} failed", operation);
            }

            return pex;
        }
    }
}
