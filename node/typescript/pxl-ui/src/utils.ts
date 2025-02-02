export const nothing: void = undefined;

export const Path = {
  join: (seg1: string, seg2: string): string => {
    const del = '/';
    const norm = (s: string) => s.trim().replace('\\', del);
    const a = norm(seg1).replace(new RegExp(`${del}$`), '');
    const b = norm(seg2).replace(new RegExp(`^${del}|${del}$`, 'g'), '');
    return a + del + b;
  },
};

export function clamp(lb: number, ub: number, value: number): number {
  return Math.max(lb, Math.min(ub, value));
}

export const clamp01 = (value: number) => clamp(0.0, 1.0, value);

export interface Size {
  w: number;
  h: number;
}

export function xyToIdx(width: number, x: number, y: number): number {
  return y * width + x;
}

export function idxToXy(width: number, idx: number): [number, number] {
  return [idx % width, Math.floor(idx / width)];
}

export function sleep(ms: number): Promise<void> {
  return ms <= 0
    ? Promise.resolve()
    : new Promise((resolve) => setTimeout(resolve, ms));
}
