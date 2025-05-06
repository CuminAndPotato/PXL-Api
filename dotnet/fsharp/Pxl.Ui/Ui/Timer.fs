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

    static member inline interval(interval: TimeSpan, [<InlineIfLambda>] f: StopWatchController -> RenderCtx -> unit, ?autoStart: bool) =
        scene {
            let! swc = Timer.stopWatch(?autoStart = autoStart)
            let! ctx = getCtx ()
            if swc.elapsed >= interval then
                do swc.rewind(TimeSpan.Zero)
                do f swc ctx
        }

    static member inline interval(intervalInS, [<InlineIfLambda>] f, ?autoStart) =
        Timer.interval(TimeSpan.FromSeconds (float intervalInS), f, ?autoStart = autoStart)

    static member inline computeInterval(interval: TimeSpan, [<InlineIfLambda>] f, ?autoStart) =
        scene {
            let! v = useState { f () }
            Timer.interval(interval, (fun swc ctx -> v.value <- f()), ?autoStart = autoStart)
            return v.value
        }

    static member inline computeInterval(intervalInS: float, [<InlineIfLambda>] f, ?autoStart) =
        Timer.computeInterval(TimeSpan.FromSeconds (float intervalInS), f, ?autoStart = autoStart)

    // TODO: Delay
