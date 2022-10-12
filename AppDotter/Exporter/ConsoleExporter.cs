using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using static System.Collections.Specialized.BitVector32;

namespace AppDotter.Exporter
{

    /// <summary>
    /// 每60秒打点一次 到 控制台
    /// </summary>
    public class ConsoleExporter : DotExporterBase, IDisposable
    {
        private Timer _timer;
        private int intervalSedonds;

        /// <summary>
        /// 被依赖服务的打点数据
        /// </summary>
        private ConcurrentDictionary<string, CounterInfo> ServiceCounter
            = new ConcurrentDictionary<string, CounterInfo>();

        public ConsoleExporter(int intervalSeconds = 60)
        {
            this.intervalSedonds = intervalSeconds; 
            _timer = new Timer(TimerCallback);
            _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
        }

        private void TimerCallback(object? state)
        {
            CalcService();
        }


        private void CalcService()
        {
            foreach (var counterInfo in ServiceCounter.Values)
            {
                var metrics = counterInfo.GetMetrics(intervalSedonds);

                _ = Write(metrics);
            }

        }

        protected virtual Task Write(List<string>? metrics)
        {
            if (metrics == null)
            {
                return Task.CompletedTask;
            }
            foreach (var metric in metrics)
            {
                //Log.Logger.Information($"metric:");
                Console.WriteLine(metric);
            }
            return Task.CompletedTask;
        }


        public override void Dot(
            TimeSpan duration,
            string callServiceName,
            string callMethodName,
            bool success, Dictionary<string, string>? labels)
        {
            if (!ServiceCounter.ContainsKey(callServiceName))
            {
                ServiceCounter[callServiceName] = new CounterInfo(callServiceName, labels ?? new Dictionary<string, string>());
            }
            ServiceCounter[callServiceName].Count(success, duration);


            var methodName = $"{callServiceName}_{callMethodName}";

            if (!ServiceCounter.ContainsKey(methodName))
            {
                ServiceCounter[methodName] = new CounterInfo(methodName, labels ?? new Dictionary<string, string>());
            }
            ServiceCounter[methodName].Count(success, duration);
        }

        protected override void InnerDispose()
        {
            _timer.Change(-1, -1);
            _timer.Dispose();
        }
    }


    /// <summary>
    /// 每60秒打点一次 到 "./metrics.log"
    /// </summary>
    public class FileExporter : ConsoleExporter
    {
        private readonly StreamWriter streamWriter;
        public FileExporter(int intervalSeconds = 60) :base(intervalSeconds)
        {
            streamWriter = new StreamWriter("./metrics.log");
        }

        protected override async Task Write(List<string>? metrics)
        {
            if(metrics == null)
            {
                return;
            }
            List<Task> tasks = new List<Task>();

            foreach (var item in metrics)
            {
                tasks.Add(
                    streamWriter.WriteLineAsync(item)
                );
            }

            await Task.WhenAll(tasks);
        }


        protected override void InnerDispose()
        {
            streamWriter?.Dispose();
            base.InnerDispose();
        }

    }

}
