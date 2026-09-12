using Agrumy.Dal;
using Agrumy.Api.Dal;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Quota
{
    /// Shared Serializable-transaction-plus-Contention-retry shape for a repository Add method an optional TenantQuota count-based check gates - the check and the insert must run in the SAME transaction, or two concurrent requests at the exact organization limit can both read the same stale count and both slip through. Postgres detects the resulting write skew at commit and aborts one side (SqlState 40001); MySQL's InnoDB gap-locking blocks the second transaction's count read until the first commits instead - both surface through DbExceptionClassifier as DbFailureKind.Contention, the same transient-retry signal DbExceptionFilter already uses elsewhere in this codebase. Takes db as a parameter (every repository method already has its own) rather than TenantQuotaEnforcer holding one itself - keeps the enforcer trivially mockable in controller unit tests, which stub out the whole repository call and never reach this code at all.
    internal static class QuotaGuard
    {
        private const int MaxContentionRetries = 3;

        /// Null quotaCheckAsync (an internal/system-initiated insert, e.g. a new organization's automatic first farm) skips the transaction and check entirely - same cost as an unguarded insert.
        public static async Task<T> RunAsync<T>(AgrumyDbContext db, Func<Task<string?>>? quotaCheckAsync, Func<Task<T>> insertAsync)
        {
            if (quotaCheckAsync == null)
            {
                return await insertAsync();
            }
            for (int attempt = 1; ; attempt++)
            {
                await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    if (await quotaCheckAsync() is string limitError)
                    {
                        await tx.RollbackAsync();
                        throw new QuotaLimitExceededException(limitError);
                    }
                    T value = await insertAsync();
                    await tx.CommitAsync();
                    return value;
                }
                catch (Exception ex) when (DbExceptionClassifier.Classify(ex) == DbFailureKind.Contention && attempt < MaxContentionRetries)
                {
                    await tx.RollbackAsync();
                }
            }
        }
    }
}
