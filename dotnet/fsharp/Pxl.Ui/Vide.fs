[<AutoOpen>]
module Pxl.Vide

// important: The check for apps is dyncmic - remember when changing
type Vide<'v,'s> = ('s option -> RenderCtx -> 'v * 's option)

module State =
    let none : unit option = None

[<AbstractClass>]
type DrawableBuilder() =
    member _.Zero() : Vide<_,_> =
        fun _ ctx -> (), State.none

[<RequireQualifiedAccess>]
module Vide =
    open System
    open FSharp.Reflection
    open System.Reflection

    let inline bind ([<InlineIfLambda>] f) ([<InlineIfLambda>] m: Vide<_,_>) : Vide<_,_> =
        fun s ctx ->
            let ms,fs =
                match s with
                | None -> None,None
                | Some (ms,fs) -> ms,fs
            let mv,ms = m ms ctx
            let f: Vide<_,_> = f mv
            let fv,fs = f fs ctx
            fv, Some (ms,fs)

    let ofValue x : Vide<_,_>  =
        fun _ _ -> x, State.none

    let inline yieldUnitVide ([<InlineIfLambda>] v: Vide<unit,_>) : Vide<_,_> =
        fun s ctx ->
            let _,s = v s ctx
            (),s

    let zeroNoState () : Vide<_,_> =
        fun _ ctx -> (), State.none

    let zeroAdaptiveState<'s> () : Vide<_,'s> =
        fun _ ctx -> (), None

    let inline combineUsingResultOfB
        ([<InlineIfLambda>] a: Vide<_,_>)
        ([<InlineIfLambda>] b: Vide<_,_>)
        : Vide<_,_>
        =
        fun s ctx ->
            let sa,sb =
                match s with
                | None -> None,None
                | Some (ms,fs) -> ms,fs
            let va,sa = a sa ctx
            let vb,sb = b sb ctx
            vb, Some (sa,sb)

    let inline forElements<'e,'s when 'e : comparison>
        (elems: seq<'e>)
        ([<InlineIfLambda>] body: 'e -> Vide<unit,'s>)
        : Vide<unit, Map<'e,'s option>>
        =
        fun s ctx ->
            let mutable currMap = defaultArg s Map.empty
            let resValues,resStates =
                [ for elem in elems do
                    let matchingState =
                        let found,maybeValue = currMap.TryGetValue(elem)
                        if found then maybeValue else None
                    let v,s =
                        let v : Vide<_,_> = body elem
                        v matchingState ctx
                    do currMap.Remove(elem) |> ignore
                    v,(elem,s)
                ]
                |> List.unzip
            let newState = resStates |> Map.ofList
            if newState.Count <> resStates.Length then
                failwith "Duplicate key in forWithKVP"
            (), Some newState

    let asObjectState (v: Vide<_,'s>) : Vide<_,obj> =
        fun s ctx ->
            let s = s |> Option.map (fun s -> s :?> 's)
            let v,s = v s ctx
            v, s |> Option.map (fun s -> s :> obj)

    type HiddenState = { originalState: obj }

    let hideState (videMethod: MethodInfo) : Vide<unit,obj> =
        let rec completeFuncSig typ =
            let d,r = FSharpType.GetFunctionElements(typ)
            match FSharpType.IsFunction r with
            | true -> d :: completeFuncSig r
            | false -> [ d; r ]
        if videMethod.GetParameters().Length <> 0 then
            failwith "No parameters expected"
        let currSig = completeFuncSig videMethod.ReturnType
        let types =
            match currSig with
            | [ inState; renderCtx; valueAndOutState ] ->
                // important: The check is dyncmic - if we change Vide, we have to change this.
                // type Vide<'v,'s> = ('s option -> RenderCtx -> 'v * 's option)
                if renderCtx <> typedefof<RenderCtx> then
                    failwith "Expected RenderCtx as second argument"
                if inState.GetGenericTypeDefinition() <> typedefof<option<_>> then
                    failwith "Expected inState to be option<_>"
                if valueAndOutState.GetGenericTypeDefinition() <> typedefof<Tuple<_,_>> then
                    failwith "Expected valueAndOutState to be a tuple of (_ * option<_>)"
                let value = valueAndOutState.GetGenericArguments()[0]
                let outState = valueAndOutState.GetGenericArguments()[1]
                if inState <> outState then
                    failwith "Expected inState and outState to be the same"
                {|
                    inState = inState
                    valueAndOutState = valueAndOutState
                    value = value
                    outState = outState
                |}
            | _ -> failwith "Expected 3 arguments"
        let getStateFromRes =
            // let p1 = valueAndOutState.GetProperty("Item1")
            let p2 = types.valueAndOutState.GetProperty("Item2")
            fun res ->
                // let value = p1.GetValue(res, null)
                p2.GetValue(res, null)
        let getOptionValueAsObj (o: obj) =
            let t = o.GetType()
            let getValue = t.GetProperty("Value")
            let getIsSome = t.GetProperty("IsSome")
            if not (getIsSome.GetValue(o, [| o |]) :?> bool)
            then None
            else Some (getValue.GetValue(o))
        let fsharpfunc = videMethod.Invoke(null, Array.empty)
        let f = fsharpfunc.GetType().GetMethods() |> Array.find (fun x -> x.Name = "Invoke" && x.GetParameters().Length = 2)
        fun s ctx ->
            let s =
                match s with
                | None -> None :> obj
                | Some s -> (s :?> HiddenState).originalState
            let res = f.Invoke(fsharpfunc, [| s; ctx |])
            let s = getStateFromRes res
            (), Some { originalState = s }

    type VideBaseBuilder() =
        member inline _.Bind(m, [<InlineIfLambda>] f) = bind f m

        member _.Return(x) = ofValue x
        member _.ReturnFrom(x) = x

        member inline _.Yield(x) = yieldUnitVide x

        member _.Zero() = zeroNoState ()

        // nested ifs make problem and this solves it - see comments in Vide:
        // The "else" branch of those ifs are not specified
        // member _.Zero<'s>() = zeroAdaptiveState<'s> ()

        member inline _.Delay(f: unit -> Vide<_,_>) = f
        member inline _.Combine(a, b) = combineUsingResultOfB a (b ())
        member inline _.For(seq, body) = forElements seq body

    type VideBuilder() =
        inherit VideBaseBuilder()
        member inline _.Run([<InlineIfLambda>] f: unit -> Vide<_,_>) : Vide<_,_> =
            fun s ctx ->
                let saveCount = ctx.canvas.SaveLayer()
                let v : Vide<_,_> = f ()
                let res = v s ctx
                do ctx.canvas.RestoreToCount(saveCount)
                res

    type Mutable<'a when 'a: equality>(initial: 'a) =
        let mutable currVal = initial
        member this.Reset() =
            this.value <- initial
        member _.value
            with get() = currVal
            and set(value) =
                // Not just a perf opt: prevent stack overflows (see demo case asyncHelloWorld)!
                if value <> currVal then
                    do currVal <- value
        override _.ToString() = sprintf $"Mutable({currVal})"

    type DelayBuilder() =
        member _.Yield(x) = x
        member _.Zero() = ()
        member _.Combine(_, b) = b
        member _.Delay(f) = f

    type UseMemoBuilder() =
        inherit DelayBuilder()
        member inline _.Run([<InlineIfLambda>] f) : Vide<_,_> =
            fun s ctx ->
                let s = s |> Option.defaultWith (fun () -> f ())
                s, Some s

    type UseStateBuilder() =
        inherit DelayBuilder()
        member inline _.Run([<InlineIfLambda>] f) : Vide<_,_> =
            fun s ctx ->
                let s = s |> Option.defaultWith (fun () -> Mutable(f ()))
                s, Some s

let scene = Vide.VideBuilder()
let useMemo = Vide.UseMemoBuilder()
let useState = Vide.UseStateBuilder()

let x () = scene {
    let! a = useState { 0 }
    let! b = useState { 0 }
    return a.value + b.value
}

// this is for "else" branches
[<GeneralizableValue>]
let preserveState<'s> : Vide<_,_> =
    fun (s: 's option) ctx ->
        (),s

[<GeneralizableValue>]
let discardState<'s> : Vide<_,_> =
    fun (s: 's option) ctx -> (),None

let getCtx () : Vide<_,_> =
    fun _ ctx -> ctx, State.none


[<AutoOpen>]
module ControlFlow =

    type Branch2<'b1, 'b2> =
        | B1Of2 of 'b1
        | B2Of2 of 'b2

    type Branch3<'b1, 'b2, 'b3> =
        | B1Of3 of 'b1
        | B2Of3 of 'b2
        | B3Of3 of 'b3

    type Branch4<'b1, 'b2, 'b3, 'b4> =
        | B1Of4 of 'b1
        | B2Of4 of 'b2
        | B3Of4 of 'b3
        | B4Of4 of 'b4

    type Branch5<'b1, 'b2, 'b3, 'b4, 'b5> =
        | B1Of5 of 'b1
        | B2Of5 of 'b2
        | B3Of5 of 'b3
        | B4Of5 of 'b4
        | B5Of5 of 'b5

    type Branch6<'b1, 'b2, 'b3, 'b4, 'b5, 'b6> =
        | B1Of6 of 'b1
        | B2Of6 of 'b2
        | B3Of6 of 'b3
        | B4Of6 of 'b4
        | B5Of6 of 'b5
        | B6Of6 of 'b6

    type Branch7<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7> =
        | B1Of7 of 'b1
        | B2Of7 of 'b2
        | B3Of7 of 'b3
        | B4Of7 of 'b4
        | B5Of7 of 'b5
        | B6Of7 of 'b6
        | B7Of7 of 'b7

    type Branch8<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8> =
        | B1Of8 of 'b1
        | B2Of8 of 'b2
        | B3Of8 of 'b3
        | B4Of8 of 'b4
        | B5Of8 of 'b5
        | B6Of8 of 'b6
        | B7Of8 of 'b7
        | B8Of8 of 'b8

    type Branch9<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8, 'b9> =
        | B1Of9 of 'b1
        | B2Of9 of 'b2
        | B3Of9 of 'b3
        | B4Of9 of 'b4
        | B5Of9 of 'b5
        | B6Of9 of 'b6
        | B7Of9 of 'b7
        | B8Of9 of 'b8
        | B9Of9 of 'b9

    type Branch10<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8, 'b9, 'b10> =
        | B1Of10 of 'b1
        | B2Of10 of 'b2
        | B3Of10 of 'b3
        | B4Of10 of 'b4
        | B5Of10 of 'b5
        | B6Of10 of 'b6
        | B7Of10 of 'b7
        | B8Of10 of 'b8
        | B9Of10 of 'b9
        | B10Of10 of 'b10

    type Branch11<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8, 'b9, 'b10, 'b11> =
        | B1Of11 of 'b1
        | B2Of11 of 'b2
        | B3Of11 of 'b3
        | B4Of11 of 'b4
        | B5Of11 of 'b5
        | B6Of11 of 'b6
        | B7Of11 of 'b7
        | B8Of11 of 'b8
        | B9Of11 of 'b9
        | B10Of11 of 'b10
        | B11Of11 of 'b11

    type Branch12<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8, 'b9, 'b10, 'b11, 'b12> =
        | B1Of12 of 'b1
        | B2Of12 of 'b2
        | B3Of12 of 'b3
        | B4Of12 of 'b4
        | B5Of12 of 'b5
        | B6Of12 of 'b6
        | B7Of12 of 'b7
        | B8Of12 of 'b8
        | B9Of12 of 'b9
        | B10Of12 of 'b10
        | B11Of12 of 'b11
        | B12Of12 of 'b12

    type Branch13<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8, 'b9, 'b10, 'b11, 'b12, 'b13> =
        | B1Of13 of 'b1
        | B2Of13 of 'b2
        | B3Of13 of 'b3
        | B4Of13 of 'b4
        | B5Of13 of 'b5
        | B6Of13 of 'b6
        | B7Of13 of 'b7
        | B8Of13 of 'b8
        | B9Of13 of 'b9
        | B10Of13 of 'b10
        | B11Of13 of 'b11
        | B12Of13 of 'b12
        | B13Of13 of 'b13

    type Branch14<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8, 'b9, 'b10, 'b11, 'b12, 'b13, 'b14> =
        | B1Of14 of 'b1
        | B2Of14 of 'b2
        | B3Of14 of 'b3
        | B4Of14 of 'b4
        | B5Of14 of 'b5
        | B6Of14 of 'b6
        | B7Of14 of 'b7
        | B8Of14 of 'b8
        | B9Of14 of 'b9
        | B10Of14 of 'b10
        | B11Of14 of 'b11
        | B12Of14 of 'b12
        | B13Of14 of 'b13
        | B14Of14 of 'b14

    type Branch15<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8, 'b9, 'b10, 'b11, 'b12, 'b13, 'b14, 'b15> =
        | B1Of15 of 'b1
        | B2Of15 of 'b2
        | B3Of15 of 'b3
        | B4Of15 of 'b4
        | B5Of15 of 'b5
        | B6Of15 of 'b6
        | B7Of15 of 'b7
        | B8Of15 of 'b8
        | B9Of15 of 'b9
        | B10Of15 of 'b10
        | B11Of15 of 'b11
        | B12Of15 of 'b12
        | B13Of15 of 'b13
        | B14Of15 of 'b14
        | B15Of15 of 'b15

    type Branch16<'b1, 'b2, 'b3, 'b4, 'b5, 'b6, 'b7, 'b8, 'b9, 'b10, 'b11, 'b12, 'b13, 'b14, 'b15, 'b16> =
        | B1Of16 of 'b1
        | B2Of16 of 'b2
        | B3Of16 of 'b3
        | B4Of16 of 'b4
        | B5Of16 of 'b5
        | B6Of16 of 'b6
        | B7Of16 of 'b7
        | B8Of16 of 'b8
        | B9Of16 of 'b9
        | B10Of16 of 'b10
        | B11Of16 of 'b11
        | B12Of16 of 'b12
        | B13Of16 of 'b13
        | B14Of16 of 'b14
        | B15Of16 of 'b15
        | B16Of16 of 'b16

    module ControlFlowBuilderBricks =

        let inline yieldBranch2 (x: Branch2<Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of2 v -> yield v | _ -> yield discardState
                match x with B2Of2 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch3 (x: Branch3<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of3 v -> yield v | _ -> yield discardState
                match x with B2Of3 v -> yield v | _ -> yield discardState
                match x with B3Of3 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch4 (x: Branch4<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of4 v -> yield v | _ -> yield discardState
                match x with B2Of4 v -> yield v | _ -> yield discardState
                match x with B3Of4 v -> yield v | _ -> yield discardState
                match x with B4Of4 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch5 (x: Branch5<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of5 v -> yield v | _ -> yield discardState
                match x with B2Of5 v -> yield v | _ -> yield discardState
                match x with B3Of5 v -> yield v | _ -> yield discardState
                match x with B4Of5 v -> yield v | _ -> yield discardState
                match x with B5Of5 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch6 (x: Branch6<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of6 v -> yield v | _ -> yield discardState
                match x with B2Of6 v -> yield v | _ -> yield discardState
                match x with B3Of6 v -> yield v | _ -> yield discardState
                match x with B4Of6 v -> yield v | _ -> yield discardState
                match x with B5Of6 v -> yield v | _ -> yield discardState
                match x with B6Of6 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch7 (x: Branch7<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of7 v -> yield v | _ -> yield discardState
                match x with B2Of7 v -> yield v | _ -> yield discardState
                match x with B3Of7 v -> yield v | _ -> yield discardState
                match x with B4Of7 v -> yield v | _ -> yield discardState
                match x with B5Of7 v -> yield v | _ -> yield discardState
                match x with B6Of7 v -> yield v | _ -> yield discardState
                match x with B7Of7 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch8 (x: Branch8<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of8 v -> yield v | _ -> yield discardState
                match x with B2Of8 v -> yield v | _ -> yield discardState
                match x with B3Of8 v -> yield v | _ -> yield discardState
                match x with B4Of8 v -> yield v | _ -> yield discardState
                match x with B5Of8 v -> yield v | _ -> yield discardState
                match x with B6Of8 v -> yield v | _ -> yield discardState
                match x with B7Of8 v -> yield v | _ -> yield discardState
                match x with B8Of8 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch9 (x: Branch9<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of9 v -> yield v | _ -> yield discardState
                match x with B2Of9 v -> yield v | _ -> yield discardState
                match x with B3Of9 v -> yield v | _ -> yield discardState
                match x with B4Of9 v -> yield v | _ -> yield discardState
                match x with B5Of9 v -> yield v | _ -> yield discardState
                match x with B6Of9 v -> yield v | _ -> yield discardState
                match x with B7Of9 v -> yield v | _ -> yield discardState
                match x with B8Of9 v -> yield v | _ -> yield discardState
                match x with B9Of9 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch10 (x: Branch10<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of10 v -> yield v | _ -> yield discardState
                match x with B2Of10 v -> yield v | _ -> yield discardState
                match x with B3Of10 v -> yield v | _ -> yield discardState
                match x with B4Of10 v -> yield v | _ -> yield discardState
                match x with B5Of10 v -> yield v | _ -> yield discardState
                match x with B6Of10 v -> yield v | _ -> yield discardState
                match x with B7Of10 v -> yield v | _ -> yield discardState
                match x with B8Of10 v -> yield v | _ -> yield discardState
                match x with B9Of10 v -> yield v | _ -> yield discardState
                match x with B10Of10 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch11 (x: Branch11<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of11 v -> yield v | _ -> yield discardState
                match x with B2Of11 v -> yield v | _ -> yield discardState
                match x with B3Of11 v -> yield v | _ -> yield discardState
                match x with B4Of11 v -> yield v | _ -> yield discardState
                match x with B5Of11 v -> yield v | _ -> yield discardState
                match x with B6Of11 v -> yield v | _ -> yield discardState
                match x with B7Of11 v -> yield v | _ -> yield discardState
                match x with B8Of11 v -> yield v | _ -> yield discardState
                match x with B9Of11 v -> yield v | _ -> yield discardState
                match x with B10Of11 v -> yield v | _ -> yield discardState
                match x with B11Of11 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch12 (x: Branch12<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of12 v -> yield v | _ -> yield discardState
                match x with B2Of12 v -> yield v | _ -> yield discardState
                match x with B3Of12 v -> yield v | _ -> yield discardState
                match x with B4Of12 v -> yield v | _ -> yield discardState
                match x with B5Of12 v -> yield v | _ -> yield discardState
                match x with B6Of12 v -> yield v | _ -> yield discardState
                match x with B7Of12 v -> yield v | _ -> yield discardState
                match x with B8Of12 v -> yield v | _ -> yield discardState
                match x with B9Of12 v -> yield v | _ -> yield discardState
                match x with B10Of12 v -> yield v | _ -> yield discardState
                match x with B11Of12 v -> yield v | _ -> yield discardState
                match x with B12Of12 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch13 (x: Branch13<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of13 v -> yield v | _ -> yield discardState
                match x with B2Of13 v -> yield v | _ -> yield discardState
                match x with B3Of13 v -> yield v | _ -> yield discardState
                match x with B4Of13 v -> yield v | _ -> yield discardState
                match x with B5Of13 v -> yield v | _ -> yield discardState
                match x with B6Of13 v -> yield v | _ -> yield discardState
                match x with B7Of13 v -> yield v | _ -> yield discardState
                match x with B8Of13 v -> yield v | _ -> yield discardState
                match x with B9Of13 v -> yield v | _ -> yield discardState
                match x with B10Of13 v -> yield v | _ -> yield discardState
                match x with B11Of13 v -> yield v | _ -> yield discardState
                match x with B12Of13 v -> yield v | _ -> yield discardState
                match x with B13Of13 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch14 (x: Branch14<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of14 v -> yield v | _ -> yield discardState
                match x with B2Of14 v -> yield v | _ -> yield discardState
                match x with B3Of14 v -> yield v | _ -> yield discardState
                match x with B4Of14 v -> yield v | _ -> yield discardState
                match x with B5Of14 v -> yield v | _ -> yield discardState
                match x with B6Of14 v -> yield v | _ -> yield discardState
                match x with B7Of14 v -> yield v | _ -> yield discardState
                match x with B8Of14 v -> yield v | _ -> yield discardState
                match x with B9Of14 v -> yield v | _ -> yield discardState
                match x with B10Of14 v -> yield v | _ -> yield discardState
                match x with B11Of14 v -> yield v | _ -> yield discardState
                match x with B12Of14 v -> yield v | _ -> yield discardState
                match x with B13Of14 v -> yield v | _ -> yield discardState
                match x with B14Of14 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch15 (x: Branch15<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of15 v -> yield v | _ -> yield discardState
                match x with B2Of15 v -> yield v | _ -> yield discardState
                match x with B3Of15 v -> yield v | _ -> yield discardState
                match x with B4Of15 v -> yield v | _ -> yield discardState
                match x with B5Of15 v -> yield v | _ -> yield discardState
                match x with B6Of15 v -> yield v | _ -> yield discardState
                match x with B7Of15 v -> yield v | _ -> yield discardState
                match x with B8Of15 v -> yield v | _ -> yield discardState
                match x with B9Of15 v -> yield v | _ -> yield discardState
                match x with B10Of15 v -> yield v | _ -> yield discardState
                match x with B11Of15 v -> yield v | _ -> yield discardState
                match x with B12Of15 v -> yield v | _ -> yield discardState
                match x with B13Of15 v -> yield v | _ -> yield discardState
                match x with B14Of15 v -> yield v | _ -> yield discardState
                match x with B15Of15 v -> yield v | _ -> yield discardState
            }

        let inline yieldBranch16 (x: Branch16<Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>, Vide<unit,_>>) =
            scene {
                match x with B1Of16 v -> yield v | _ -> yield discardState
                match x with B2Of16 v -> yield v | _ -> yield discardState
                match x with B3Of16 v -> yield v | _ -> yield discardState
                match x with B4Of16 v -> yield v | _ -> yield discardState
                match x with B5Of16 v -> yield v | _ -> yield discardState
                match x with B6Of16 v -> yield v | _ -> yield discardState
                match x with B7Of16 v -> yield v | _ -> yield discardState
                match x with B8Of16 v -> yield v | _ -> yield discardState
                match x with B9Of16 v -> yield v | _ -> yield discardState
                match x with B10Of16 v -> yield v | _ -> yield discardState
                match x with B11Of16 v -> yield v | _ -> yield discardState
                match x with B12Of16 v -> yield v | _ -> yield discardState
                match x with B13Of16 v -> yield v | _ -> yield discardState
                match x with B14Of16 v -> yield v | _ -> yield discardState
                match x with B15Of16 v -> yield v | _ -> yield discardState
                match x with B16Of16 v -> yield v | _ -> yield discardState
            }


    // -------------------------------------------------------------------
    // "Yielsd"s
    // -------------------------------------------------------------------

    type Vide.VideBuilder with
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch2(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch3(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch4(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch5(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch6(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch7(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch8(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch9(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch10(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch11(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch12(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch13(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch14(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch15(v)
        member _.Yield(v) = ControlFlowBuilderBricks.yieldBranch16(v)

