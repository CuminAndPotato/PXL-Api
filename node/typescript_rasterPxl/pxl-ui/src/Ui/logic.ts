import { videWithInitState } from '../vide.js';

export function counter(initial: number, step: number): number {
  return videWithInitState(initial, (state, ctx) => {
    return [state, state + step];
  });
}

export function delayBy1<V>(value: V, initial: V): V {
  return videWithInitState(initial, (state, ctx) => {
    return [state, value];
  });
}
