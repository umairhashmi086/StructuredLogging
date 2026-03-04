using Logging.Interface;
using Logging.Models;
using Microsoft.Extensions.Options;
using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using System.Text.Json;

namespace Logging.Configuration;

public class LoggerService: ILoggerService
{
    private readonly ILogger _fileLogger;
    private readonly ILogger? _dbAuditLogger;
    private readonly ILogger? _dbExceptionLogger;
    private readonly ILogger? _dbBaseLogger;
    private readonly SerilogConfiguration _options;
    private readonly bool _dbAvailable;

    public LoggerService(IOptions<SerilogConfiguration> serilogOptions)
    {
        _options = serilogOptions.Value;

        // File Logger - for ALL logs
        _fileLogger = Log.Logger;
        _dbBaseLogger = Log.Logger;
        // Database Logger - ONLY for request/response
        _dbAuditLogger = InitializeDatabaseLogger();
        _dbExceptionLogger = InitializeExceptionLogger();
        _dbAvailable = _dbAuditLogger != null && _dbExceptionLogger != null;

        if (_dbAvailable)
        {
            _fileLogger.Information("✅ Database logger initialized successfully for table: {Table}",
                _options.AuditTableName);
        }
        else
        {
            _fileLogger.Warning("⚠️ Database logger not available. Request/Response will go to file only.");
        }
    }

