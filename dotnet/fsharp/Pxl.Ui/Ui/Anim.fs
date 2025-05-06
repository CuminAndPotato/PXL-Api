namespace Pxl.Ui

open System
open Pxl

// https://easings.net/

[<RequireQualifiedAccess>]
type Repeat = StopAtEnd | Loop | RewindAndStop

type Clamping = NoClamp | ClampSmallerZero | ClampBiggerOne | ClampBoth

type AnimationController
    (
        f,
        sw: StopWatchController,
        duration: TimeSpan,
        startValue,
        endValue,
        repeat,
        clamping
    )
        as this
    =
    let mutable _value = startValue
    let mutable _isAtEndTrigger = false

    member val startValue = startValue with get, set
    member val endValue = endValue with get, set

    member _.value = _value
    member _.valuei = int _value
    member _.elapsed = sw.elapsed
    member _.elapsedRel = sw.elapsed.TotalSeconds / duration.TotalSeconds
    member _.isAtStart = this.elapsedRel <= 0.0
    member _.isAtEnd = this.elapsedRel >= 1.0
    member _.isAtEndTrigger = _isAtEndTrigger
    member _.isRunning = sw.isRunning
    member _.isPaused = sw.isPaused
    member _.pause() = do sw.pause()
    member _.onPausing(f) = sw.onPausing(f)
    member _.resume() = do sw.resume()
    member _.onResuming(f) = sw.onResuming(f)
    member this.restart() =
        do sw.rewind(TimeSpan.Zero)
        do sw.resume()
        do _isAtEndTrigger <- false
        do _value <- this.startValue
        do _value <- this.calcValue()
    member this.calcValue() =
        let t =
            match clamping with
            | NoClamp -> this.elapsedRel
            | ClampSmallerZero -> max 0.0 this.elapsedRel
            | ClampBiggerOne -> min 1.0 this.elapsedRel
            | ClampBoth -> max 0.0 (min 1.0 this.elapsedRel)
        (f t) * (this.endValue - this.startValue) + this.startValue
    member this.eval() =
        // TODO: why do we have "two" isAtEnd? There is a timing issue...
        // get rid of the mutable value
        let isAtEndLocal = this.elapsedRel >= 1.0
        if isAtEndLocal then
            match repeat with
            | Repeat.StopAtEnd ->
                do sw.pause()
            | Repeat.RewindAndStop ->
                do sw.rewind(TimeSpan.Zero)
                do sw.pause()
            | Repeat.Loop ->
                let overTheEndTime = TimeSpan.FromSeconds(sw.elapsed.TotalSeconds - duration.TotalSeconds)
                do sw.rewind(overTheEndTime)
            do _isAtEndTrigger <- true
        else
            do _isAtEndTrigger <- false
        do _value <- this.calcValue()

type Anim =
    static member inline calculate(f, durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) : Vide<_,_> =
        fun s ctx ->
            let swc,ac =
                match s with
                | Some (swc,ac) -> swc,ac
                | None ->
                    let swc = StopWatchController(defaultArg autoStart true)
                    let ac = AnimationController(
                        f,
                        swc,
                        TimeSpan.FromSeconds(durationInS),
                        startValue,
                        endValue,
                        defaultArg repeat Repeat.StopAtEnd,
                        defaultArg clamping ClampBoth)
                    swc,ac
            if swc.isRunning then
                do swc.eval(ctx.now)
                do ac.eval()
            ac, Some (swc,ac)

    // Linear

    static member inline linear(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t -> t),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    // Quadratic

    static member inline easeIn(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t -> t ** 2.0),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    static member inline easeOut(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t -> 1.0 - (1.0 - t) ** 2.0),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    static member inline easeInOut(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t ->
                if t < 0.5
                then 2.0 * t ** 2.0
                else 1.0 - (-2.0 * t + 2.0) ** 2.0 / 2.0),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    // Sine

    static member inline easeInSine(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t -> 1.0 - cos(t * Math.PI / 2.0)),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    static member inline easeOutSine(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t -> sin(t * Math.PI / 2.0)),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    static member inline easeInOutSine(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t -> -0.5 * (cos(Math.PI * t) - 1.0)),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    // Cubic

    static member inline easeInCubic(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t -> t ** 3.0),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    static member inline easeOutCubic(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t -> 1.0 - (1.0 - t) ** 3.0),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    static member inline easeInOutCubic(durationInS, startValue, endValue, ?repeat, ?autoStart, ?clamping) =
        Anim.calculate(
            (fun t ->
                if t < 0.5
                then 4.0 * t ** 3.0
                else 1.0 - (-2.0 * t + 2.0) ** 3.0 / 2.0),
            float durationInS, float startValue, float endValue, ?repeat = repeat, ?autoStart = autoStart, ?clamping = clamping)

    static member inline toggleValues(durationInS, values: _ list, ?repeat, ?autoStart) =
        if values.Length = 0 then
            failwith "At least one values is required."
        scene {
            let! idx = useState 0
            let! ac = Anim.linear(durationInS, 0, 1, ?repeat = repeat, ?autoStart = autoStart)
            let! shallIncrement = Trigger.falseToTrue ac.isAtEndTrigger
            if shallIncrement then
                do idx.value <- (idx.value + 1) % values.Length
            return values[idx.value]
        }
