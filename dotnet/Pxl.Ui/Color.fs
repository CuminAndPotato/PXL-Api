namespace Pxl

open System.Runtime.InteropServices
open System.Runtime.CompilerServices

// Important: Don't change the layout;
// it has to be the same as the Skia Color struct
[<StructLayout(LayoutKind.Sequential)>]
type [<Struct>] Color =
    {
        r: byte
        g: byte
        b: byte
        a: byte
    }

    static member inline argb(a, r, g, b) =
        { a = byte a; r = byte r; g = byte g; b = byte b }

    static member inline rgba(r, g, b, a) =
        { a = byte a; r = byte r; g = byte g; b = byte b }

    static member inline rgb(r, g, b) =
        { a = 255uy; r = byte r; g = byte g; b = byte b }

    static member inline mono(v) =
        let v = byte v in Color.rgb(v, v, v)

    static member inline hsv(hue: float, saturation: float, value: float) =
        // Hue can be large or negative, so let’s normalize it into [0..360)
        let hue = hue % 360.0 |> (fun x -> if x < 0.0 then x + 360.0 else x)
        let saturation = clamp01 saturation
        let value = clamp01 value

        // C is the "chroma": the difference between the maximum and minimum
        // values of RGB (based on saturation and value)
        let c = value * saturation

        // Find the position within the 6 regions each of 60°
        // hh is basically (h / 60)
        let hh = hue / 60.0

        // X is an intermediate value determined by which region hue is in
        let x = c * (1.0 - abs(hh % 2.0 - 1.0))

        // Determine base RGB based on sector
        let r1, g1, b1 =
            if hh < 1.0 then c, x, 0.0
            elif hh < 2.0 then x, c, 0.0
            elif hh < 3.0 then 0.0, c, x
            elif hh < 4.0 then 0.0, x, c
            elif hh < 5.0 then x, 0.0, c
            else c, 0.0, x

        // m shifts all channels to match the actual value v
        let m = value - c

        // Convert rgb to 8-bit values (0..255)
        {
            a = 255uy
            r = byte (255.0 * (r1 + m))
            g = byte (255.0 * (g1 + m))
            b = byte (255.0 * (b1 + m))
        }

    static member hsva(h: float, s: float, v: float, a: float) =
        let color = Color.hsv(h, s, v)
        { color with a = a * 255.0 |> byte }

    /// Convert an RGB color (plus alpha) to HSV
    member this.toHSV() =
        let rf = float this.r / 255.0
        let gf = float this.g / 255.0
        let bf = float this.b / 255.0

        let maxVal = max rf (max gf bf)
        let minVal = min rf (min gf bf)
        let delta = maxVal - minVal

        // Compute H
        let mutable h =
            if delta < 1e-6 then
                0.0
            elif maxVal = rf then
                60.0 * (((gf - bf) / delta) % 6.0)
            elif maxVal = gf then
                60.0 * (((bf - rf) / delta) + 2.0)
            else
                60.0 * (((rf - gf) / delta) + 4.0)
        // Normalize hue to [0..360)
        if h < 0.0 then h <- h + 360.0

        // Compute S
        let s =
            if maxVal < 1e-6
            then 0.0
            else delta / maxVal

        // Compute V
        let v = maxVal

        (h, s, v)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.opacity() =
        float this.a / 255.0

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.opacity(value: float) =
        let this = this
        { this with a = (clamp01 value) * 255.0 |> byte }

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.brightness() =
        let r, g, b = this.r, this.g, this.b
        // Standard brightness calculation using maximum value of the RGB components
        // This will return 1.0 for white (255,255,255)
        let maxColor =
            let maxRG = if r > g then r else g
            if maxRG > b then maxRG else b
        float maxColor / 255.0

    /// Return a new Color by scaling this color’s RGB values by the given factor.
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.brightness(value: float) =
        let value = clamp01 value
        {
            a = this.a
            r = int this.r * int (value * 256.0) >>> 8 |> byte
            g = int this.g * int (value * 256.0) >>> 8 |> byte
            b = int this.b * int (value * 256.0) >>> 8 |> byte
        }

    /// Return a new Color by setting (or shifting) this color’s hue.
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.hue(value: float) =
        // 1. Convert old color to HSV
        let (_,s,v) = this.toHSV()
        // 2. Create new color with the given hue, same s/v
        let newColor = Color.hsv(value, s, v)
        // 3. Preserve the existing alpha channel
        { a = this.a; r = newColor.r; g = newColor.g; b = newColor.b }


