namespace MusselSheet;

using System.Text;

using Microsoft.JSInterop;

internal sealed class TextStream(string text) : IJSStreamReference
{
    private readonly byte[] bytes = Encoding.UTF8.GetBytes(text);

    public bool Disposed { get; private set; }

    public long Length => bytes.Length;

    public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<Stream>(new MemoryStream(bytes));

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
