using Prometheus;

namespace Simkit;

internal sealed class SimulatedTimeMetrics
{
    internal SimulatedTimeMetrics(IMetricFactory metricFactory)
    {
        ElapsedTicks = metricFactory.CreateCounter("simkit_time_elapsed_ticks_total", "Number of simulation ticks that have elapsed on the simulated timeline.");
        ElapsedTime = metricFactory.CreateCounter("simkit_time_elapsed_seconds_total", "Number of seconds that have elapsed on the simulated timeline.");
        SimulatedTimestamp = metricFactory.CreateGauge("simkit_unixtime_seconds", "Current timestamp on the simulated timeline, in Unix seconds.");

        DelaysTotal = metricFactory.CreateCounter("simkit_time_delays_total", "Number of Task-based delays that have been requested from the simulated timeline.");
        DelaysCurrent = metricFactory.CreateGauge("simkit_time_delays_pending", "Number of Task-based delays that are waiting for the right moment to arrive on the simulated timeline.");

        SynchronousTimersTotal = metricFactory.CreateCounter("simkit_time_synchronous_timers_total", "Number of synchronous timers that have ever been registered for the simulated timeline.");
        AsynchronousTimersTotal = metricFactory.CreateCounter("simkit_time_asynchronous_timers_total", "Number of asynchronous timers that have ever been registered for the simulated timeline.");
        SynchronousTimersCurrent = metricFactory.CreateGauge("simkit_time_synchronous_timers_current", "Number of synchronous timers currently registered for the simulated timeline.");
        AsynchronousTimersCurrent = metricFactory.CreateGauge("simkit_time_asynchronous_timers_current", "Number of asynchronous timers currently registered for the simulated timeline.");
        TimerCallbacksTotal = metricFactory.CreateCounter("simkit_time_timer_callback_total", "Number of times that timer callbacks have been called for simulated timeline timers.");
    }

    internal Counter ElapsedTicks { get; }
    internal Counter ElapsedTime { get; }
    internal Gauge SimulatedTimestamp { get; }

    internal Counter DelaysTotal { get; }
    internal Gauge DelaysCurrent { get; }

    internal Counter SynchronousTimersTotal { get; }
    internal Counter AsynchronousTimersTotal { get; }
    internal Gauge SynchronousTimersCurrent { get; }
    internal Gauge AsynchronousTimersCurrent { get; }
    internal Counter TimerCallbacksTotal { get; }
}
