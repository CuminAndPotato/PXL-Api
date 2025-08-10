import { Color } from './color.js';

export interface CanvasMetadata {
  width: number;
  height: number;
  fps: number;
}

export class Canvas {
  private buffer: (readonly Color[])[] = [];

  public isCancellationRequested = false;

  constructor(
    public readonly metadata: CanvasMetadata,
    public readonly sendFrameBufferSize: number,
  ) {}

  pushFrame(frame: readonly Color[]) {
    if (this.buffer.length >= this.sendFrameBufferSize) {
      this.buffer.shift();
    }

    this.buffer.push(frame);
  }

  popAllFrames() {
    const buffer = this.buffer;
    this.buffer = [];
    return buffer;
  }

  clearCanvas(canvas: Canvas, color: Color) {
    const buffer = new Array(
      canvas.metadata.width * canvas.metadata.height,
    ).fill(color);
    canvas.pushFrame(buffer);
  }
}
