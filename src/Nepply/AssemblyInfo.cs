using System.Windows;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// First-party P/Invokes target Windows/installed system driver DLLs only.
// Third-party assemblies retain their own resolution policies.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
[assembly: InternalsVisibleTo("Nepply.Tests")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
