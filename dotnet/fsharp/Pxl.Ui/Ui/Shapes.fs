[<AutoOpen>]
module Pxl.Ui.Shapes

open Pxl
open SkiaSharp
open Pxl.Ui.Internal.Drawing


type pxls =
    static member inline get() : Vide<_,_> =
        fun _ ctx ->
            ctx.GetSnapshot(), State.none

    static member inline set(pxls: Color array) : Vide<_,_> =
        // TODO: Optimize this
        fun _ ctx ->
            for y in 0.0 .. ctx.height - 1.0 do
                for x in 0.0 .. ctx.width - 1.0 do
                    let idx = int (y * ctx.width + x)
                    ctx.canvas.DrawPoint(f32 x, f32 y, Color.toSkiaPaint pxls[idx])
            (), State.none


type pxl() =
    inherit StrokeBuilder(false)

    member val _xy =
        {|
            x = 0.0f
            y = 0.0f
        |} with get, set

    member inline this.x(x) = this._xy <- {| this._xy with x = f32 x |}; this
    member inline this.y(y) = this._xy <- {| this._xy with y = f32 y |}; this
    member inline this.xy(x, y) = this.x(x).y(y)

    interface IDirectDrawable with
        member this.End(ctx: RenderCtx) =
            this.WithStroke <| fun paint ->
                ctx.canvas.DrawPoint(this._xy.x, this._xy.y, paint)

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            (this :> IDirectDrawable).End(ctx)
            (), State.none

    static member inline xy(x, y) = pxl().x(x).y(y)

type Vide.VideBaseBuilder with member inline _.Yield(b: pxl) = b {()}

type DrawEntry with
    member inline _.pxl(x, y) = pxl().x(x).y(y)


type rect() =
    inherit StrokeFillBuilder(true)

    member val _xy =
        {|
            x = 0.0f
            y = 0.0f
        |} with get, set

    member val _wh =
        {|
            w = 10.0f
            h = 10.0f
        |} with get, set

    member inline this.x(x) = this._xy <- {| this._xy with x = f32 x |}; this
    member inline this.y(y) = this._xy <- {| this._xy with y = f32 y |}; this
    member inline this.xy(x, y) = this.x(x).y(y)

    /// Sets the width based on the current x position.
    member inline this.x2(x2) = this._wh <- {| this._wh with w = f32 x2 - this._xy.x |}; this
    /// Sets the height based on the current y position.
    member inline this.y2(y2) = this._wh <- {| this._wh with h = f32 y2 - this._xy.y |}; this

    member inline this.w(w) = this._wh <- {| this._wh with w = f32 w |}; this
    member inline this.h(h) = this._wh <- {| this._wh with h = f32 h |}; this
    member inline this.wh(w, h) = this.w(w).h(h)

    interface IDirectDrawable with
        member this.End(ctx: RenderCtx) =
            this.WithStrokeFill <| fun paint ->
                ctx.canvas.DrawRect(this._xy.x, this._xy.y, this._wh.w, this._wh.h, paint)

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            (this :> IDirectDrawable).End(ctx)
            (), State.none

    static member inline xy(x, y) = rect().x(x).y(y)
    static member inline xywh(x, y, w, h) = rect().x(x).y(y).w(w).h(h)

type Vide.VideBaseBuilder with member inline _.Yield(b: rect) = b {()}

type DrawEntry with
    member inline _.rect(x, y, w, h) = rect().x(x).y(y).w(w).h(h)


