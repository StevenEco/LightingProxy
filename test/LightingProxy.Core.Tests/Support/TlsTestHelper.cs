using System.Text;
using LightingProxy.Core.Transport;

namespace LightingProxy.Core.Tests.Support;

internal static class TlsTestHelper
{
    public static byte[] BuildClientHello(string host)
    {
        var hostBytes = Encoding.ASCII.GetBytes(host);
        var entryLength = 1 + 2 + hostBytes.Length;
        var serverNameList = new byte[2 + entryLength];
        serverNameList[0] = (byte)((entryLength >> 8) & 0xFF);
        serverNameList[1] = (byte)(entryLength & 0xFF);
        serverNameList[2] = 0x00;
        serverNameList[3] = (byte)((hostBytes.Length >> 8) & 0xFF);
        serverNameList[4] = (byte)(hostBytes.Length & 0xFF);
        hostBytes.CopyTo(serverNameList, 5);

        var extensions = new byte[4 + serverNameList.Length];
        extensions[0] = 0x00;
        extensions[1] = 0x00;
        extensions[2] = (byte)((serverNameList.Length >> 8) & 0xFF);
        extensions[3] = (byte)(serverNameList.Length & 0xFF);
        serverNameList.CopyTo(extensions, 4);

        var body = new byte[40 + extensions.Length];
        body[0] = 0x03;
        body[1] = 0x03;
        body[34] = 0x00;
        body[35] = 0x00;
        body[36] = 0x00;
        body[37] = 0x00;
        body[38] = (byte)((extensions.Length >> 8) & 0xFF);
        body[39] = (byte)(extensions.Length & 0xFF);
        extensions.CopyTo(body, 40);

        var handshake = new byte[4 + body.Length];
        handshake[0] = 0x01;
        handshake[1] = (byte)((body.Length >> 16) & 0xFF);
        handshake[2] = (byte)((body.Length >> 8) & 0xFF);
        handshake[3] = (byte)(body.Length & 0xFF);
        body.CopyTo(handshake, 4);

        var record = new byte[5 + handshake.Length];
        record[0] = 0x16;
        record[1] = 0x03;
        record[2] = 0x01;
        record[3] = (byte)((handshake.Length >> 8) & 0xFF);
        record[4] = (byte)(handshake.Length & 0xFF);
        handshake.CopyTo(record, 5);
        return record;
    }

    public static void AssertParsesServerName(string host)
    {
        var hello = BuildClientHello(host);
        Assert.True(SniParser.TryGetServerName(hello, out var serverName));
        Assert.Equal(host, serverName);
    }
}
