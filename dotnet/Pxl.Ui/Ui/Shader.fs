[<AutoOpen>]
module Pxl.Ui.Shader

open Pxl
open Pxl.Ui.Internal.Drawing

type [<Struct>] ShaderFxInput =
    {
        point: Point
        pxlColor: Color
    }

let inline shaderGenerative
    (
        [<InlineIfLambda>] shaderFunc: Point -> Color
    )
    : Vide<_,_>
    =
    fun _ ctx ->
        for x in 0.0 .. ctx.width - 1.0 do
            for y in 0.0 .. ctx.height - 1.0 do
                let newColor = shaderFunc { x = x; y = y }
                use paint = Color.toSkiaPaint newColor
                do ctx.canvas.DrawPoint(f32 x, f32 y, paint)
        (), State.none

let inline shaderEffect
    (
        [<InlineIfLambda>] shaderFunc: ShaderFxInput -> Color
    )
    : Vide<_,_>
    =
    fun _ ctx ->
        use bmp = ctx.GetRawSnapshot()
        for x in 0. .. ctx.width - 1. do
            for y in 0. .. ctx.height - 1. do
                let currColor =
                    let c = bmp.GetPixelColor(int x, int y)
                    Color.argb(c.Alpha, c.Red, c.Green, c.Blue)
                let newColor = shaderFunc { point = { x = x; y = y }; pxlColor = currColor }
                use paint = Color.toSkiaPaint newColor
                do ctx.canvas.DrawPoint(f32 x, f32 y, paint)
        (), State.none
