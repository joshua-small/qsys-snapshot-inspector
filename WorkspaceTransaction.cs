namespace Designer.Modules.SnapshotInspector;

internal interface IWorkspaceTransaction
{
    Guid Start();
    void Finish(Guid operation);
    void Cancel();
}

internal sealed class WorkspaceCommitUncertainException(string message, Exception inner) : InvalidOperationException(message, inner);

internal static class WorkspaceTransaction
{
    internal static void Apply(IWorkspaceTransaction host, Func<bool> current, Action mutate, Action verify,
        CancellationToken cancellation = default, Action? verifyRollback = null)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!current()) throw new InvalidOperationException("The source or destination changed. Refresh the preview.");
        Guid operation = host.Start();
        if (operation == Guid.Empty) throw new InvalidOperationException("Another Designer operation is active. Nothing changed.");
        bool finishing = false;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (!current()) throw new InvalidOperationException("The source or destination changed. Refresh the preview.");
            mutate();
            cancellation.ThrowIfCancellationRequested();
            verify();
            finishing = true;
            host.Finish(operation);
            verify();
        }
        catch (Exception failure)
        {
            if (finishing)
                throw new WorkspaceCommitUncertainException("Designer could not confirm the completed operation. Its commit state is uncertain; inspect the document before continuing.", failure);
            try { host.Cancel(); host.Finish(operation); verifyRollback?.Invoke(); }
            catch (Exception rollback)
            {
                throw new AggregateException("Designer could not cancel the workspace operation. Inspect the document before continuing.", failure, rollback);
            }
            throw;
        }
    }
}
