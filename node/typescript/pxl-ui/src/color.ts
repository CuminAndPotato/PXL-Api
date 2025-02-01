import { clamp01 } from './utils.js';

export class Color {
  public readonly a: number;
  public readonly r: number;
  public readonly g: number;
  public readonly b: number;

  private constructor(a: number, r: number, g: number, b: number) {
    this.a = a;
    this.r = r;
    this.g = g;
    this.b = b;
  }

  static argb(a: number, r: number, g: number, b: number): Color {
    return new Color(a, r, g, b);
  }

  static rgb(r: number, g: number, b: number): Color {
    return new Color(255, r, g, b);
  }

  static rgba(r: number, g: number, b: number, a: number): Color {
    return new Color(a, r, g, b);
  }

  static mono(v: number): Color {
    return Color.rgb(v, v, v);
  }

  opacity(): number {
    return this.a / 255.0;
  }

  setOpacity(value: number): Color {
    return Color.argb(clamp01(value) * 255.0, this.r, this.g, this.b);
  }

  brightness(): number {
    let maxColor = Math.max(Math.max(this.r, this.g), this.b);
    let minColor = Math.min(Math.min(this.r, this.g), this.b);
    return (maxColor + minColor) / (2.0 * 255.0);
  }

  setBrightness(value: number): Color {
    value = clamp01(value);
    return Color.argb(this.a, this.r * value, this.g * value, this.b * value);
  }

  toHex(): string {
    // Ensure values are within the valid range
    const r = Math.max(0, Math.min(255, Math.round(this.r)));
    const g = Math.max(0, Math.min(255, Math.round(this.g)));
    const b = Math.max(0, Math.min(255, Math.round(this.b)));
    const a = Math.max(0, Math.min(255, Math.round(this.a)));

    // Convert to hexadecimal and pad with zeros if needed
    const hexR = r.toString(16).padStart(2, "0");
    const hexG = g.toString(16).padStart(2, "0");
    const hexB = b.toString(16).padStart(2, "0");
    const hexA = a.toString(16).padStart(2, "0");

    return `#${hexR}${hexG}${hexB}${hexA}`;
  }
}
