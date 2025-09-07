// This is a hack, required because it's not possible to pass
// context to a promise callback, thus enabling to pass and collect
// state to Vide functions "under the hood" in conjkunction with using await.

export type State<S> = S | undefined;

export type StateAwareFunc<V, S> = (state: State<S>, ctx: RenderCtx) => [V, S];

export type SceneFunc<S> = () => S;

// const log = (message?: unknown, ...optionalParams: unknown[]) =>
//   console.debug(message, ...optionalParams);
const log = (message?: unknown, ...optionalParams: unknown[]) => { };


// We can't generalize _ctx because it's all singleton and single-thread hacks here,
// so we would either specialize it to RenderCtx (which we may do later), or
// leave it as any and lose type safety.
export const ResumableStateHack: {
  popStack?: readonly unknown[];
  pushStack: readonly unknown[];
  ctx?: RenderCtx;
} = {
  pushStack: []
};

export function popState<S>(): State<S> {
  // Why Option?
  // undefined can indeed be a state value, which has semantic different than
  // "no state", which then would result in taking the initial state, which
  // would be wrong. We need to distinguish between "no state at all"
  // and "whatever(!) value was used explicitly as state".
  // For this statement, we use an DU Option, that lets us safely
  // distinguish between "Some" and "None".
  if (ResumableStateHack.popStack === undefined)
    return undefined;

  if (ResumableStateHack.popStack.length === 0)
    throw new Error('popState: stack underflow - are you using language-builtin for / if similar?');

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

    if (this.onCycleStart)
      this.onCycleStart();

    const videRes = this.vide();
    this._state = teardownCycle();

    if (this.onCycleEnd)
      this.onCycleEnd();

    return videRes;
  }
}

import { RenderCtx } from './renderCtx.js';


// -------------------
// vide ctor functions
// -------------------

export function vide<V, S>(f: StateAwareFunc<V, S>): V {
  const s = popState<S>();
  const [vf, sf] = f(s, ResumableStateHack.ctx!);
  pushState(sf);
  return vf;
}

export function videWithInitStateFunc<V, S>(initial: () => S, f: (state: S, ctx: RenderCtx) => [V, S]): V {
  const s = popState<S>() ?? initial();
  const [vf, sf] = f(s, ResumableStateHack.ctx!);
  pushState(sf);
  return vf;
}

export function videWithInitState<V, S>(initial: S, f: (state: S, ctx: RenderCtx) => [V, S]): V {
  // manually inlining ok here (i.e. don't use videWithInitStateFunc)
  const s = popState<S>() ?? initial;
  const [vf, sf] = f(s, ResumableStateHack.ctx!);
  pushState(sf);
  return vf;
}

class Mutable<V> {
  constructor(public value: V) { }
}


// -------------------
// base functions
// -------------------

// TODO: For / Conditional

// Yes, we could just have returned Hack._ctx, but this is more as it should be.
export function getCtx() {
  return vide((_, ctx) => [ctx, ctx]);
}

// type IfElseState<SIf, SElse> = {
//   ifBranchState: SIf;
//   elseBranchState: SElse;
// }

// export function ifElse<V>(cond: boolean, ifBranch: () => V, elseBranch: () => V): V {
//   return vide((s, ctx) => {

//   });
// }


// -------------------
// useMemo
// -------------------

export function useMemoWith<V>(initial: () => V): V {
  return videWithInitStateFunc(
    initial,
    (state, ctx) => [state, state],
  );
}

export function useMemo<V>(initial: V): V {
  return videWithInitState(
    initial,
    (state, ctx) => [state, state],
  );
}


// -------------------
// useState
// -------------------

export function useStateWith<V>(initial: () => V): Mutable<V> {
  return useMemoWith(() => new Mutable(initial()));
}

export function useState<V>(initial: V): Mutable<V> {
  return useMemoWith(() => new Mutable(initial));
}


// -------------------
// usePromise
// -------------------

export function usePromiseWith<V>(defaultWith: () => V, promise: Promise<V>): V {
  return videWithInitStateFunc(
    () => {
      const defValue = defaultWith();
      const mutable = new Mutable(defValue);
      promise.then((v) => mutable.value = v);
      return mutable;
    },
    (state, ctx) => [state.value, state],
  );
}

export function usePromiseEx<V>(defaultValue: V, promise: Promise<V>): V {
  return usePromiseWith(() => defaultValue, promise);
}


// -------------------
// useObservable
// -------------------

import { Observable } from 'rxjs';

export function useObservableWith<V>(defaultWith: () => V, observable: Observable<V>): V {
  return videWithInitStateFunc(
    () => {
      const defValue = defaultWith();
      const mutable = new Mutable(defValue);
      observable.subscribe({
        next: (v) => (mutable.value = v),
        error: (err) => console.error('Observable error:', err)
      });
      return mutable;
    },
    (state, ctx) => [state.value, state]
  );
}

export function useObservableEx<V>(defaultValue: V, observable: Observable<V>): V {
  return useObservableWith(() => defaultValue, observable);
}
