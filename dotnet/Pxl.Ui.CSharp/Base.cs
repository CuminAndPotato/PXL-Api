namespace Pxl.Ui;

using System.Collections;
using System.Collections.Concurrent;
using Microsoft.FSharp.Core;
using Pxl;
using Unit = Microsoft.FSharp.Core.Unit;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class BuilderStyleAttribute : Attribute { public BuilderStyleAttribute() { } }

public class DrawingFunctions(RenderCtx ctx)
{
    public DrawingContext Context() => new(ctx);
}

public class DrawingContext(RenderCtx ctx)
{
    class NoOperation : IDirectDrawable { public void End(RenderCtx value) { } }

    private IDirectDrawable _builder = new NoOperation();

    private T Begin<T>(Func<T> factory) where T : IDirectDrawable
    {
        _builder.End(ctx);
        var builder = factory();
        _builder = builder;
        return builder;
    }
}
