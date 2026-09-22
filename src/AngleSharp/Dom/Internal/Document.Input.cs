namespace AngleSharp.Dom;

using AngleSharp.Dom.Events;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public abstract partial class Document
{
    private CancellationTokenSource? _inputLifetime;
    private Int32 _inputGeneration;

    internal CancellationToken InputLifetime => (_inputLifetime ??= new CancellationTokenSource()).Token;

    private void RetireInputLifetime()
    {
        // Change ownership before cancellation invokes any old continuations.
        Interlocked.Increment(ref _inputGeneration);
        _inputLifetime?.Cancel();
        _inputLifetime?.Dispose();
        _inputLifetime = null;
    }

    /// <summary>
    /// Finishes only the input stream that requested close. A load listener or
    /// pending download may reopen the same document while this work is queued.
    /// </summary>
    internal async Task FinishLoadingAsync()
    {
        var generation = _inputGeneration;
        Boolean IsCurrent() => generation == _inputGeneration;

        await this.QueueTaskAsync(_ =>
        {
            if (IsCurrent()) ReadyState = DocumentReadyState.Interactive;
        }).ConfigureAwait(false);

        while (IsCurrent() && _loadingScripts.Count > 0)
        {
            await this.WaitForReadyAsync().ConfigureAwait(false);
            if (!IsCurrent()) return;
            await _loadingScripts.Dequeue().RunAsync(CancellationToken.None).ConfigureAwait(false);
        }

        if (!IsCurrent()) return;
        await this.QueueTaskAsync(_ =>
        {
            if (IsCurrent()) this.FireSimpleEvent(EventNames.DomContentLoaded, bubble: true);
        }).ConfigureAwait(false);

        while (IsCurrent())
        {
            var tasks = await this.QueueTaskAsync(_ =>
            {
                if (!IsCurrent()) return Array.Empty<Task>();
                var pending = GetAttachedReferences<Task>().Where(task => !task.IsCompleted).ToArray();
                if (pending.Length == 0)
                {
                    ReadyState = DocumentReadyState.Complete;
                    if (!IsCurrent()) return Array.Empty<Task>();
                    _view.FireSimpleEvent(EventNames.Load);
                    if (IsCurrent() && IsInBrowsingContext && !_shown)
                    {
                        _shown = true;
                        this.Fire<PageTransitionEvent>(ev => ev.Init(EventNames.PageShow, false, false, false), _view);
                    }
                }
                return pending;
            }).ConfigureAwait(false);
            if (tasks.Length == 0) break;
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!IsCurrent())
            {
                return;
            }
        }

        if (!IsCurrent()) return;
        this.QueueTask(EmptyAppCache);
        if (IsToBePrinted) await PrintAsync().ConfigureAwait(false);
    }
}