    private ILogger? InitializeDatabaseLogger()
    {
        try
        {
            if (string.IsNullOrEmpty(_options.ConnectionString) ||
                _options.EnabledLogger != Loggers.Npgsql)
            {
                _fileLogger.Warning("Database connection not configured or not using Npgsql");
                return null;
            }

            // FIXED: Column names MUST match your table EXACTLY (case-sensitive!)
            var columnOptions = new Dictionary<string, ColumnWriterBase>
            {
                { "Id", new IdAutoIncrementColumnWriter() },
                { "RequestId", new SinglePropertyColumnWriter("RequestId", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },
                { "ClientIp", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },
                { "URL", new SinglePropertyColumnWriter("URL", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },
                { "RequestTime", new SinglePropertyColumnWriter("RequestTime", PropertyWriteMethod.Raw, NpgsqlDbType.Timestamp) }, // timestamp without time zone
                { "ResponseTime", new SinglePropertyColumnWriter("ResponseTime", PropertyWriteMethod.Raw, NpgsqlDbType.Timestamp) }, // timestamp without time zone
                { "Request", new SinglePropertyColumnWriter("Request", PropertyWriteMethod.Raw, NpgsqlDbType.Jsonb) },
                { "Response", new SinglePropertyColumnWriter("Response", PropertyWriteMethod.Raw, NpgsqlDbType.Jsonb) },
                { "HTTPStatusCode", new SinglePropertyColumnWriter("HTTPStatusCode", PropertyWriteMethod.Raw, NpgsqlDbType.Integer) }, // Note: HTTPStatusCode not HttpStatusCode
                { "UserAgent", new SinglePropertyColumnWriter("UserAgent", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },
                { "Source", new SinglePropertyColumnWriter("Source", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },
                { "TimeStamp", new TimestampColumnWriter(NpgsqlDbType.TimestampTz) }, // timestamp with time zone
                { "Headers", new SinglePropertyColumnWriter("Headers", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },
                { "Method", new SinglePropertyColumnWriter("Method", PropertyWriteMethod.Raw, NpgsqlDbType.Text) }
            };

            var dbLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.PostgreSQL(
                    connectionString: _options.ConnectionString,
                    tableName: _options.AuditTableName, // This should be "AuditLogsUAT"
                    columnOptions: columnOptions,
                    needAutoCreateTable: false, // Set to false since table already exists
                    batchSizeLimit: 1, // Immediate logging
                    period: TimeSpan.FromSeconds(1))
                .CreateLogger();

            _fileLogger.Information($"✅ Database logger configured for table: {_options.AuditTableName}");
            return dbLogger;
        }
        catch (Exception ex)
        {
            _fileLogger.Error(ex, "❌ Failed to initialize database logger");
            return null;
        }
    }

    private ILogger? InitializeExceptionLogger()
    {
        try
        {
            if (string.IsNullOrEmpty(_options.ConnectionString) ||
                _options.EnabledLogger != Loggers.Npgsql)
            {
                return null;
            }

            // Different columns for exception table
            var exceptionColumnOptions = new Dictionary<string, ColumnWriterBase>
        {
            { "Id", new IdAutoIncrementColumnWriter() },
            { "RequestId", new SinglePropertyColumnWriter("RequestId", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },
            { "TimeStamp", new TimestampColumnWriter(NpgsqlDbType.TimestampTz) },
            { "URL", new SinglePropertyColumnWriter("URL", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },

            { "Exception", new SinglePropertyColumnWriter("Exception", PropertyWriteMethod.Raw, NpgsqlDbType.Text) },
            { "ServiceOrigin", new SinglePropertyColumnWriter("ServiceOrigin", PropertyWriteMethod.Raw, NpgsqlDbType.Text) }
        };

            return new LoggerConfiguration()
                .MinimumLevel.Error() // Only log errors and above
                .WriteTo.PostgreSQL(
                    connectionString: _options.ConnectionString,
                    tableName: _options.ExceptionTableName,
                    columnOptions: exceptionColumnOptions,
                    needAutoCreateTable: false,
                    batchSizeLimit: 1,
                    period: TimeSpan.FromSeconds(1))
                .CreateLogger();
        }
        catch (Exception ex)
        {
            _fileLogger.Error(ex, "❌ Failed to initialize exception logger");
            return null;
        }
    }


    public bool IsEnabled(LogEventLevel logEventLevel)
    {
        return _dbBaseLogger.IsEnabled(logEventLevel);
    }
    private void LogExceptionToDatabase(Exception e, string url, string? RequestId, string? serviceOrigin)
    {
        if (_dbExceptionLogger == null) return;


        // Log to exception table
        _dbExceptionLogger
            .ForContext("RequestId", RequestId ?? "")
            .ForContext("URL", url)
            .ForContext("Exception", e.Message)
             .ForContext("ServiceOrigin", serviceOrigin ?? "")
                .Error("Exception occurred at {URL}: {Exception}", url, e.Message);

        _fileLogger.Debug("✅ Exception logged to database table '{Table}'. KongId: {RequestId}",
            _options.ExceptionTableName, RequestId);
    }
    // =========================================
    // GENERAL LOGS → FILE ONLY
    // =========================================

    public Task Info(string message)
    {
        _fileLogger.Information(message);
        return Task.CompletedTask;
    }

    public Task Information(string message)
    {
        _fileLogger.Information(message);
        return Task.CompletedTask;
    }

    public Task Warn(string message)
    {
        _fileLogger.Warning(message);
        return Task.CompletedTask;
    }

    public Task Warning(string message)
    {
        _fileLogger.Warning(message);
        return Task.CompletedTask;
    }

    public Task Error(Exception e, string url, string? RequestId, string? serviceOrigin = null)
    {
        _fileLogger.Error(e, "Error at {URL} | KongId: {RequestId} | Service: {ServiceOrigin}",
       url, RequestId, serviceOrigin);

        // Log to exception table if configured
        if (_dbExceptionLogger != null)
        {
            try
            {
                LogExceptionToDatabase(e, url, RequestId, serviceOrigin);
            }
            catch (Exception logEx)
            {
                _fileLogger.Error(logEx, "Failed to log exception to database");
            }
        }

        return Task.CompletedTask;
    }

    // =========================================
    // REQUEST/RESPONSE LOGS → DATABASE + FILE
    // =========================================

    public Task Info(RequestLogDetails logDetails)
    {
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // Ensure Request is valid JSON for JSONB column
        if (string.IsNullOrEmpty(logDetails.Request))
        {
            logDetails.Request = JsonSerializer.Serialize(new { Empty = "Empty" }, options);
        }

        // Ensure Response is valid JSON for JSONB column
        if (string.IsNullOrEmpty(logDetails.Response))
        {
            logDetails.Response = JsonSerializer.Serialize(new { Empty = "Empty" }, options);
        }

        // Ensure ResponseTime is set to avoid default DateTime value
        if (logDetails.ResponseTime == default)
        {
            logDetails.ResponseTime = DateTime.Now;
        }

        // Always log to FILE
        //  LogToFile(logDetails); we need to discuss, request and response always we will log in file or not

        // Log to Db if db exist
        if (_dbAvailable && _dbAuditLogger != null)
        {
            try
            {
                LogToDatabase(logDetails);
            }
            catch (Exception ex)
            {
                _fileLogger.Error(ex, $"❌ Failed to log to database. Id: {logDetails.RequestId}",
                    logDetails.RequestId);
            }
        }

        return Task.CompletedTask;
    }

    private Task LogToFile(RequestLogDetails logDetails)
    {
        var duration = (logDetails.ResponseTime - logDetails.RequestTime).TotalMilliseconds;

        _fileLogger.Information($"{{{AuditColumns.RequestId}}}{{{AuditColumns.ClientIp}}}{{{AuditColumns.URL}}}{{{AuditColumns.Request}}}{{{AuditColumns.Response}}}{{{AuditColumns.RequestTime}}}{{{AuditColumns.ResponseTime}}}{{{AuditColumns.HTTPStatusCode}}}{{{AuditColumns.Headers}}}"
            , logDetails.RequestId, logDetails.ClientIp, logDetails.URL.ToLower(), logDetails.Request, logDetails.Response, logDetails.RequestTime, logDetails.ResponseTime, logDetails.HttpStatusCode, logDetails.Headers);
        return Task.CompletedTask;
    }

    private void LogToDatabase(RequestLogDetails logDetails)
    {
        if (_dbAuditLogger == null) return;

        try
        {
            // Prepare JSON for database
            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // Prepare request JSON
            string requestJson;
            if (string.IsNullOrWhiteSpace(logDetails.Request) || logDetails.Request.Trim() == "{}")
            {
                requestJson = "{\"message\": \"Empty request body\"}";
            }
            else if (IsValidJson(logDetails.Request))
            {
                requestJson = logDetails.Request;
            }
            else
            {
                requestJson = JsonSerializer.Serialize(new { raw = logDetails.Request }, options);
            }

            // Prepare response JSON
            string responseJson;
            if (string.IsNullOrWhiteSpace(logDetails.Response) || logDetails.Response.Trim() == "{}")
            {
                responseJson = "{\"message\": \"Empty response body\"}";
            }
            else if (IsValidJson(logDetails.Response))
            {
                responseJson = logDetails.Response;
            }
            else
            {
                responseJson = JsonSerializer.Serialize(new { raw = logDetails.Response }, options);
            }

            // FIXED: Log with properties that match your table columns EXACTLY
            _dbAuditLogger.ForContext("RequestId", logDetails.RequestId ?? "")
                     .ForContext("ClientIp", logDetails.ClientIp ?? "")
                     .ForContext("URL", logDetails.URL)
                     .ForContext("RequestTime", logDetails.RequestTime)
                     .ForContext("ResponseTime", logDetails.ResponseTime)
                     .ForContext("Request", requestJson)
                     .ForContext("Response", responseJson)
                     .ForContext("HTTPStatusCode", logDetails.HttpStatusCode ?? 0)
                     .ForContext("Headers", logDetails.Headers ?? "")
                     .Information("API Request: {URL}", logDetails.URL);

            _fileLogger.Debug("✅ Request/Response logged to database table '{Table}'. RequestId: {RequestId}",
                _options.AuditTableName, logDetails.RequestId);
        }
        catch (Exception ex)
        {
            _fileLogger.Error(ex, "❌ Error in LogToDatabase for KongId: {RequestId}",
                logDetails.RequestId);
            throw;
        }
    }

    private bool IsValidJson(string str)
    {
        if (string.IsNullOrWhiteSpace(str)) return false;

        str = str.Trim();
        if ((str.StartsWith("{") && str.EndsWith("}")) ||
            (str.StartsWith("[") && str.EndsWith("]")))
        {
            try
            {
                JsonDocument.Parse(str);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }
}
