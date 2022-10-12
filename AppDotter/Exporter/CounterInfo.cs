using System.Collections.Concurrent;

namespace AppDotter.Exporter
{
    public class CounterInfo
    {
        public string Name { get; private set; }
        public Dictionary<string, string> Labels { get; private set; }
        public CounterInfo(string name, Dictionary<string, string> labels)
        {
            Name = name;
            Labels = labels;
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


        public List<string> GetMetrics(int seconds)
        {

            // # HELP http_request_duration_seconds The duration of HTTP requests processed by an ASP.NET Core application.
            // # TYPE http_request_duration_seconds histogram
            // http_request_duration_seconds_sum{code="200",method="GET",controller="WeatherForecast",action="Get",endpoint="WeatherForecast"} 0.2046416
            // http_request_duration_seconds_count{code="200",method="GET",controller="WeatherForecast",action="Get",endpoint="WeatherForecast"} 14
            // http_request_duration_seconds_bucket{code="200",method="GET",controller="WeatherForecast",action="Get",endpoint="WeatherForecast",le="0.008"} 0
            // http_request_duration_seconds_bucket{code="200",method="GET",controller="WeatherForecast",action="Get",endpoint="WeatherForecast",le="0.016"} 13

            // http_request_duration_seconds{ code="200",service="WeatherForecast",method="Get",quantile="0.5"} 0.1

            // 第一行表示这个 metrics 对应的 description，大概介绍
            // 第二行表示这个 metrics 对应的类型
            // 第三行后面的表示 metrics 的数据  metrics_name{tags} value

            string labels = GetLabels();

            List<string> result = new List<string>(32);
            long ms = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            lock (this)
            {
                var sla = this.CalcAvailability();
                result.Add($"availability_{seconds}sec{{{labels}}} {sla:0.0000}");
                var total = this.Total;
                var failed = this.Failed;
                result.Add($"total_{seconds}sec_count{{{labels}}} {total}");
                result.Add($"success_{seconds}sec_count{{{labels}}} {total - failed}");
                result.Add($"failed_{seconds}sec_count{{{labels}}} {failed}");

                var totalCps = total / ((double)seconds);
                var successCps = (total - failed) / ((double)seconds);
                var failedCps = failed / ((double)seconds);

                result.Add($"cps_total_{seconds}sec{{{labels}}} {totalCps:0.00}");
                result.Add($"cps_success_{seconds}sec{{{labels}}} {successCps:0.00}");
                result.Add($"cps_failed _{seconds}sec{{{labels}}} {failedCps:0.00}");

                var pxxTotal = this.PxxTotal();
                var pxxFailed = this.PxxFailed();
                var pxxSuccess = this.PxxSuccess();
                result.AddRange(GetPxx(ms, $"total_{seconds}sec", pxxTotal));
                result.AddRange(GetPxx(ms, $"success_{seconds}sec", pxxSuccess));
                result.AddRange(GetPxx(ms, $"failed_{seconds}sec", pxxFailed));
            }


            return result;
        }

        private List<string> GetPxx(long ms, string type, (double P99, double P95, double P90, double P75, double P50) pxx)
        {
            string labels = GetLabels();

            return new List<string>
            {
                $"p99_{type}{{{labels}}} {pxx.P99 :0.00}",
                $"p95_{type}{{{labels}}} {pxx.P95 :0.00}",
                $"p90_{type}{{{labels}}} {pxx.P90 :0.00}",
                $"p75_{type}{{{labels}}} {pxx.P75 :0.00}",
                $"p50_{type}{{{labels}}} {pxx.P50 :0.00}",
            };
        }


        private string? _labels = null;
        private string GetLabels()
        {
            if (_labels == null)
            {
                _labels = $"name=\"{Name}\"";
                if (Labels.Count > 0)
                {
                    _labels = _labels + "," + string.Join(",", Labels.Select(x => $"{x.Key}=\"{x.Value}\""));
                }

            }
            return _labels;
        }
    }

}
