using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Nepply.Models;

namespace Nepply.Modules.Desktop;

// UI-thread only. Keep unchanged ends; large replacements publish one reset.
internal sealed class DesktopIconCollection : ObservableCollection<DesktopIcon>
{
    internal void ApplySnapshot(IReadOnlyList<DesktopIcon> next)
    {
        int prefix=0,suffix=0;
        while(prefix<Count && prefix<next.Count && ReferenceEquals(this[prefix],next[prefix])) prefix++;
        while(suffix<Count-prefix && suffix<next.Count-prefix
            && ReferenceEquals(this[Count-1-suffix],next[next.Count-1-suffix])) suffix++;
        int removed=Count-prefix-suffix,added=next.Count-prefix-suffix;
        if(removed==0 && added==0) return;
        CheckReentrancy();
        if(Math.Max(removed,added)>32)
        {
            bool countChanged=Count!=next.Count;
            Items.Clear();
            foreach(var item in next) Items.Add(item);
            if(countChanged) OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            return;
        }
        int replaced=Math.Min(removed,added);
        for(int i=0;i<replaced;i++)
            if(!ReferenceEquals(this[prefix+i],next[prefix+i])) this[prefix+i]=next[prefix+i];
        for(int i=removed-1;i>=replaced;i--) RemoveAt(prefix+i);
        for(int i=replaced;i<added;i++) Insert(prefix+i,next[prefix+i]);
    }
}
