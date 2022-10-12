namespace AppDotter.Exporter
{
    public interface IDotExporter : IDisposable
    {
        public void Dot(
           TimeSpan duration,
           string callServiceName, 
           string callMethodName,
           bool success,
           Dictionary<string, string>? labels);
    }
}
