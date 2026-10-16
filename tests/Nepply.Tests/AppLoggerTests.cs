using Nepply.Services;

internal static class AppLoggerTests
{
    internal static void Run(Action<string,bool> check)
    {
        check("Oversized log text is bounded with an explicit marker",AppLogger.LimitText(new string('x',100000)).Length<=AppLogger.MaximumEntryCharacters
            && AppLogger.LimitText(new string('x',100000)).Contains("truncated"));
        check("Ordinary log text remains unchanged",AppLogger.LimitText("fixture")=="fixture");
        int caller=Environment.CurrentManagedThreadId,formatter=0;
        using var formatted=new ManualResetEventSlim();
        AppLogger.Error(new FormattingException(()=>{formatter=Environment.CurrentManagedThreadId;formatted.Set();}),"Formatting fixture");
        check("Error formatting stays off telemetry caller thread",formatted.Wait(4000) && formatter!=caller);
        AppLogger.Error(new FormattingException(()=>throw new InvalidOperationException("Injected formatter failure")),"Bad formatter fixture");
        using var next=new ManualResetEventSlim();
        AppLogger.Error(new FormattingException(()=>next.Set()),"After bad formatter fixture");
        check("Bad exception formatter does not stop log consumer",next.Wait(4000));
    }
    private sealed class FormattingException(Action format) : Exception
    {
        public override string ToString() { format(); return "Test diagnostic"; }
    }
}
