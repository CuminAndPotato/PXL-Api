[<AutoOpen>]
module Pxl.Ui.Utils

let private r = System.Random()

let randomInt (min, max) = r.Next(min, max)
let randomFloat (min, max) = r.NextDouble() * (max - min) + min
let random01 () = r.NextDouble()
