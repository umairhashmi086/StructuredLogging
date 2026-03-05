using Microsoft.AspNetCore.Mvc;
using Logging.Interface;
namespace StructuredLogging.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController(ILoggerService _loggerservice) : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            try
            {
                _ = _loggerservice.Information("GetWeatherForecast -  API");
                return Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                })
                .ToArray();
            }
            catch (Exception ex)
            {
                _ = _loggerservice.Error(ex,"GetWeatherForecast -  API","12345");
                throw;
            }
            
        }
    }
}
