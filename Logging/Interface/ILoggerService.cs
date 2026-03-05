using Logging.Models;
using Serilog.Events;

namespace Logging.Interface;

public interface ILoggerService
{
    Task Info(string message);
    Task Information(string message);
    Task Warn(string message);
    Task Warning(string message);

    Task Error(
         Exception e,
         string url,
         string? requestId,
         string? serviceOrigin = null);

    Task Info(RequestLogDetails logDetails);

    bool IsEnabled(LogEventLevel logEventLevel);
}