type line() =
    inherit StrokeBuilder(true)

    member val _xy1 =
        {|
            x = 0.0f
            y = 0.0f
        |} with get, set

    member val _xy2 =
        {|
            x = 0.0f
            y = 0.0f
        |} with get, set

    member inline this.x1(x) = this._xy1 <- {| this._xy1 with x = f32 x |}; this
    member inline this.y1(y) = this._xy1 <- {| this._xy1 with y = f32 y |}; this

    member inline this.x2(x) = this._xy2 <- {| this._xy2 with x = f32 x |}; this
    member inline this.y2(y) = this._xy2 <- {| this._xy2 with y = f32 y |}; this

    member inline this.p1(x, y) = this.x1(x).y1(y)
    member inline this.p2(x, y) = this.x2(x).y2(y)
    member inline this.p1p2(x1, y1, x2, y2) = this.x1(x1).y1(y1).x2(x2).y2(y2)

    interface IDirectDrawable with
        member this.End(ctx: RenderCtx) =
            this.WithStroke <| fun paint ->
                ctx.canvas.DrawLine(this._xy1.x, this._xy1.y, this._xy2.x, this._xy2.y, paint)

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            (this :> IDirectDrawable).End(ctx)
            (), State.none

    static member inline p1p2(x1, y1, x2, y2) = line().p1p2(x1, y1, x2, y2)
    static member inline p1(x, y) = line().p1(x, y)
    // TODO: H/V line; line with angle

type Vide.VideBaseBuilder with member inline _.Yield(b: line) = b {()}

type DrawEntry with
    member inline _.line(x1, y1, x2, y2) = line().p1p2(x1, y1, x2, y2)


type circle() =
    inherit StrokeFillBuilder(true)

    member val _xyr =
        {|
            x = 0.0f
            y = 0.0f
            r = 10.0f
        |} with get, set

    member inline this.x(x) = this._xyr <- {| this._xyr with x = f32 x |}; this
    member inline this.y(y) = this._xyr <- {| this._xyr with y = f32 y |}; this
    member inline this.r(r) = this._xyr <- {| this._xyr with r = f32 r |}; this

    member inline this.xy(x, y) = this.x(x).y(y)

    interface IDirectDrawable with
        member this.End(ctx: RenderCtx) =
            this.WithStrokeFill <| fun paint ->
                ctx.canvas.DrawCircle(this._xyr.x, this._xyr.y, this._xyr.r, paint)

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            (this :> IDirectDrawable).End(ctx)
            (), State.none

    static member inline xy(x, y) = circle().x(x).y(y)
    static member inline xyr(x, y, r) = circle().x(x).y(y).r(r)

type Vide.VideBaseBuilder with member inline _.Yield(b: circle) = b {()}

type DrawEntry with
    member inline _.circle(x, y, r) = circle().x(x).y(y).r(r)


// TODO: we still need this - or can we just make an overload of Oval?
type oval() =
    inherit StrokeFillBuilder(true)

    member val _xy =
        {|
            x = 0.0f
            y = 0.0f
        |} with get, set

    member val _wh =
        {|
            w = 10.0f
            h = 10.0f
        |} with get, set

    member inline this.x(x) = this._xy <- {| this._xy with x = f32 x |}; this
    member inline this.y(y) = this._xy <- {| this._xy with y = f32 y |}; this
    member inline this.xy(x, y) = this.x(x).y(y)

    /// Sets the width based on the current x position.
    member inline this.x2(x2) = this._wh <- {| this._wh with w = f32 x2 - this._xy.x |}; this
    /// Sets the height based on the current y position.
    member inline this.y2(y2) = this._wh <- {| this._wh with h = f32 y2 - this._xy.y |}; this

    member inline this.w(w) = this._wh <- {| this._wh with w = f32 w |}; this
    member inline this.h(h) = this._wh <- {| this._wh with h = f32 h |}; this
    member inline this.wh(w, h) = this.w(w).h(h)

    interface IDirectDrawable with
        member this.End(ctx: RenderCtx) =
            this.WithStrokeFill <| fun paint ->
                ctx.canvas.DrawOval(SKRect.Create(this._xy.x, this._xy.y, this._wh.w, this._wh.h), paint)

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            (this :> IDirectDrawable).End(ctx)
            (), State.none

    static member inline xywh(x, y, w, h) = oval().x(x).y(y).w(w).h(h)
    static member inline centerRad(cx, cy, rx, ry) = oval().x(cx - rx).y(cy - ry).w(rx * 2.0).h(ry * 2.0)

