module Pxl.Ui.Fonts

open System
open SkiaSharp

type BuiltinFont =
    {
        height: float
        ascent: float
        skFont: SKFont
    }

let getBuiltinTypeface fontFileName =
    use stream =
        Reflection.Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream("Pxl.Ui.Ui.Fonts." + fontFileName)
    if stream = null then
        failwithf "Font (resource) '%s' not found" fontFileName
    new SKFont(SKTypeface.FromStream(stream))

let buildFont height ascent (font: SKFont) =
    {
        height = height
        ascent = ascent |> Option.defaultValue (int font.Metrics.Ascent)
        skFont = font
    }

let var3x5 =
    getBuiltinTypeface "cg-pixel-3x5-prop.otf" |> buildFont 5 (Some 0)

let mono3x5 =
    getBuiltinTypeface "cg-pixel-3x5-mono.otf" |> buildFont 5 (Some 0)

let var4x5 =
    getBuiltinTypeface "cg-pixel-4x5-prop.otf" |> buildFont 5 (Some 0)

let mono4x5 =
    getBuiltinTypeface "cg-pixel-4x5-mono.otf" |> buildFont 5 (Some 0)

let mono6x6 =
    getBuiltinTypeface "6x6-pixel-yc-fs.ttf" |> buildFont 6 (Some 0)

let mono7x10 =
    getBuiltinTypeface "7kh10.ttf" |> buildFont 12 (Some -2)

let var10x10 =
    getBuiltinTypeface "super04b.ttf" |> buildFont 10 (Some 0)

let mono10x10 =
    getBuiltinTypeface "10x10-monospaced-font.ttf" |> buildFont 16 (Some -6)

let mono16x16 =
    getBuiltinTypeface "ascii-sector-16x16-tileset.otf" |> buildFont 16 (Some -2)

