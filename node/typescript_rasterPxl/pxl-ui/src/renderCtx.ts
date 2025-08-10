import { Color } from './color.js';
import { RasterGraphics } from './graphics.js';

export class RenderCtx {
  public readonly w: number;
  public readonly h: number;

  public readonly graphics: RasterGraphics;

  private _now: Date = new Date();

  constructor(width: number, height: number) {
    this.w = width;
    this.h = height;

    this.graphics = new RasterGraphics(this.w, this.h);
  }

  get size() {
    return { w: this.w, h: this.h };
  }

  get width() {
    return this.w;
  }

  get height() {
    return this.h;
  }

  get halfSize() {
    return { w: this.w / 2, h: this.h / 2 };
  }

  get widthHalf() {
    return this.w / 2;
  }

  get heightHalf() {
    return this.h / 2;
  }

  get now() {
    return this._now;
  }

  prepareCycle(now: Date) {
    this._now = now;
    // Clear the graphics buffer (equivalent to clearRect and resetTransform)
    this.graphics.clear(Color.argb(0, 0, 0, 0)); // Transparent black
  }

  endCycle() {
    return this.pixelsToColors(this.graphics.getRGBAArray());
  }

  getSnapshot() {
    return this.pixelsToColors(this.graphics.getRGBAArray());
  }

  setSnapshot(colors: Color[], compositionMode?: string) {
    // Note: RasterGraphics doesn't support composition modes like Canvas
    // This will always use source-over behavior
    const pixels = this.colorsToPixels(colors);
    this.setRawPixels(pixels);
  }

  // Helper method to convert RGBA pixel array to Color array
  private pixelsToColors(pixels: Uint8ClampedArray): Color[] {
    const colors: Color[] = [];
    
    for (let i = 0; i < pixels.length; i += 4) {
      const color = Color.rgba(
        pixels[i],     // r
        pixels[i + 1], // g  
        pixels[i + 2], // b
        pixels[i + 3]  // a
      );
      colors.push(color);
    }

    return colors;
  }

  // Helper method to convert Color array to RGBA pixel array
  private colorsToPixels(colors: Color[]): Uint8ClampedArray {
    const pixels = new Uint8ClampedArray(colors.length * 4);
    
    for (let i = 0; i < colors.length; i++) {
      const color = colors[i];
      const index = i * 4;
      pixels[index] = color.r;     // r
      pixels[index + 1] = color.g; // g
      pixels[index + 2] = color.b; // b
      pixels[index + 3] = color.a; // a
    }

    return pixels;
  }

  // Helper method to set raw pixel data
  setRawPixels(pixels: Uint8ClampedArray) {
    // Since RasterGraphics doesn't have a direct setPixels method,
    // we need to set each pixel individually
    for (let y = 0; y < this.h; y++) {
      for (let x = 0; x < this.w; x++) {
        const index = (y * this.w + x) * 4;
        const color = Color.rgba(
          pixels[index],
          pixels[index + 1], 
          pixels[index + 2],
          pixels[index + 3]
        );
        
        // Access the private buffer directly for efficiency
        this.graphics.buffer.setPixel(x, y, color);
      }
    }
  }

  /**
   * Set antialiasing level for subsequent drawing operations
   */
  setAntiAliasing(level: 'off' | 's' | 'm' | 'l'): this {
    this.graphics.setAntiAliasing(level);
    return this;
  }

  /**
   * Clear with a specific color
   */
  clearWith(color: Color): this {
    this.graphics.clear(color);
    return this;
  }

  /**
   * Get raw RGBA pixel data (more efficient than getSnapshot for raw access)
   */
  getRawPixels(): Uint8ClampedArray {
    return this.graphics.getRGBAArray();
  }
}
