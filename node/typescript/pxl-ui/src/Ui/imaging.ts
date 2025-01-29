import sharp, { Sharp } from 'sharp';
import { Color } from '../color.js';

export type Frame = {
  bmp: readonly Color[];
  duration: number;
};

async function loadFrames(path: string): Promise<Frame[]> {
  // TODO Oh no: Duration is not supported by

  const image = sharp(path);
  const metadata = await image.metadata();

  const mkFrame = async (img: Sharp) => {
    let bmp = await img.raw().toBuffer();
    let colors = new Array<Color>(bmp.length / 4);
    for (let i = 0; i < colors.length; i++) {
      const idx = i * 4;
      colors[i] = Color.rgba(bmp[idx], bmp[idx + 1], bmp[idx + 2], bmp[idx + 3]);
    }

    return { bmp: colors, duration: 0 };
  };

  if (metadata.pages === undefined || metadata.pages === 1) {
    return [await mkFrame(image)];
  } else {
    return Promise.all(
      Array.from({ length: metadata.pages }, async (_, i) =>
        mkFrame(image.extract({ left: 0, top: 0, width: metadata.width!, height: metadata.height! })),
      ),
    );
  }
}
