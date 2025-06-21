namespace Pxl.Ui

open System
open Pxl

type StopWatchController(isRunning) =
    let mutable _isRunning = isRunning
    let mutable _lastTickTime = None
    let mutable _elapsed = TimeSpan.Zero
    let mutable _onPausing = None
    let mutable _onResuming = None
    let invokeOnResuming () =
        match _onResuming with | Some f -> f() | None -> ()

    member _.isRunning = _isRunning
    member this.isPaused = not this.isRunning
    member _.elapsed = _elapsed
    member _.lastTickTime = _lastTickTime

    member _.pause() =
        _isRunning <- false
        match _onPausing with | Some f -> f() | None -> ()
    member _.onPausing(f) =
        _onPausing <- Some f

    member _.resume() =
        _isRunning <- true
        invokeOnResuming ()
    member _.onResuming(f) =
        _onResuming <- Some f

    member _.rewind(elapsed) =
        _elapsed <- elapsed
        _lastTickTime <- None

    member _.eval(now: DateTimeOffset) =
        let lastTickTime,isResuming =
            match _lastTickTime with
            | None -> now, true
            | Some lastTickTime -> lastTickTime, false
        if isResuming then
            invokeOnResuming ()
        _lastTickTime <- Some now
        _elapsed <- _elapsed + (now - lastTickTime)

type Timer =
    // TODO: Repeat and Clamping
    static member stopWatch(?autoStart) : Vide<_,_> =
        fun s ctx ->
            let controller =
                match s with
                | Some controller -> controller
                | None -> StopWatchController(defaultArg autoStart true)
            if controller.isRunning then
                do controller.eval(ctx.now)
            controller, Some controller

    static member inline interval(interval: TimeSpan, ?autoStart: bool) =
        scene {
            let! swc = Timer.stopWatch(?autoStart = autoStart)
            let trigger =
                if swc.elapsed >= interval then
                    do swc.rewind(TimeSpan.Zero)
                    true
                else
                    false
            return {| isElapsed = trigger; controller = swc |}
        }

    static member inline interval(intervalInS, ?autoStart) =
        Timer.interval(TimeSpan.FromSeconds (float intervalInS), ?autoStart = autoStart)

    static member inline toggleValues(durationInS, values: _ list, ?repeat, ?autoStart) =
        if values.Length = 0 then
            failwith "At least one values is required."
        scene {
            let! idx = useState { 0 }
            let! changeValue = Timer.interval(durationInS, ?autoStart = autoStart)
            if changeValue.isElapsed then
                let nextIdx = (idx.value + 1) % values.Length
                idx.value <- nextIdx
            return values[idx.value]
        }

    // TODO: Delay
