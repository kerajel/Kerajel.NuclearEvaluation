using Bunit;
using NuclearEvaluation.Kernel.Data.Context;

namespace NuclearEvaluation.Server.Tests;

public abstract class TestBase : IClassFixture<TestFixture>
{
    protected TestContext TestContext { get; }
    protected NuclearEvaluationServerDbContext DbContext { get; }
    protected TimeSpan DefaultWaitForStateTimeout { get; }

    protected TestBase(TestFixture fixture)
    {
        TestContext = fixture.TestContext;
        DbContext = fixture.DbContext;
        DefaultWaitForStateTimeout = fixture.DefaultWaitForStateTimeout;
    }
}
