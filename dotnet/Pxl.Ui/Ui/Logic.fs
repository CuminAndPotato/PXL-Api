namespace Pxl.Ui

open Pxl

type Logic =

    static member inline counterCtrl(init: 'a, increment: 'a) =
        scene {
            let! count = useState { init }
            do count.value <- count.value + increment
            return count
        }

    static member inline count(init: 'a, increment: 'a) =
        scene {
            let! countCtrl = Logic.counterCtrl(init, increment)
            return countCtrl.value
        }

    /// Count until a certain value is reached (inclusive),
    /// then reset to the initial value and continues counting.
    static member inline countUntil(init: 'a, increment: 'a, until: 'a) =
        scene {
            let! countCtrl = Logic.counterCtrl(init, increment)
            if countCtrl.value > until then
                countCtrl.value <- init
            return countCtrl.value
        }

    static member inline delayBy1(init, current) : Vide<_,_> =
        fun s _ ->
            let s = defaultArg s init
            s, Some current

    static member inline hasChanged(current, ?initial) =
        scene {
            let! last = useState {
                match defaultArg initial false with
                | true -> Some current
                | false -> None
            }
            let current = Some current
            let hasChanged = last.value <> current
            do last.value <- current
            return hasChanged
        }

    /// Returns true if the current value has equaled the expected value for at least the specified timespan (in milliseconds)
    static member inline lag(currentValue: 'a, expectedValue: 'a, timespanMs: int) =
        scene {
            let! lastMatchTime = useState { None }
            let now = System.DateTime.UtcNow
            let res =
                if currentValue = expectedValue then
                    // Update the match time if this is the first match
                    if lastMatchTime.value = None then
                        do lastMatchTime.value <- Some now

                    // Calculate how long the value has matched
                    let matchStartTime = lastMatchTime.value.Value
                    let elapsed = now - matchStartTime
                    elapsed.TotalMilliseconds >= float timespanMs
                else
                    // Reset the timer when values don't match
                    do lastMatchTime.value <- None
                    false
            return res
        }

