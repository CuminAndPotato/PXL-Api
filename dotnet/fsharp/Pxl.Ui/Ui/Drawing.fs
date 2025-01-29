namespace Pxl.Ui.Internal.Drawing

open Pxl
open SkiaSharp

module Color =
    let toSkiaColor (this: Color) =
        new SKColor(byte this.r, byte this.g, byte this.b, byte this.a)
    let toSkiaPaint (this: Color) =
        new SKPaint(Color = toSkiaColor this)

type StrokeData =
    {
        stroke: Color
        strokeThickness: float
    }

type FillData =
    {
        fill: Color
    }

type AntiAliasData =
    {
        isAntiAlias: bool
    }

type IStrokeBuilder =
    abstract member _stroke: StrokeData with get, set

type IFillBuilder =
    abstract member _fill: FillData with get, set

type IAntiAliasBuilder =
    abstract member _antiAlias: AntiAliasData with get, set

[<AbstractClass>]
type StrokeBuilder(isAntialias) =
    inherit DrawableBuilder()

    interface IStrokeBuilder with
        member val _stroke =
            {
                stroke = Colors.transparentBlack
                strokeThickness = 1.0
            } with get, set

    interface IAntiAliasBuilder with
        member val _antiAlias =
            {
                isAntiAlias = isAntialias
            } with get, set



[<AbstractClass>]
type FillBuilder(isAntialias) =
    inherit DrawableBuilder()

    interface IFillBuilder with
        member val _fill =
            {
                fill = Colors.transparentBlack
            } with get, set

    interface IAntiAliasBuilder with
        member val _antiAlias =
            {
                isAntiAlias = isAntialias
            } with get, set


[<AbstractClass>]
type StrokeFillBuilder(isAntialias) =
    inherit DrawableBuilder()

    interface IStrokeBuilder with
        member val _stroke =
            {
                stroke = Colors.transparentBlack
                strokeThickness = 1.0
            } with get, set

    interface IFillBuilder with
        member val _fill =
            {
                fill = Colors.transparentBlack
            } with get, set

    interface IAntiAliasBuilder with
        member val _antiAlias =
            {
                isAntiAlias = isAntialias
            } with get, set


namespace Pxl.Ui

open System.Runtime.CompilerServices
open SkiaSharp
open Pxl
open Pxl.Ui.Internal.Drawing

[<Extension>]
type StrokeDrawableExtensions =

    [<Extension>]
    static member stroke(this: IStrokeBuilder) = this._stroke.stroke
    [<Extension>]
    static member stroke<'b when 'b :> IStrokeBuilder>(this: 'b, color: Color, ?thickness: float) =
        this._stroke <- { this._stroke with stroke = color }
        match thickness with Some t -> this._stroke <- { this._stroke with strokeThickness = t } | None -> ()
        this
    [<Extension>]
    static member noStroke<'b when 'b :> IStrokeBuilder>(this: 'b) = this._stroke <- { this._stroke with stroke = Colors.transparentBlack }; this

    [<Extension>]
    static member strokeThickness(this: IStrokeBuilder) = this._stroke.strokeThickness
    [<Extension>]
    static member strokeThickness<'b when 'b :> IStrokeBuilder>(this: 'b, thickness: float) = this._stroke <- { this._stroke with strokeThickness = thickness }; this

    [<Extension>]
    static member fill(this: IFillBuilder) = this._fill.fill
    [<Extension>]
    static member fill<'b when 'b :> IFillBuilder>(this: 'b, color: Color) = this._fill <- { this._fill with fill = color }; this
    [<Extension>]
    static member noFill<'b when 'b :> IFillBuilder>(this: 'b) = this._fill <- { this._fill with fill = Colors.transparentBlack }; this

    [<Extension>]
    static member isAntiAlias(this: IAntiAliasBuilder) = this._antiAlias.isAntiAlias
    [<Extension>]
    static member isAntiAlias<'b when 'b :> IAntiAliasBuilder>(this: 'b, value: bool) = this._antiAlias <- { this._antiAlias with isAntiAlias = value }; this
    [<Extension>]
    static member useAntiAlias<'b when 'b :> IAntiAliasBuilder>(this: 'b) = this._antiAlias <- { this._antiAlias with isAntiAlias = true }; this
    [<Extension>]
    static member noAntiAlias<'b when 'b :> IAntiAliasBuilder>(this: 'b) = this._antiAlias <- { this._antiAlias with isAntiAlias = false }; this

    [<Extension>]
    static member internal WithStroke<'b when 'b :> IStrokeBuilder and 'b :> IAntiAliasBuilder>(this: 'b, f) =
        let sb = this :> IStrokeBuilder
        if sb._stroke.stroke.a > 0uy || sb._stroke.strokeThickness > 0.0 then
            let paint = Color.toSkiaPaint sb._stroke.stroke
            paint.StrokeWidth <- f32 sb._stroke.strokeThickness
            paint.IsAntialias <- (this :> IAntiAliasBuilder)._antiAlias.isAntiAlias
            paint.Style <- SKPaintStyle.Stroke
            f paint

    [<Extension>]
    static member internal WithFill<'b when 'b :> IFillBuilder and 'b :> IAntiAliasBuilder>(this: 'b, f) =
        let fb = this :> IFillBuilder
        if fb._fill.fill.a > 0uy then
            let paint = Color.toSkiaPaint fb._fill.fill
            paint.IsAntialias <- (this :> IAntiAliasBuilder)._antiAlias.isAntiAlias
            paint.Style <- SKPaintStyle.Fill
            f paint


    [<Extension>]
    static member internal WithStrokeFill<'b when 'b :> IStrokeBuilder and 'b :> IFillBuilder and 'b :> IAntiAliasBuilder>(this: 'b, f) =
        this.WithStroke(f)
        this.WithFill(f)
