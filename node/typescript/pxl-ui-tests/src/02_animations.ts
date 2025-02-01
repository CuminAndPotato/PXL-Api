import * as Draw from 'pxl-ui/src/Ui/draw.js';
import * as Anim from 'pxl-ui/src/Ui/anim.js';
import * as Eval from 'pxl-ui/src/evaluation.js';
import { useState, getCtx } from 'pxl-ui/src/vide.js';
import * as Proxy from 'pxl-ui/src/canvasProxy.js';

const host = "localhost";
// const host = "192.168.178.52";

// a component that grows a red rectangle over time,
// using an animation controller to store the current size.
function growingRect(x: number, y: number, maxDim: number, paint: Draw.Paint) {
  const ctx = getCtx();
  const { value } = Anim.easeOut(1.5, 0, maxDim, 'Loop', true);
  Draw.rect(ctx, x, y, 10, value, paint);
}

// this is the final view, using 2 "instances" of the growingRect component.
function demo() {
  const ctx = getCtx();
  const { w: w2, h: h2 } = getCtx().halfSize;

  growingRect(0, 0, h2, 'red');
  growingRect(w2, h2, h2, 'green');
}

// a continuous animation
Eval.start(await Proxy.create(host, 20), demo);
