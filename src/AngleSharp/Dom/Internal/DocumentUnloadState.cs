namespace AngleSharp.Dom
{
    using System;
    using System.Threading;

    internal sealed class DocumentUnloadState
    {
        private Int32 _depth;

        internal Boolean IsUnloading => Volatile.Read(ref _depth) != 0;

        internal IDisposable Enter()
        {
            Interlocked.Increment(ref _depth);
            return new Scope(this);
        }

        private sealed class Scope : IDisposable
        {
            private DocumentUnloadState? _owner;

            internal Scope(DocumentUnloadState owner) => _owner = owner;

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner != null) Interlocked.Decrement(ref owner._depth);
            }
        }
    }
}
