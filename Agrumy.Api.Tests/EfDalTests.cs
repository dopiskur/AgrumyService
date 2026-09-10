using System.Text.Json;
using Agrumy.Dal;
using Agrumy.Api.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;

namespace Agrumy.Api.Tests;

/// Unit tests for the sensor-report shaping port - JSON assembly only now (grouping/CO2-nulling/averaging all happen in SQL, see EfSensorDataRepository, and are exercised there against real databases). No database - pure functions only.
public class SensorReportShaperTests
{
    private static BucketedSensorRow Bucket(string bucketStart, double? temp = 20.0, double? humidity = null) => new()
    {
        Temperature = temp,
        Humidity = humidity,
        BucketStart = DateTime.Parse(bucketStart, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal),
    };

    private static AveragedSensorBucket AveragedBucket(string bucketStart, double? temp = 20.0, double? soilTemp = null) => new()
    {
        Temperature = temp,
        SoilTemperature = soilTemp,
        BucketStart = DateTime.Parse(bucketStart, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal),
    };

    [Fact]
    public void Build_NoRows_ReturnsEmptyString()
    {
        Assert.Equal("", SensorReportShaper.Build(Array.Empty<BucketedSensorRow>()));
    }

    [Fact]
    public void Build_OneRowPerBucket_ProducesOneRecordEach()
    {
        var buckets = new[]
        {
            Bucket("2026-08-29 09:50:00", temp: 2),
            Bucket("2026-08-29 09:51:00", temp: 3),
        };

        var json = SensorReportShaper.Build(buckets);
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("sensorData");

        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal(2, arr[0].GetProperty("temperature").GetDouble());
        Assert.Equal(3, arr[1].GetProperty("temperature").GetDouble());
    }

    [Fact]
    public void Build_Record_HasProcKeysAndDateFormat()
    {
        var json = SensorReportShaper.Build(new[] { Bucket("2026-08-29 09:50:00") });
        using var doc = JsonDocument.Parse(json);
        var rec = doc.RootElement.GetProperty("sensorData")[0];

        foreach (var key in new[] { "battery", "temperature", "soilTemperature", "humidity", "vpd", "moisture",
                                    "light", "co2", "tvoc", "barometer", "liquidPH", "rainLevel",
                                    "waterLevel", "wind", "dateCreated" })
        {
            Assert.True(rec.TryGetProperty(key, out _), $"missing key: {key}");
        }

        Assert.Equal("2026-08-29 09:50:00", rec.GetProperty("dateCreated").GetString());
    }

    [Fact]
    public void Build_Vpd_NullWhenHumidityMissing()
    {
        var json = SensorReportShaper.Build(new[] { Bucket("2026-08-29 09:50:00", temp: 20) });
        var rec = JsonDocument.Parse(json).RootElement.GetProperty("sensorData")[0];

        Assert.Equal(JsonValueKind.Null, rec.GetProperty("vpd").ValueKind);
    }

    [Fact]
    public void Build_Vpd_ComputedWhenTemperatureAndHumidityBothPresent()
    {
        var json = SensorReportShaper.Build(new[] { Bucket("2026-08-29 09:50:00", temp: 20, humidity: 60) });
        var rec = JsonDocument.Parse(json).RootElement.GetProperty("sensorData")[0];

        Assert.True(Math.Abs(rec.GetProperty("vpd").GetDouble() - 0.9353) < 0.001);
    }

    [Fact]
    public void BuildAveraged_NoRows_ReturnsEmptyString()
    {
        Assert.Equal("", SensorReportShaper.BuildAveraged(Array.Empty<AveragedSensorBucket>()));
    }

    [Fact]
    public void BuildAveraged_OneRowPerBucket_ProducesOneRecordEach()
    {
        var buckets = new[]
        {
            AveragedBucket("2026-08-29 09:00:00", temp: 15, soilTemp: 18),
            AveragedBucket("2026-08-29 10:00:00", temp: 30),
        };

        var json = SensorReportShaper.BuildAveraged(buckets);
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("sensorData");

        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal(15, arr[0].GetProperty("temperature").GetDouble());
        Assert.Equal(18, arr[0].GetProperty("soilTemperature").GetDouble());
        Assert.Equal(30, arr[1].GetProperty("temperature").GetDouble());
        Assert.Equal(JsonValueKind.Null, arr[1].GetProperty("soilTemperature").ValueKind);
    }
}

public class VpdCalculatorTests
{
    [Fact]
    public void Compute_TemperatureMissing_ReturnsNull() =>
        Assert.Null(Agrumy.Shared.Utils.VpdCalculator.Compute(null, 60));

    [Fact]
    public void Compute_HumidityMissing_ReturnsNull() =>
        Assert.Null(Agrumy.Shared.Utils.VpdCalculator.Compute(20, null));

