import { RenderCtx } from '../renderCtx.js';

// TODO: crap: use Color - or not?
export type Paint = string | CanvasGradient | CanvasPattern;

// --------------------------
// Drawing primitives
// --------------------------

export function pxl(ctx: RenderCtx, x: number, y: number, paint: Paint) {
  ctx.canvas2d.fillStyle = paint;
  ctx.canvas2d.fillRect(x, y, 1, 1);
}

export function rect(ctx: RenderCtx, x: number, y: number, w: number, h: number, paint: Paint) {
  ctx.canvas2d.fillStyle = paint;
  ctx.canvas2d.fillRect(x, y, w, h);
}

export function line(ctx: RenderCtx, x1: number, y1: number, x2: number, y2: number, paint: Paint) {
  ctx.canvas2d.strokeStyle = paint;
  ctx.canvas2d.beginPath();
  ctx.canvas2d.moveTo(x1, y1);
  ctx.canvas2d.lineTo(x2, y2);
  ctx.canvas2d.stroke();
}

export function circle(ctx: RenderCtx, x: number, y: number, r: number, paint: Paint) {
  ctx.canvas2d.fillStyle = paint;
  ctx.canvas2d.beginPath();
  ctx.canvas2d.arc(x, y, r, 0, 2 * Math.PI);
  ctx.canvas2d.fill();
}

export function ovalOfRect(ctx: RenderCtx, x: number, y: number, w: number, h: number, paint: Paint) {
  ctx.canvas2d.fillStyle = paint;
  ctx.canvas2d.beginPath();
  ctx.canvas2d.ellipse(x + w / 2, y + h / 2, w / 2, h / 2, 0, 0, 2 * Math.PI);
  ctx.canvas2d.fill();
}

export function oval(ctx: RenderCtx, cx: number, cy: number, rx: number, ry: number, paint: Paint) {
  ctx.canvas2d.fillStyle = paint;
  ctx.canvas2d.beginPath();
  ctx.canvas2d.ellipse(cx, cy, rx, ry, 0, 0, 2 * Math.PI);
  ctx.canvas2d.fill();
}

export function arc(ctx: RenderCtx, x: number, y: number, w: number, h: number, startAngle: number, sweepAngle: number, paint: Paint) {
  ctx.canvas2d.fillStyle = paint;
  ctx.canvas2d.beginPath();
  ctx.canvas2d.arc(x + w / 2, y + h / 2, Math.min(w, h) / 2, startAngle, startAngle + sweepAngle);
  ctx.canvas2d.fill();
}

export function bg(ctx: RenderCtx, color: string) {
  ctx.canvas2d.fillStyle = color;
  ctx.canvas2d.fillRect(0, 0, ctx.canvas2d.canvas.width, ctx.canvas2d.canvas.height);
}

// ------------------------------
// Imaging
// ------------------------------

// Important: Make use uf usePromise, because we are dealing with async functions

// export function image(loadFrame: () => Promise<Frame>, x: number, y: number) {
//     return vide(nothing, async (_, ctx) => {
//         let frame = useMemo(loadFrame);
//         ctx.canvasCtx.drawImage(frame.bmp, x, y);
//         return [nothing, nothing];
//     });
// }

// export function image(loadFrames: () => Imaging.Frame[], x: number, y: number, loop?: boolean) {
//     return vide(nothing, async (_, ctx) => {
//         let frames = preserveValueWith(loadFrames);
//         let currFrameInfo = useState({ None });
//         let state;
//         let mkState = (idx: number) => {
//             let frame = frames[idx];
//             return {
//                 frame: frame,
//                 idx: idx,
//                 expiresAt: ctx.now + frame.duration
//             };
//         };
//         if (currFrameInfo.value === None) {
//             state = mkState(0);
//         } else {
//             if (currFrameInfo.expiresAt < ctx.now) {
//                 let nextIdx = (currFrameInfo.idx + 1 === frames.length) ? (loop ? 0 : currFrameInfo.idx) : currFrameInfo.idx + 1;
//                 state = mkState(nextIdx);
//             } else {
//                 state = currFrameInfo;
//             }
//         }
//         ctx.requestMaxFps();
//         currFrameInfo.value = Some(state);
//         ctx.canvasCtx.drawImage(state.frame.bmp, x, y);
//         return [nothing, nothing];
//     });
// }

// export function image(path: string, x: number, y: number, resize?: Size, loop?: boolean, useAntiAlias?: boolean) {
//     return vide(nothing, async (_, ctx) => {
//         let frames = preserveValueWith(() => {
//             let frames = Imaging.loadFrames(path);
//             return resize ? Imaging.resizeFrames(resize.w, resize.h, useAntiAlias || false) : frames;
//         });
//         if (frames.length === 1) {
//             return Draw.image(() => frames[0], x, y);
//         } else if (frames.length > 1) {
//             return Draw.image(() => frames, x, y, loop);
//         } else {
//             return discardState;
//         }
//     });
// }
