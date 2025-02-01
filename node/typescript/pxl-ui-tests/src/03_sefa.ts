import * as Draw from 'pxl-ui/src/Ui/draw.js';
import * as Anim from 'pxl-ui/src/Ui/anim.js';
import * as Eval from 'pxl-ui/src/evaluation.js';
import { Color } from 'pxl-ui/src/color.js';
import { Colors } from 'pxl-ui/src/colors.js';
import { useState, getCtx } from 'pxl-ui/src/vide.js';
import * as Proxy from 'pxl-ui/src/canvasProxy.js';

/*
  Test on the PXL-Clock:

  1. Tell the daemon to turn off the canvas:

    > curl -v -X POST http://192.168.178.52/daemon/dev/canvas/off

    Important:
      - turn on again with "on" instead of "off"
      - after a while, the display will turn "on" again automatically.
        Just make the POST request again.

  2. Run the script shown here
    - adjust the package.json to use this file

    - start the script
      > npm start

*/

// Simulator: localhost; a PXL-Clock: The IP address of the PXL-Clock
// const host = "localhost";
const host = "192.168.178.52";



// Modify this to generate whatever test pattern
function testbild() {
  const ctx = getCtx();

  const { value: brightness } = Anim.linear(5, 0, 1, 'Loop', true);
  const color = Color.rgb(255, 255, 255).setBrightness(brightness);
  Draw.rect(ctx, 0, 0, ctx.widthHalf, ctx.heightHalf, color.toHex());
  Draw.rect(ctx, ctx.widthHalf, ctx.heightHalf, ctx.widthHalf, ctx.heightHalf, color.toHex());
}


// This is the clamping algorithm
// using side effects to incoming pixels.
  // ww have to transform that here a tiny bit later
  // when we integrate it in the LedMatrix
  function clamp(pixels: Color[]) {
  for (let i = 0; i < pixels.length; i++) {
    const color = pixels[i];
    pixels[i] = color.setBrightness(0.5);
  }
}





// this is the final view
function demo() {
  testbild();

  const ctx = getCtx();
  const snapshot = ctx.getSnapshot();
  clamp(snapshot);
  ctx.setSnapshot(snapshot);
}

// a continuous animation
Eval.start(await Proxy.create(host, 20), demo);
