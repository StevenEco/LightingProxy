using System.Buffers.Binary;
using System.Security.Cryptography;
using LightingProxy.Domain.Enums;

namespace LightingProxy.Core.Transport;

/// <summary>
/// AEAD 帧加密流。每帧格式：[4 字节长度][nonce][密文+tag]。
/// </summary>
internal sealed class AeadRecordStream : Stream
{
    private const int LengthPrefixSize = 4;
    private const int MaxPayloadSize = 64 * 1024;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly Stream _inner;
    private readonly byte[] _key;
    private readonly TransportEncryptionMethod _method;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private byte[]? _readBuffer;
    private int _readOffset;
    private int _readCount;

    public AeadRecordStream(Stream inner, byte[] key, TransportEncryptionMethod method)
    {
        if (method is not (TransportEncryptionMethod.ChaCha20Poly1305 or TransportEncryptionMethod.Aes256Gcm))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported AEAD method.");
        }

        _inner = inner;
        _key = key;
        _method = method;
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
            if (_readCount > 0)
            {
                var toCopy = Math.Min(count, _readCount);
                Buffer.BlockCopy(_readBuffer!, _readOffset, buffer, offset, toCopy);
                _readOffset += toCopy;
                _readCount -= toCopy;
                return toCopy;
            }

            var plaintext = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            if (plaintext.Length == 0)
            {
                return 0;
            }

            var copied = Math.Min(count, plaintext.Length);
            Buffer.BlockCopy(plaintext, 0, buffer, offset, copied);
            if (copied < plaintext.Length)
            {
                _readBuffer = plaintext;
                _readOffset = copied;
                _readCount = plaintext.Length - copied;
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
        if (count > MaxPayloadSize)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "AEAD frame payload exceeds maximum size.");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ciphertext = new byte[count + TagSize];
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            Encrypt(nonce, buffer.AsSpan(offset, count), ciphertext);

            var frameLength = NonceSize + ciphertext.Length;
            var header = new byte[LengthPrefixSize + frameLength];
            BinaryPrimitives.WriteInt32LittleEndian(header, frameLength);
            nonce.CopyTo(header.AsSpan(LengthPrefixSize));
            ciphertext.CopyTo(header.AsSpan(LengthPrefixSize + NonceSize));

            await _inner.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        _readLock.Dispose();
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _writeLock.Dispose();
            _readLock.Dispose();
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    private void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext)
    {
        switch (_method)
        {
            case TransportEncryptionMethod.ChaCha20Poly1305:
                using (var chacha = new ChaCha20Poly1305(_key))
                {
                    chacha.Encrypt(nonce, plaintext, ciphertext[..plaintext.Length], ciphertext[plaintext.Length..]);
                }

                break;
            case TransportEncryptionMethod.Aes256Gcm:
                using (var gcm = new AesGcm(_key, TagSize))
                {
                    gcm.Encrypt(nonce, plaintext, ciphertext[..plaintext.Length], ciphertext[plaintext.Length..]);
                }

                break;
            default:
                throw new InvalidOperationException($"Unsupported AEAD method '{_method}'.");
        }
    }

    private void Decrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        switch (_method)
        {
            case TransportEncryptionMethod.ChaCha20Poly1305:
                using (var chacha = new ChaCha20Poly1305(_key))
                {
                    chacha.Decrypt(nonce, ciphertext[..^TagSize], ciphertext[^TagSize..], plaintext);
                }

                break;
            case TransportEncryptionMethod.Aes256Gcm:
                using (var gcm = new AesGcm(_key, TagSize))
                {
                    gcm.Decrypt(nonce, ciphertext[..^TagSize], ciphertext[^TagSize..], plaintext);
                }

                break;
            default:
                throw new InvalidOperationException($"Unsupported AEAD method '{_method}'.");
        }
    }

    private async Task<byte[]> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[LengthPrefixSize];
        var read = await ReadExactAsync(_inner, lengthBytes, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return [];
        }

        var frameLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (frameLength <= 0 || frameLength > MaxPayloadSize + NonceSize + TagSize)
        {
            throw new CryptographicException("Invalid AEAD frame length.");
        }

        var frame = new byte[frameLength];
        await ReadExactAsync(_inner, frame, cancellationToken).ConfigureAwait(false);

        if (frame.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("AEAD frame is too short.");
        }

        var nonce = frame.AsSpan(0, NonceSize);
        var ciphertext = frame.AsSpan(NonceSize);
        var plaintext = new byte[ciphertext.Length - TagSize];
        Decrypt(nonce, ciphertext, plaintext);
        return plaintext;
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return offset == 0 ? 0 : throw new EndOfStreamException("Unexpected end of stream while reading AEAD frame.");
            }

            offset += read;
        }

        return offset;
    }
}