[<RequireQualifiedAccess>]
module Colors =
    let transparentBlack = { a = 0uy; r = 0uy; g = 0uy; b = 0uy }
    let transparentWhite = { a = 0uy; r = 255uy; g = 255uy; b = 255uy }

    let aliceBlue = { a = 255uy; r = 240uy; g = 248uy; b = 255uy }
    let antiqueWhite = { a = 255uy; r = 250uy; g = 235uy; b = 215uy }
    let aqua = { a = 255uy; r = 0uy; g = 255uy; b = 255uy }
    let aquamarine = { a = 255uy; r = 127uy; g = 255uy; b = 212uy }
    let azure = { a = 255uy; r = 240uy; g = 255uy; b = 255uy }
    let beige = { a = 255uy; r = 245uy; g = 245uy; b = 220uy }
    let bisque = { a = 255uy; r = 255uy; g = 228uy; b = 196uy }
    let black = { a = 255uy; r = 0uy; g = 0uy; b = 0uy }
    let blanchedAlmond = { a = 255uy; r = 255uy; g = 235uy; b = 205uy }
    let blue = { a = 255uy; r = 0uy; g = 0uy; b = 255uy }
    let blueViolet = { a = 255uy; r = 138uy; g = 43uy; b = 226uy }
    let brown = { a = 255uy; r = 165uy; g = 42uy; b = 42uy }
    let burlyWood = { a = 255uy; r = 222uy; g = 184uy; b = 135uy }
    let cadetBlue = { a = 255uy; r = 95uy; g = 158uy; b = 160uy }
    let chartreuse = { a = 255uy; r = 127uy; g = 255uy; b = 0uy }
    let chocolate = { a = 255uy; r = 210uy; g = 105uy; b = 30uy }
    let coral = { a = 255uy; r = 255uy; g = 127uy; b = 80uy }
    let cornflowerBlue = { a = 255uy; r = 100uy; g = 149uy; b = 237uy }
    let cornsilk = { a = 255uy; r = 255uy; g = 248uy; b = 220uy }
    let crimson = { a = 255uy; r = 220uy; g = 20uy; b = 60uy }
    let cyan = { a = 255uy; r = 0uy; g = 255uy; b = 255uy }
    let darkBlue = { a = 255uy; r = 0uy; g = 0uy; b = 139uy }
    let darkCyan = { a = 255uy; r = 0uy; g = 139uy; b = 139uy }
    let darkGoldenrod = { a = 255uy; r = 184uy; g = 134uy; b = 11uy }
    let darkGray = { a = 255uy; r = 169uy; g = 169uy; b = 169uy }
    let darkGreen = { a = 255uy; r = 0uy; g = 100uy; b = 0uy }
    let darkKhaki = { a = 255uy; r = 189uy; g = 183uy; b = 107uy }
    let darkMagenta = { a = 255uy; r = 139uy; g = 0uy; b = 139uy }
    let darkOliveGreen = { a = 255uy; r = 85uy; g = 107uy; b = 47uy }
    let darkOrange = { a = 255uy; r = 255uy; g = 140uy; b = 0uy }
    let darkOrchid = { a = 255uy; r = 153uy; g = 50uy; b = 204uy }
    let darkRed = { a = 255uy; r = 139uy; g = 0uy; b = 0uy }
    let darkSalmon = { a = 255uy; r = 233uy; g = 150uy; b = 122uy }
    let darkSeaGreen = { a = 255uy; r = 143uy; g = 188uy; b = 143uy }
    let darkSlateBlue = { a = 255uy; r = 72uy; g = 61uy; b = 139uy }
    let darkSlateGray = { a = 255uy; r = 47uy; g = 79uy; b = 79uy }
    let darkTurquoise = { a = 255uy; r = 0uy; g = 206uy; b = 209uy }
    let darkViolet = { a = 255uy; r = 148uy; g = 0uy; b = 211uy }
    let deepPink = { a = 255uy; r = 255uy; g = 20uy; b = 147uy }
    let deepSkyBlue = { a = 255uy; r = 0uy; g = 191uy; b = 255uy }
    let dimGray = { a = 255uy; r = 105uy; g = 105uy; b = 105uy }
    let dodgerBlue = { a = 255uy; r = 30uy; g = 144uy; b = 255uy }
    let firebrick = { a = 255uy; r = 178uy; g = 34uy; b = 34uy }
    let floralWhite = { a = 255uy; r = 255uy; g = 250uy; b = 240uy }
    let forestGreen = { a = 255uy; r = 34uy; g = 139uy; b = 34uy }
    let fuchsia = { a = 255uy; r = 255uy; g = 0uy; b = 255uy }
    let gainsboro = { a = 255uy; r = 220uy; g = 220uy; b = 220uy }
    let ghostWhite = { a = 255uy; r = 248uy; g = 248uy; b = 255uy }
    let gold = { a = 255uy; r = 255uy; g = 215uy; b = 0uy }
    let goldenrod = { a = 255uy; r = 218uy; g = 165uy; b = 32uy }
    let gray = { a = 255uy; r = 128uy; g = 128uy; b = 128uy }
    let green = { a = 255uy; r = 0uy; g = 128uy; b = 0uy }
    let greenYellow = { a = 255uy; r = 173uy; g = 255uy; b = 47uy }
    let honeydew = { a = 255uy; r = 240uy; g = 255uy; b = 240uy }
    let hotPink = { a = 255uy; r = 255uy; g = 105uy; b = 180uy }
    let indianRed = { a = 255uy; r = 205uy; g = 92uy; b = 92uy }
    let indigo = { a = 255uy; r = 75uy; g = 0uy; b = 130uy }
    let ivory = { a = 255uy; r = 255uy; g = 255uy; b = 240uy }
    let khaki = { a = 255uy; r = 240uy; g = 230uy; b = 140uy }
    let lavender = { a = 255uy; r = 230uy; g = 230uy; b = 250uy }
    let lavenderBlush = { a = 255uy; r = 255uy; g = 240uy; b = 245uy }
    let lawnGreen = { a = 255uy; r = 124uy; g = 252uy; b = 0uy }
    let lemonChiffon = { a = 255uy; r = 255uy; g = 250uy; b = 205uy }
    let lightBlue = { a = 255uy; r = 173uy; g = 216uy; b = 230uy }
    let lightCoral = { a = 255uy; r = 240uy; g = 128uy; b = 128uy }
    let lightCyan = { a = 255uy; r = 224uy; g = 255uy; b = 255uy }
    let lightGoldenrodYellow = { a = 255uy; r = 250uy; g = 250uy; b = 210uy }
    let lightGray = { a = 255uy; r = 211uy; g = 211uy; b = 211uy }
    let lightGreen = { a = 255uy; r = 144uy; g = 238uy; b = 144uy }
    let lightPink = { a = 255uy; r = 255uy; g = 182uy; b = 193uy }
    let lightSalmon = { a = 255uy; r = 255uy; g = 160uy; b = 122uy }
    let lightSeaGreen = { a = 255uy; r = 32uy; g = 178uy; b = 170uy }
    let lightSkyBlue = { a = 255uy; r = 135uy; g = 206uy; b = 250uy }
    let lightSlateGray = { a = 255uy; r = 119uy; g = 136uy; b = 153uy }
    let lightSteelBlue = { a = 255uy; r = 176uy; g = 196uy; b = 222uy }
    let lightYellow = { a = 255uy; r = 255uy; g = 255uy; b = 224uy }
    let lime = { a = 255uy; r = 0uy; g = 255uy; b = 0uy }
    let limeGreen = { a = 255uy; r = 50uy; g = 205uy; b = 50uy }
    let linen = { a = 255uy; r = 250uy; g = 240uy; b = 230uy }
    let magenta = { a = 255uy; r = 255uy; g = 0uy; b = 255uy }
    let maroon = { a = 255uy; r = 128uy; g = 0uy; b = 0uy }
    let mediumAquamarine = { a = 255uy; r = 102uy; g = 205uy; b = 170uy }
    let mediumBlue = { a = 255uy; r = 0uy; g = 0uy; b = 205uy }
    let mediumOrchid = { a = 255uy; r = 186uy; g = 85uy; b = 211uy }
    let mediumPurple = { a = 255uy; r = 147uy; g = 112uy; b = 219uy }
    let mediumSeaGreen = { a = 255uy; r = 60uy; g = 179uy; b = 113uy }
    let mediumSlateBlue = { a = 255uy; r = 123uy; g = 104uy; b = 238uy }
    let mediumSpringGreen = { a = 255uy; r = 0uy; g = 250uy; b = 154uy }
    let mediumTurquoise = { a = 255uy; r = 72uy; g = 209uy; b = 204uy }
    let mediumVioletRed = { a = 255uy; r = 199uy; g = 21uy; b = 133uy }
    let midnightBlue = { a = 255uy; r = 25uy; g = 25uy; b = 112uy }
    let mintCream = { a = 255uy; r = 245uy; g = 255uy; b = 250uy }
    let mistyRose = { a = 255uy; r = 255uy; g = 228uy; b = 225uy }
    let moccasin = { a = 255uy; r = 255uy; g = 228uy; b = 181uy }
    let navajoWhite = { a = 255uy; r = 255uy; g = 222uy; b = 173uy }
    let navy = { a = 255uy; r = 0uy; g = 0uy; b = 128uy }
    let oldLace = { a = 255uy; r = 253uy; g = 245uy; b = 230uy }
    let olive = { a = 255uy; r = 128uy; g = 128uy; b = 0uy }
    let oliveDrab = { a = 255uy; r = 107uy; g = 142uy; b = 35uy }
    let orange = { a = 255uy; r = 255uy; g = 165uy; b = 0uy }
    let orangeRed = { a = 255uy; r = 255uy; g = 69uy; b = 0uy }
    let orchid = { a = 255uy; r = 218uy; g = 112uy; b = 214uy }
    let paleGoldenrod = { a = 255uy; r = 238uy; g = 232uy; b = 170uy }
    let paleGreen = { a = 255uy; r = 152uy; g = 251uy; b = 152uy }
    let paleTurquoise = { a = 255uy; r = 175uy; g = 238uy; b = 238uy }
    let paleVioletRed = { a = 255uy; r = 219uy; g = 112uy; b = 147uy }
    let papayaWhip = { a = 255uy; r = 255uy; g = 239uy; b = 213uy }
    let peachPuff = { a = 255uy; r = 255uy; g = 218uy; b = 185uy }
    let peru = { a = 255uy; r = 205uy; g = 133uy; b = 63uy }
    let pink = { a = 255uy; r = 255uy; g = 192uy; b = 203uy }
    let plum = { a = 255uy; r = 221uy; g = 160uy; b = 221uy }
    let powderBlue = { a = 255uy; r = 176uy; g = 224uy; b = 230uy }
    let purple = { a = 255uy; r = 128uy; g = 0uy; b = 128uy }
    let rebeccaPurple = { a = 255uy; r = 102uy; g = 51uy; b = 153uy }
    let red = { a = 255uy; r = 255uy; g = 0uy; b = 0uy }
    let rosyBrown = { a = 255uy; r = 188uy; g = 143uy; b = 143uy }
    let royalBlue = { a = 255uy; r = 65uy; g = 105uy; b = 225uy }
    let saddleBrown = { a = 255uy; r = 139uy; g = 69uy; b = 19uy }
    let salmon = { a = 255uy; r = 250uy; g = 128uy; b = 114uy }
    let sandyBrown = { a = 255uy; r = 244uy; g = 164uy; b = 96uy }
    let seaGreen = { a = 255uy; r = 46uy; g = 139uy; b = 87uy }
    let seaShell = { a = 255uy; r = 255uy; g = 245uy; b = 238uy }
    let sienna = { a = 255uy; r = 160uy; g = 82uy; b = 45uy }
    let silver = { a = 255uy; r = 192uy; g = 192uy; b = 192uy }
    let skyBlue = { a = 255uy; r = 135uy; g = 206uy; b = 235uy }
    let slateBlue = { a = 255uy; r = 106uy; g = 90uy; b = 205uy }
    let slateGray = { a = 255uy; r = 112uy; g = 128uy; b = 144uy }
    let snow = { a = 255uy; r = 255uy; g = 250uy; b = 250uy }
    let springGreen = { a = 255uy; r = 0uy; g = 255uy; b = 127uy }
    let steelBlue = { a = 255uy; r = 70uy; g = 130uy; b = 180uy }
    let tan = { a = 255uy; r = 210uy; g = 180uy; b = 140uy }
    let teal = { a = 255uy; r = 0uy; g = 128uy; b = 128uy }
    let thistle = { a = 255uy; r = 216uy; g = 191uy; b = 216uy }
    let tomato = { a = 255uy; r = 255uy; g = 99uy; b = 71uy }
    let turquoise = { a = 255uy; r = 64uy; g = 224uy; b = 208uy }
    let violet = { a = 255uy; r = 238uy; g = 130uy; b = 238uy }
    let wheat = { a = 255uy; r = 245uy; g = 222uy; b = 179uy }
    let white = { a = 255uy; r = 255uy; g = 255uy; b = 255uy }
    let whiteSmoke = { a = 255uy; r = 245uy; g = 245uy; b = 245uy }
    let yellow = { a = 255uy; r = 255uy; g = 255uy; b = 0uy }
    let yellowGreen = { a = 255uy; r = 154uy; g = 205uy; b = 50uy }

// module Debug =
//     let printPixels (pixels: Color array) width =
//         let colorToString (c: Color) = sprintf "#%02X%02X%02X%02X" c.a c.r c.g c.b
//         for i in 0 .. pixels.Length - 1 do
//             printf "%s " (colorToString pixels.[i])
//             if (i + 1) % width = 0 then printfn ""
