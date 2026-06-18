using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Common;

/// <summary>
/// 应用层传输加密配置，参考 frp 的 useEncryption，并扩展现代 AEAD 算法。
/// </summary>
public class TransportEncryptionConfig
{
    public TransportEncryptionMethod Method { get; set; } = TransportEncryptionMethod.None;
}
