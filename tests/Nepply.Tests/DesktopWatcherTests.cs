using System.IO;
using Nepply.Services;

internal static class DesktopWatcherTests
{
    public static void Run(string root, Action<string,bool> check)
    {
        // Only touch uniquely named fixture directories under the test output.
        var original=Path.Combine(root,"desktop-watch");
        var moved=Path.Combine(root,"desktop-moved");
        var replacement=Path.Combine(root,"desktop-new");
        Directory.CreateDirectory(original);
        string current=original;
        using var signaled=new AutoResetEvent(false);
        long now=0;
        using var watcher=new DesktopWatcherService(()=>signaled.Set(),()=>[current,current.ToUpperInvariant()],clock:()=>now);
        watcher.ReconcileAsync().GetAwaiter().GetResult();
        check("Watcher normalizes duplicate folder paths",watcher.WatchedCount==1);
        check("Watcher requests initial reconciliation",signaled.WaitOne(3000));
        watcher.ReconcileAsync().GetAwaiter().GetResult();
        check("Healthy watcher does not rescan unchanged Desktop",!signaled.WaitOne(650));
        now=300001;
        watcher.ReconcileAsync().GetAwaiter().GetResult();
        check("Slow repair still reconciles missed notifications",signaled.WaitOne(3000));
        watcher.ReconcileAsync().GetAwaiter().GetResult();
        check("Completed scan resets repair deadline",!signaled.WaitOne(650));
        File.WriteAllText(Path.Combine(original,"first.txt"),"fixture");
        check("Watcher receives real filesystem changes",signaled.WaitOne(3000));
        Directory.Move(original,moved);
        watcher.ReconcileAsync().GetAwaiter().GetResult();
        check("Watcher detaches unavailable directory",watcher.WatchedCount==0);
        Directory.CreateDirectory(original);
        watcher.ReconcileAsync().GetAwaiter().GetResult();
        check("Watcher reconnects recreated directory",watcher.WatchedCount==1);
        signaled.WaitOne(3000);
        File.WriteAllText(Path.Combine(original,"recreated.txt"),"fixture");
        check("Recreated Desktop receives new file events",signaled.WaitOne(3000));
        Directory.CreateDirectory(replacement); current=replacement;
        watcher.ReconcileAsync().GetAwaiter().GetResult();
        check("Watcher follows changed Desktop location",watcher.WatchedCount==1 && signaled.WaitOne(3000));
        File.WriteAllText(Path.Combine(replacement,"relocated.txt"),"fixture");
        check("Relocated Desktop receives new file events",signaled.WaitOne(3000));
        watcher.Dispose();
        while(signaled.WaitOne(0)) { }
        File.WriteAllText(Path.Combine(replacement,"after-dispose.txt"),"fixture");
        watcher.ReconcileAsync().GetAwaiter().GetResult();
        check("Disposed watcher stays detached and silent",watcher.WatchedCount==0 && !signaled.WaitOne(600));
        var lateFolder=Path.Combine(root,"desktop-created-later");
        using var recovered=new AutoResetEvent(false);
        using var periodic=new DesktopWatcherService(()=>recovered.Set(),()=>[lateFolder],100);
        periodic.ReconcileAsync().GetAwaiter().GetResult();
        check("Missing folder initially has no watcher",periodic.WatchedCount==0);
        recovered.WaitOne(3000); // Drain the initial catalog refresh before recovery.
        Directory.CreateDirectory(lateFolder);
        check("Periodic recovery discovers newly available folder",recovered.WaitOne(3000) && periodic.WatchedCount==1);
        periodic.Dispose(); periodic.Completion.GetAwaiter().GetResult();
        BlockedIo(root,check,blockFactory:false);
        BlockedIo(root,check,blockFactory:true);
        BatchEvents(root,check);
    }

