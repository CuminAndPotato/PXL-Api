import { RenderCtx } from '../renderCtx.js';
import { getCtx, videWithInitStateFunc } from '../vide.js';

export class StopWatchController {
  private _isRunning = false;
  private _lastTickTime: Date | null = null;
  private _elapsed = 0;
  private _onPausing: (() => void) | null = null;
  private _onResuming: (() => void) | null = null;

  constructor(isRunning: boolean) {
    this._isRunning = isRunning;
  }

  get isRunning(): boolean {
    return this._isRunning;
  }

  get isPaused(): boolean {
    return !this.isRunning;
  }

  get elapsed(): number {
    return this._elapsed;
  }

  get lastTickTime(): Date | null {
    return this._lastTickTime;
  }

  pause(): void {
    this._isRunning = false;
    if (this._onPausing) {
      this._onPausing();
    }
  }

  onPausing(f: () => void): void {
    this._onPausing = f;
  }

  resume(): void {
    this._isRunning = true;
    if (this._onResuming) {
      this._onResuming();
    }
  }

  onResuming(f: () => void): void {
    this._onResuming = f;
  }

  rewind(elapsed: number): void {
    this._elapsed = elapsed;
    this._lastTickTime = null;
  }

  eval(now: Date): void {
    let lastTickTime = this._lastTickTime;
    let isResuming = false;
    if (this._lastTickTime === null) {
      lastTickTime = now;
      isResuming = true;
    }
    if (isResuming && this._onResuming) {
      this._onResuming();
    }
    this._lastTickTime = now;
    this._elapsed += (now.getTime() - (lastTickTime as Date).getTime()) / 1000;
  }
}

export function stopWatch(autoStart = true) {
  return videWithInitStateFunc(
    () => new StopWatchController(autoStart),
    (controller, ctx) => {
      if (controller.isRunning) {
        controller.eval(ctx.now);
      }
      return [controller, controller];
    },
  );
}

export function interval(interval: number, f: (swc: StopWatchController, renderCtx: RenderCtx) => void, autoStart = true) {
  return async () => {
    const controller = stopWatch(autoStart);
    const ctx = getCtx();
    if (controller.elapsed >= interval) {
      controller.rewind(0);
      f(controller, ctx);
    }
  };
}
