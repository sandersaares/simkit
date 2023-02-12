using Prometheus;

namespace Simkit;

internal sealed class SimulatedTimeMetrics
{
    internal SimulatedTimeMetrics(IMetricFactory metricFactory)
    {
        ElapsedTicks = metricFactory.CreateCounter("simkit_time_elapsed_ticks_total", "Number of simulation ticks that have elapsed on the simulated timeline.");
        ElapsedTime = metricFactory.CreateCounter("simkit_time_elapsed_seconds_total", "Number of seconds that have elapsed on the simulated timeline.");
        SimulatedTimestamp = metricFactory.CreateGauge("simkit_unixtime_seconds", "Current timestamp on the simulated timeline, in Unix seconds.");

        DelaysTotal = metricFactory.CreateCounter("simkit_time_delays_total", "Number of delays that have been requested from the simulated timeline.");
        DelaysCurrent = metricFactory.CreateGauge("simkit_time_delays_pending", "Number of delays that are waiting for the right moment to arrive on the simulated timeline.");

        TimersTotal = metricFactory.CreateCounter("simkit_time_timers_total", "Number of timers that have ever been registered for the simulated timeline.");
        TimersCurrent = metricFactory.CreateGauge("simkit_time_timers_current", "Number of timers currently registered for the simulated timeline.");
        TimerCallbacksTotal = metricFactory.CreateCounter("simkit_time_timer_callback_total", "Number of times that timer callbacks have been called for simulated timeline timers.");
    }

    internal Counter ElapsedTicks { get; }
    internal Counter ElapsedTime { get; }
    internal Gauge SimulatedTimestamp { get; }

    internal Counter DelaysTotal { get; }
    internal Gauge DelaysCurrent { get; }

    internal Counter TimersTotal { get; }
    internal Gauge TimersCurrent { get; }
    internal Counter TimerCallbacksTotal { get; }
}
