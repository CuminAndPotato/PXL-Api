import * as Draw from '../Ui/draw.js';
import * as Anim from '../Ui/anim.js';
import * as Eval from '../evaluation.js';
import { useState, getCtx } from '../vide.js';
import * as Proxy from '../canvasProxy.js';

// const host = "localhost";
const host = "192.168.178.52";

// a component that grows a red rectangle over time,
// using an animation controller to store the current size.
async function growingRect(x: number, y: number, maxDim: number, paint: Draw.Paint) {
  const ctx = await getCtx();
  const { value } = await Anim.easeOut(1.5, 0, maxDim, 'Loop', true);
  Draw.rect(ctx, x, y, 10, value, paint);
}

// this is the final view, using 2 "instances" of the growingRect component.
async function demo() {
  const { w, h } = (await getCtx()).size;
  const [w2, h2] = [w / 2, h / 2];

  await growingRect(0, 0, h2, 'red');
  await growingRect(w2, h2, h2, 'green');
}

// a continuous animation
Eval.start(await Proxy.create(host, 20), demo);
