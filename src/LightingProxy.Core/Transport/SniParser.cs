namespace LightingProxy.Core.Transport;

public static class SniParser
{
    public static bool TryGetServerName(ReadOnlySpan<byte> clientHello, out string? serverName)
    {
        serverName = null;

        if (clientHello.Length < 5 || clientHello[0] != 0x16)
        {
            return false;
        }

        var recordLength = (clientHello[3] << 8) | clientHello[4];
        if (clientHello.Length < 5 + recordLength)
        {
            return false;
        }

        var handshake = clientHello.Slice(5, recordLength);
        if (handshake.Length < 4 || handshake[0] != 0x01)
        {
            return false;
        }

        var handshakeLength = (handshake[1] << 16) | (handshake[2] << 8) | handshake[3];
        if (handshake.Length < 4 + handshakeLength)
        {
            return false;
        }

        var body = handshake.Slice(4, handshakeLength);
        if (body.Length < 34)
        {
            return false;
        }

        var offset = 34;
        if (offset >= body.Length)
        {
            return false;
        }

        var sessionIdLength = body[offset];
        offset += 1 + sessionIdLength;
        if (offset + 2 > body.Length)
        {
            return false;
        }

        var cipherLength = (body[offset] << 8) | body[offset + 1];
        offset += 2 + cipherLength;
        if (offset >= body.Length)
        {
            return false;
        }

        var compressionLength = body[offset];
        offset += 1 + compressionLength;
        if (offset + 2 > body.Length)
        {
            return false;
        }

        var extensionsLength = (body[offset] << 8) | body[offset + 1];
        offset += 2;
        var extensionsEnd = offset + extensionsLength;

        while (offset + 4 <= extensionsEnd && offset + 4 <= body.Length)
        {
            var extensionType = (body[offset] << 8) | body[offset + 1];
            var extensionLength = (body[offset + 2] << 8) | body[offset + 3];
            offset += 4;

            if (offset + extensionLength > body.Length)
            {
                break;
            }

            if (extensionType == 0x00)
            {
                serverName = ParseServerNameExtension(body.Slice(offset, extensionLength));
                return serverName is not null;
            }

            offset += extensionLength;
        }

        return false;
    }

    private static string? ParseServerNameExtension(ReadOnlySpan<byte> extension)
    {
        if (extension.Length < 5)
        {
            return null;
        }

        var listLength = (extension[0] << 8) | extension[1];
        if (extension.Length < 2 + listLength)
        {
            return null;
        }

        var offset = 2;
        while (offset + 3 <= 2 + listLength)
        {
            var nameType = extension[offset];
            var nameLength = (extension[offset + 1] << 8) | extension[offset + 2];
            offset += 3;

            if (nameType == 0 && offset + nameLength <= extension.Length)
            {
                return System.Text.Encoding.ASCII.GetString(extension.Slice(offset, nameLength));
            }

            offset += nameLength;
        }

        return null;
    }
}
