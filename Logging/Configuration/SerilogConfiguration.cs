namespace Logging.Configuration;

public class SerilogConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string AuditTableName { get; set; } = string.Empty;
    public string ExceptionTableName { get; set; } = string.Empty;
    public Loggers EnabledLogger { get; set; } = Loggers.Npgsql;
}
public enum Loggers
{
    None = 0,
    Npgsql = 1,
    File = 2,
    Sql = 3
}