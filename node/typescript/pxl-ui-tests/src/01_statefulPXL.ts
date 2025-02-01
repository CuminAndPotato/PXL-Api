import * as Draw from 'pxl-ui/src/Ui/draw.js';
import * as Eval from 'pxl-ui/src/evaluation.js';
import { useState, getCtx } from 'pxl-ui/src/vide.js';
import * as Proxy from 'pxl-ui/src/canvasProxy.js';

const canvas = Proxy.create("localhost", 20);

// a component that grows a red rectangle over time,
// using a single state variable to store the current size.
function growingRect(x: number, y: number, maxDim: number, paint: Draw.Paint) {
  const ctx = getCtx();

  const minDim = 0;
  const currSize = useState(minDim);
  Draw.rect(ctx, x, y, currSize.value, currSize.value, paint);

  currSize.value = currSize.value <= maxDim ? currSize.value + 0.3 : minDim;
}

// this is the final view, using 2 "instances" of the growingRect component.
function demo() {
  growingRect(0, 0, 5, 'red');
  growingRect(10, 10, 10, 'green');

  // you can retrieve an instance of the renderContext
  // to access additional data, low level drawing functions, etc.
  const _ctx = getCtx();
}

// a continuous animation
Eval.start(await canvas, demo);
