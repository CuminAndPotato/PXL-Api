[<AutoOpen>]
module Pxl.Draw.Imaging

open System
open System.Runtime.CompilerServices
open Pxl
open Pxl.Ui.Vide
open SkiaSharp
open System.IO


// should always work on frames list
// some functions then just take the fst frame
// all functions on Image class?


type Frame =
    {
        bmp: SKBitmap
        duration: TimeSpan
    }
    member inline this.withDuration(value) =
        { this with duration = TimeSpan.FromMilliseconds(value) }


type AnimatedFramwState =
    {
        frame: Frame
        idx: int
        expiresAt: DateTimeOffset
    }


[<Sealed>]
type SpriteMap(cells, cols) =
    inherit Grid<Frame>(cells, cols)
    // member this.animate(cellIndexes: list<int * int>) =
    //     buildImage { cellIndexes |> List.map (fun (x, y) -> this[x, y]) }


[<Extension>]
type FrameExtensions =

    [<Extension>]
    static member resize(frame: Frame, w, h, useAntiAlias) =
        let bmp = frame.bmp.Resize(
            SKImageInfo(w, h),
            if useAntiAlias then SKFilterQuality.High else SKFilterQuality.None)
        { frame with bmp = bmp }

    [<Extension>]
    static member resize(frames: Frame list, w, h, useAntiAlias) =
        [
            for frame in frames do
                frame.resize(w, h, useAntiAlias)
        ]

    [<Extension>]
    static member crop(frame: Frame, left, top, right, bottom) =
        let bmp = frame.bmp
        let croppedBmp = new SKBitmap()
        let success =
            bmp.ExtractSubset(
                croppedBmp,
                SKRectI(left, top, bmp.Width - right, bmp.Height - bottom))
        if not success then
            failwith $"Failed to crop image: w={bmp.Width}, h={bmp.Height}, top={top}, right={right}, bottom={bottom}, left={left}"
        { frame with bmp = croppedBmp }

    [<Extension>]
    static member crop(frames: Frame list, left, top, right, bottom) =
        [
            for frame in frames do
                frame.crop(top, right, bottom, left)
        ]

    // TODO: add more convenience methods

    [<Extension>]
    static member inline makeSpriteMap
        (
            frame: Frame,
            cellWidth, cellHeight,
            ?frameDurationInMs,
            ?marginLeft, ?marginTop, ?marginRight, ?marginBottom
        ) =
        let bmp = frame.bmp
        let rows = bmp.Height / cellHeight
        let cols = bmp.Width / cellWidth
        if rows <= 0 || cols <= 0 then
            failwith $"Invalid sprite size (bmp: {bmp}, rows: {rows}, cols: {cols})"
        let defZero = Option.defaultValue 0
        let mt, mr, mb, ml = defZero marginTop, defZero marginRight, defZero marginBottom, defZero marginLeft
        let frameDurationInMs = defaultArg frameDurationInMs 100
        let cells =
            [
                for row in 0 .. rows - 1 do
                    for col in 0 .. cols - 1 do
                        let x = col * cellWidth
                        let y = row * cellHeight
                        let rect = SKRectI(x + ml, y + mt, x + cellWidth - mr, y + cellHeight - mb)
                        let destinationBmp = new SKBitmap(cellWidth, cellHeight)
                        if not (bmp.ExtractSubset(destinationBmp, rect)) then
                            failwith $"Warning: Failed to extract subset (bmp: {bmp}; row: {row}; col: {col}; rect: {rect})"
                        {
                            bmp = destinationBmp
                            duration = TimeSpan.FromMilliseconds(frameDurationInMs)
                        }
            ]
        SpriteMap(cells, cols)


let loadFramesFromStream (stream: Stream) =
    use stream = new SKManagedStream(stream, true)
    use codec = SKCodec.Create(stream)
    match codec.FrameCount with
    | 0 ->
        [
            { bmp = SKBitmap.Decode(codec); duration = TimeSpan.Zero }
        ]
    | frameCount ->
        [
            for i in 0 .. frameCount - 1 do
                let imageInfo = SKImageInfo(codec.Info.Width, codec.Info.Height)
                let frame = new SKBitmap(imageInfo)
                let res = codec.GetPixels(
                    imageInfo,
                    frame.GetPixels(),
                    SKCodecOptions(i))
                if res <> SKCodecResult.Success then
                    failwith $"Failed to decode frame. Result: {res}"
                {
                    bmp = frame
                    duration = TimeSpan.FromMilliseconds (float codec.FrameInfo[i].Duration)
                }
        ]


    // static member inline image(spriteMap: SpriteMap, x, y, ?repeat) =
    //     DrawImage.image(spriteMap.cells, x, y, ?repeat = repeat)

    // static member inline image(stream: Stream, x, y, ?repeat) =
    //     scene {
    //         let! frames = useMemo { loadFrames stream }
    //         DrawImage.image(frames, x, y, ?repeat = repeat)
    //     }

    // static member inline image(path: string, x, y, ?repeat) =
    //     image (File.OpenRead(path), x, y, ?repeat = repeat)

    // // ----------------------------
    // // and here the Vide-version - a convenience for buildImage stuff
    // // ----------------------------

    // static member inline image(frame: Vide<Frame,_>, x, y) =
    //     scene {
    //         let! frame = frame
    //         DrawImage.image(frame, x, y)
    //     }

    // static member inline image(frames: Vide<Frame list,_>, x, y, ?repeat) =
    //     scene {
    //         let! frames = frames
    //         DrawImage.image(frames, x, y, ?repeat = repeat)
    //     }

    // static member inline image(spriteMap: Vide<SpriteMap,_>, x, y, ?repeat) =
    //     scene {
    //         let! sprite = spriteMap
    //         DrawImage.image(sprite, x, y, ?repeat = repeat)
    //     }


[<AutoOpen>]
type Image(createFrames: unit -> list<Frame>) =
    inherit DrawableBuilder()

    member val _x = 0.0f with get, set
    member inline this.x(x) = this._x <- f32 x; this

    member val _y = 0.0f with get, set
    member inline this.y(y) = this._y <- f32 y; this

    member val _repeat = false with get, set
    member inline this.repeat(repeat) = this._repeat <- repeat; this

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        scene {
            let! ctx = getCtx ()

            let! frames = useMemo { createFrames () }

            if frames.Length = 1 then
                do ctx.canvas.DrawBitmap(frames.[0].bmp, f32 this._x, f32 this._y)

            if frames.Length > 1 then
                let! currFrameState = useState { None }
                let state =
                    let mkState idx =
                        let frame = frames[idx]
                        {
                            frame = frame
                            idx = idx
                            expiresAt = ctx.now + frame.duration
                        }
                    match currFrameState.value with
                    | None -> mkState 0
                    | Some currFrameInfo ->
                        if currFrameInfo.expiresAt < ctx.now then
                            let nextIdx =
                                if currFrameInfo.idx + 1 = frames.Length then
                                    if this._repeat then 0 else currFrameInfo.idx
                                else currFrameInfo.idx + 1
                            mkState nextIdx
                        else
                            currFrameInfo
                do currFrameState.value <- Some state
                do ctx.canvas.DrawBitmap(state.frame.bmp, f32 this._x, f32 this._y)
            else
                discardState
        }

    static member image(stream: Stream) =
        Image(fun () -> loadFramesFromStream stream)

    static member image(path: string) =
        Image(fun () -> loadFramesFromStream (File.OpenRead path))

type Vide.VideBuilderBase with member inline _.Yield(b: Image) = b {()}
