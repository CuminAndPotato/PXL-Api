import { StopWatchController } from './timer.js';
import { vide } from '../vide.js';

type Repeat = 'StopAtEnd' | 'Loop' | 'RewindAndStop';
type Clamping = 'NoClamp' | 'ClampSmallerZero' | 'ClampBiggerOne' | 'ClampBoth';

export class AnimationController {
  private _value: number = 0.0;
  private _isAtEnd: boolean = false;

  constructor(
    private f: (t: number) => number,
    private sw: StopWatchController,
    private duration: number,
    private startValue: number,
    private endValue: number,
    private repeat: Repeat,
    private clamping: Clamping,
  ) { }

  get value(): number {
    return this._value;
  }

  get elapsed(): number {
    return this.sw.elapsed;
  }

  get elapsedRel(): number {
    return this.sw.elapsed / this.duration;
  }

  get isAtStart(): boolean {
    return this.elapsedRel <= 0.0;
  }

  get isAtEnd(): boolean {
    return this._isAtEnd;
  }

  get isRunning(): boolean {
    return this.sw.isRunning;
  }

  get isPaused(): boolean {
    return this.sw.isPaused;
  }

  pause(): void {
    this.sw.pause();
  }

  onPausing(f: () => void): void {
    this.sw.onPausing(f);
  }

  resume(): void {
    this.sw.resume();
  }

  onResuming(f: () => void): void {
    this.sw.onResuming(f);
  }

  restart(): void {
    this.sw.rewind(0);
    this.sw.resume();
    this._value = this.calcValue();
  }

  calcValue(): number {
    let t: number;
    switch (this.clamping) {
      case 'NoClamp':
        t = this.elapsedRel;
        break;
      case 'ClampSmallerZero':
        t = Math.max(0.0, this.elapsedRel);
        break;
      case 'ClampBiggerOne':
        t = Math.min(1.0, this.elapsedRel);
        break;
      case 'ClampBoth':
        t = Math.max(0.0, Math.min(1.0, this.elapsedRel));
        break;
    }
    return this.f(t) * (this.endValue - this.startValue) + this.startValue;
  }

  eval(): void {
    let isAtEndLocal = this.elapsedRel >= 1.0;
    if (isAtEndLocal) {
      switch (this.repeat) {
        case 'StopAtEnd':
          this.sw.pause();
          break;
        case 'RewindAndStop':
          this.sw.rewind(0);
          this.sw.pause();
          break;
        case 'Loop':
          let overTheEndTime = this.sw.elapsed - this.duration;
          this.sw.rewind(overTheEndTime);
          break;
        default:
          throw new Error('Unknown repeat type.');
      }
      this._isAtEnd = true;
    } else {
      this._isAtEnd = false;
    }

    this._value = this.calcValue();
  }
}

function calculate(
  f: (x: number) => number,
  duration: number,
  startValue: number,
  endValue: number,
  repeat: Repeat = 'StopAtEnd',
  autoStart = true,
  clamping: Clamping = 'ClampBoth',
) {
  return vide(
    () => {
      let swc = new StopWatchController(autoStart !== undefined ? autoStart : true);
      let ac = new AnimationController(f, swc, duration, startValue, endValue, repeat, clamping);
      return { swc, ac };
    },
    (state, ctx) => {
      const { swc, ac } = state;
      if (swc === undefined) {
        throw new Error('swc is undefined');
      }

      if (swc.isRunning) {
        swc.eval(ctx.now);
        ac.eval();
      }

      return [ac, state];
    },
  );
}

export function linear(duration: number, startValue: number, endValue: number, repeat?: Repeat, autoStart?: boolean, clamping?: Clamping) {
  return calculate((t) => t, duration, startValue, endValue, repeat, autoStart, clamping);
}

export function easeIn(duration: number, startValue: number, endValue: number, repeat?: Repeat, autoStart?: boolean, clamping?: Clamping) {
  return calculate((t) => Math.pow(t, 2), duration, startValue, endValue, repeat, autoStart, clamping);
}

export function easeOut(duration: number, startValue: number, endValue: number, repeat?: Repeat, autoStart?: boolean, clamping?: Clamping) {
  return calculate((t) => 1 - Math.pow(1 - t, 2), duration, startValue, endValue, repeat, autoStart, clamping);
}

export function easeInOut(
  duration: number,
  startValue: number,
  endValue: number,
  repeat?: Repeat,
  autoStart?: boolean,
  clamping?: Clamping,
) {
  return calculate(
    (t) => (t < 0.5 ? 2 * Math.pow(t, 2) : 1 - Math.pow(-2 * t + 2, 2) / 2),
    duration,
    startValue,
    endValue,
    repeat,
    autoStart,
    clamping,
  );
}

export function easeInSine(
  duration: number,
  startValue: number,
  endValue: number,
  repeat?: Repeat,
  autoStart?: boolean,
  clamping?: Clamping,
) {
  return calculate((t) => 1 - Math.cos((t * Math.PI) / 2), duration, startValue, endValue, repeat, autoStart, clamping);
}

export function easeOutSine(
  duration: number,
  startValue: number,
  endValue: number,
  repeat?: Repeat,
  autoStart?: boolean,
  clamping?: Clamping,
) {
  return calculate((t) => Math.sin((t * Math.PI) / 2), duration, startValue, endValue, repeat, autoStart, clamping);
}

export function easeInOutSine(
  duration: number,
  startValue: number,
  endValue: number,
  repeat?: Repeat,
  autoStart?: boolean,
  clamping?: Clamping,
) {
  return calculate((t) => -0.5 * (Math.cos(Math.PI * t) - 1), duration, startValue, endValue, repeat, autoStart, clamping);
}

export function easeInCubic(
  duration: number,
  startValue: number,
  endValue: number,
  repeat?: Repeat,
  autoStart?: boolean,
  clamping?: Clamping,
) {
  return calculate((t) => Math.pow(t, 3), duration, startValue, endValue, repeat, autoStart, clamping);
}

export function easeOutCubic(
  duration: number,
  startValue: number,
  endValue: number,
  repeat?: Repeat,
  autoStart?: boolean,
  clamping?: Clamping,
) {
  return calculate((t) => 1 - Math.pow(1 - t, 3), duration, startValue, endValue, repeat, autoStart, clamping);
}

export function easeInOutCubic(
  duration: number,
  startValue: number,
  endValue: number,
  repeat?: Repeat,
  autoStart?: boolean,
  clamping?: Clamping,
) {
  return calculate(
    (t) => (t < 0.5 ? 4 * Math.pow(t, 3) : 1 - Math.pow(-2 * t + 2, 3) / 2),
    duration,
    startValue,
    endValue,
    repeat,
    autoStart,
    clamping,
  );
}
