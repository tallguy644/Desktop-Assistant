# Bundled calendar libraries

These unmodified assemblies are distributed beside Skynet.exe for .NET Framework 4.8 compatibility. Packages were downloaded from the official NuGet v3 flat-container service and pinned to these versions. Keep the Licenses folder with redistributed builds.

| Package | Version | License |
| --- | --- | --- |
| [Ical.Net](https://www.nuget.org/packages/Ical.Net/5.2.3) | 5.2.3 | MIT; see Licenses/Ical.Net-MIT.md |
| [NodaTime](https://www.nuget.org/packages/NodaTime/3.2.2) | 3.2.2 | Apache-2.0; see Licenses/NodaTime-Apache-2.0.txt |
| [Portable.System.DateTimeOnly](https://www.nuget.org/packages/Portable.System.DateTimeOnly/9.0.0) | 9.0.0 | MIT; see Licenses/DateTimeOnly-MIT.txt |
| [System.Memory](https://www.nuget.org/packages/System.Memory/4.6.0) | 4.6.0 | Microsoft MIT |
| [System.Buffers](https://www.nuget.org/packages/System.Buffers/4.6.0) | 4.6.0 | Microsoft MIT |
| [System.Numerics.Vectors](https://www.nuget.org/packages/System.Numerics.Vectors/4.6.0) | 4.6.0 | Microsoft MIT |
| [System.Runtime.CompilerServices.Unsafe](https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.1.0) | 6.1.0 | Microsoft MIT |

Microsoft's MIT license and the runtime package's third-party notices are included in Licenses/Microsoft-MIT.txt and Licenses/Microsoft-Third-Party-Notices.txt. Ical.Net's license includes its upstream notices. NodaTime copyright: Jon Skeet and contributors. Portable.System.DateTimeOnly copyright: Oleg Rakhmatulin. Microsoft libraries copyright: Microsoft Corporation.

Skynet.exe.config redirects the Unsafe assembly to the included 6.0.1.0 assembly from package 6.1.0, satisfying the bundled libraries' requirements. Package versions and assembly versions are distinct.

## Embedded browser

Microsoft.Web.WebView2 SDK **1.0.4258.31** (official NuGet package), Microsoft.Web.WebView2.Core.dll, Microsoft.Web.WebView2.Wpf.dll, and the Windows x64 WebView2Loader.dll are included under Microsoft's SDK terms in **Licenses/WebView2-LICENSE.txt**. WebView2 Runtime is installed separately from Microsoft; the runtime itself is not redistributed in this package. See [the official package](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31).
