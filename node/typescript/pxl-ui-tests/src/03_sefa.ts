import * as Anim from 'pxl-ui/src/Ui/anim';
import * as Draw from 'pxl-ui/src/Ui/draw';
import { Colors } from 'pxl-ui/src/colors';
import { getCtx } from 'pxl-ui/src/vide';

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

export function scene() {
  const { value: brightness } = Anim.linear(5, 0, 1, 'Loop', true);
  const color = Colors.blue.setBrightness(brightness);
  const ctx = getCtx();

  Draw.rect(ctx, 0, 0, ctx.widthHalf, ctx.heightHalf, color.toHex());
  Draw.rect(
    ctx,
    ctx.widthHalf,
    ctx.heightHalf,
    ctx.widthHalf,
    ctx.heightHalf,
    color.toHex(),
  );
}
