using Microsoft.EntityFrameworkCore;

namespace api.Dal
{
    /// Builds DbContextOptions for the selected provider, pointing each at its own per-provider migrations assembly (Agrumy.Api.Migrations.MySql / .Postgres) - the two providers' generated SQL differs too much to share one migrations history.
    public static class DbOptionsFactory
    {
        private const string MySqlMigrationsAssembly = "Agrumy.Api.Migrations.MySql";
        private const string PostgresMigrationsAssembly = "Agrumy.Api.Migrations.Postgres";

        public static DbContextOptions<AgrumyDbContext> Build(DbProviderKind provider, string connectionString)
        {
            var builder = new DbContextOptionsBuilder<AgrumyDbContext>();
            switch (provider)
            {
                case DbProviderKind.Postgres:
                    // NpgsqlCompat's module initializer already opted into legacy timestamp behaviour (DateTime -> `timestamp without time zone`).
                    builder.UseNpgsql(connectionString, o => o.MigrationsAssembly(PostgresMigrationsAssembly));
                    builder.AddInterceptors(new SessionTimeZoneInterceptor("SET TIME ZONE 'UTC';"));
                    break;
                default:
                    // Fixed MariaDB version keeps construction connection-free (AutoDetect would open a socket during static init).
                    builder.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 4, 0)), o => o.MigrationsAssembly(MySqlMigrationsAssembly));
                    builder.AddInterceptors(new SessionTimeZoneInterceptor("SET time_zone = '+00:00';"));
                    break;
            }
            return builder.Options;
        }
    }
}
