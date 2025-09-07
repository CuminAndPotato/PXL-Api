#load "csharpToTypescriptSignatures.fsx"
open CsharpToTypescriptSignatures

open System
open System.IO

let scriptDir = __SOURCE_DIRECTORY__

TsGen.generate
    [ Path.Combine(scriptDir, "skiasharp.3.119.0/lib/net8.0/SkiaSharp.dll") ]
    (Path.Combine(scriptDir, "src/types.d.ts"))
    (Some "SkiaSharp")


// TsGen.generate
//     [ Path.Combine(scriptDir, "../../../dotnet/", "skiasharp.3.119.0/lib/net8.0/SkiaSharp.dll") ]
//     (Path.Combine(scriptDir, "src/types.d.ts"))
//     (Some "SkiaSharp")
