using LightingProxy.Core.Protocol;

namespace LightingProxy.Core.Tests;

public class ProtocolTests
{
    [Fact]
    public async Task MessageSerializer_RoundTripsRequestId()
    {
        var requestId = Guid.NewGuid();
        var message = MessageSerializer.CreateReqWorkConn(requestId, "tcp-echo");

        using var stream = new MemoryStream();
        await MessageSerializer.WriteAsync(stream, message);
        stream.Position = 0;

        var read = await MessageSerializer.ReadAsync(stream);
        Assert.NotNull(read);
        Assert.Equal(MessageType.ReqWorkConn, read!.Type);
        Assert.Equal(requestId, read.RequestId);
        Assert.Equal("tcp-echo", read.ProxyName);
    }

    [Fact]
    public async Task MessageSerializer_RoundTripsWorkConnReady()
    {
        var requestId = Guid.NewGuid();
        var message = MessageSerializer.CreateWorkConnReady(requestId);

        using var stream = new MemoryStream();
        await MessageSerializer.WriteAsync(stream, message);
        stream.Position = 0;

        var read = await MessageSerializer.ReadAsync(stream);
        Assert.NotNull(read);
        Assert.Equal(MessageType.WorkConnReady, read!.Type);
        Assert.Equal(requestId, read.RequestId);
    }

    [Fact]
    public async Task MessageSerializer_RoundTripsStartWorkConn()
    {
        var message = MessageSerializer.CreateStartWorkConn("tcp-echo");

        using var stream = new MemoryStream();
        await MessageSerializer.WriteAsync(stream, message);
        stream.Position = 0;

        var read = await MessageSerializer.ReadAsync(stream);
        Assert.NotNull(read);
        Assert.Equal(MessageType.StartWorkConn, read!.Type);
        Assert.Equal("tcp-echo", read.ProxyName);
    }

    [Fact]
    public async Task WorkConnectionBroker_CompletesMatchingRequest()
    {
        var broker = new LightingProxy.Core.Server.WorkConnectionBroker();
        var requestId = Guid.NewGuid();
        var payload = new MemoryStream([1, 2, 3]);

        var waitTask = broker.WaitForWorkConnectionAsync(requestId, CancellationToken.None);
        broker.Complete(requestId, payload);

        var stream = await waitTask;
        Assert.Same(payload, stream);
    }
}
