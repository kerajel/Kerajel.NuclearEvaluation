using System.Transactions;

namespace Kerajel.Primitives.Helpers;

public static class TransactionProvider
{
    public static readonly TimeSpan MaxTransactionTimeout = TimeSpan.FromMinutes(10);

    static readonly TimeSpan _defaultTransactionTimeout = TimeSpan.FromMinutes(1);

    public static TransactionScope CreateScope(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        TransactionScopeAsyncFlowOption asyncFlowOption = TransactionScopeAsyncFlowOption.Enabled,
        TimeSpan transactionTimeout = default)
    {
        if (Transaction.Current != null)
        {
            isolationLevel = Transaction.Current.IsolationLevel;
        }

        TransactionOptions transactionOptions = new()
        {
            IsolationLevel = isolationLevel,
            Timeout = transactionTimeout == default
                ? _defaultTransactionTimeout
                : transactionTimeout,
        };

        return new TransactionScope(TransactionScopeOption.Required,
            transactionOptions, asyncFlowOption);
    }
}