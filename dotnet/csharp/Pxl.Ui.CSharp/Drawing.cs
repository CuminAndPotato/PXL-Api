namespace Pxl.Ui;

using Pxl;

/* AGENT COMMAND: GEN_FLUENT

    - scan for extension classes (e.g. public static class LineDrawOperationExtensions)
    - remove this code
    - Scan all classes that inherit from IDirectDrawable
      (e.g. LineDrawOperation)
    - directly under their definition, create a new extension class
      (e.g. LineDrawOperationExtensions)
    - Search every property or field is decorated with [BuilderStyle],
      (e.g. public double X1 { get; set; } )
    - Generate an extension method for every found item
      (e.g. public static LineDrawOperation X1(this LineDrawOperation op, double x1) { op.X1 = x1; return op; } )

    ALSO:
    - When the class (e.g. LineDrawOperation) has a constructor with parameters,
      create a static method on the extension class with the same parameters that calls the constructor
      e.g.: public static LineDrawOperation DrawLine(this RenderCtx ctx, double x1, double y1, double x2, double y2) => ctx.BeginDirectDrawable(new(x1, y1, x2, y2));
    - The name of the method should be the same as the class name without the "DrawOperation" suffix.

    IMPORTANT:
    - You only change this file - no other files!
    - You only do exactly what is asked - no other things!
    - Stick to the examples as close as possible!
    - Don't be verbose - only a small summary of what you did.
*/

public partial class LineDrawOperation(double x1, double y1, double x2, double y2) : IDirectDrawable
{
    [BuilderStyle] public double X1 { get; set; } = x1;
    [BuilderStyle] public double Y1 { get; set; } = y1;
    [BuilderStyle] public double X2 { get; set; } = x2;
    [BuilderStyle] public double Y2 { get; set; } = y2;
    [BuilderStyle] public Color Color { get; set; } = Colors.lime;
    [BuilderStyle] public double Thickness { get; set; } = 1.0;
    [BuilderStyle] public bool AntiAlias { get; set; } = false;

    public void End(RenderCtx value)
    {
        var skiaCanvas = value.canvas;
        var skColor = new SkiaSharp.SKColor(Color.r, Color.g, Color.b, Color.a);
        using var paint = new SkiaSharp.SKPaint
        {
            Color = skColor,
            StrokeWidth = (float)Thickness,
            IsAntialias = AntiAlias,
            Style = SkiaSharp.SKPaintStyle.Stroke
        };
        skiaCanvas.DrawLine((float)X1, (float)Y1, (float)X2, (float)Y2, paint);
    }
}

public static class LineDrawOperationExtensions
{
    public static LineDrawOperation DrawLine(this RenderCtx ctx, double x1, double y1, double x2, double y2) => ctx.BeginDirectDrawable(new LineDrawOperation(x1, y1, x2, y2));

    public static LineDrawOperation X1(this LineDrawOperation op, double x1)
    {
        op.X1 = x1;
        return op;
    }

    public static LineDrawOperation Y1(this LineDrawOperation op, double y1)
    {
        op.Y1 = y1;
        return op;
    }

    public static LineDrawOperation X2(this LineDrawOperation op, double x2)
    {
        op.X2 = x2;
        return op;
    }

    public static LineDrawOperation Y2(this LineDrawOperation op, double y2)
    {
        op.Y2 = y2;
        return op;
    }

    public static LineDrawOperation Color(this LineDrawOperation op, Color color)
    {
        op.Color = color;
        return op;
    }

    public static LineDrawOperation Thickness(this LineDrawOperation op, double thickness)
    {
        op.Thickness = thickness;
        return op;
    }

    public static LineDrawOperation AntiAlias(this LineDrawOperation op, bool antiAlias)
    {
        op.AntiAlias = antiAlias;
        return op;
    }
}

public partial class PxlDrawOperation(double x, double y) : IDirectDrawable
{
    [BuilderStyle] public double X { get; set; } = x;
    [BuilderStyle] public double Y { get; set; } = y;
    [BuilderStyle] public Color Color { get; set; } = Colors.lime;
    [BuilderStyle] public double Size { get; set; } = 1.0;

    public void End(RenderCtx value)
    {
        var skiaCanvas = value.canvas;
        var skColor = new SkiaSharp.SKColor(Color.r, Color.g, Color.b, Color.a);
        using var paint = new SkiaSharp.SKPaint
        {
            Color = skColor,
            IsAntialias = false,
            Style = SkiaSharp.SKPaintStyle.Fill
        };
        if (Size <= 1.0)
            skiaCanvas.DrawPoint((float)X, (float)Y, paint);
        else
            skiaCanvas.DrawRect((float)X, (float)Y, (float)Size, (float)Size, paint);
    }
}

public static class PxlDrawOperationExtensions
{
    public static PxlDrawOperation Pxl(this RenderCtx ctx, double x, double y) => ctx.BeginDirectDrawable(new PxlDrawOperation(x, y));

    public static PxlDrawOperation X(this PxlDrawOperation op, double x)
    {
        op.X = x;
        return op;
    }

    public static PxlDrawOperation Y(this PxlDrawOperation op, double y)
    {
        op.Y = y;
        return op;
    }

    public static PxlDrawOperation Color(this PxlDrawOperation op, Color color)
    {
        op.Color = color;
        return op;
    }

    public static PxlDrawOperation Size(this PxlDrawOperation op, double size)
    {
        op.Size = size;
        return op;
    }
}
