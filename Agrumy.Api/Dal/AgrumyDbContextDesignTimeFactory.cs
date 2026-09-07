using Microsoft.EntityFrameworkCore.Design;

namespace api.Dal
{
    /// Lets `dotnet ef` build an AgrumyDbContext without booting the web host - provider via --provider mysql|postgres or AGRUMY_DB_PROVIDER, else mysql; connection via --connection or ConnectionStrings__DefaultConnection, else a localhost placeholder (only commands that touch the database need a real one).
    public class AgrumyDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AgrumyDbContext>
    {
        public AgrumyDbContext CreateDbContext(string[] args)
        {
            var provider = DbProviderKindParser.Parse(
                GetArg(args, "--provider")
                ?? Environment.GetEnvironmentVariable("AGRUMY_DB_PROVIDER"));

            string conn =
                GetArg(args, "--connection")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? Environment.GetEnvironmentVariable("DefaultConnection")
                ?? (provider == DbProviderKind.Postgres
                        ? "Host=localhost;Port=5432;Database=agrumyapi;Username=postgres;Password=postgres"
                        : "server=localhost;port=3306;database=agrumyapi;user id=root;password=;");

            return new AgrumyDbContext(DbOptionsFactory.Build(provider, conn));
        }

        private static string? GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
