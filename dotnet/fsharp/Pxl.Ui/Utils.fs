[<AutoOpen>]
module Pxl.Utils

open System
open System.Collections.Generic
open System.Threading
open System.Runtime.CompilerServices

[<MethodImpl(MethodImplOptions.AggressiveInlining)>]
let clamp lb ub value = max lb (min ub value)

[<MethodImpl(MethodImplOptions.AggressiveInlining)>]
let clamp01 = clamp 0.0 1.0

[<MethodImpl(MethodImplOptions.AggressiveInlining)>]
let inline f32 x = float32 x

[<MethodImpl(MethodImplOptions.AggressiveInlining)>]
let inline f32opt x = x |> Option.map f32

type Size = { w: float; h: float }

[<MethodImpl(MethodImplOptions.AggressiveInlining)>]
let xyToIdx width x y = y * width + x

[<MethodImpl(MethodImplOptions.AggressiveInlining)>]
let idxToXy width idx = idx % width, idx / width

type Grid<'a>(cells: 'a list, _cols: int) =
    do
        if _cols <= 0 then
            failwith $"Invalid number of columns in grid: {_cols}"
        if cells.Length % _cols <> 0 then
            failwith $"Number of cells in grid must be a multiple of the number of columns (cells: {cells.Length}, cols: {_cols})"

    let _rows = cells.Length / _cols

    member _.cols = _cols
    member _.rows = _rows

    member _.cells = cells
    member _.cells1d =
        [
            for i in 0..cells.Length - 1 do
                i, cells[i]
        ]
    member _.cells2d =
        [
            for row in 0.._rows - 1 do
                for col in 0.._cols - 1 do
                    col, row, cells[row * _cols + col]
        ]

    member _.Item
        with get(idx: int) =
            if idx < 0 || idx >= cells.Length
            then failwith $"Index out of range in the grid (index: {idx}, cells: {cells.Length})"
            else cells[idx]

    member _.Item
        with get(row: int, col: int) =
            if row < 0 || row >= _rows || col < 0 || col >= _cols then
                failwith $"Index out of range in sprite (row: {row}, col: {col}, rows: {_rows}, cols: {_cols})"
            cells[row * _cols + col]

    member _.GetSlice(?startIdx, ?endIdx) =
        let startIdx = defaultArg startIdx 0
        let endIdx = defaultArg endIdx (cells.Length - 1)
        if startIdx < 0 || endIdx >= cells.Length || startIdx > endIdx then
            failwith $"Index range out of bounds in sprite (startIdx: {startIdx}, endIdx: {endIdx}, cells: {cells.Length})"
        cells[startIdx..endIdx]

    member _.GetSlice(?startRow, ?startCol, ?endRow, ?endCol) =
        let startRow = defaultArg startRow 0
        let startCol = defaultArg startCol 0
        let endRow = defaultArg endRow (_rows - 1)
        let endCol = defaultArg endCol (_cols - 1)
        if startRow < 0 || endRow >= _rows || startCol < 0 || endCol >= _cols || startRow > endRow || startCol > endCol then
            failwith $"Row or column range out of bounds in sprite (startRow: {startRow}, startCol: {startCol}, endRow: {endRow}, endCol: {endCol}, rows: {_rows}, cols: {_cols})"
        [
            for row in startRow..endRow do
                for col in startCol..endCol do
                    yield cells[row * _cols + col]
        ]

type TimeUntil(maxTime: TimeSpan) =
    let now = DateTimeOffset.Now
    let elapsingOn = now + maxTime
    member _.Since = now
    member _.RemainingTimeSpan = elapsingOn - DateTimeOffset.Now
    member this.isElapsed = this.RemainingTimeSpan <= TimeSpan.Zero

type BoundedQueue<'a>(maxSize: int) =
    let queue = Queue<'a>()
    member _.Enqueue(item) =
        if queue.Count >= maxSize then
            queue.Dequeue() |> ignore
        queue.Enqueue(item)
    member _.IsFull =
        queue.Count >= maxSize
    member _.DequeueAll() =
        [ while queue.Count > 0 do queue.Dequeue() ]
    member _.Count =
        queue.Count

type DebouncedLogger<'a>(initPayload: 'a, logInterval: TimeSpan, getMsg) =
    let mutable lastLog = DateTimeOffset.MinValue
    member val Payload = initPayload with get, set
    member this.Log() =
        let now = DateTimeOffset.Now
        if now - lastLog > logInterval then
            match getMsg this.Payload with
            | None -> ()
            | Some msg ->
                printfn "%s" msg
                lastLog <- now
                this.Payload <- initPayload

type BoundedBlockingQueue<'a>(maxSize: int, producerOnEnd) =
    let dropLogger =
        DebouncedLogger(
            0,
            TimeSpan.FromSeconds 10.0,
            fun dropped ->
                if dropped > 0
                then Some $"Dropped {dropped} items from the queue"
                else None
        )

    let cts = new CancellationTokenSource()
    let queue = BoundedQueue<'a>(maxSize)
    let consumeEvent = new AutoResetEvent(false)

    let mutable isConsumerRegistered = false

    member _.Cts = cts

    member _.Push(x) =
        if cts.Token.IsCancellationRequested then () else

        dropLogger.Log()
        if isConsumerRegistered && queue.IsFull then
            dropLogger.Payload <- dropLogger.Payload + 1
        lock queue <| fun () -> queue.Enqueue(x)
        consumeEvent.Set() |> ignore

    member this.Consume(processItem, onEnd) =
        lock this <| fun () ->
            if isConsumerRegistered then
                failwith "Only one consumer at a time"
            isConsumerRegistered <- true

        Thread(fun () ->
            try
                while not cts.Token.IsCancellationRequested do
                    consumeEvent.WaitOne() |> ignore
                    if not cts.Token.IsCancellationRequested then
                        let elems =  lock queue <| fun () -> queue.DequeueAll()
                        for item in elems do
                            processItem item
            with ex ->
                printfn "Error in client iteration: %A" ex
                (this :> IDisposable).Dispose()

            onEnd()
        )
        |> fun t ->
            t.IsBackground <- true
            t.Name <- "BoundedBlockingQueue consumer"
            t.Start()

    interface IDisposable with
        member _.Dispose() =
            printfn "Disposing BoundedBlockingQueue"
            cts.Cancel()
            consumeEvent.Set() |> ignore
            producerOnEnd ()

[<RequireQualifiedAccess>]
module Thread =
    let startBackground name (threadProc: unit -> unit) =
        let t = new Thread(threadProc)
        t.IsBackground <- true
        t.Name <- name
        t.Start()
