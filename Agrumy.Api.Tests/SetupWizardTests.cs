using Agrumy.Dal;
using Agrumy.Api.Setup;

namespace Agrumy.Api.Tests;

/// SetupWizard.BuildConnectionString must escape each field via the provider's own
/// builder, not raw string interpolation, so a password containing connection-string-special
/// characters (';', '=') can't break parsing or inject extra parameters.
public class SetupWizardTests
{
    [Fact]
    public void Postgres_PasswordWithSemicolon_IsEscaped_NotInjected()
    {
        string cs = SetupWizard.BuildConnectionString(DbProviderKind.Postgres, "db.example.com", 5432, "agrumy", "admin", "hunter2;Ssl Mode=Disable");

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("hunter2;Ssl Mode=Disable", builder.Password);
        Assert.NotEqual(Npgsql.SslMode.Disable, builder.SslMode);
    }

    [Fact]
    public void Mysql_PasswordWithSemicolon_IsEscaped_NotInjected()
    {
        string cs = SetupWizard.BuildConnectionString(DbProviderKind.MySql, "db.example.com", 3306, "agrumy", "admin", "hunter2;AllowUserVariables=true");

        var builder = new MySqlConnector.MySqlConnectionStringBuilder(cs);
        Assert.Equal("hunter2;AllowUserVariables=true", builder.Password);
        Assert.False(builder.AllowUserVariables);
    }

    [Fact]
    public void Postgres_RoundTrips_HostPortDatabaseUsername()
    {
        string cs = SetupWizard.BuildConnectionString(DbProviderKind.Postgres, "db.example.com", 5432, "agrumy", "admin", "plainpassword");

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("db.example.com", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("agrumy", builder.Database);
        Assert.Equal("admin", builder.Username);
    }

    [Fact]
    public void Mysql_RoundTrips_HostPortDatabaseUsername_AndPrefersTls()
    {
        string cs = SetupWizard.BuildConnectionString(DbProviderKind.MySql, "db.example.com", 3306, "agrumy", "admin", "plainpassword");

        var builder = new MySqlConnector.MySqlConnectionStringBuilder(cs);
        Assert.Equal("db.example.com", builder.Server);
        Assert.Equal(3306u, builder.Port);
        Assert.Equal("agrumy", builder.Database);
        Assert.Equal("admin", builder.UserID);
        Assert.Equal(MySqlConnector.MySqlSslMode.Preferred, builder.SslMode);
    }

    [Fact]
    public void Mysql_PasswordWithEqualsSign_IsEscaped()
    {
        string cs = SetupWizard.BuildConnectionString(DbProviderKind.MySql, "db.example.com", 3306, "agrumy", "admin", "a=b;c=d");

        var builder = new MySqlConnector.MySqlConnectionStringBuilder(cs);
        Assert.Equal("a=b;c=d", builder.Password);
        Assert.Equal("agrumy", builder.Database); // unaffected by the injected-looking fragment
    }
}
