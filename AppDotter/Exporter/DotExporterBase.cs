using System.Collections.Concurrent;

namespace AppDotter.Exporter
{
    public abstract class DotExporterBase : IDotExporter
    {
        public void Dispose()
        {
            InnerDispose();
        }


        protected abstract void InnerDispose();

        public abstract void Dot(
            TimeSpan duration,
            string callServiceName,
            string callMethodName,
            bool success
            );

    }
}
