import * as Pxl from './pxl.js';
import { PNG } from 'pngjs';
import { writeFileSync } from 'fs';

// Create a small 24x24 canvas
const ctx = new Pxl.DrawingContext(24, 24);

// Clear with white background
ctx.clear(Pxl.colors.white);

// Red diagonal line
ctx.drawLine(2, 2, 22, 22)
  .color(Pxl.colors.red)
  .antiAlias(0)
  .thickness(3);

// // Red diagonal line
// ctx.drawLine(2, 22, 22, 2)
//   .color(Pxl.colors.red)
//   .antiAlias(3)
//   .thickness(2);

// // Blue horizontal line  
// ctx.drawLine(4, 12, 20, 12)
//   .color(Pxl.colors.blue)
//   .thickness(2)
//   .antiAlias(0);


// // Green vertical line
// ctx.drawLine(12, 4, 12, 20)
//    .color(Pxl.colors.green)
//    .thickness(1);

// // Purple anti-aliased diagonal
// ctx.drawLine(2, 22, 22, 2)
//    .color(Pxl.color.fromRgba(128, 0, 128, 1))
//    .thickness(1)
//    .antiAlias(1);

// // Orange thick line
// ctx.drawLine(8, 8, 16, 16)
//    .color(Pxl.color.fromRgba(255, 165, 0, 1))
//    .thickness(3);





const rendered = ctx.render(240, 240);

console.log('Saving as PNG...');
// Create PNG and save
const png = new PNG({
  width: rendered.width,
  height: rendered.height,
  colorType: 6 // RGBA
});

// Copy pixel data
png.data = Buffer.from(rendered.data);

// Save to file
const buffer = PNG.sync.write(png);
writeFileSync('output.png', buffer);

console.log('✅ Saved output.png (240x240) with colorful lines!');
console.log(`Original: 24x24, Scaled: ${rendered.width}x${rendered.height}`);
