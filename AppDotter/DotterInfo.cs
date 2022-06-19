using System.Diagnostics.Metrics;

namespace AppDotter
{
    public class DotterInfo : IDisposable
    {
        const string ServiceSuccessCounterName = "Success";
        const string ServiceFailedCounterName = "Failed";
        const string ServicePxxHistogramName = "Pxx";
        const string ServiceQpsGaugeName = "Qps";

        const string CounterUnit = "sec";
        const string PxxUnit = "ms";

        public bool HasMethodName { get; init; }

        public Meter ServiceMeter { get; init; }
        public Meter? MethodMeter { get; init; }

        #region 个数统计

        /// <summary>
        /// 用于统计成功的个数
        /// </summary>
        public Counter<long> ServiceSuccessCounter { get; init; }
        public Counter<long>? MethodSuccessCounter { get; init; }


        /// <summary>
        /// 用于统计失败的个数
        /// </summary>
        public Counter<long> ServiceFailedCounter { get; init; }
        public Counter<long>? MethodFailedCounter { get; init; }

        #endregion

        #region Pxx

        public Histogram<double> ServicePxxHistogram { get; init; }

        public Histogram<double>? MethodPxxHistogram { get; init; }

        #endregion


        private readonly MeterListener? MeterListener = null;

        public DotterInfo(string calledServiceName, string calledMethodName = "")
        {
            HasMethodName = !string.IsNullOrWhiteSpace(calledMethodName);

            ServiceMeter = new Meter(calledServiceName);
            ServiceSuccessCounter = ServiceMeter
                .CreateCounter<long>(GetMetricsName(calledServiceName, ServiceSuccessCounterName), CounterUnit);
            ServiceFailedCounter = ServiceMeter
                .CreateCounter<long>(GetMetricsName(calledServiceName, ServiceFailedCounterName), CounterUnit);
            // 对于涉及计时的情况，通常首选的是 Histogram。
            ServicePxxHistogram = ServiceMeter
                .CreateHistogram<double>(GetMetricsName(calledServiceName, ServicePxxHistogramName), PxxUnit);

            if (HasMethodName)
            {
                string methodMeterName = GetMethodMeterName(calledServiceName, calledMethodName);
                MethodMeter = new Meter(methodMeterName);
                MethodSuccessCounter = MethodMeter
                    .CreateCounter<long>(GetMetricsName(calledServiceName, ServiceSuccessCounterName, calledMethodName), CounterUnit);
                MethodFailedCounter = MethodMeter
                    .CreateCounter<long>(GetMetricsName(calledServiceName, methodMeterName, calledMethodName), CounterUnit);
                MethodPxxHistogram = MethodMeter
                    .CreateHistogram<double>(GetMetricsName(methodMeterName, ServicePxxHistogramName, calledMethodName), PxxUnit);

            }
        }

        private string GetMetricsName(string calledServiceName, string suffixName, string calledMethodName = "")
        {
            if (string.IsNullOrWhiteSpace(calledMethodName))
            {
                return $"{calledServiceName}_null_{suffixName}";
            }
            return $"{calledServiceName}_{calledMethodName}_{suffixName}";
        }


        ~DotterInfo()
        {
            this.Dispose();
        }

        public void DotCounter(bool success)
        {
            if (success)
            {
                ServiceSuccessCounter.Add(1);
            }
            else
            {
                ServiceFailedCounter.Add(1);
            }

            if (HasMethodName)
            {
                if (success)
                {
                    MethodSuccessCounter!.Add(1);
                }
                else
                {
                    MethodFailedCounter!.Add(1);
                }
            }
        }


        public void DotPxx(TimeSpan timeSpan)
        {
            ServicePxxHistogram.Record(timeSpan.TotalMilliseconds);

            if (HasMethodName)
            {
                MethodPxxHistogram!.Record(timeSpan.TotalMilliseconds);
            }
        }


        public static string GetMethodMeterName(string calledServiceName, string calledMethodName = "")
        {
            return calledServiceName + "_" + calledMethodName;
        }

        public void Dispose()
        {
            MeterListener?.Dispose();
        }
    }
}