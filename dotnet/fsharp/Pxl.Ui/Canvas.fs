namespace Pxl

// --------------------------------------------------------------------------
// All types and signatures have to be mirrored
// in the dotnet / node implementations.
//
// see:
//   src/Pxl.BufferedHttpCanvas/BufferedHttpCanvas.fs
//   src/Pxl.Ui/Canvas.fs
//   src/pxl-local-display/src/domain.ts
//   src/pxl-local-display/src/server.ts
// --------------------------------------------------------------------------

open System

type CanvasMetadata =
    {
        width: int
        height: int
        fps: int
    }

[<RequireQualifiedAccess>]
type CanvasMode = On | Off

type Canvas
    (
        metadata: CanvasMetadata,
        sendFrameBufferSize: int,
        onEnd: unit -> unit
    )
    =
    let bbq = new BoundedBlockingQueue<_>(sendFrameBufferSize, onEnd)

    let onFrameReceivedEvent = new Event<Color[]>()

    let mutable _mode = CanvasMode.On
    let mutable _brightness = 1.0

    do
        bbq.Consume(
            onFrameReceivedEvent.Trigger,
            // TODO: What now?
            (fun () -> ())
        )

    [<CLIEvent>]
    member _.OnFrameReceived = onFrameReceivedEvent.Publish

    member _.Mode with get() = _mode and set(v) = _mode <- v
    member _.Metadata = metadata
    member _.Brightness
        with get() = _brightness
        and set(v) = _brightness <- clamp01 v
    member _.SendBufferSize = sendFrameBufferSize
    member _.PushFrameSafe(pixels: Color[]) =
        // TODO: Non linear brightness
        for i = 0 to pixels.Length - 1 do
            pixels[i] <- pixels[i].brightness(_brightness)
        if _mode = CanvasMode.On then
            // TODO: Safe - good?
            bbq.Push(pixels)
    member _.Ct = bbq.Cts.Token

    interface IDisposable with
        member _.Dispose() =
            (bbq :> IDisposable).Dispose()


[<RequireQualifiedAccess>]
module CanvasProxy =

    open System.Net.Sockets
    open FsHttp
    open Pxl

    // --------------------------------------------------------------------------
    // All types and signatures have to be mirrored
    // in the dotnet / node implementations.
    //
    // see:
    //   src/Pxl.BufferedHttpCanvas/BufferedHttpCanvas.fs
    //   src/Pxl.Ui/Canvas.fs
    //   src/pxl-local-display/src/domain.ts
    //   src/pxl-local-display/src/server.ts
    // --------------------------------------------------------------------------

    let InvariantServicePorts =
        {|
            http = 5001
            tcp = 5002
        |}

    // TODO:
    // - FsHttp port exhaustion issue
    // - use a streaming model instead of copying
    // - and then use FsHttp to stream the data

    // Info: This really has to play nice with the logic in Runner regarding exception handling and retries

    let frameBytesToColors (bytes: byte[]) =
        let mutable i = 0
        [|
            while i < bytes.Length do
                yield Color.rgb(bytes[i], bytes[i + 1], bytes[i + 2])
                i <- i + 3
        |]

    let frameColorsToBytes (colors: Color[]) =
        [|
            for c in colors do
                byte c.r
                byte c.g
                byte c.b
        |]

    let create remote sendBufferSize onEnd =
        let clientMetadata =
            http {
                GET $"http://{remote}:{InvariantServicePorts.http}/metadata"
                config_timeoutInSeconds 3.0
            }
            |> Request.send
            |> Response.assert2xx
            |> Response.deserializeJson<CanvasMetadata>

        let client = new TcpClient(remote, InvariantServicePorts.tcp)
        let stream = client.GetStream()

        let sendFrame (pixels: Color array) =
            let bytes = frameColorsToBytes pixels
            stream.Write(bytes, 0, bytes.Length)

        let dispose () =
            stream.Dispose()
            client.Dispose()
            onEnd ()

        let canvas =
            let metadata =
                {
                    width = clientMetadata.width
                    height = clientMetadata.height
                    fps = clientMetadata.fps
                }
            new Canvas(metadata, sendBufferSize, dispose)
        // do canvas.RegisterSender(sendFrame, dispose)
        do canvas.OnFrameReceived.Add(sendFrame)
        canvas

    let createWithDefaults remote = create remote 20
