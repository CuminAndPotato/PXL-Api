namespace Pxl.Ui

open Pxl

[<RequireQualifiedAccess>]
type Layer =

    static member inline rotate(deg, ?cx, ?cy) : Vide<_,_> =
        fun _ ctx ->
            let cx = defaultArg cx ctx.halfWidth
            let cy = defaultArg cy ctx.halfHeight
            do ctx.canvas.RotateDegrees(f32 deg, f32 cx, f32 cy)
            (), State.none

    static member inline offset(x, y) : Vide<_,_> =
        fun _ ctx ->
            do ctx.canvas.Translate(f32 x, f32 y)
            (), State.none
