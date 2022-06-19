using OpenTelemetry.Metrics;
using OpenTelemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace AppDotter.Exporter
{
    public class PrometheusDotExporter : DotExporterBase, IDisposable
    {

        private MeterProvider meterProvider;
        private ConcurrentDictionary<string, DotterInfo> DotterInfoDict = new ConcurrentDictionary<string, DotterInfo>();

        public PrometheusDotExporter()
        {
            meterProvider = Sdk.CreateMeterProviderBuilder()
               .AddMeter("*")
               .AddPrometheusExporter(opt =>
               {
                   opt.StartHttpListener = true;
                   opt.HttpListenerPrefixes =
                   new string[]
                   {
                       $"http://localhost:9184/"
                   };
               })
               .Build();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="calleeServiceName">rpc : className ; http : host</param>
        /// <param name="calleeMethodName">rpc : methodName ; http : urlPath</param>
        /// <param name="success"></param>
        public override void Dot(
             TimeSpan duration,
             string calledServiceName,
             string calledMethodName = "",
             bool success = true)
        {
            string key = DotterInfo.GetMethodMeterName(calledServiceName, calledMethodName);

            DotterInfo dotterInfo;
            if (!DotterInfoDict.ContainsKey(key))
            {
                dotterInfo = new DotterInfo(calledServiceName, calledMethodName);
                DotterInfoDict[key] = dotterInfo;
            }
            else
            {
                dotterInfo = DotterInfoDict[key];
            }
            dotterInfo.DotCounter(success);
            dotterInfo.DotPxx(duration);
        }

        protected override void InnerDispose()
        {
            base.Dispose();
            meterProvider?.Dispose();
        }
    }
}
