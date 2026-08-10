namespace ActivationWs.Activation;

/// <summary>
/// Serializes work per string key within this process. Concurrent first-requests for the same
/// Installation ID and Extended Product ID are coalesced so only one call reaches Microsoft; the
/// others wait and then find the freshly cached Confirmation ID.
/// </summary>
/// <remarks>
/// This lock is process local. The application is designed for a single, non clustered IIS host, so
/// that is sufficient. The unique key in the store still prevents duplicate rows if two processes
/// briefly overlap during an IIS recycle.
/// </remarks>
public sealed class KeyedAsyncLock {
    // _gate serialises reference-count mutations and dictionary updates so that Retain and Release
    // are atomic: a Release that reaches zero cannot race with a concurrent Retain that observed the
    // same entry still in the dictionary.
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async Task<IDisposable> LockAsync(string key, CancellationToken cancellationToken = default) {
        Entry entry;
        lock (_gate) {
            if (_entries.TryGetValue(key, out Entry? existing)) {
                existing.Retain();
                entry = existing;
            } else {
                entry = new Entry();
                _entries[key] = entry;
            }
        }

        try {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        } catch {
            Release(key, entry);
            throw;
        }

        return new Releaser(this, key, entry);
    }

    private void Release(string key, Entry entry) {
        lock (_gate) {
            if (entry.ReleaseReference()) {
                _entries.Remove(key);
            }
        }
    }

    private sealed class Entry {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        private int _references = 1;

        public void Retain() => _references++;
        public bool ReleaseReference() => --_references == 0;
    }

    private sealed class Releaser : IDisposable {
        private readonly KeyedAsyncLock _owner;
        private readonly string _key;
        private readonly Entry _entry;
        private int _disposed;

        public Releaser(KeyedAsyncLock owner, string key, Entry entry) {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) {
                _entry.Semaphore.Release();
                _owner.Release(_key, _entry);
            }
        }
    }
}
