module Pxl.Debug

let mutable logPerf = false
let mutable doLog = printfn "%s"

let log (msg: string) = doLog msg
