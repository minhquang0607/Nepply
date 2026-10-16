using Nepply.Models;
using Nepply.Modules.Desktop;
using Nepply.Services;

internal static class DesktopCatalogStoreTests
{
    internal static void Run(Action<string,bool> check)
    {
        var store = new DesktopCatalogStore();
        var empty = store.Read();
        var first = new DesktopIcon { Path = "a", Name = "A" };
        check("Catalog publishes initial reconciliation",store.Reconcile([first],DesktopSnapshot.Reconcile));
        var initial = store.Read();
        check("Catalog reads reuse collection without copying",ReferenceEquals(initial,store.Read()) && empty.Count==0);
        check("Unchanged reconciliation retains collection and item identity",!store.Reconcile([new DesktopIcon {Path="a",Name="A"}],DesktopSnapshot.Reconcile)
            && ReferenceEquals(initial,store.Read()) && ReferenceEquals(initial[0],first));
        bool readOnly = false;
        try { ((IList<DesktopIcon>)initial).Add(first); } catch(NotSupportedException) { readOnly=true; }
        check("Catalog consumer cannot mutate membership",readOnly);
        check("Duplicate incremental addition is ignored",!store.Add(new DesktopIcon {Path="A",Name="duplicate"}) && ReferenceEquals(initial,store.Read()));
        store.Add(new DesktopIcon {Path="b",Name="B"});
        var added = store.Read();
        check("Addition preserves previously published collection",initial.Count==1 && added.Count==2);
        check("Missing removal preserves collection identity",!store.Remove("missing") && ReferenceEquals(added,store.Read()));
        check("Removal publishes new membership without mutating old snapshot",store.Remove("A") && store.Read().Count==1 && added.Count==2);
        var beforeFailure=store.Read();
        try { store.Reconcile([],(_,_)=>throw new InvalidOperationException("Fixture reconciliation failure")); } catch(InvalidOperationException) { }
        check("Reconciliation failure retains last complete snapshot",ReferenceEquals(beforeFailure,store.Read()));
        var concurrent=new DesktopCatalogStore();
        Parallel.For(0,100,i=>concurrent.Add(new DesktopIcon {Path=i.ToString(),Name=i.ToString()}));
        check("Concurrent additions serialize without losing membership",concurrent.Read().Count==100);
    }
}
