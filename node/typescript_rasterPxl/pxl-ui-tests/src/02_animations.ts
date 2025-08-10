import * as Anim from 'pxl-ui/src/Ui/anim.js';
import { Fill } from 'pxl-ui/src/graphics';
import { getCtx } from 'pxl-ui/src/vide.js';

// a component that grows a red rectangle over time,
// using an animation controller to store the current size.
function growingRect(x: number, y: number, maxDim: number, fill: Fill) {
  const ctx = getCtx();
  const { value } = Anim.easeOut(1.5, 0, maxDim, 'Loop', true);
  ctx.graphics.rectangle(x, y, 10, value).fill(fill);
}

// this is the final view, using 2 "instances" of the growingRect component.
export function scene() {
  const { w: w2, h: h2 } = getCtx().halfSize;

  growingRect(0, 0, h2, 'red');
  growingRect(w2, h2, h2, 'green');
}
