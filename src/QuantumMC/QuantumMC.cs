using Serilog;
using QuantumMC.Config;

namespace QuantumMC
{
    public class QuantumMC
    {
        public static string DataFolder { get; } = Directory.GetCurrentDirectory();

        public static void Main(string[] args)
        {
            var config = ConfigManager.Load();
            ConfigureLogger(config);

            try
            {
                Log.Information("Starting QuantumMC v{Version}...", Utils.Version.Current);

                var server = new Server(config);
                server.Start();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Server crashed!");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureLogger(dynamic config)
        {
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(config.DebugMode
                    ? Serilog.Events.LogEventLevel.Debug
                    : Serilog.Events.LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ThreadName", "Main Thread")
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{ThreadName}] [{Level:u4}] {Message:lj}{NewLine}{Exception}"
                );

            Log.Logger = loggerConfig.CreateLogger();
        }
    }
}