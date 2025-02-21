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
