using System.Net;
using NuclearEvaluation.Server.Services.Assistant;

namespace NuclearEvaluation.Server.Tests;

public class AssistantRequestLimiterTests
{
    [Fact]
    public void ClientLimitUsesARollingMinuteAndSharesMappedAddresses()
    {
        ManualClock clock = new();
        AssistantRequestLimiter limiter = new(clock);
        IPAddress client = ClientAddress(1);

        Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(client));
        clock.Advance(TimeSpan.FromSeconds(10));
        for (int request = 0; request < 4; request++)
            Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(client.MapToIPv6()));

        Assert.Equal(TimeSpan.FromSeconds(50), limiter.TryAcquire(client));
        clock.Advance(TimeSpan.FromMilliseconds(49_999));
        Assert.Equal(TimeSpan.FromMilliseconds(1), limiter.TryAcquire(client));

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(client));
        Assert.Equal(TimeSpan.FromSeconds(10), limiter.TryAcquire(client));
    }

    [Fact]
    public void SharedLimitReturnsTheLaterRetryDelayAndDoesNotChargeDeniedClients()
    {
        ManualClock clock = new();
        AssistantRequestLimiter limiter = new(clock);
        IPAddress limitedClient = ClientAddress(2);
        IPAddress nextClient = ClientAddress(100);

        Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(ClientAddress(1)));
        clock.Advance(TimeSpan.FromSeconds(30));
        for (int request = 0; request < 5; request++)
            Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(limitedClient));
        for (int address = 3; address <= 56; address++)
            Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(ClientAddress(address)));

        Assert.Equal(TimeSpan.FromSeconds(60), limiter.TryAcquire(limitedClient));
        for (int request = 0; request < 6; request++)
            Assert.Equal(TimeSpan.FromSeconds(30), limiter.TryAcquire(nextClient));

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(nextClient));
        Assert.Equal(TimeSpan.FromSeconds(30), limiter.TryAcquire(nextClient));

        clock.Advance(TimeSpan.FromSeconds(30));
        for (int request = 0; request < 4; request++)
            Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(nextClient));
        Assert.Equal(TimeSpan.FromSeconds(30), limiter.TryAcquire(nextClient));
    }

    [Fact]
    public void RequestsRejectedByTheClientLimitDoNotConsumeTheSharedAllowance()
    {
        AssistantRequestLimiter limiter = new(new ManualClock());
        IPAddress client = ClientAddress(1);

        for (int request = 0; request < 5; request++)
            Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(client));
        for (int request = 0; request < 60; request++)
            Assert.Equal(TimeSpan.FromSeconds(60), limiter.TryAcquire(client));
        for (int address = 2; address <= 56; address++)
            Assert.Equal(TimeSpan.Zero, limiter.TryAcquire(ClientAddress(address)));

        Assert.Equal(TimeSpan.FromSeconds(60), limiter.TryAcquire(ClientAddress(57)));
    }

    [Theory]
    [InlineData(true, 5)]
    [InlineData(false, 60)]
    public void ConcurrentRequestsRespectBothLimits(bool sameClient, int expectedAdmissions)
    {
        AssistantRequestLimiter limiter = new(new ManualClock());
        int admissions = 0;

        Parallel.For(1, 101, index =>
        {
            IPAddress client = ClientAddress(sameClient ? 1 : index);
            if (limiter.TryAcquire(client) == TimeSpan.Zero)
                Interlocked.Increment(ref admissions);
        });

        Assert.Equal(expectedAdmissions, admissions);
    }

    static IPAddress ClientAddress(int index) => IPAddress.Parse($"192.0.2.{index}");

    sealed class ManualClock : TimeProvider
    {
        long _ticks;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }
}
