using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDotter
{
    public interface IDotterExport
    {

    }

    public class DotterExport
    {
        public void Export(Instrument instrument, MeterListener listener)
        {
            if (instrument.Meter.Name == "HatCo.HatStore")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        }

        static void OnMeasurementRecorded<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
        {
            Console.WriteLine($"{instrument.Name} recorded measurement {measurement}");
        }
    }
}
