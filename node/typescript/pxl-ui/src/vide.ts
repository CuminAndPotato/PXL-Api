// eslint-disable @typescript/eslint/no-explicit-any

// This is a hack, required because it's not possible to pass
// context to a promise callback, thus enabling to pass and collect
// state to Vide functions "under the hood" in conjkunction with using await.

export type SceneFunc<V> = () => V;

const log = (message?: unknown, ...optionalParams: unknown[]) =>
  console.debug(message, ...optionalParams);

// We can't generalize _ctx because it's all singleton and single-thread hacks here,
// so we would either specialize it to RenderCtx (which we may do later), or
// leave it as any and lose type safety.
export const ResumableStateHack: {
  popStack?: readonly unknown[];
  pushStack: readonly unknown[];
  ctx?: RenderCtx;
} = { pushStack: [] };

export function popState<S>() {
  if (ResumableStateHack.popStack === undefined) {
    return undefined;
  }
  if (ResumableStateHack.popStack.length === 0) {
    throw new Error(
      'popState: stack underflow - are you using language-builtin for / if similar?',
    );
  }
  const [x, ...xs] = ResumableStateHack.popStack;
  ResumableStateHack.popStack = xs;
  return x as S;
}

export function pushState(state: unknown) {
  log('Pushing state ...', state);
  ResumableStateHack.pushStack =
    ResumableStateHack.pushStack === undefined
      ? [state]
      : [...ResumableStateHack.pushStack, state];
}

export class Evaluable<T> {
  private _state: readonly unknown[] | undefined;

  constructor(
    private readonly vide: SceneFunc<T>,
    private readonly ctx: RenderCtx,
    private readonly onCycleStart?: () => void,
    private readonly onCycleEnd?: () => void,
  ) {
    log('Evaluable: constructor ...');
  }

  async next() {
    const prepareCycle = () => {
      ResumableStateHack.popStack = this._state;
      ResumableStateHack.pushStack = [];
      ResumableStateHack.ctx = this.ctx;
    };

    const teardownCycle = () => {
      const s = ResumableStateHack.pushStack;
      ResumableStateHack.popStack = undefined;
      ResumableStateHack.pushStack = [];
      ResumableStateHack.ctx = undefined;
      return s;
    };

    // we know that all vide functions are sync!
    prepareCycle();

    if (this.onCycleStart) {
      this.onCycleStart();
    }

    const videRes = this.vide();
    this._state = teardownCycle();

    if (this.onCycleEnd) {
      this.onCycleEnd();
    }

    return videRes;
  }
}

import { RenderCtx } from './renderCtx.js';

export function vide<V, S>(
  func: (state: S | undefined, ctx: RenderCtx) => [V, S],
): V;
export function vide<V, S>(
  initial: () => S,
  func: (state: S, ctx: RenderCtx) => [V, S],
): V;
export function vide<V, S>(
  initial: S,
  func: (state: S, ctx: RenderCtx) => [V, S],
): V;
export function vide<V, S>(
  ...args:
    | [(state: S | undefined, ctx: RenderCtx) => [V, S]]
    | [initial: () => S, func: (state: S, ctx: RenderCtx) => [V, S]]
    | [initial: S, func: (state: S, ctx: RenderCtx) => [V, S]]
): V {
  if (args.length === 1) {
    const [func] = args;
    const [v, s] = func(popState<S>(), ResumableStateHack.ctx!);
    pushState(s);
    return v;
  }

  if (args.length === 2) {
    const [initializer, func] = args;

    return vide<V, S>((state, ctx) => {
      return func(
        state === undefined
          ? typeof initializer === 'function'
            ? (initializer as () => S)()
            : initializer
          : state,
        ctx,
      );
    });
  }

  throw new Error('Invalid arguments for vide function');
}

class Mutable<V> {
  constructor(public value: V) {}
}

export function useMemo<V>(initial: () => V): V;
export function useMemo<V>(initial: V): V;
export function useMemo<V>(initial: unknown): V {
  return vide(
    () => (typeof initial === 'function' ? initial() : initial),
    (state, ctx) => [state, state],
  );
}

export function useState<V>(initial: () => V): Mutable<V>;
export function useState<V>(initial: V): Mutable<V>;
export function useState<V>(initial: unknown): Mutable<V> {
  return useMemo(
    () => new Mutable(typeof initial === 'function' ? initial() : initial),
  );
}

// type PromiseState<V, D> = { value: V | D; isResolved: boolean };

// export function usePromiseEx<V, D>(defaultWith: () => D, promise: V): Promise<PromiseState<V, D>>;
// export function usePromiseEx<V, D>(defaultValue: D, promise: V): Promise<PromiseState<V, D>>;
// export function usePromiseEx<V, D>(def: any, promise: V): Promise<PromiseState<V, D>> {
//   return vide(
//     () => {
//       const defValue = typeof def === 'function' ? def() : def;
//       const promiseState: PromiseState<V, D> = { value: defValue, isResolved: false };
//       const mutable = new Mutable<PromiseState<V, D>>(promiseState);
//       promise.then((v) => (mutable.value = { value: v, isResolved: true }));
//       return mutable.value;
//     },
//     async (state, ctx) => {
//       return [state, state];
//     },
//   );
// }

// export async function usePromise<V, D>(defaultWith: () => D, promise: V): Promise<V | D>;
// export async function usePromise<V, D>(defaultValue: D, promise: V): Promise<V | D>;
// export async function usePromise<V, D>(def: any, promise: V): Promise<V | D> {
//   return await map(usePromiseEx(def, promise), (v) => v.value);
// }

// Yes, we could just have returned Hack._ctx, but this is more as it should be.
export function getCtx() {
  return vide((_, ctx) => [ctx, ctx]);
}
