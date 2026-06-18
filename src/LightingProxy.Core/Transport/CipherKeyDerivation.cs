using System.Security.Cryptography;
using System.Text;

namespace LightingProxy.Core.Transport;

internal static class CipherKeyDerivation
{
    private static readonly byte[] FrpSalt = "frp"u8.ToArray();
    private static readonly byte[] AeadInfo = "lighting-proxy-aead-v1"u8.ToArray();

    public static byte[] DeriveAes128CfbKey(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(token),
            FrpSalt,
            64,
            HashAlgorithmName.SHA1,
            16);
    }

    public static byte[] DeriveAeadKey(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(token),
            outputLength: 32,
            salt: FrpSalt,
            info: AeadInfo);
    }
}
