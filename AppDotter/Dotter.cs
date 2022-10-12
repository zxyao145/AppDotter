using OpenTelemetry.Metrics;
using OpenTelemetry;
using System.Collections.Concurrent;
using System.Reflection;
using AppDotter.Exporter;
using System.Runtime.CompilerServices;

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
             bool success = true,
             [CallerFilePath] string calledServiceName = "",
             [CallerMemberNameAttribute] string calledMethodName = "",
             [CallerLineNumber] int sourceLineNumber = 0,
             Dictionary<string, string>? labels = null)
        {
            calledServiceName = Path.GetFileNameWithoutExtension(calledServiceName);
            calledMethodName = calledMethodName + "-" + sourceLineNumber;
            Exporter.Dot(duration, calledServiceName, calledMethodName, success, labels);
        }


        public static void Dispose()
        {
            Exporter.Dispose();
        }
    }
}