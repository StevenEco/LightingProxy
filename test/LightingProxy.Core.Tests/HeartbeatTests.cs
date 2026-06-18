namespace LightingProxy.Core.Tests;

public class HeartbeatTests
{
    [Fact]
    public async Task Client_SendsPeriodicPing_AndServerReceivesHeartbeat()
    {
        var controlPort = Support.TestPorts.Allocate();
        var clientConfig = Support.ProxyTestHarness.CreateClientConfig(controlPort);
        clientConfig.Transport.HeartbeatIntervalSeconds = 1;
        clientConfig.Transport.HeartbeatTimeoutSeconds = 10;

        await using var harness = new Support.ProxyTestHarness(
            Support.ProxyTestHarness.CreateServerConfig(controlPort),
            clientConfig);

        await harness.WaitReadyAsync();

        var session = harness.Server.GetFirstSession();
        Assert.NotNull(session);

        var before = session!.LastHeartbeatUtc;
        await Task.Delay(2500);

        Assert.True(session.LastHeartbeatUtc > before);
        Assert.False(harness.ClientTask.IsFaulted);
    }
}