    private static void BatchEvents(string root,Action<string,bool> check)
    {
        var path=Path.Combine(root,"watch-batches");Directory.CreateDirectory(path);
        using var ready=new AutoResetEvent(false);
        var batches=new System.Collections.Concurrent.ConcurrentQueue<string[]?>();
        long now=0;
        var watcher=new DesktopWatcherService(()=>{},()=>[path],clock:()=>now,scanChanges:paths=>{batches.Enqueue(paths);ready.Set();});
        try
        {
            watcher.ReconcileAsync().GetAwaiter().GetResult();
            check("Batch watcher begins with full baseline",ready.WaitOne(3000) && batches.TryDequeue(out var initial) && initial==null);
            string file=Path.Combine(path,"first.txt");File.WriteAllText(file,"fixture");
            check("Real created event retains changed path",ready.WaitOne(3000) && batches.TryDequeue(out var created) && created?.Contains(file)==true);
            string renamed=Path.Combine(path,"đổi 日本語.txt");File.Move(file,renamed);
            check("Real rename batch retains old and new path",ready.WaitOne(3000) && batches.TryDequeue(out var changed)
                && changed?.Contains(file)==true && changed.Contains(renamed));
            now=299900;watcher.RequestChanges([renamed]);
            check("Partial activity before repair deadline stays incremental",ready.WaitOne(3000) && batches.TryDequeue(out var recent) && recent!=null);
            now=300001;watcher.ReconcileAsync().GetAwaiter().GetResult();
            check("Recent partial activity cannot starve periodic full repair",ready.WaitOne(3000) && batches.TryDequeue(out var periodic) && periodic==null);
            typeof(DesktopWatcherService).GetMethod("Error",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
                .Invoke(watcher,[watcher,new ErrorEventArgs(new InternalBufferOverflowException("Fixture overflow"))]);
            check("Watcher overflow invalidates partial batch",ready.WaitOne(3000) && batches.TryDequeue(out var repair) && repair==null);
        }
        finally {watcher.Dispose();watcher.Completion.GetAwaiter().GetResult();}
    }

    private static void BlockedIo(string root,Action<string,bool> check,bool blockFactory)
    {
        string path=Path.Combine(root,blockFactory ? "watch-late-create" : "watch-slow-probe");
        Directory.CreateDirectory(path);
        using var entered=new ManualResetEventSlim();
        using var release=new ManualResetEventSlim();
        int calls=0,refreshes=0;
        FileSystemWatcher? created=null;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var watcher=new DesktopWatcherService(()=>Interlocked.Increment(ref refreshes),()=>[path],
            probe:folder=>
            {
                if(!blockFactory) {Interlocked.Increment(ref calls);entered.Set();release.Wait();}
                return Directory.GetCreationTimeUtc(folder);
            },createWatcher:folder=>
            {
                created=new FileSystemWatcher(folder);
                if(blockFactory) {Interlocked.Increment(ref calls);entered.Set();release.Wait();}
                return created;
            });
        try
        {
            // The injected I/O blocks until released, so any wait would never finish; 2 s only absorbs slow runners.
            check($"Watcher constructor never waits for blocked I/O ({blockFactory})",timer.ElapsedMilliseconds<2000);
            check($"Watcher starts probe off caller thread ({blockFactory})",entered.Wait(3000));
            var pending=watcher.ReconcileAsync();
            for(int i=0;i<100;i++) watcher.ReconcileAsync();
            check($"Reconciliation burst remains single-flight ({blockFactory})",calls==1 && !pending.IsCompleted);
            timer.Restart(); watcher.RequestRefresh(); watcher.Dispose();
            check($"Refresh and shutdown do not wait for blocked I/O ({blockFactory})",timer.ElapsedMilliseconds<2000);
            check($"Disposed watcher publishes no subscriptions ({blockFactory})",watcher.WatchedCount==0);
        }
        finally { release.Set(); watcher.Dispose(); }
        check($"Late worker cleanup finishes ({blockFactory})",watcher.Completion.Wait(3000));
        check($"Disposed worker sends no refresh ({blockFactory})",refreshes==0);
        if(created!=null)
        {
            bool disposed=false;
            try {created.EnableRaisingEvents=true;} catch(ObjectDisposedException) {disposed=true;}
            check("Watcher created after shutdown is released",disposed);
        }
    }
}
