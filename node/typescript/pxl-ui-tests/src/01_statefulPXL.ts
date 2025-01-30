import * as Draw from '../Ui/draw.js';
import * as Eval from '../evaluation.js';
import { useState, getCtx } from '../vide.js';
import * as Proxy from '../canvasProxy.js';

const canvas = Proxy.create("localhost", 20);

// a component that grows a red rectangle over time,
// using a single state variable to store the current size.
async function growingRect(x: number, y: number, maxDim: number, paint: Draw.Paint) {
  const ctx = await getCtx();

  const minDim = 0;
  const currSize = await useState(minDim);
  Draw.rect(ctx, x, y, currSize.value, currSize.value, paint);

  currSize.value = currSize.value <= maxDim ? currSize.value + 0.3 : minDim;
}

// this is the final view, using 2 "instances" of the growingRect component.
async function demo() {
  await growingRect(0, 0, 5, 'red');
  await growingRect(10, 10, 10, 'green');

  // you can retrieve an instance of the renderContext
  // to access additional data, low level drawing functions, etc.
  const _ctx = await getCtx();
}

// a continuous animation
Eval.start(await canvas, demo);

// a single "plot"
// Eval.plot(pxlConsoleCanvas, demo);

// a single "plot" with a fixed number of cycles
// const view = Eval.build(pxlConsoleCanvas, demo);
// await view.evaluable.next();
// await view.evaluable.next();
// await view.evaluable.next();
// await view.evaluable.next();

// a single "plot" with a fixed number of cycles (alternative)
// await Eval.times(pxlConsoleCanvas, demo, 3);





/*
 SEFA TALK

const walkingRectComponent = (speed: number) => {
  let stateOfDrawingFunc = { posX: 0 }
  return (ctx: SKRSContext2D) => {
    ctx.rect(stateOfDrawingFunc.posX, 0, 10, 10);
    stateOfDrawingFunc.posX += speed;
  }
}


const myTwoWalklingRectsApp = () => {
  const rect1 = walkingRectComponent(3);
  const rect2 = walkingRectComponent(23);
  return (ctx: SKRSContext2D) => {
    if (cond)
      rect1(ctx);

    // rect1(ctx);
    rect2(ctx);
  }
}


const myTwoWalklingRectsApp = () => {
  await walkingRectComponent(3);
  await walkingRectComponent(23);
}



Eval.start(myTwoWalklingRectsApp())

*/
