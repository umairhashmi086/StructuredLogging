using Logging.Configuration;
using Logging.Interface;
namespace StructuredLogging;

public static class ServiceCollectionExtension
{
    public static void RegisterLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SerilogConfiguration>(
              configuration.GetSection(nameof(SerilogConfiguration)));

        services.AddSingleton<ILoggerService, LoggerService>();

        Serilog.Debugging.SelfLog.Enable(msg =>
        {
            Console.WriteLine($"Serilog Debug: {msg}");
            System.Diagnostics.Debug.WriteLine(msg);
        });

    }
}
