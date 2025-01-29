namespace Pxl.Ui

open Pxl

module TriggerFuncs =
    let inline trigger(inValue, [<InlineIfLambda>] triggerFunc) : Vide<_,_> =
        fun s ctx ->
            let lastValue = defaultArg s inValue
            let trigger = triggerFunc lastValue
            trigger, Some inValue

type Trigger =
    static member falseToTrue(inValue) =
        TriggerFuncs.trigger(inValue, fun lastValue -> not lastValue && inValue)

    static member trueToFalse(inValue) =
        TriggerFuncs.trigger(inValue, fun lastValue -> lastValue && not inValue)

    static member thresholdUp(threshold, inValue) =
        TriggerFuncs.trigger(inValue, fun lastValue -> lastValue < threshold && inValue >= threshold)

    static member thresholdDown(threshold, inValue) =
        TriggerFuncs.trigger(inValue, fun lastValue -> lastValue > threshold && inValue <= threshold)

    static member startAndHold(startWhen, holdAsLongAs, ?initialValue, ?onStart, ?onHold, ?onEnd) =
        scene {
            let! startTrigger = Trigger.falseToTrue(startWhen)
            let! endTrigger = Trigger.trueToFalse(holdAsLongAs)
            let! isHolding = useState { defaultArg initialValue false }
            do
                if startTrigger then
                    isHolding.value <- true
                    match onStart with Some f -> f() | _ -> ()
                if isHolding.value then
                    match onHold with Some f -> f() | _ -> ()
                if endTrigger then
                    isHolding.value <- false
                    match onEnd with Some f -> f() | _ -> ()
            return isHolding.value
        }

    static member startAndHoldForCycles(startWhen, holdCycleCount: int, ?initialValue) =
        scene {
            let! count = useState { holdCycleCount }
            return! Trigger.startAndHold(
                startWhen,
                count.value > 0,
                ?initialValue = initialValue,
                onStart = (fun () -> count.value <- holdCycleCount),
                onHold = (fun () -> count.value <- count.value - 1))
        }

    static member inline restartWhen(restartWhen, [<InlineIfLambda>] child: Vide<_,_>) : Vide<_,_> =
        scene {
            let! restartTrigger = Trigger.falseToTrue(restartWhen)
            return! fun s ctx ->
                let s =
                    match restartTrigger with
                    | true -> None
                    | false -> s
                child s ctx
        }

    static member inline restartWhenValueChanges(value, [<InlineIfLambda>] child, ?startImmediately) =
        scene {
            let! lastValue = useState { None }
            let shouldRestart =
                match lastValue.value, defaultArg startImmediately true with
                | None, true -> true
                | None, false -> false
                | Some lastValue, _ -> lastValue <> value
            do lastValue.value <- Some value
            return! Trigger.restartWhen(shouldRestart, child)
        }

    static member inline valueChanged(value: 'a) =
        scene {
            let! lastValue = useState { value }
            let changed = lastValue.value <> value
            do lastValue.value <- value
            return changed
        }
