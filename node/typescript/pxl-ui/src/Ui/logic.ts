import { vide } from '../vide.js';

export function counter(initial: number, step: number): number {
  return vide(initial, (state, ctx) => {
    return [state, state + step];
  });
}

export function delayBy1<V>(value: V, initial: V): V {
  return vide(initial, (state, ctx) => {
    return [state, value];
  });
}
