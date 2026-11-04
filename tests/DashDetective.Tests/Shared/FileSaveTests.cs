using DashDetective.Shared;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>
/// Pins <see cref="FileSave.TryWriteAsync"/>: content is rendered before the destination is opened, so a
/// renderer failure never truncates the chosen file and returns false; a failed I/O write returns false.
/// </summary>
public class FileSaveTests {
    [Fact]
    public async Task TryWriteAsync_RendererThrows_NeverOpensDestination() {
        var opened = false;

        var ok = await FileSave.TryWriteAsync(
            () => { opened = true; return Task.FromResult<Stream>(new MemoryStream()); },
            () => throw new InvalidOperationException("render"));

        Assert.False(ok);
        Assert.False(opened);
    }

    [Fact]
    public async Task TryWriteAsync_Success_WritesRenderedText() {
        var stream = new NonClosingStream();

        var ok = await FileSave.TryWriteAsync(() => Task.FromResult<Stream>(stream), () => "hello");

        Assert.True(ok);
        Assert.Equal("hello", System.Text.Encoding.UTF8.GetString(stream.Inner.ToArray()));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task TryWriteAsync_OpenFailsWithIo_ReturnsFalse(Type error) {
        var ok = await FileSave.TryWriteAsync(
            () => throw (Exception)Activator.CreateInstance(error)!, () => "x");

        Assert.False(ok);
    }

    [Fact]
    public async Task TryWriteAsync_OpenFailsWithOtherError_Propagates() {
        await Assert.ThrowsAsync<InvalidOperationException>(() => FileSave.TryWriteAsync(
            () => throw new InvalidOperationException(), () => "x"));
    }

    [Fact]
    public async Task GuardAsync_OpenThrowsNonIoError_ReturnsNull() {
        var result = await FileSave.GuardAsync(async () => {
            await FileSave.TryWriteAsync(() => throw new InvalidOperationException(), () => "x");
            return "written";
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task GuardAsync_Success_PassesTheResultThrough() =>
        Assert.Equal("p", await FileSave.GuardAsync(() => Task.FromResult<string?>("p")));

    /// <summary>A MemoryStream the writer can dispose without losing the bytes.</summary>
    private sealed class NonClosingStream : Stream {
        public MemoryStream Inner { get; } = new();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => Inner.Length;
        public override long Position { get => Inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Inner.Write(buffer, offset, count);
        protected override void Dispose(bool disposing) { }
    }
}
