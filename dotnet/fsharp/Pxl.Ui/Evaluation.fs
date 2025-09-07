namespace Pxl

open System
open System.Threading
open Pxl
open Pxl.Ui

type Reality
    (
        onCycleFinished: DateTimeOffset -> unit,
        [<InlineIfLambda>] getNow: unit -> DateTimeOffset
    )
    =
    member _.OnCycleFinished(nextPlannedEvaluation: DateTimeOffset) =
        onCycleFinished nextPlannedEvaluation
    member _.Now =
        getNow()

module Reality =
    let forRealTime () =
        let getNow () = DateTimeOffset.Now
        let onCycleFinished nextPlannedEvaluation =
                let timeToWait = nextPlannedEvaluation - getNow ()
                if timeToWait > TimeSpan.Zero then
                    Thread.Sleep(timeToWait)
        Reality(onCycleFinished, getNow)

// TODO: ggf. eine HandleErrorStrategy aus onError machen
type Evaluation() =
    static let mutable startedTimes = 0
    static let mutable accumulatedConsumerCount = 0

    static member Start
        (
            canvas: Canvas,
            renderCtx: RenderCtx,
            onEvalError: Exception -> unit,
            reality: Reality,
            readButtons: unit -> Buttons,
            scene: Vide<unit,'s>
        ) =
        let hangDetectionTimeSpan = TimeSpan.FromSeconds(5.0)

        let mutable shouldEvaluate = true
        let isRunning () = shouldEvaluate && not canvas.Ct.IsCancellationRequested

        let mutable lastEvaluationTime : DateTimeOffset option = None

        startedTimes <- startedTimes + 1
        fun () ->
            let frameArrays =
                [
                    for i in 0 .. canvas.SendBufferSize - 1 do
                        Array.zeroCreate<Color>(canvas.Metadata.width * canvas.Metadata.height)
                ]
            let durationForOneFrame = 1.0 / float canvas.Metadata.fps
            let mutable sceneStartTime = reality.Now
            let mutable lastSceneState = None
            let mutable completeCycleCount = 0
            let calcTimeForCycle cycleNr = sceneStartTime.AddSeconds(durationForOneFrame * float cycleNr)
            while isRunning () do
                // when the diff between last eval time and now is > 1s, we reset the whole thing
                do
                    match lastEvaluationTime with
                    | Some lastEvaluationTime ->
                        let diff = reality.Now - lastEvaluationTime
                        if
                            diff.Ticks <= 0
                            || abs diff.Ticks > TimeSpan.FromSeconds(1.0).Ticks
                        then
                            sceneStartTime <- reality.Now
                            lastSceneState <- None
                            completeCycleCount <- 0
                    | _ -> ()

                do lastEvaluationTime <- Some reality.Now

                try
                    // Für die Szene ist es wichtig, dass "now" keinen Jitter hat.
                    // Wir berechnen jeden Zyklus - egal, wie weit wir hintendran sind.
                    // Die Puffer gleichen das wieder aus durh Frame-Dropping im härtesten Fall.
                    // Im Schnitt ist der RPi schon stark genug, um wieder aufzuholen.
                    let frameNow = calcTimeForCycle completeCycleCount
                    let frame =
                        do renderCtx.PrepareCycle(sceneStartTime, frameNow, readButtons ())
                        do lastSceneState <- scene lastSceneState renderCtx |> snd
                        let frame = frameArrays[completeCycleCount % frameArrays.Length]
                        do renderCtx.EndCycle(frame)
                        frame
                    do
                        canvas.PushFrameSafe(frame)
                        completeCycleCount <- completeCycleCount + 1
                        reality.OnCycleFinished(calcTimeForCycle completeCycleCount)
                with ex ->
                    printfn $"Error in evaluation: {ex.Message}"
                    onEvalError ex
        |> Thread.startBackground $"Evaluation_{startedTimes}"
        |> ignore

        fun () ->
            while isRunning () do
                match lastEvaluationTime with
                | Some lastEvaluationTime ->
                    if reality.Now - lastEvaluationTime > hangDetectionTimeSpan then
                        onEvalError <| new Exception("Evaluation hanging")
                | None -> ()
                Thread.Sleep(100)
        |> Thread.startBackground $"EvaluationHangDetection_{startedTimes}"
        |> ignore

        fun () -> shouldEvaluate <- false

    static member Start
        (
            canvas: Canvas,
            renderCtx: RenderCtx,
            onEvalError: Exception -> unit,
            reality: Reality,
            readButtons: unit -> Buttons,
            startScene: unit -> unit
        ) =

        accumulatedConsumerCount <- accumulatedConsumerCount + 1

        let consumerThread =
            fun () ->
                try
                    startScene ()
                with ex ->
                    printfn $"Error in consuming frames: {ex}"
            |> Thread.startBackground $"ConsumerThread_{accumulatedConsumerCount}"

        let threadBasedCtx =
            CSharpSceneHandling.initializeEvaluation renderCtx Thread.CurrentThread consumerThread

        let videScene =
            scene {
                do
                    try
                        threadBasedCtx.TriggerFrameProcessing()
                    with ex ->
                        printfn $"Error in triggering frame processing: {ex}"
                        (threadBasedCtx :> IDisposable).Dispose()
                        reraise ()
            }

        Evaluation.Start(
            canvas,
            renderCtx,
            onEvalError,
            reality,
            readButtons,
            videScene
        )
