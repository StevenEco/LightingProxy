using System.Security.Cryptography;

namespace LightingProxy.Core.Transport;

/// <summary>
/// frp 兼容的 AES-128-CFB 全双工流包装。发送方首次写入时发送 IV，接收方首次读取时接收 IV。
/// </summary>
internal sealed class Aes128CfbTransformStream : Stream
{
    private const int IvSize = 16;

    private readonly Stream _inner;
    private readonly byte[] _key;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _readLock = new(1, 1);

    private AesCfb8Cipher? _encryptCipher;
    private AesCfb8Cipher? _decryptCipher;
    private bool _writeInitialized;
    private bool _readInitialized;
    private byte[]? _decryptedBuffer;
    private int _decryptedOffset;
    private int _decryptedCount;

    public Aes128CfbTransformStream(Stream inner, byte[] key)
    {
        _inner = inner;
        _key = key;
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => _inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

    public override void Write(byte[] buffer, int offset, int count)
        => WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureReadInitializedAsync(cancellationToken).ConfigureAwait(false);

            if (_decryptedCount > 0)
            {
                var toCopy = Math.Min(count, _decryptedCount);
                Buffer.BlockCopy(_decryptedBuffer!, _decryptedOffset, buffer, offset, toCopy);
                _decryptedOffset += toCopy;
                _decryptedCount -= toCopy;
                return toCopy;
            }

            var encrypted = new byte[4096];
            var read = await _inner.ReadAsync(encrypted, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return 0;
            }

            var decrypted = new byte[read];
            _decryptCipher!.Transform(encrypted.AsSpan(0, read), decrypted);

            var copied = Math.Min(count, decrypted.Length);
            Buffer.BlockCopy(decrypted, 0, buffer, offset, copied);
            if (copied < decrypted.Length)
            {
                _decryptedBuffer = decrypted;
                _decryptedOffset = copied;
                _decryptedCount = decrypted.Length - copied;
            }

            return copied;
        }
        finally
        {
            _readLock.Release();
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureWriteInitializedAsync(cancellationToken).ConfigureAwait(false);

            var encrypted = new byte[count];
            _encryptCipher!.Transform(buffer.AsSpan(offset, count), encrypted);
            await _inner.WriteAsync(encrypted, cancellationToken).ConfigureAwait(false);
            await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _encryptCipher?.Dispose();
        _decryptCipher?.Dispose();
        _writeLock.Dispose();
        _readLock.Dispose();
        await _inner.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _encryptCipher?.Dispose();
            _decryptCipher?.Dispose();
            _writeLock.Dispose();
            _readLock.Dispose();
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    private async Task EnsureWriteInitializedAsync(CancellationToken cancellationToken)
    {
        if (_writeInitialized)
        {
            return;
        }

        var iv = RandomNumberGenerator.GetBytes(IvSize);
        await _inner.WriteAsync(iv, cancellationToken).ConfigureAwait(false);
        await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        _encryptCipher = new AesCfb8Cipher(_key, iv, forEncryption: true);
        _writeInitialized = true;
    }

    private async Task EnsureReadInitializedAsync(CancellationToken cancellationToken)
    {
        if (_readInitialized)
        {
            return;
        }

        var iv = new byte[IvSize];
        await ReadExactAsync(_inner, iv, cancellationToken).ConfigureAwait(false);
        _decryptCipher = new AesCfb8Cipher(_key, iv, forEncryption: false);
        _readInitialized = true;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading IV.");
            }

            offset += read;
        }
    }
}
