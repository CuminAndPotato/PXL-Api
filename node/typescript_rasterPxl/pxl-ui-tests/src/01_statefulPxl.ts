import { Fill } from 'pxl-ui/src/graphics';
import { getCtx, useState } from 'pxl-ui/src/vide.js';

// a component that grows a red rectangle over time,
// using a single state variable to store the current size.
function growingRect(x: number, y: number, maxDim: number, fill: Fill) {
  const ctx = getCtx();

  const minDim = 0;
  const currSize = useState(minDim);
  ctx.graphics
    .rectangle(x, y, currSize.value, currSize.value)
    .fill(fill);

  currSize.value = currSize.value <= maxDim ? currSize.value + 0.3 : minDim;
}

// this is the final view, using 2 "instances" of the growingRect component.
export function scene() {
  growingRect(0, 0, 5, 'red');
  growingRect(10, 10, 10, 'green');

  // you can retrieve an instance of the renderContext
  // to access additional data, low level drawing functions, etc.
  // const _ctx = getCtx();
}