type Vide.VideBaseBuilder with member inline _.Yield(b: oval) = b {()}

type DrawEntry with
    member inline _.oval(x, y, w, h) = oval().x(x).y(y).w(w).h(h)


type arc() =
    inherit StrokeFillBuilder(true)

    member val _xy =
        {|
            x = 0.0f
            y = 0.0f
        |} with get, set

    member val _wh =
        {|
            w = 10.0f
            h = 10.0f
        |} with get, set

    member val _angles =
        {|
            start = -90.0f
            arc = 0.0f
        |} with get, set

    member inline this.x(x) = this._xy <- {| this._xy with x = f32 x |}; this
    member inline this.y(y) = this._xy <- {| this._xy with y = f32 y |}; this
    member inline this.xy(x, y) = this.x(x).y(y)

    /// Sets the width based on the current x position.
    member inline this.x2(x2) = this._wh <- {| this._wh with w = f32 x2 - this._xy.x |}; this
    /// Sets the height based on the current y position.
    member inline this.y2(y2) = this._wh <- {| this._wh with h = f32 y2 - this._xy.y |}; this

    member inline this.w(w) = this._wh <- {| this._wh with w = f32 w |}; this
    member inline this.h(h) = this._wh <- {| this._wh with h = f32 h |}; this
    member inline this.wh(w, h) = this.w(w).h(h)

    member inline this.startAngle(angle) = this._angles <- {| this._angles with start = f32 angle |}; this
    member inline this.angle(angle) = this._angles <- {| this._angles with arc = f32 angle |}; this

    interface IDirectDrawable with
        member this.End(ctx: RenderCtx) =
            this.WithStrokeFill <| fun paint ->
                let w = this._wh.w
                let h = this._wh.h
                ctx.canvas.DrawArc(
                    new SKRect(f32 this._xy.x, f32 this._xy.y, f32 (this._xy.x + w), f32 (this._xy.y + h)),
                    this._angles.start,
                    this._angles.arc,
                    true,
                    paint)

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            (this :> IDirectDrawable).End(ctx)
            (), State.none

    static member inline xywh(x, y, w, h) = arc().x(x).y(y).w(w).h(h)

type Vide.VideBaseBuilder with member inline _.Yield(b: arc) = b {()}

type DrawEntry with
    member inline _.arc(x, y, w, h) = arc().x(x).y(y).w(w).h(h)


// TODO: we could provide another, more PXL-like API / fluent
type Polygon() =
    inherit StrokeFillBuilder(true)

    member val _geo = None with get, set
    member inline this.geo(f) = this._geo <- Some f; this

    interface IDirectDrawable with
        member this.End(ctx: RenderCtx) =
            let path = new SKPath()
            match this._geo with
            | Some f ->
                do f path
                this.WithStrokeFill <| fun paint ->
                    ctx.canvas.DrawPath(path, paint)
            | None -> ()

    member this.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            (this :> IDirectDrawable).End(ctx)
            (), State.none

type polygon =
    static member inline define(func) = Polygon().geo(func)

type Vide.VideBaseBuilder with member inline _.Yield(b: Polygon) = b {()}

type DrawEntry with
    member inline _.polygon(f: System.Action<SKPath>) = Polygon().geo(fun path -> f.Invoke(path))

type bg(color: Color) =
    inherit DrawableBuilder()

    member _.Run(_: Vide<_,_>) : Vide<_,_> =
        fun _ ctx ->
            // that had made really huge problems (complete RPi crash)
            // with WPS animation...
            // ctx.canvas.Clear(Color.toSkiaColor color)

            // ...so it's a "scene" BG now.
            ctx.canvas.DrawRect(0f, 0f, f32 ctx.width, f32 ctx.height, Color.toSkiaPaint color)
            (), State.none

    static member inline color(color) = bg(color)

type Vide.VideBaseBuilder with member inline _.Yield(b: bg) = b {()}

type DrawEntry with
    member inline _.bg(color) = bg(color)
