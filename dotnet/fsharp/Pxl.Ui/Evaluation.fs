namespace Pxl

open System
open System.Threading
open Pxl

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
module Evaluation =
    let mutable startedTimes = 0

    let hangDetectionTimeSpan = TimeSpan.FromSeconds(5.0)

    let start
        (
            canvas: Canvas,
            onEvalError: Exception -> unit,
            reality: Reality,
            readButtons: unit -> Buttons,
            scene: Vide<unit,'s>
        ) =
        let mutable shouldEvaluate = true
        let isRunning () = shouldEvaluate && not canvas.Ct.IsCancellationRequested

        let mutable lastEvaluationTime = None

        fun () ->
            let frameArrays =
                [
                    for i in 0 .. canvas.SendBufferSize - 1 do
                        Array.zeroCreate<Color>(canvas.Metadata.width * canvas.Metadata.height)
                ]
            let renderCtx = RenderCtx(canvas.Metadata.width, canvas.Metadata.height, canvas.Metadata.fps)
            let durationForOneFrame = 1.0 / float canvas.Metadata.fps
            let sceneStartTime = reality.Now
            let mutable lastSceneState = None
            let mutable completeCycleCount = 0
            let calcTimeForCycle cycleNr = sceneStartTime.AddSeconds(durationForOneFrame * float cycleNr)
            while isRunning () do
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

        fun () ->
            while isRunning () do
                match lastEvaluationTime with
                | Some lastEvaluationTime ->
                    if reality.Now - lastEvaluationTime > hangDetectionTimeSpan then
                        onEvalError <| new Exception("Evaluation hanging")
                | None -> ()
                Thread.Sleep(100)
        |> Thread.startBackground $"EvaluationHangDetection_{startedTimes}"

        fun () -> shouldEvaluate <- false
