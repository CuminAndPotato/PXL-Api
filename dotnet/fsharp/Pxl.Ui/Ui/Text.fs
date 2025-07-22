[<AutoOpen>]
module Pxl.Ui.Text

open Pxl
open Pxl.Ui.Internal.Drawing
open SkiaSharp

type text(text: string) =
    inherit DrawableBuilder()

    let f = Fonts.var4x5
    member val _data =
        {|
            text = text
            x = 0.0
            y = 0.0
            color = Colors.white
            typeface = f.skFont.Typeface
            fontSize = f.height
            ascent = f.ascent
            isAntiAlias = false
        |} with get, set

    member inline this.x(x) = this._data <- {| this._data with x = x |}; this
    member inline this.y(y) = this._data <- {| this._data with y = y |}; this
    member inline this.xy(x, y) = this.x(x).y(y)

    member inline this.text(text) = this._data <- {| this._data with text = text |}; this
    member inline this.color(color) = this._data <- {| this._data with color = color |}; this
    member inline this.typeface(typeface) = this._data <- {| this._data with typeface = typeface |}; this
    member inline this.fontSize(fontSize) = this._data <- {| this._data with fontSize = fontSize |}; this
    member inline this.ascent(ascent) = this._data <- {| this._data with ascent = ascent |}; this

    member inline this.isAntiAlias() = this._data.isAntiAlias
    member inline this.isAntiAlias(value: bool) = this._data <- {| this._data with isAntiAlias = value |}; this
    member inline this.useAntiAlias(useAntiAlias) = this._data <- {| this._data with isAntiAlias = useAntiAlias |}; this
    member inline this.noAntiAlias() = this._data <- {| this._data with isAntiAlias = false |}; this

    member this.createPaint() =
        new SKPaint(
            Color = Color.toSkiaColor this._data.color,
            TextSize = f32 this._data.fontSize,
            Typeface = this._data.typeface,
            IsAntialias = this._data.isAntiAlias)

    member inline this.measure() =
        use paint = this.createPaint()
        let text : string = this._data.text
        let res = paint.MeasureText(text)
        float res

    member inline this.font(font: Fonts.BuiltinFont) =
        this
            .typeface(font.skFont.Typeface)
            .fontSize(font.height)
            .ascent(font.ascent)

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            use paint = this.createPaint()
            ctx.canvas.DrawText(
                this._data.text,
                f32 this._data.x,
                f32 <| this._data.y + this._data.ascent + this._data.fontSize, // ascent is negative (!)
                paint)
            (), State.none

    static member inline mono3x5(value, ?x, ?y) = text(value).font(Fonts.mono3x5).xy(defaultArg x 0.0, defaultArg y 0.0)
    static member inline var3x5(value, ?x, ?y) = text(value).font(Fonts.var3x5).xy(defaultArg x 0.0, defaultArg y 0.0)
    static member inline mono4x5(value, ?x, ?y) = text(value).font(Fonts.mono4x5).xy(defaultArg x 0.0, defaultArg y 0.0)
    static member inline var4x5(value, ?x, ?y) = text(value).font(Fonts.var4x5).xy(defaultArg x 0.0, defaultArg y 0.0)
    static member inline mono6x6(value, ?x, ?y) = text(value).font(Fonts.mono6x6).xy(defaultArg x 0.0, defaultArg y 0.0)
    static member inline mono7x10(value, ?x, ?y) = text(value).font(Fonts.mono7x10).xy(defaultArg x 0.0, defaultArg y 0.0)
    static member inline var10x10(value, ?x, ?y) = text(value).font(Fonts.var10x10).xy(defaultArg x 0.0, defaultArg y 0.0)
    static member inline mono10x10(value, ?x, ?y) = text(value).font(Fonts.mono10x10).xy(defaultArg x 0.0, defaultArg y 0.0)
    static member inline mono16x16(value, ?x, ?y) = text(value).font(Fonts.mono16x16).xy(defaultArg x 0.0, defaultArg y 0.0)

type Vide.VideBaseBuilder with member inline _.Yield(b: text) = b {()}


[<RequireQualifiedAccess>]
type VAlign =
    | Top
    | Center
    | Bottom
    | Row of int
    | Y of float

[<AutoOpen>]
type DrawText =

    // TODO: the VAlign should be more general for tex
    static member tickerText
        (
            displayText: string,
            color: Color,
            ?speedInPxPerSec: float,
            ?addPadding,
            ?vAlign: VAlign,
            ?restartWhenTextChanges: bool
        ) =
        let speedInPxPerSec = defaultArg speedInPxPerSec 16.0
        let font = Fonts.var4x5
        let vAlign = defaultArg vAlign VAlign.Center
        let space = " "
        let paint = new SKPaint(
            Color = Color.toSkiaColor color,
            TextSize = f32 font.height,
            Typeface = font.skFont.Typeface,
            IsAntialias = true)
        let measureText (text: string) = paint.MeasureText(text)
        let spaceWidth = measureText space |> float
        scene {
            let! ctx = getCtx()
            let! hasTextChanged = Logic.hasChanged(displayText)
            let padStr = space |> String.replicate (int <| if defaultArg addPadding true then ctx.width / spaceWidth else 0.0)
            let displayText = padStr + displayText
            let y =
                match vAlign with
                | VAlign.Top -> 0.0
                | VAlign.Center -> (ctx.height - font.height) / 2.0
                | VAlign.Bottom -> ctx.height - font.height
                | VAlign.Row row -> (font.height + 1.0) * float row
                | VAlign.Y y -> y
            let textWidth = measureText displayText
            let! x = Anim.linear(float textWidth / speedInPxPerSec, 0, -textWidth, repeat = Repeat.Loop)
            let x =
                if hasTextChanged && defaultArg restartWhenTextChanges true
                then 0.0
                else x.value
            text(displayText).xy(x, y).color(color).font(font)
        }
