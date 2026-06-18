using LightingProxy.Domain.Common;
using LightingProxy.Domain.Enums;
using LightingProxy.Domain.Transport;

namespace LightingProxy.Core.Transport;

public static class TransportStreamFactory
{
    public static async Task<Stream> ConnectClientAsync(
        string host,
        int port,
        ClientTransportConfig transport,
        string? authToken,
        CancellationToken cancellationToken = default)
    {
        var socket = await NetworkHelper.ConnectTcpAsync(host, port, cancellationToken).ConfigureAwait(false);
        var networkStream = NetworkHelper.CreateNetworkStream(socket, ownsSocket: true);
        return await WrapClientAsync(networkStream, transport, authToken, host, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Stream> WrapClientAsync(
        Stream networkStream,
        ClientTransportConfig transport,
        string? authToken,
        string? serverHost,
        CancellationToken cancellationToken = default)
    {
        var stream = await TlsStreamHelper.AuthenticateClientAsync(
            networkStream,
            transport.Tls,
            serverHost,
            cancellationToken).ConfigureAwait(false);

        return ApplyEncryption(stream, transport.Encryption, authToken);
    }

    public static async Task<Stream> WrapServerAsync(
        Stream networkStream,
        ServerTransportConfig transport,
        string? authToken,
        CancellationToken cancellationToken = default)
    {
        if (transport.Tls.Force && !transport.Tls.Enabled)
        {
            transport.Tls.Enabled = true;
        }

        var stream = await TlsStreamHelper.AuthenticateServerAsync(
            networkStream,
            transport.Tls,
            cancellationToken).ConfigureAwait(false);

        return ApplyEncryption(stream, transport.Encryption, authToken);
    }

    private static Stream ApplyEncryption(Stream stream, TransportEncryptionConfig encryption, string? authToken)
    {
        if (encryption.Method == TransportEncryptionMethod.None)
        {
            return stream;
        }

        if (string.IsNullOrWhiteSpace(authToken))
        {
            throw new InvalidOperationException("auth.token is required when transport encryption is enabled.");
        }

        return encryption.Method switch
        {
            TransportEncryptionMethod.Aes128Cfb => new Aes128CfbTransformStream(
                stream,
                CipherKeyDerivation.DeriveAes128CfbKey(authToken)),
            TransportEncryptionMethod.ChaCha20Poly1305 or TransportEncryptionMethod.Aes256Gcm => new AeadRecordStream(
                stream,
                CipherKeyDerivation.DeriveAeadKey(authToken),
                encryption.Method),
            _ => throw new InvalidOperationException($"Unsupported transport encryption method '{encryption.Method}'.")
        };
    }
}
