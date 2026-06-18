namespace LightingProxy.Domain.Enums;

/// <summary>
/// 应用层传输加密方式（在 TLS 之下或单独使用）。
/// </summary>
public enum TransportEncryptionMethod
{
    /// <summary>不启用应用层加密。</summary>
    None,

    /// <summary>frp 兼容的 AES-128-CFB，密钥由 auth.token 经 PBKDF2 派生。</summary>
    Aes128Cfb,

    /// <summary>AEAD 帧加密：ChaCha20-Poly1305（IETF，12 字节 nonce）。</summary>
    ChaCha20Poly1305,

    /// <summary>AEAD 帧加密：AES-256-GCM（frp wire v2 同类算法）。</summary>
    Aes256Gcm
}
