// See https://aka.ms/new-console-template for more information
using AppDotter;
using OpenTelemetry.Metrics;
using OpenTelemetry;
using System.Diagnostics.Metrics;
using AppDotter.Exporter;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Debug()
             .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
             .Enrich.FromLogContext()
             .WriteTo.Console()
             .CreateLogger();
Log.Logger.Information("start");


void ConsoleExporter()
{
    
    Dotter.Exporter = new ConsoleExporter(10); // FileExporter --> ./metrics.log

    int index = 1;
    while (!Console.KeyAvailable)
    {
        Console.Write($"\r{index}");
        var success = index % 2 == 0;
        Dotter.Dot(TimeSpan.FromMilliseconds(index++), success, "Great", "SayHi");
        Thread.Sleep(1000);
    }
}


void PrometheusExporter()
{
    // 不声明，默认是使用 Prometheus 
    // Dotter.Exporter = new PrometheusDotExporter();

    int index = 1;
    while (!Console.KeyAvailable)
    {
        var success = index % 2 == 0;
        Dotter.Dot(TimeSpan.FromMilliseconds(index++), success);
        Thread.Sleep(1000);
    }
}


ConsoleExporter();
// PrometheusExporter();

Dotter.Dispose();

Console.ReadLine();