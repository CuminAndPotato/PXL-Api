namespace Pxl

open System
open Pxl

module internal CSharpSceneHandling =
    open System.Collections.Generic
    open System.Threading

    let private contextsLock = obj()

    type private ThreadBasedContexts =
        static member producerThreadBasedContexts: Dictionary<Thread, ThreadBasedContext> = Dictionary()
        static member consumerThreadBasedContexts: Dictionary<Thread, ThreadBasedContext> = Dictionary()

    // TODO: Dispose

    and ThreadBasedContext(renderCtx: RenderCtx) =
        let waitForConsumptionTimeoutInMs = 2000

        let processFrameEvent = new AutoResetEvent(false)
        let frameProcessedEvent = new AutoResetEvent(false)

        let producerThread = Thread.CurrentThread

        let mutable isDisposed = false
        let mutable isConsumerRegistered = false
        let mutable consumerException : Exception option = None

        member _.TriggerFrameProcessing() =
            if not isDisposed then
                processFrameEvent.Set() |> ignore
                if not (frameProcessedEvent.WaitOne(waitForConsumptionTimeoutInMs)) then
                    printfn "Waiting for frame consumption timed out - raising exception"
                    failwith "Waiting for frame consumption timed out"
            match consumerException with
            | Some ex ->
                printfn $"Consumer exception detected - re- raising: {ex}"
                raise ex
            | None -> ()

        member _.ConsumeFrames() : IEnumerable<RenderCtx> =
            if isDisposed then
                failwith "Context is disposed"
            if isConsumerRegistered then
                failwith "A consumer is already registered for this context"
            if Thread.CurrentThread = producerThread then
                failwith "The producer thread cannot be the consumer thread"
            isConsumerRegistered <- true
            seq {
                try
                    while not isDisposed do
                        if processFrameEvent.WaitOne(100) then
                            yield renderCtx
                with ex ->
                    printfn $"Error in consuming frames: {ex}"
                    consumerException <- Some ex
            }

        interface IDisposable with
            member _.Dispose() =
                if not isDisposed then
                    isDisposed <- true
                    processFrameEvent.Set() |> ignore
                    frameProcessedEvent.Set() |> ignore
                    lock contextsLock <| fun () ->
                        ThreadBasedContexts.producerThreadBasedContexts.Remove(producerThread) |> ignore
                        ThreadBasedContexts.consumerThreadBasedContexts.Remove(Thread.CurrentThread) |> ignore

    let initializeEvaluation (renderCtx: RenderCtx) producerThread consumerThread =
        let context = new ThreadBasedContext(renderCtx)
        lock contextsLock <| fun () ->
            if ThreadBasedContexts.producerThreadBasedContexts.ContainsKey(producerThread) then
                failwith "A producer thread can only have one context"
            if ThreadBasedContexts.consumerThreadBasedContexts.ContainsKey(consumerThread) then
                failwith "A consumer thread can only have one context"
            ThreadBasedContexts.producerThreadBasedContexts.Add(producerThread, context)
            ThreadBasedContexts.consumerThreadBasedContexts.Add(consumerThread, context)
        context

    let getConsumingEnumerable () =
        let context = lock contextsLock <| fun () ->
            match ThreadBasedContexts.consumerThreadBasedContexts.TryGetValue(Thread.CurrentThread) with
            | true, ctx -> ctx
            | false, _ -> failwith "No context found for the current consumer thread"
        context.ConsumeFrames()


module CSharpEvaluation =
    ()



namespace Pxl.Ui

open Pxl

type Scene =
    static member Frames with get() =
        // this is called from CSharp - consumer side
        CSharpSceneHandling.getConsumingEnumerable()
