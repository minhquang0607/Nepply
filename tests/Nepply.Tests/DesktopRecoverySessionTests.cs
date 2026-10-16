using Nepply.Services;

internal static class DesktopRecoverySessionTests
{
    internal static void Run(Action<string, bool> check)
    {
        Guid generation=Guid.NewGuid(), stale=Guid.NewGuid();
        bool? visible=true;
        int shows=0;
        bool showSucceeds=true, showChangesState=true, throwOnShow=false;
        var session=new DesktopRecoverySession(generation,()=>visible,()=>
        {
            shows++;
            if(throwOnShow) throw new InvalidOperationException("Injected native failure");
            if(showSucceeds && showChangesState) visible=true;
            return showSucceeds;
        });
        check("Guardian cannot recover before arm",!session.Recover(generation) && shows==0);
        check("Guardian rejects foreign generation arm",!session.Arm(stale) && !session.IsArmed);
        visible=false;
        check("Guardian never claims pre-hidden Desktop",!session.Arm(generation) && !session.IsArmed);
        visible=null;
        check("Guardian rejects unavailable Desktop",!session.Arm(generation));
        visible=true;
        check("Guardian arms visible Desktop",session.Arm(generation) && session.IsArmed);
        check("Guardian duplicate arm rejected",!session.Arm(generation));
        visible=false; // Owner hid Desktop. No journal is needed by this state machine.
        check("Guardian rejects stale disarm",!session.Disarm(stale) && session.IsArmed);
        check("Guardian rejects premature disarm",!session.Disarm(generation) && session.IsArmed);
        check("Guardian rejects stale recovery",!session.Recover(stale) && shows==0);
        visible=null;
        check("Guardian retains authority when Explorer unavailable",!session.Recover(generation) && session.IsArmed && shows==0);
        visible=false;showSucceeds=false;
        check("Guardian retains authority after show fails",!session.Recover(generation) && session.IsArmed);
        showSucceeds=true;showChangesState=false;
        check("Guardian verifies actual visibility after native success",!session.Recover(generation) && session.IsArmed);
        throwOnShow=true;
        bool threw=false;
        try{session.Recover(generation);}catch(InvalidOperationException){threw=true;}
        check("Guardian exception preserves recovery authority",threw && session.IsArmed);
        throwOnShow=false;showChangesState=true;
        check("Guardian restores without journal",session.Recover(generation) && visible==true && !session.IsArmed);
        int completedShows=shows;
        check("Guardian recovery idempotent",session.Recover(generation) && shows==completedShows);
        check("Guardian completed generation never rearms",!session.Arm(generation));

        var normal=new DesktopRecoverySession(generation,()=>visible,()=>{shows++;visible=true;return true;});
        check("Guardian normal session arms",normal.Arm(generation));
        check("Guardian disarms only after confirmed restore",normal.Disarm(generation) && !normal.IsArmed);
        visible=false; // A later user action must not be undone by the old completed session.
        check("Guardian completed session does not override later user action",normal.Recover(generation) && visible==false && shows==completedShows);

        visible=true;
        var concurrent=new DesktopRecoverySession(generation,()=>visible,()=>{Interlocked.Increment(ref shows);visible=true;return true;});
        concurrent.Arm(generation);visible=false;
        Parallel.For(0,32,_=>concurrent.Recover(generation));
        check("Guardian concurrent recovery performs one native show",shows==completedShows+1 && visible==true && !concurrent.IsArmed);
    }
}
