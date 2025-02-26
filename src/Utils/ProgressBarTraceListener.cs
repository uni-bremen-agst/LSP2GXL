using System.Diagnostics;
using Essential.Diagnostics;
using ShellProgressBar;

namespace LSP2GXL.Utils;

public class ProgressBarTraceListener(ProgressBar progressBar) : TraceListenerBase
{
    private ProgressBar ProgressBar { get; } = progressBar;

    public bool AnyErrors { get; private set; } = false;

    protected override void WriteTrace(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message, Guid? relatedActivityId, object[] data)
    {
        if (eventType is TraceEventType.Error or TraceEventType.Critical)
        {
            AnyErrors = true;
            ProgressBar.WriteErrorLine($"{eventType}: {message}");
        }
        else
        {
            ProgressBar.WriteLine($"{eventType}: {message}");
        }
    }
}