    [Fact]
    public void Compute_20C_60Percent_MatchesTetensFormula()
    {
        double? vpd = Agrumy.Shared.Utils.VpdCalculator.Compute(20, 60);
        Assert.True(vpd.HasValue);
        Assert.True(Math.Abs(vpd!.Value - 0.9353) < 0.001);
    }
}

public class TankCalculatorTests
{
    [Theory]
    [InlineData(null, 0, 100, 200.0)]
    [InlineData(50.0, null, 100, 200.0)]
    [InlineData(50.0, 0, null, 200.0)]
    [InlineData(50.0, 0, 100, null)]
    [InlineData(50.0, 50, 50, 200.0)] // rawEmpty == rawFull - undefined calibration, not a divide-by-zero crash
    public void Compute_MissingCalibrationInput_ReturnsNullPair(double? raw, int? empty, int? full, double? capacity)
    {
        var (percent, liters) = Agrumy.Shared.Utils.TankCalculator.Compute(raw, empty, full, capacity);
        Assert.Null(percent);
        Assert.Null(liters);
    }

    [Fact]
    public void Compute_Midpoint_Returns50PercentAndHalfCapacity()
    {
        var (percent, liters) = Agrumy.Shared.Utils.TankCalculator.Compute(50, 0, 100, 200.0);
        Assert.Equal(50.0, percent);
        Assert.Equal(100.0, liters);
    }

    [Fact]
    public void Compute_BelowEmptyCalibration_ClampsToZero()
    {
        var (percent, liters) = Agrumy.Shared.Utils.TankCalculator.Compute(-10, 0, 100, 200.0);
        Assert.Equal(0.0, percent);
        Assert.Equal(0.0, liters);
    }

    [Fact]
    public void Compute_AboveFullCalibration_ClampsTo100()
    {
        var (percent, liters) = Agrumy.Shared.Utils.TankCalculator.Compute(150, 0, 100, 200.0);
        Assert.Equal(100.0, percent);
        Assert.Equal(200.0, liters);
    }

    [Fact]
    public void Compute_InvertedCalibration_StillInterpolatesCorrectly()
    {
        // Some raw sensors read HIGHER when the tank is more empty - rawEmpty > rawFull is a valid, deliberately supported calibration.
        var (percent, liters) = Agrumy.Shared.Utils.TankCalculator.Compute(75, 100, 0, 200.0);
        Assert.Equal(25.0, percent);
        Assert.Equal(50.0, liters);
    }
}

public class ClassifyExceptionTests
{
    // ClassifyException is a pure function - never touches the DbContext, so a never-connected one and default settings are enough to construct it.
    private readonly SchemaBootstrapper _repo = new(
        new AgrumyDbContext(DbOptionsFactory.Build(DbProviderKind.MySql, "server=unused;database=unused;")),
        NullLogger<SchemaBootstrapper>.Instance,
        new Mock<IServerConfigRepository>().Object,
        new DataSeeder(NullLogger<DataSeeder>.Instance));

    [Fact]
    public void PlainException_MentioningMissingTable_IsSchemaMissing()
    {
        Assert.Equal(DbFailureKind.SchemaMissing,
            _repo.ClassifyException(new Exception("Table 'agrumy.device' doesn't exist")));
    }

    [Fact]
    public void UnknownTableWording_IsSchemaMissing()
    {
        Assert.Equal(DbFailureKind.SchemaMissing,
            _repo.ClassifyException(new Exception("Unknown table 'agrumy.user'")));
    }

    [Fact]
    public void DbUpdateException_WrappingMissingTable_IsSchemaMissing()
    {
        var ex = new Microsoft.EntityFrameworkCore.DbUpdateException(
            "An error occurred while saving the entity changes.",
            new Exception("Table 'agrumy.sensorData' doesn't exist"));

        Assert.Equal(DbFailureKind.SchemaMissing, _repo.ClassifyException(ex));
    }

    [Fact]
    public void TransportError_IsConnectionFailure()
    {
        Assert.Equal(DbFailureKind.ConnectionFailure,
            _repo.ClassifyException(new TimeoutException("connect timeout")));
        Assert.Equal(DbFailureKind.ConnectionFailure,
            _repo.ClassifyException(new System.Net.Sockets.SocketException()));
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(NullReferenceException))]
    public void UnrecognisedException_IsUnknown(Type exType)
    {
        var ex = (Exception)Activator.CreateInstance(exType)!;
        Assert.Equal(DbFailureKind.Unknown, _repo.ClassifyException(ex));
    }

    [Fact]
    public void NotFoundArgumentException_IsUnknown_NotConnectionFailure()
    {
        // The DAL throws ArgumentException for "no such user/device"; that must not read as a DB outage.

        Assert.Equal(DbFailureKind.Unknown,
            _repo.ClassifyException(new ArgumentException("Wrong id, no such person")));
    }

    private static Npgsql.PostgresException Pg(string sqlState) =>
        new("boom", "ERROR", "ERROR", sqlState);

    [Theory]
    [InlineData("23503")] // foreign_key_violation
    [InlineData("23505")] // unique_violation
    [InlineData("23514")] // check_violation
    public void PostgresConstraintSqlState_IsConstraintViolation(string sqlState)
    {
        Assert.Equal(DbFailureKind.ConstraintViolation, _repo.ClassifyException(Pg(sqlState)));
    }

    [Theory]
    [InlineData("40P01")] // deadlock_detected
    [InlineData("40001")] // serialization_failure
    [InlineData("55P03")] // lock_not_available
    public void PostgresContentionSqlState_IsContention(string sqlState)
    {
        Assert.Equal(DbFailureKind.Contention, _repo.ClassifyException(Pg(sqlState)));
    }

    [Fact]
    public void PostgresUndefinedTable_StaysSchemaMissing()
    {
        Assert.Equal(DbFailureKind.SchemaMissing, _repo.ClassifyException(Pg("42P01")));
    }

    [Fact]
    public void DbUpdateException_WrappingConstraintViolation_IsConstraintViolation()
    {
        var ex = new Microsoft.EntityFrameworkCore.DbUpdateException(
            "An error occurred while saving the entity changes.", Pg("23505"));

        Assert.Equal(DbFailureKind.ConstraintViolation, _repo.ClassifyException(ex));
    }
}

