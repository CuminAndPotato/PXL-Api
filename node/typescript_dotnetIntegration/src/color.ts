import { clamp01 } from './utils.js';

export class Color {
  public readonly a: number;
  public readonly r: number;
  public readonly g: number;
  public readonly b: number;

  private constructor(a: number, r: number, g: number, b: number) {
    this.a = Color._byte(a);
    this.r = Color._byte(r);
    this.g = Color._byte(g);
    this.b = Color._byte(b);
    Object.freeze(this);
  }

  // ---------- Static constructors ----------

  static argb(a: number, r: number, g: number, b: number): Color {
    return new Color(a, r, g, b);
  }

  static rgba(r: number, g: number, b: number, a: number): Color {
    return Color.argb(a, r, g, b);
  }

  static rgb(r: number, g: number, b: number): Color {
    return Color.argb(255, r, g, b);
  }

  static mono(v: number): Color {
    return Color.rgb(v, v, v);
  }

  /** Create a color from HSV (h ∈ ℝ, s,v ∈ [0,1]) */
  static hsv(hue: number, saturation: number, value: number): Color {
    let h = hue % 360;
    if (h < 0) h += 360;

    const s = clamp01(saturation);
    const v = clamp01(value);

    const c = v * s;
    const hh = h / 60;
    const x = c * (1 - Math.abs((hh % 2) - 1));

    let r1 = 0, g1 = 0, b1 = 0;
    if (hh < 1) { r1 = c; g1 = x; b1 = 0; }
    else if (hh < 2) { r1 = x; g1 = c; b1 = 0; }
    else if (hh < 3) { r1 = 0; g1 = c; b1 = x; }
    else if (hh < 4) { r1 = 0; g1 = x; b1 = c; }
    else if (hh < 5) { r1 = x; g1 = 0; b1 = c; }
    else { r1 = c; g1 = 0; b1 = x; }

    const m = v - c;

    return Color.argb(
      255,
      Math.round((r1 + m) * 255),
      Math.round((g1 + m) * 255),
      Math.round((b1 + m) * 255)
    );
  }

  /** Create a color from HSV + alpha (a ∈ [0,1]) */
  static hsva(h: number, s: number, v: number, a: number): Color {
    const base = Color.hsv(h, s, v);
    return Color.argb(Color._byte(a * 255), base.r, base.g, base.b);
  }

  // ---------- Instance API ----------

  /** Get opacity (0..1) or set it when a value is provided */
  opacity(): number;
  opacity(value: number): Color;
  opacity(value?: number): number | Color {
    if (arguments.length === 0) return this.a / 255.0;
    const v = clamp01(value as number);
    return Color.argb(Color._byte(v * 255), this.r, this.g, this.b);
  }

  /** @deprecated Use `opacity(value)` instead. */
  setOpacity(value: number): Color {
    return this.opacity(value) as Color;
  }

  /** Brightness getter (max(r,g,b)/255) or setter that scales channels. */
  brightness(): number;
  brightness(value: number): Color;
  brightness(value?: number): number | Color {
    if (arguments.length === 0) {
      return Math.max(this.r, this.g, this.b) / 255.0;
    }
    const v = clamp01(value as number);
    const scale = Math.floor(v * 256); // int(value * 256.0)
    const r = Math.floor((this.r * scale) / 256);
    const g = Math.floor((this.g * scale) / 256);
    const b = Math.floor((this.b * scale) / 256);
    return Color.argb(this.a, r, g, b);
  }

  /** Convert this color to HSV. Returns [h, s, v]. */
  toHSV(): [number, number, number] {
    const rf = this.r / 255.0;
    const gf = this.g / 255.0;
    const bf = this.b / 255.0;

    const maxVal = Math.max(rf, gf, bf);
    const minVal = Math.min(rf, gf, bf);
    const delta = maxVal - minVal;

    let h: number;
    if (delta < 1e-6) {
      h = 0;
    } else if (maxVal === rf) {
      h = 60 * (((gf - bf) / delta) % 6);
    } else if (maxVal === gf) {
      h = 60 * (((bf - rf) / delta) + 2);
    } else {
      h = 60 * (((rf - gf) / delta) + 4);
    }
    if (h < 0) h += 360;

    const s = maxVal < 1e-6 ? 0 : delta / maxVal;
    const v = maxVal;

    return [h, s, v];
  }

  /** Return a new color with the hue set to `value`, preserving s & v and alpha. */
  hue(value: number): Color {
    const [, s, v] = this.toHSV();
    const c = Color.hsv(value, s, v);
    return Color.argb(this.a, c.r, c.g, c.b);
  }

  /**
   * Hex string. Default order is RRGGBBAA.
   * If you need AARRGGBB, pass 'argb'.
   */
  toHex(order: 'rgba' | 'argb' = 'rgba'): string {
    const r = this.r.toString(16).padStart(2, '0');
    const g = this.g.toString(16).padStart(2, '0');
    const b = this.b.toString(16).padStart(2, '0');
    const a = this.a.toString(16).padStart(2, '0');

    return order === 'argb' ? `#${a}${r}${g}${b}` : `#${r}${g}${b}${a}`;
  }

  // ---------- Helpers ----------

  private static _byte(v: number): number {
    return Math.max(0, Math.min(255, Math.round(v)));
  }
}
