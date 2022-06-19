using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace AppDotter.Exporter
{
    public class CounterInfo
    {
        public string Name { get; private set; }
        public CounterInfo(string name)
        {
            Name = name;

        }

        private long _total = 0;
        private long _failed = 0;

        /// <summary>
        /// 总请求数
        /// </summary>
        public long Total => _total;

        /// <summary>
        /// 失败请求数
        /// </summary>
        public long Failed => _failed;

        public ConcurrentBag<double> TotalDurationMs { get; private set; } = new ConcurrentBag<double>();

        public ConcurrentBag<double> SuccessDurationMs { get; private set; } = new ConcurrentBag<double>();

        public ConcurrentBag<double> FailedDurationMs { get; private set; } = new ConcurrentBag<double>();


        public void Count(bool success, TimeSpan timeSpan)
        {
            Interlocked.Increment(ref _total);
            double durationMs = timeSpan.TotalMilliseconds;
            TotalDurationMs.Add(durationMs);
            if (success)
            {
                SuccessDurationMs.Add(durationMs);
            }
            else
            {
                Interlocked.Increment(ref _failed);
                FailedDurationMs.Add(durationMs);
            }
        }

        public void Reset()
        {
            _total = 0;
            _failed = 0;
            TotalDurationMs.Clear();
            SuccessDurationMs.Clear();
            FailedDurationMs.Clear();
        }

        #region pxx

        public (double P99, double P95, double P90, double P75, double P50) PxxTotal()
        {
            double[] sequence = TotalDurationMs.ToArray();
            if (sequence.Length == 0)
            {
                return (0, 0, 0, 0, 0);
            }
            Array.Sort(sequence);
            return (
                Percentile(sequence, 0.99),
                Percentile(sequence, 0.95),
                Percentile(sequence, 0.90),
                Percentile(sequence, 0.75),
                Percentile(sequence, 0.50)
                );
        }

        public (double P99, double P95, double P90, double P75, double P50) PxxSuccess()
        {
            double[] sequence = SuccessDurationMs.ToArray();
            if (sequence.Length == 0)
            {
                return (0, 0, 0, 0, 0);
            }
            Array.Sort(sequence);
            return (
                Percentile(sequence, 0.99),
                Percentile(sequence, 0.95),
                Percentile(sequence, 0.90),
                Percentile(sequence, 0.75),
                Percentile(sequence, 0.50)
                );
        }

        public (double P99, double P95, double P90, double P75, double P50) PxxFailed()
        {
            double[] sequence = FailedDurationMs.ToArray();
            if (sequence.Length == 0)
            {
                return (0, 0, 0, 0, 0);
            }

            Array.Sort(sequence);
            return (
                Percentile(sequence, 0.99),
                Percentile(sequence, 0.95),
                Percentile(sequence, 0.90),
                Percentile(sequence, 0.75),
                Percentile(sequence, 0.50)
                );
        }


        #endregion


        /// <summary>
        /// 实际计算的是SLA (返回0-1)
        /// </summary>
        /// <param name="counterStep"></param>
        /// <returns></returns>
        public double CalcAvailability()
        {
            return (Total - Failed) / (double)Total;
        }


        private double Percentile(double[] sequence, double excelPercentile)
        {
            int length = sequence.Length;
            double n = (length - 1) * excelPercentile + 1;
            // Another method: double n = (N + 1) * excelPercentile;
            if (n == 1d)
            {
                return sequence[0];
            }
            else if (n == length)
            {
                return sequence[length - 1];
            }
            else
            {
                int k = (int)n;
                double d = n - k;
                return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
            }
        }


        public List<string> GetMetrics()
        {
            List<string> result = new List<string>(32);
            long ms = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            lock (this)
            {
                var sla = this.CalcAvailability();
                result.Add($"{Name}_availability_1min{{}}@{ms}=>{sla:0.0000}");
                var total = this.Total;
                var failed = this.Failed;
                result.Add($"{Name}_total_1min_count{{}}@{ms}=>{total}");
                result.Add($"{Name}_success_1min_count{{}}@{ms}=>{total - failed}");
                result.Add($"{Name}_failed _1min_count{{}}@{ms}=>{failed}");

                var totalCps = total / 60.0;
                var successCps = (total - failed) / 60.0;
                var failedCps = failed / 60.0;

                result.Add($"{Name}_cps_total_1min{{}}@{ms}=>{totalCps:0.00}");
                result.Add($"{Name}_cps_success_1min{{}}@{ms}=>{successCps:0.00}");
                result.Add($"{Name}_cps_failed _1min{{}}@{ms}=>{failedCps:0.00}");

                var pxxTotal = this.PxxTotal();
                var pxxFailed = this.PxxFailed();
                var pxxSuccess = this.PxxSuccess();
                result.AddRange(GetPxx(ms, "total_1min", pxxTotal));
                result.AddRange(GetPxx(ms, "success_1min", pxxSuccess));
                result.AddRange(GetPxx(ms, "failed_1min", pxxFailed));
            }


            return result;
        }

        private List<string> GetPxx(long ms, string type, (double P99, double P95, double P90, double P75, double P50) pxx)
        {
            return new List<string>
            {
                $"{Name}_p99_{type}{{}}@{ms}=>{pxx.P99 :0.00}",
                $"{Name}_p95_{type}{{}}@{ms}=>{pxx.P95 :0.00}",
                $"{Name}_p90_{type}{{}}@{ms}=>{pxx.P90 :0.00}",
                $"{Name}_p75_{type}{{}}@{ms}=>{pxx.P75 :0.00}",
                $"{Name}_p50_{type}{{}}@{ms}=>{pxx.P50 :0.00}",
            };
        }
    }

    /// <summary>
    /// 每60秒打点一次 到 控制台
    /// </summary>
    public class ConsoleExporter : DotExporterBase, IDisposable
    {
        private Timer _timer;


        /// <summary>
        /// 被依赖服务的打点数据
        /// </summary>
        private ConcurrentDictionary<string, CounterInfo> ServiceCounter
            = new ConcurrentDictionary<string, CounterInfo>();


        /// <summary>
        /// 被依赖方法的打点数据
        /// </summary>
        private ConcurrentDictionary<string, CounterInfo> MethodAvalability
            = new ConcurrentDictionary<string, CounterInfo>();

        public ConsoleExporter()
        {
            _timer = new Timer(TimerCallback);
            _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(60));
        }

        private void TimerCallback(object? state)
        {
            CalcService();
        }


        private void CalcService()
        {
            foreach (var counterInfo in ServiceCounter.Values)
            {
                var metrics = counterInfo.GetMetrics();

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
                Log.Logger.Information($"metric:{metric}");
            }
            return Task.CompletedTask;
        }


        public override void Dot(
            TimeSpan duration,
            string callServiceName,
            string callMethodName,
            bool success)
        {
            if (!ServiceCounter.ContainsKey(callServiceName))
            {
                ServiceCounter[callServiceName] = new CounterInfo(callServiceName);
            }
            ServiceCounter[callServiceName].Count(success, duration);


            var methodName = $"{callServiceName}_{callMethodName}";

            if (!ServiceCounter.ContainsKey(methodName))
            {
                ServiceCounter[methodName] = new CounterInfo(methodName);
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
        public FileExporter()
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
