[<AutoOpen>]
module Pxl.PxlLocalDevShadow

open System
open System.IO
open System.Reflection

let mutable private _isFsi = None

// This seems like a HACK - it propably is. But it works.
let private isFsi () =
    match _isFsi with
    | Some x -> x
    | None ->
        let isFsi =
            AppDomain.CurrentDomain.GetAssemblies()
            |> Array.map (fun asm -> asm.GetName().Name)
            |> Array.exists (fun asmName -> asmName.StartsWith("FSI-ASSEMBLY", StringComparison.Ordinal))
        _isFsi <- Some isFsi
        isFsi


[<RequireQualifiedAccess>]
module Asset =
    let loadFromAssembly (assetName: string) (assembly: Assembly) =
        let assetName = assetName.Replace(" ", "_")
        // "assets" is omitted somwhow - but only in pre-compiled apps. Why? Who knows :(
        let resourceNames =
            [
                $"{assembly.GetName().Name}.assets.{assetName}"
                $"{assembly.GetName().Name}.{assetName}"
            ]
        let res =
            resourceNames
            |> List.map (fun resourceName -> assembly.GetManifestResourceStream(resourceName))
            |> List.filter (fun stream -> stream <> null)
            |> List.tryHead
        match res with
        | None ->
            let existingNames = assembly.GetManifestResourceNames() |> String.concat ", "
            let resourceNamesString = resourceNames |> String.concat ", "
            failwith $"Asset not found: {assetName} -- Assembly name: {assembly.GetName().Name} -- resource names: {resourceNamesString}. Existing names: {existingNames}"
        | Some stream -> stream

    /// This function must only called from the assembly that contains the assets.
    let load (sourceDir: string, assetName: string) =
        if isFsi() then
            let path = Path.Combine(sourceDir, "assets", assetName)
            let content = File.ReadAllBytes(path)
            new MemoryStream(content) :> Stream
        else
            let assembly = Assembly.GetCallingAssembly()
            loadFromAssembly assetName assembly


[<RequireQualifiedAccess>]
module Image =

    let internal load (sourceDir: string) (assetName: string) f =
        if isFsi() then
            Asset.load(sourceDir, assetName)
            |> f
        else
            Asset.loadFromAssembly assetName (Assembly.GetCallingAssembly())
            |> f

    /// This function must only called from the assembly that contains the assets.
    let loadFromAsset (sourceDir: string, assetName: string) =
        load sourceDir assetName Pxl.Ui.Image.load

    let loadFramesFromAsset (sourceDir: string, assetName: string) =
        load sourceDir assetName Pxl.Ui.Image.loadFrames


[<RequireQualifiedAccess>]
module Simulator =
    open System
    open Pxl

    // this mechanism prevents multiple running evaluators when loading the assembly multiple times
    let private stopInternal =
        let stopFuncId = "__Pxl.Draw.Fsi.Eval.stop"
        let ensureStop () =
            match AppDomain.CurrentDomain.GetData(stopFuncId) with
            | null -> ()
            | stopFunc -> (stopFunc :?> (unit -> unit)) ()
        let setStopFunc (stopFunc: unit -> unit) =
            ensureStop ()
            AppDomain.CurrentDomain.SetData(stopFuncId, box stopFunc)
        let invoke () =
            setStopFunc (fun () -> ())
        {|
            setFunc = setStopFunc
            invoke = invoke
        |}

    let startEx (createCanvas: (unit -> unit) -> Canvas) scene =
        if isFsi() then
            let mutable retry = true

            let rec reStart () =
                let onCanvasEnd () =
                    if retry then
                        let sleepTimeInMs = 2000
                        printfn $"Error in flushing Canvas. Waiting {sleepTimeInMs}ms before retrying ..."
                        System.Threading.Thread.Sleep(sleepTimeInMs)
                        reStart ()

                try
                    let canvas = createCanvas onCanvasEnd
                    // printfn $"Canvas created - Metadata: {canvas.Metadata}"
                    let stop =
                        Evaluation.startVide(
                            canvas,
                            RenderCtx(canvas.Metadata.width, canvas.Metadata.height, canvas.Metadata.fps),
                            (fun ex ->
                                printfn $"Error in evaluating App logic: {ex.Message}"
                                onCanvasEnd ()
                            ),
                            Reality.forRealTime (),
                            (fun () -> { lowerButtonPressed = false; upperButtonPressed = false }),
                            scene)
                    stopInternal.setFunc (
                        fun () ->
                            retry <- false
                            stop ()
                            (canvas :> IDisposable).Dispose())
                with ex ->
                    printfn $"Error in creating Canvas (ending it): {ex.Message}"
                    onCanvasEnd ()

            stopInternal.invoke()
            reStart ()
        else
            ()

    let start (receiver: string) scene =
        scene |> startEx (CanvasProxy.createWithDefaults receiver)

    let stop () =
        if isFsi()
        then stopInternal.invoke()
        else ()
