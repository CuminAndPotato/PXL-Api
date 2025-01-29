namespace Pxl.Ui

open Pxl

[<AutoOpen>]
type Global =
    static member inline getSize() : Vide<_,_> =
        fun _ ctx ->
            { Size.w = ctx.width; h = ctx.height }, State.none

    static member inline getHalfSize() : Vide<_,_> =
        fun _ ctx ->
            { Size.w = ctx.halfWidth; h = ctx.halfHeight }, State.none

    static member inline getNow() : Vide<_,_> =
        fun _ ctx ->
            ctx.now, State.none

    static member clearScreenOnCycleCompleted(value) : Vide<_,_> =
        fun _ ctx ->
            ctx.ClearScreenOnCycleCompleted(value)
            (), State.none

    static member buttons : Vide<_,_> =
        fun _ ctx ->
            ctx.buttons, State.none

    static member lowerButtonPressed : Vide<_,_> =
        fun _ ctx ->
            ctx.buttons.lowerButtonPressed, State.none

    static member upperButtonPressed : Vide<_,_> =
        fun _ ctx ->
            ctx.buttons.upperButtonPressed, State.none
