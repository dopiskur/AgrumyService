using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Agrumy.Dal
{
    /// See AgrumyDbContext.ConfigureConventions - applied to every DateTimeOffset property so its Offset is always 0 on both write and read, independent of provider or host OS timezone.
    public sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
        v => v.ToUniversalTime(), v => v.ToUniversalTime());
}
