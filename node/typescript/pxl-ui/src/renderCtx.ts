import { Canvas, SKRSContext2D, createCanvas, loadImage } from '@napi-rs/canvas';
import { Color } from './color.js';

export class RenderCtx {
  public readonly w: number;
  public readonly h: number;

  public readonly canvas: Canvas;
  public readonly canvas2d: SKRSContext2D;

  private _now: Date = new Date();

  constructor(width: number, height: number) {
    this.w = width;
    this.h = height;

    this.canvas = createCanvas(this.w, this.h);
    this.canvas2d = this.canvas.getContext('2d');
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
    this.canvas2d.clearRect(0, 0, this.w, this.h);
    this.canvas2d.resetTransform();
  }

  endCycle() {
    const imageData = this.canvas2d.getImageData(0, 0, this.w, this.h);
    const data = imageData.data;
    const colors: Color[] = [];

    for (let i = 0; i < data.length; i += 4) {
      const color: Color = Color.rgba(
        data[i],
        data[i + 1],
        data[i + 2],
        data[i + 3]);
      colors.push(color);
    }

    return colors;
  }

  getSnapshot() {
    const imageData = this.canvas2d.getImageData(0, 0, this.w, this.h);
    const data = imageData.data;
    const colors: Color[] = [];

    for (let i = 0; i < data.length; i += 4) {
      const color: Color = Color.rgba(
        data[i],
        data[i + 1],
        data[i + 2],
        data[i + 3]);
      colors.push(color);
    }

    return colors;
  }

  setSnapshot(colors: Color[], compositionMode?: GlobalCompositeOperation) {
    const newImageData = this.canvas2d.createImageData(this.w, this.h);
    const data = newImageData.data;

    for (let i = 0; i < colors.length; i++) {
      const { r, g, b, a } = colors[i];
      const index = i * 4;
      data[index] = r;
      data[index + 1] = g;
      data[index + 2] = b;
      data[index + 3] = a;
    }

    const compo = compositionMode ?? 'source-over';
    this.canvas2d.globalCompositeOperation = compo;
    this.canvas2d.putImageData(newImageData, 0, 0);
    this.canvas2d.globalCompositeOperation = 'source-over';
  }
}
