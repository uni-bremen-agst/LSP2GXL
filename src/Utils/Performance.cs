using System.Diagnostics;
using System.Globalization;

namespace LSP2GXL.Utils;

/// <summary>
/// Allows us to measure and emit the elapsed time for long-running actions.
///
/// Example use:
///
///   Performance p = Performance.Begin("loading graph data");
///   ... do something
///   p.End();
/// </summary>
public class Performance
{
    private Performance(string action, Stopwatch sw, string? outputPath = null)
    {
        this.action = action;
        this.outputPath = outputPath;
        stopWatch = sw;
    }

    private readonly Stopwatch stopWatch;

    private readonly string action;

    private readonly string? outputPath;

    private double totalTimeInMilliSeconds;

    /// <summary>
    /// Returns a new performance time stamp and emits given action.
    /// </summary>
    /// <param name="action">name of action started to be printed</param>
    /// <returns></returns>
    public static Performance Begin(string action, string? outputPath = null)
    {
        Stopwatch sw = new();
        Performance result = new(action, sw, outputPath);
        sw.Start();
        return result;
    }

    /// <summary>
    /// Emits the elapsed time from the start of the performance time span
    /// until now. Reports it to Debug.Log along with the action name.
    /// </summary>
    /// <param name="print">if true, the elapsed time will be printed</param>
    public void End(bool print = false)
    {
        stopWatch.Stop();
        TimeSpan ts = stopWatch.Elapsed;
        totalTimeInMilliSeconds = ts.TotalMilliseconds;
        if (outputPath != null)
        {
            WriteToCsv();
        }
        if (print)
        {
            Trace.TraceInformation($"Action {action} finished in {totalTimeInMilliSeconds} [h:m:s:ms] elapsed time).\n");
        }
    }

    private void WriteToCsv()
    {
        File.AppendAllText(outputPath!, $"{action},{totalTimeInMilliSeconds.ToString(CultureInfo.InvariantCulture)}\n");
    }
}
