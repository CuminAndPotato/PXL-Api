// This is a hack, required because it's not possible to pass
// context to a promise callback, thus enabling to pass and collect
// state to Vide functions "under the hood" in conjkunction with using await.

export type SceneFunc<V> = () => V;

let log = (message?: any, ...optionalParams: any[]) =>
  console.debug(message, ...optionalParams);

// We can't generalize _ctx because it's all singleton and single-thread hacks here,
// so we would either specialize it to RenderCtx (which we may do later), or
// leave it as any and lose type safety.
export namespace ResumableStateHack {
  export let popStack: readonly any[] | undefined = undefined;
  export let pushStack: readonly any[] = [];
  export let ctx: RenderCtx | undefined;
}

export function popState() {
  if (ResumableStateHack.popStack === undefined) {
    return undefined;
  }
  if (ResumableStateHack.popStack.length === 0) {
    throw new Error('popState: stack underflow - are you using language-builtin for / if similar?');
  }
  const [x, ...xs] = ResumableStateHack.popStack;
  ResumableStateHack.popStack = xs;
  return x;
}

export function pushState(state: any) {
  log('Pushing state ...', state);
  ResumableStateHack.pushStack = ResumableStateHack.pushStack === undefined ? [state] : [...ResumableStateHack.pushStack, state];
}

export class Evaluable<T> {
  private _state: any = undefined;

  constructor(
    private readonly vide: SceneFunc<T>,
    private readonly ctx: any,
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
    this.onCycleStart && this.onCycleStart();
    const videRes = await this.vide();
    this._state = teardownCycle();
    this.onCycleEnd && this.onCycleEnd();
    return videRes;
  }
}

import { RenderCtx } from './renderCtx.js';

export function vide<V, S>(func: (state: S | undefined, ctx: RenderCtx) => [V, S]): V;
export function vide<V, S>(initial: () => S, func: (state: S, ctx: RenderCtx) => [V, S]): V;
export function vide<V, S>(initial: S, func: (state: S, ctx: RenderCtx) => [V, S]): V;
export function vide<V, S>(arg1: any, arg2?: any): V {
  if (typeof arg1 === 'function' && arg2 === undefined) {
    const state = popState();
    const [v, s] = arg1(state, ResumableStateHack.ctx!);
    pushState(s);
    return v;
  }

  return vide<V, S>((state, ctx) => {
    const s = state === undefined ? (typeof arg1 === 'function' ? arg1() : arg1) : state;
    return arg2(s, ctx);
  });
}

class Mutable<V> {
  constructor(public value: V) { }
}

export function useMemo<V>(initial: () => V): V;
export function useMemo<V>(initial: V): V;
export function useMemo<V>(initial: any): V {
  return vide(
    () => (typeof initial === 'function' ? initial() : initial),
    (state, ctx) => [state, state],
  );
}

export function useState<V>(initial: () => V): Mutable<V>;
export function useState<V>(initial: V): Mutable<V>;
export function useState<V>(initial: any): Mutable<V> {
  return useMemo(() => new Mutable(typeof initial === 'function' ? initial() : initial));
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