public class DbErrorResponseForTests
{
    private static string Json(DbFailureKind kind) =>
        JsonSerializer.Serialize(DbErrorResponse.For(kind));

    [Fact]
    public void For_ConstraintViolation_HasConstraintReason()
    {
        Assert.Contains("constraint_violation", Json(DbFailureKind.ConstraintViolation));
    }

    [Fact]
    public void For_Contention_HasContentionReason()
    {
        Assert.Contains("contention", Json(DbFailureKind.Contention));
    }

    [Fact]
    public void For_Unknown_HasServerErrorReason()
    {
        Assert.Contains("server_error", Json(DbFailureKind.Unknown));
    }

    [Theory]
    [InlineData(DbFailureKind.ConstraintViolation, 409)]
    [InlineData(DbFailureKind.Contention, 503)]
    [InlineData(DbFailureKind.SchemaMissing, 503)]
    [InlineData(DbFailureKind.ConnectionFailure, 503)]
    [InlineData(DbFailureKind.Unknown, 500)]
    public void StatusCodeFor_MapsKindToHttpStatus(DbFailureKind kind, int expected)
    {
        Assert.Equal(expected, DbErrorResponse.StatusCodeFor(kind));
    }
}

public class DbErrorResponseMentionsTests
{
    [Fact]
    public void Mentions_WalksInnerExceptionChain()
    {
        var ex = new Exception("outer",
            new Exception("An error occurred while saving the entity changes.",
                new Exception("Duplicate entry 'a@b.com' for key 'user.email_UNIQUE'")));

        Assert.True(DbErrorResponse.Mentions(ex, "email_UNIQUE"));
        Assert.False(DbErrorResponse.Mentions(ex, "Username_UNIQUE"));
        Assert.False(DbErrorResponse.Mentions(null, "email_UNIQUE"));
    }

    // MySqlException has no public constructor (every overload is internal to MySqlConnector), so the 1062/parsed-name
    // path can only be exercised against a real driver-thrown exception - see RelationalIntegrationTests, which already
    // asserts MentionsConstraint against genuine duplicate-key exceptions on both providers.

    [Fact]
    public void MentionsConstraint_RealPostgresUniqueViolation_MatchesOnConstraintNameFieldNotMessageText()
    {
        var pgEx = new PostgresException(
            messageText: "duplicate key value violates unique constraint \"email_UNIQUE\"",
            severity: "ERROR", invariantSeverity: "ERROR", sqlState: PostgresErrorCodes.UniqueViolation,
            detail: "", hint: "", position: 0, internalPosition: 0, internalQuery: "",
            where: "", schemaName: "", tableName: "user", columnName: "", dataTypeName: "",
            constraintName: "email_UNIQUE", file: "", line: "", routine: "");

        Assert.True(DbErrorResponse.MentionsConstraint(pgEx, "email_UNIQUE"));
        Assert.False(DbErrorResponse.MentionsConstraint(pgEx, "Username_UNIQUE"));
    }

    [Fact]
    public void MentionsConstraint_PostgresErrorWithDifferentSqlState_NeverMatchesEvenIfConstraintNameSet()
    {
        // A non-23505 PostgresException must not be treated as a duplicate-key hit even if ConstraintName happens to be set.
        var pgEx = new PostgresException(
            messageText: "deadlock detected",
            severity: "ERROR", invariantSeverity: "ERROR", sqlState: "40P01",
            detail: "", hint: "", position: 0, internalPosition: 0, internalQuery: "",
            where: "", schemaName: "", tableName: "user", columnName: "", dataTypeName: "",
            constraintName: "email_UNIQUE", file: "", line: "", routine: "");

        Assert.False(DbErrorResponse.MentionsConstraint(pgEx, "email_UNIQUE"));
    }
}
