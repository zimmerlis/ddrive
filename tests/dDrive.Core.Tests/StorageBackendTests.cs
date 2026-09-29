using System.Security;
using dDrive.Core.Connections;
using dDrive.Core.Storage;
using dDrive.Storage;
using dDrive.Storage.WebDav;
using Xunit;

namespace dDrive.Core.Tests;

public sealed class StorageBackendTests
{
    [Fact]
    public void WebDavEndpoint_AcceptsUrlHostAndKeepsConfiguredPort()
    {
        var endpoint = WebDavEndpoint.CreateRootUri(new ConnectionProfile
        {
            Host = "https://rediuserver.synology.me",
            Port = 5001,
            RemotePath = "/"
        });

        Assert.Equal("https", endpoint.Scheme);
        Assert.Equal("rediuserver.synology.me", endpoint.Host);
        Assert.Equal(5001, endpoint.Port);
        Assert.Equal("/", endpoint.AbsolutePath);
    }

    [Fact]
    public void WebDavEndpoint_AppendsConnectionPathToUrlPrefix()
    {
        var endpoint = WebDavEndpoint.CreateRootUri(new ConnectionProfile
        {
            Host = "https://storage.example.test/dav",
            Port = 443,
            RemotePath = "/team files"
        });

        Assert.Equal("/dav/team%20files/", endpoint.AbsolutePath);
    }

    [Theory]
    [InlineData("ftp://storage.example.test")]
    [InlineData("https://user:password@storage.example.test")]
    public void WebDavEndpoint_RejectsUnsupportedOrCredentialBearingUrl(string host)
    {
        Assert.Throws<ArgumentException>(() => WebDavEndpoint.CreateRootUri(new ConnectionProfile { Host = host }));
    }

    [Fact]
    public async Task CachingBackend_CachesListingsAndInvalidatesAfterMutation()
    {
        var inner = new FakeStorageBackend();
        await using var backend = new CachingStorageBackend(inner);

        await backend.ListDirectoryAsync("/");
        await backend.ListDirectoryAsync("/");
        Assert.Equal(1, inner.ListCallCount);

        await backend.CreateDirectoryAsync("/new");
        await backend.ListDirectoryAsync("/");
        Assert.Equal(2, inner.ListCallCount);
    }

    [Fact]
    public async Task ReconnectingBackend_ReconnectsAndRetriesOnceForTransientReadFailure()
    {
        var inner = new FakeStorageBackend { FailFirstList = true };
        await using var backend = new ReconnectingStorageBackend(inner);
        await backend.ConnectAsync(new ConnectionProfile { Host = "files.example.test" }, null);

        var entries = await backend.ListDirectoryAsync("/");

        Assert.Empty(entries);
        Assert.Equal(2, inner.ConnectCallCount);
        Assert.Equal(2, inner.ListCallCount);
    }

    private sealed class FakeStorageBackend : IStorageBackend
    {
        public int ConnectCallCount { get; private set; }
        public int ListCallCount { get; private set; }
        public bool FailFirstList { get; init; }

        public Task ConnectAsync(ConnectionProfile connection, SecureString? secret, CancellationToken cancellationToken = default)
        {
            ConnectCallCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StorageEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            ListCallCount++;
            if (FailFirstList && ListCallCount == 1)
            {
                throw new IOException("Connection was interrupted.", new System.Net.Sockets.SocketException());
            }

            return Task.FromResult<IReadOnlyList<StorageEntry>>([]);
        }

        public Task<StorageEntry?> GetEntryAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult<StorageEntry?>(null);

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) => NotSupported<Stream>();

        public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken = default) => NotSupported<Stream>();

        public Task<Stream> OpenAsync(string path, FileMode mode, FileAccess access, CancellationToken cancellationToken = default) => NotSupported<Stream>();

        public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(string path, bool isDirectory, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MoveAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static Task<T> NotSupported<T>() => Task.FromException<T>(new NotSupportedException());
    }
}