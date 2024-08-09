using Azure;
using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class InMemoryBlobContainer(string name, IDictionary<string, string>? metadata, InMemoryBlobService service)
{

    private readonly object _lock = new();
    private readonly Dictionary<string, BlobEntry> _blobEntries = [];

    private readonly BlobContainerProperties _properties = BlobsModelFactory.BlobContainerProperties(
            lastModified: service.Account.Provider.TimeProvider.GetUtcNow(),
            eTag: new ETag(Guid.NewGuid().ToString()),
            metadata: metadata);

    public string Name { get; } = name;

    public string AccountName => Service.Account.Name;

    public BlobContainerProperties GetProperties()
    {
        lock (_lock)
        {
            return _properties;
        }
    }

    public InMemoryBlobService Service { get; } = service;

    public override string? ToString() => $"{Service} / {Name}";

    public IReadOnlyList<BlobItem> GetBlobs(string? prefix)
    {
        lock (_lock)
        {
            return _blobEntries
                .Values
                .Where(entry => entry.Blob.Exists)
                .Where(entry => prefix is null ? true : entry.Blob.Name.StartsWith(prefix))
                .Select(entry => BlobsModelFactory.BlobItem(entry.Blob.Name))
                .ToList();
        }
    }

    public AcquiredBlob AcquireBlob(string blobName, CancellationToken cancellationToken)
    {
        var entry = GetBlobEntry(blobName);

        entry.Semaphore.Wait(cancellationToken);

        return new(entry.Blob, entry.Semaphore);
    }

    private BlobEntry GetBlobEntry(string blobName)
    {
        BlobEntry? entry;

        lock (_lock)
        {
            if (!_blobEntries.TryGetValue(blobName, out entry))
            {
                var blob = new InMemoryBlockBlob(blobName, this);
                entry = new(blob, new(1, 1));
                _blobEntries.Add(blobName, entry);
            }
        }

        return entry;
    }

    public sealed class AcquiredBlob(InMemoryBlockBlob blob, SemaphoreSlim semaphore) : IDisposable
    {
        public InMemoryBlockBlob Value { get; } = blob ?? throw new ArgumentNullException(nameof(blob));

        public void Dispose() => semaphore.Release();
    }

    private record BlobEntry(InMemoryBlockBlob Blob, SemaphoreSlim Semaphore);

}
