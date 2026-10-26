using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HiringPs.Core;

/// <summary>يؤخر إبطال الكاش إلى اعتماد المعاملة الصريحة ويهمل التغييرات المتراجَع عنها.</summary>
public sealed class PublicCatalogTransactionInterceptor : DbTransactionInterceptor
{
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        (eventData.Context as HiringDb)?.CommitPublicCatalogChanges();

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionCommitted(transaction, eventData);
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        (eventData.Context as HiringDb)?.DiscardPublicCatalogChanges();

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionRolledBack(transaction, eventData);
        return Task.CompletedTask;
    }
}
