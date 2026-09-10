using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Atomic, undoable write scopes.
    ///
    /// Callers: the Guarded helper in McpServerWrite.cs, which wraps every project-mutating
    /// tool. Affected API: no signature changes - the wrapper is invisible to callers, it only
    /// changes what TIA Portal does with the edits underneath. Reads and writes no data files.
    ///
    /// Why: before this, a write tool that failed halfway left the project half-changed, and a
    /// batch of edits appeared in the TIA Portal undo stack as an unlabelled pile of steps.
    /// Siemens.Engineering.ExclusiveAccess.Transaction gives both an all-or-nothing commit and
    /// a single named undo entry.
    ///
    /// Deliberately best-effort: if the running TIA Portal refuses exclusive access - another
    /// dialog holds it, or the project does not support transactions - the write still runs,
    /// unwrapped, exactly as it did before. Refusing to write at all would be a regression for
    /// the sake of a safety net.
    /// </summary>
    public partial class Portal
    {
        /// <summary>
        /// Runs <paramref name="body"/> inside a named transaction when TIA Portal allows one.
        /// The transaction commits only if the body returns without throwing, so a failed write
        /// rolls back instead of leaving the project half-edited.
        /// </summary>
        /// <param name="description">
        /// What the operator sees: the exclusive-access banner while it runs, and the undo entry
        /// afterwards. Keep it short and name the tool.
        /// </param>
        public T InTransaction<T>(string description, Func<T> body)
        {
            if (_portal == null || _project is not ITransactionSupport persistence)
            {
                // No portal, or a project that cannot host a transaction: run the write directly
                // rather than failing it.
                return body();
            }

            ExclusiveAccess? access;

            try
            {
                access = _portal.ExclusiveAccess(description);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Exclusive access refused for '{Description}'; writing without a transaction", description);

                return body();
            }

            using (access)
            {
                Transaction? transaction;

                try
                {
                    transaction = access.Transaction(persistence, description);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Transaction refused for '{Description}'; writing without one", description);

                    return body();
                }

                using (transaction)
                {
                    var result = body();

                    // Only reached when the body did not throw. Without this call the using
                    // block rolls the transaction back on dispose.
                    transaction.CommitOnDispose();

                    if (access.IsCancellationRequested)
                    {
                        _logger?.LogInformation("The operator requested cancellation during '{Description}'", description);
                    }

                    return result;
                }
            }
        }

        /// <summary>Void counterpart of <see cref="InTransaction{T}"/>.</summary>
        public void InTransaction(string description, Action body)
        {
            InTransaction(description, () =>
            {
                body();

                return true;
            });
        }
    }
}
