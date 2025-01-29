import { vide } from '../vide.js';

export function counter(initial: number, step: number): Promise<number> {
  return vide(initial, async (state, ctx) => {
    return [state, state + step];
  });
}

export function delayBy1<V>(value: V, initial: V): Promise<V> {
  return vide(initial, async (state, ctx) => {
    return [state, value];
  });
}
