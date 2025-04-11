namespace Pxl

open SkiaSharp
open Pxl
open System
open System.Runtime.InteropServices

type Buttons =
    {
        lowerButtonPressed : bool
        upperButtonPressed : bool
    }

module Interop =
    // we know that the struct layout of SkColor in our case is RGBA8888 (see above)

    let copyPixelSpanToArray (srcPixelxSpan: ReadOnlySpan<byte>) (dest: Color[]) =
        let colorSpan = MemoryMarshal.Cast<byte, Color>(srcPixelxSpan)
        colorSpan.CopyTo(dest)

    let pixelSpanToArray (srcPixelxSpan: ReadOnlySpan<byte>) =
        let dest = Array.zeroCreate<Color>(srcPixelxSpan.Length / 4)
        copyPixelSpanToArray srcPixelxSpan dest
        dest

[<Sealed>]
type RenderCtx
    (
        width: int,
        height: int,
        fps: int,
        ?onEndCycle: RenderCtx -> Color array -> unit
    ) =
    let _skSurface = SKSurface.Create(SKImageInfo(width, height))
    let _skCanvas = _skSurface.Canvas
    let _skImageInfo = SKImageInfo(width, height, SKColorType.Rgba8888)
    let _skBmp = new SKBitmap(_skImageInfo)

    let _width = float width
    let _height = float height

    let defaultClearBackground = true

    let mutable _now = DateTimeOffset.MinValue
    let mutable _startTime = DateTimeOffset.MinValue
    let mutable _clear = defaultClearBackground
    let mutable _buttons = { lowerButtonPressed = false; upperButtonPressed = false }

    member _.width = _width
    member _.height = _height
    member _.halfWidth = float _width / 2.0
    member _.halfHeight = float _height / 2.0

    member _.now = _now
    member _.elapsed = _startTime - _now
    member _.fps = fps
    member _.canvas = _skCanvas
    member _.buttons = _buttons

    member internal _.PrepareCycle(startTime: DateTimeOffset, now: DateTimeOffset, buttons: Buttons) =
        _startTime <- startTime
        _now <- now
        _buttons <- buttons
        if _clear then
            _skCanvas.Clear(SKColors.Black)
        _clear <- defaultClearBackground
        _skCanvas.ResetMatrix()

    member _.ClearScreenOnCycleCompleted(value) =
        _clear <- value

    member _.GetRawSnapshot() =
        use intermediateImage = SKImage.FromBitmap(_skBmp)
        intermediateImage.PeekPixels()

    member this.GetSnapshot() =
        Interop.pixelSpanToArray (this.GetRawSnapshot().GetPixelSpan())

    // TODO SendBuffer - pass frame array from outside
    member internal this.EndCycle(dest: Color[]) =
        do _skCanvas.Flush()

        let couldRead = _skSurface.ReadPixels(_skImageInfo, _skBmp.GetPixels(), _skImageInfo.RowBytes, 0, 0)
        if couldRead |> not then
            failwith "Failed to read pixels from SKSurface"

        // we know that the struct layout of SkColor in our case is RGBA8888 (see above)
        let srcPixelxSpan = _skBmp.GetPixelSpan()
        Interop.copyPixelSpanToArray srcPixelxSpan dest

        onEndCycle |> Option.iter (fun f -> f this dest)
