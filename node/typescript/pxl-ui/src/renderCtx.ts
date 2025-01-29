import { Canvas, SKRSContext2D, createCanvas, loadImage } from '@napi-rs/canvas';
import { Color } from './color.js';

export class RenderCtx {
  public readonly w: number;
  public readonly h: number;

  public readonly canvas: Canvas;
  public readonly canvasCtx: SKRSContext2D;

  private _now: Date = new Date();

  constructor(width: number, height: number) {
    this.w = width;
    this.h = height;

    this.canvas = createCanvas(this.w, this.h);
    this.canvasCtx = this.canvas.getContext('2d');
  }

  get size() {
    return { w: this.w, h: this.h };
  }

  get now() {
    return this._now;
  }

  prepareCycle(now: Date) {
    this._now = now;
    this.canvasCtx.clearRect(0, 0, this.w, this.h);
    this.canvasCtx.resetTransform();
  }

  endCycle() {
    const imageData = this.canvasCtx.getImageData(0, 0, this.w, this.h);
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
}
