using OpenTelemetry.Metrics;
using OpenTelemetry;
using System.Collections.Concurrent;
using System.Reflection;
using AppDotter.Exporter;

namespace AppDotter
{
    public class Dotter
    {
        private static IDotExporter? _exporter;

        public static IDotExporter Exporter
        {
            get
            {
                return _exporter ??= new PrometheusDotExporter();
            }
            set
            {
                _exporter = value;
            }
        }


        public static void Dot(
             TimeSpan duration,
             string calledServiceName,
             string calledMethodName = "",
             bool success = true)
        {

            Exporter.Dot(duration, calledServiceName, calledMethodName, success);
        }


        public static void Dispose()
        {
            Exporter.Dispose();
        }
    }
}