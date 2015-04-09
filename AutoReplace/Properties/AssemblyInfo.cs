using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("AutoReplace")]
[assembly: AssemblyTitle("AutoReplace")]
[assembly: AssemblyDescription("Replaces live data in source code files by pattern matching.")]
// autoreplace for asm © [0-9–]{4,9} to © 2015–{year}
[assembly: AssemblyCopyright("© 2015 Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly identity version. Must be a dotted-numeric version.
[assembly: AssemblyVersion("1.0")]

// Repeat for Win32 file version resource because the assembly version is expanded to 4 parts.
[assembly: AssemblyFileVersion("1.0")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]

// Version history:
//
// 1.0 (xxxx-xx-xx)
// * Created project
