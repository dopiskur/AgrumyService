using System.Diagnostics;
using Agrumy.Dal;
using Microsoft.EntityFrameworkCore;

// dotnet run --project tools/Agrumy.MqttCredentialSync -- --acl-file deploy/mosquitto/acl_file --password-file deploy/mosquitto/password_file
//   writes both files from the current device table, then (re)generates the password_file via mosquitto_passwd.
// dotnet run --project tools/Agrumy.MqttCredentialSync -- --acl-file <path> --check
//   exits 1 if the acl_file content would change, without touching the password_file or the broker - CI-friendly.
// --connection/--provider fall back to ConnectionStrings__DefaultConnection/AGRUMY_DB_PROVIDER, same convention as AgrumyDbContextDesignTimeFactory.
// --reload additionally runs `systemctl reload mosquitto` after a real (non --check) sync.

string? GetArg(string name) => args.FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..];

var check = args.Contains("--check");
var reload = args.Contains("--reload");
var aclFile = GetArg("--acl-file") ?? throw new InvalidOperationException("--acl-file=<path> is required.");
var passwordFile = GetArg("--password-file");
var mosquittoPasswd = GetArg("--mosquitto-passwd") ?? "mosquitto_passwd";

var provider = DbProviderKindParser.Parse(GetArg("--provider") ?? Environment.GetEnvironmentVariable("AGRUMY_DB_PROVIDER"));
var connection = GetArg("--connection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("--connection=<connection string> or ConnectionStrings__DefaultConnection is required.");

await using var db = new AgrumyDbContext(DbOptionsFactory.Build(provider, connection));

// TenantQuotaRow absent for a tenant means TenantQuota.Default (Agrumy.Shared), whose MqttEnabled is false - MQTT is opt-in, not opt-out, same as TenantQuotaEnforcer.IsMqttAllowedAsync.
var mqttEnabledByTenant = await db.TenantQuotas.AsNoTracking().ToDictionaryAsync(q => q.IDTenant, q => q.MqttEnabled);

var devices = await db.Devices.AsNoTracking()
    .Where(d => d.ApiId != "" && d.ApiKey != "")
    .Select(d => new { d.IDDevice, d.ApiId, d.ApiKey, d.TenantID })
    .ToListAsync();

var credentials = devices
    .Where(d => d.TenantID is null or 0 || (mqttEnabledByTenant.TryGetValue(d.TenantID.Value, out bool enabled) && enabled))
    .Select(d => new Agrumy.MqttCredentialSync.MqttDeviceCredential(d.IDDevice, d.ApiId, d.ApiKey, d.TenantID))
    .ToList();

var generatedAcl = Agrumy.MqttCredentialSync.MosquittoAclBuilder.BuildAclFile(credentials);

if (check)
{
    var existing = File.Exists(aclFile) ? File.ReadAllText(aclFile).Replace("\r\n", "\n") : "";
    if (generatedAcl.Replace("\r\n", "\n") == existing)
    {
        Console.WriteLine($"{aclFile} already matches {credentials.Count} MQTT-enabled device(s).");
        return 0;
    }
    Console.Error.WriteLine($"{aclFile} is out of date with the database ({credentials.Count} MQTT-enabled device(s)) - run without --check to regenerate.");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(aclFile))!);
await File.WriteAllTextAsync(aclFile, generatedAcl);
Console.WriteLine($"Wrote {aclFile} ({credentials.Count} device(s)).");

// So Device Details' "MQTT credentials pending sync" banner (agrumy-mqtt-sync.timer's whole reason to
// exist) knows a device created after this moment isn't in the ACL yet. Table is a singleton (one row),
// so this touches every row - there's only ever one.
await db.ServerConfigs.ExecuteUpdateAsync(s => s.SetProperty(c => c.MqttCredentialsSyncedAtUtc, DateTimeOffset.UtcNow));

if (passwordFile != null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(passwordFile))!);
    File.Delete(passwordFile); // no-op if it doesn't exist yet - -c below recreates it fresh so a revoked device's old entry never lingers
    bool first = true;
    foreach (var cred in credentials)
    {
        // -c (re)creates the file - only on the first entry, or every later call would wipe the ones written just before it.
        var psi = new ProcessStartInfo(mosquittoPasswd)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in (first ? new[] { "-b", "-c", passwordFile, cred.ApiId, cred.ApiKey } : new[] { "-b", passwordFile, cred.ApiId, cred.ApiKey }))
        {
            psi.ArgumentList.Add(a);
        }
        first = false;
        using var proc = Process.Start(psi)!;
        string stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            Console.Error.WriteLine($"mosquitto_passwd failed for device {cred.DeviceId} (apiId={cred.ApiId}): {stderr}");
            return 1;
        }
    }
    if (credentials.Count == 0)
    {
        await File.WriteAllTextAsync(passwordFile, "");
    }
    Console.WriteLine($"Wrote {passwordFile} via {mosquittoPasswd} ({credentials.Count} entrie(s)).");

    if (reload)
    {
        var reloadPsi = new ProcessStartInfo("systemctl", "reload mosquitto") { RedirectStandardError = true };
        using var reloadProc = Process.Start(reloadPsi)!;
        string reloadErr = await reloadProc.StandardError.ReadToEndAsync();
        await reloadProc.WaitForExitAsync();
        Console.WriteLine(reloadProc.ExitCode == 0
            ? "mosquitto reloaded."
            : $"WARNING: systemctl reload mosquitto failed ({reloadErr.Trim()}) - restart it manually for the new credentials/ACL to take effect.");
    }
}

return 0;
