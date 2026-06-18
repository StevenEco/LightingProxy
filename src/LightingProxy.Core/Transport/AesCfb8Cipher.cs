using System.Security.Cryptography;

namespace LightingProxy.Core.Transport;

internal sealed class AesCfb8Cipher : IDisposable
{
    private readonly Aes _aes;
    private readonly ICryptoTransform _encryptor;
    private readonly byte[] _shiftRegister;
    private readonly byte[] _keystream = new byte[16];
    private readonly bool _forEncryption;
    private int _keystreamPos = 16;

    public AesCfb8Cipher(byte[] key, byte[] iv, bool forEncryption)
    {
        _forEncryption = forEncryption;
        _aes = Aes.Create();
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
        _aes.Key = key;
        _shiftRegister = (byte[])iv.Clone();
        _encryptor = _aes.CreateEncryptor();
    }

    public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        for (var i = 0; i < input.Length; i++)
        {
            if (_keystreamPos >= _keystream.Length)
            {
                _encryptor.TransformBlock(_shiftRegister, 0, _shiftRegister.Length, _keystream, 0);
                _keystreamPos = 0;
            }

            var transformed = (byte)(input[i] ^ _keystream[_keystreamPos]);
            output[i] = transformed;
            ShiftRegister(_forEncryption ? transformed : input[i]);
            _keystreamPos++;
        }
    }

    public void Dispose()
    {
        _encryptor.Dispose();
        _aes.Dispose();
    }

    private void ShiftRegister(byte value)
    {
        Buffer.BlockCopy(_shiftRegister, 1, _shiftRegister, 0, _shiftRegister.Length - 1);
        _shiftRegister[^1] = value;
    }
}
