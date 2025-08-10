
// TypeScript Rasterization Library with Gradient Support
// No external dependencies - pure mathematical algorithms

// ============================================================================
// IMPORTS AND UTILITIES
// ============================================================================

import { Color } from './color.js';
import { clamp01 } from './utils.js';

// ============================================================================
// CORE TYPES AND INTERFACES
// ============================================================================

interface Point {
  x: number;
  y: number;
}

type AntiAliasingLevel = 'off' | 's' | 'm' | 'l';

interface BezierCurve {
  p0: Point;
  p1: Point;
  p2: Point;
  p3: Point;
}

interface Arc {
  centerX: number;
  centerY: number;
  radius: number;
  startAngle: number;
  endAngle: number;
}

// ============================================================================
// GRADIENT TYPES
// ============================================================================

interface ColorStop {
  offset: number; // 0.0 to 1.0
  color: Color;
}

interface LinearGradient {
  type: 'linear';
  x0: number;
  y0: number;
  x1: number;
  y1: number;
  stops: ColorStop[];
}

interface RadialGradient {
  type: 'radial';
  cx: number;
  cy: number;
  radius: number;
  stops: ColorStop[];
}

type Fill = Color | LinearGradient | RadialGradient;

// ============================================================================
// GRADIENT UTILITIES
// ============================================================================

class GradientUtils {
  // Interpolate between two colors at parameter t (0.0 to 1.0)
  static interpolateColor(color1: Color, color2: Color, t: number): Color {
    t = clamp01(t);
    const invT = 1 - t;

    return Color.argb(
      Math.round(color1.a * invT + color2.a * t),
      Math.round(color1.r * invT + color2.r * t),
      Math.round(color1.g * invT + color2.g * t),
      Math.round(color1.b * invT + color2.b * t)
    );
  }

  // Sample color from gradient stops at parameter t (0.0 to 1.0)
  static sampleGradient(stops: ColorStop[], t: number): Color {
    t = clamp01(t);

    if (stops.length === 0) return Color.argb(0, 0, 0, 0);
    if (stops.length === 1) return stops[0].color;

    // Sort stops by offset
    const sortedStops = [...stops].sort((a, b) => a.offset - b.offset);

    // Handle edge cases
    if (t <= sortedStops[0].offset) return sortedStops[0].color;
    if (t >= sortedStops[sortedStops.length - 1].offset) return sortedStops[sortedStops.length - 1].color;

    // Find the two stops to interpolate between
    for (let i = 0; i < sortedStops.length - 1; i++) {
      const stop1 = sortedStops[i];
      const stop2 = sortedStops[i + 1];

      if (t >= stop1.offset && t <= stop2.offset) {
        const localT = (t - stop1.offset) / (stop2.offset - stop1.offset);
        return this.interpolateColor(stop1.color, stop2.color, localT);
      }
    }

    return sortedStops[0].color;
  }

  // Calculate linear gradient parameter for point (x, y)
  static linearGradientT(gradient: LinearGradient, x: number, y: number): number {
    const dx = gradient.x1 - gradient.x0;
    const dy = gradient.y1 - gradient.y0;
    const lengthSq = dx * dx + dy * dy;

    if (lengthSq === 0) return 0;

    const dotProduct = (x - gradient.x0) * dx + (y - gradient.y0) * dy;
    return dotProduct / lengthSq;
  }

  // Calculate radial gradient parameter for point (x, y)
  static radialGradientT(gradient: RadialGradient, x: number, y: number): number {
    const dx = x - gradient.cx;
    const dy = y - gradient.cy;
    const distance = Math.sqrt(dx * dx + dy * dy);
    return distance / gradient.radius;
  }

  // Sample fill at specific coordinates
  static sampleFill(fill: Fill, x: number, y: number): Color {
    if (fill instanceof Color) {
      return fill;
    }

    if (fill.type === 'linear') {
      const t = this.linearGradientT(fill, x, y);
      return this.sampleGradient(fill.stops, t);
    }

    if (fill.type === 'radial') {
      const t = this.radialGradientT(fill, x, y);
      return this.sampleGradient(fill.stops, t);
    }

    return Color.argb(0, 0, 0, 0);
  }
}

// ============================================================================
// GRADIENT FACTORY FUNCTIONS
// ============================================================================

class Gradients {
  static linear(x0: number, y0: number, x1: number, y1: number, stops: ColorStop[]): LinearGradient {
    return {
      type: 'linear',
      x0, y0, x1, y1,
      stops: [...stops].sort((a, b) => a.offset - b.offset)
    };
  }

  static radial(cx: number, cy: number, radius: number, stops: ColorStop[]): RadialGradient {
    return {
      type: 'radial',
      cx, cy, radius,
      stops: [...stops].sort((a, b) => a.offset - b.offset)
    };
  }

  static stop(offset: number, color: Color): ColorStop {
    return { offset: Math.max(0, Math.min(1, offset)), color };
  }
}

// ============================================================================
// CORE PIXEL BUFFER AND COLOR MANAGEMENT
// ============================================================================

class PixelBuffer {
  readonly data: Uint32Array;
  readonly pixels8: Uint8ClampedArray;
  readonly width: number;
  readonly height: number;
  private readonly buffer: ArrayBuffer;

  constructor(width: number, height: number) {
    this.width = width;
    this.height = height;
    this.buffer = new ArrayBuffer(width * height * 4);
    this.data = new Uint32Array(this.buffer);
    this.pixels8 = new Uint8ClampedArray(this.buffer);
  }

  // Pack Color into 32-bit integer (optimized for little-endian)
  private packColor(color: Color): number {
    return (color.a << 24) | (color.b << 16) | (color.g << 8) | color.r;
  }

  // Unpack 32-bit integer to Color
  private unpackColor(packed: number): Color {
    return Color.argb(
      (packed >> 24) & 0xff,
      packed & 0xff,
      (packed >> 8) & 0xff,
      (packed >> 16) & 0xff
    );
  }

  setPixel(x: number, y: number, color: Color): void {
    if (x < 0 || x >= this.width || y < 0 || y >= this.height) return;
    this.data[y * this.width + x] = this.packColor(color);
  }

  getPixel(x: number, y: number): Color {
    if (x < 0 || x >= this.width || y < 0 || y >= this.height) {
      return Color.argb(0, 0, 0, 0);
    }
    return this.unpackColor(this.data[y * this.width + x]);
  }

  // Optimized alpha blending using integer arithmetic
  blendPixel(x: number, y: number, color: Color): void {
    if (x < 0 || x >= this.width || y < 0 || y >= this.height) return;

    const src = this.packColor(color);
    const srcA = (src >> 24) & 0xff;

    if (srcA === 255) {
      this.data[y * this.width + x] = src;
      return;
    }
    if (srcA === 0) return;

    const dstIndex = y * this.width + x;
    const dst = this.data[dstIndex];

    const invAlpha = 256 - srcA;
    const srcR = src & 0xff;
    const srcG = (src >> 8) & 0xff;
    const srcB = (src >> 16) & 0xff;

    const dstR = dst & 0xff;
    const dstG = (dst >> 8) & 0xff;
    const dstB = (dst >> 16) & 0xff;
    const dstA = (dst >> 24) & 0xff;

    const outR = ((srcR * srcA + dstR * invAlpha) >> 8) & 0xff;
    const outG = ((srcG * srcA + dstG * invAlpha) >> 8) & 0xff;
    const outB = ((srcB * srcA + dstB * invAlpha) >> 8) & 0xff;
    const outA = srcA + ((dstA * invAlpha) >> 8);

    this.data[dstIndex] = (outA << 24) | (outB << 16) | (outG << 8) | outR;
  }

  clear(color: Color = Color.argb(0, 0, 0, 0)): void {
    const packed = this.packColor(color);
    this.data.fill(packed);
  }

  // Get raw RGBA array for output
  getRGBAArray(): Uint8ClampedArray {
    return this.pixels8;
  }
}

// ============================================================================
// MATHEMATICAL UTILITIES
// ============================================================================

class MathUtils {
  // Fast integer square root using bit manipulation
  static fastSqrt(x: number): number {
    if (x < 0) return 0;
    if (x < 2) return x;

    let result = 0;
    let bit = 1 << 14; // Start with largest bit for 32-bit numbers

    while (bit > x) bit >>= 2;

    while (bit !== 0) {
      if (x >= result + bit) {
        x -= result + bit;
        result = (result >> 1) + bit;
      } else {
        result >>= 1;
      }
      bit >>= 2;
    }
    return result;
  }

  // Smooth step function for antialiasing
  static smoothstep(edge0: number, edge1: number, x: number): number {
    const t = Math.max(0, Math.min(1, (x - edge0) / (edge1 - edge0)));
    return t * t * (3 - 2 * t);
  }

  // Fractional part (for Wu's algorithm)
  static fpart(x: number): number {
    return x - Math.floor(x);
  }

  // Reverse fractional part
  static rfpart(x: number): number {
    return 1 - MathUtils.fpart(x);
  }

  // Distance from point to line segment
  static distanceToLineSegment(px: number, py: number, x1: number, y1: number, x2: number, y2: number): number {
    const dx = x2 - x1;
    const dy = y2 - y1;
    const lengthSq = dx * dx + dy * dy;

    if (lengthSq === 0) {
      return Math.sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
    }

    const t = Math.max(0, Math.min(1, ((px - x1) * dx + (py - y1) * dy) / lengthSq));
    const projX = x1 + t * dx;
    const projY = y1 + t * dy;

    return Math.sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
  }
}

// ============================================================================
// LINE RASTERIZATION
// ============================================================================

class LineRenderer {
  // Bresenham's line algorithm - no antialiasing
  static drawLineBresenham(buffer: PixelBuffer, x0: number, y0: number, x1: number, y1: number, color: Color): void {
    x0 = Math.floor(x0);
    y0 = Math.floor(y0);
    x1 = Math.floor(x1);
    y1 = Math.floor(y1);

    const dx = Math.abs(x1 - x0);
    const dy = Math.abs(y1 - y0);
    const sx = x0 < x1 ? 1 : -1;
    const sy = y0 < y1 ? 1 : -1;
    let err = dx - dy;

    while (true) {
      buffer.setPixel(x0, y0, color);

      if (x0 === x1 && y0 === y1) break;

      const e2 = err << 1;
      if (e2 > -dy) {
        err -= dy;
        x0 += sx;
      }
      if (e2 < dx) {
        err += dx;
        y0 += sy;
      }
    }
  }

  // Wu's antialiased line algorithm
  static drawLineWu(buffer: PixelBuffer, x0: number, y0: number, x1: number, y1: number, color: Color): void {
    const steep = Math.abs(y1 - y0) > Math.abs(x1 - x0);

    if (steep) {
      [x0, y0, x1, y1] = [y0, x0, y1, x1];
    }

    if (x0 > x1) {
      [x0, y0, x1, y1] = [x1, y1, x0, y0];
    }

    const dx = x1 - x0;
    const dy = y1 - y0;
    const gradient = dx === 0 ? 1 : dy / dx;

    // Handle first endpoint
    const xend = Math.round(x0);
    const yend = y0 + gradient * (xend - x0);
    const xgap = MathUtils.rfpart(x0 + 0.5);
    const xpxl1 = xend;
    const ypxl1 = Math.floor(yend);

    const plot = (x: number, y: number, alpha: number) => {
      const blendColor = color.opacity(color.opacity() * alpha);
      if (steep) {
        buffer.blendPixel(y, x, blendColor);
      } else {
        buffer.blendPixel(x, y, blendColor);
      }
    };

    plot(xpxl1, ypxl1, MathUtils.rfpart(yend) * xgap);
    plot(xpxl1, ypxl1 + 1, MathUtils.fpart(yend) * xgap);

    let intersectY = yend + gradient;

    // Handle second endpoint
    const xend2 = Math.round(x1);
    const yend2 = y1 + gradient * (xend2 - x1);
    const xgap2 = MathUtils.fpart(x1 + 0.5);
    const xpxl2 = xend2;
    const ypxl2 = Math.floor(yend2);

    plot(xpxl2, ypxl2, MathUtils.rfpart(yend2) * xgap2);
    plot(xpxl2, ypxl2 + 1, MathUtils.fpart(yend2) * xgap2);

    // Main loop
    for (let x = xpxl1 + 1; x < xpxl2; x++) {
      plot(x, Math.floor(intersectY), MathUtils.rfpart(intersectY));
      plot(x, Math.floor(intersectY) + 1, MathUtils.fpart(intersectY));
      intersectY += gradient;
    }
  }

  // Supersampled line for high quality antialiasing
  static drawLineSupersampled(buffer: PixelBuffer, x0: number, y0: number, x1: number, y1: number, color: Color, sampleRate: number): void {
    const samplesPerAxis = Math.sqrt(sampleRate);
    const sampleOffset = 1.0 / samplesPerAxis;

    // Get bounding box
    const minX = Math.max(0, Math.floor(Math.min(x0, x1)) - 1);
    const maxX = Math.min(buffer.width - 1, Math.ceil(Math.max(x0, x1)) + 1);
    const minY = Math.max(0, Math.floor(Math.min(y0, y1)) - 1);
    const maxY = Math.min(buffer.height - 1, Math.ceil(Math.max(y0, y1)) + 1);

    for (let py = minY; py <= maxY; py++) {
      for (let px = minX; px <= maxX; px++) {
        let coverage = 0;

        for (let sy = 0; sy < samplesPerAxis; sy++) {
          for (let sx = 0; sx < samplesPerAxis; sx++) {
            const sampleX = px + (sx + 0.5) * sampleOffset;
            const sampleY = py + (sy + 0.5) * sampleOffset;

            const distance = MathUtils.distanceToLineSegment(sampleX, sampleY, x0, y0, x1, y1);
            if (distance < 0.5) {
              coverage++;
            }
          }
        }

        if (coverage > 0) {
          const alpha = coverage / sampleRate;
          const blendColor = color.opacity(color.opacity() * alpha);
          buffer.blendPixel(px, py, blendColor);
        }
      }
    }
  }
}

// ============================================================================
// CIRCLE AND ARC RASTERIZATION
// ============================================================================

class CircleRenderer {
  // Bresenham's circle algorithm - no antialiasing
  static drawCircleBresenham(buffer: PixelBuffer, centerX: number, centerY: number, radius: number, color: Color): void {
    centerX = Math.floor(centerX);
    centerY = Math.floor(centerY);
    radius = Math.floor(radius);

    let x = 0;
    let y = radius;
    let d = 1 - radius;

    const plotCirclePoints = (cx: number, cy: number, px: number, py: number) => {
      buffer.setPixel(cx + px, cy + py, color);
      buffer.setPixel(cx - px, cy + py, color);
      buffer.setPixel(cx + px, cy - py, color);
      buffer.setPixel(cx - px, cy - py, color);
      buffer.setPixel(cx + py, cy + px, color);
      buffer.setPixel(cx - py, cy + px, color);
      buffer.setPixel(cx + py, cy - px, color);
      buffer.setPixel(cx - py, cy - px, color);
    };

    plotCirclePoints(centerX, centerY, x, y);

    while (x < y) {
      x++;
      if (d < 0) {
        d = d + (x << 1) + 1;
      } else {
        y--;
        d = d + ((x - y) << 1) + 1;
      }
      plotCirclePoints(centerX, centerY, x, y);
    }
  }

  // SDF-based antialiased circle
  static drawCircleSDF(buffer: PixelBuffer, centerX: number, centerY: number, radius: number, color: Color): void {
    const minX = Math.max(0, Math.floor(centerX - radius - 1));
    const maxX = Math.min(buffer.width - 1, Math.ceil(centerX + radius + 1));
    const minY = Math.max(0, Math.floor(centerY - radius - 1));
    const maxY = Math.min(buffer.height - 1, Math.ceil(centerY + radius + 1));

    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        const dx = x + 0.5 - centerX;
        const dy = y + 0.5 - centerY;
        const distance = Math.sqrt(dx * dx + dy * dy) - radius;

        let alpha = 0;
        if (distance <= -0.5) {
          alpha = 1;
        } else if (distance <= 0.5) {
          alpha = 0.5 - distance;
        }

        if (alpha > 0) {
          const blendColor = color.opacity(color.opacity() * alpha);
          buffer.blendPixel(x, y, blendColor);
        }
      }
    }
  }

  // Draw arc using parametric representation
  static drawArc(buffer: PixelBuffer, arc: Arc, color: Color, antialiasing: AntiAliasingLevel): void {
    const { centerX, centerY, radius, startAngle, endAngle } = arc;

    // Normalize angles
    let start = startAngle;
    let end = endAngle;
    if (end < start) end += 2 * Math.PI;

    // Calculate step size based on radius and antialiasing level
    const circumference = 2 * Math.PI * radius;
    const steps = Math.max(8, Math.ceil(circumference * (end - start) / (2 * Math.PI)));
    const angleStep = (end - start) / steps;

    const points: Point[] = [];
    for (let i = 0; i <= steps; i++) {
      const angle = start + i * angleStep;
      points.push({
        x: centerX + radius * Math.cos(angle),
        y: centerY + radius * Math.sin(angle)
      });
    }

    // Draw lines between consecutive points
    for (let i = 0; i < points.length - 1; i++) {
      const p1 = points[i];
      const p2 = points[i + 1];

      if (antialiasing === 'off') {
        LineRenderer.drawLineBresenham(buffer, p1.x, p1.y, p2.x, p2.y, color);
      } else {
        LineRenderer.drawLineWu(buffer, p1.x, p1.y, p2.x, p2.y, color);
      }
    }
  }
}

// ============================================================================
// RECTANGLE RASTERIZATION
// ============================================================================

class RectangleRenderer {
  // Optimized axis-aligned rectangle fill
  static fillAxisAlignedRect(buffer: PixelBuffer, x: number, y: number, width: number, height: number, fill: Fill): void {
    const startX = Math.max(0, Math.floor(x));
    const startY = Math.max(0, Math.floor(y));
    const endX = Math.min(buffer.width, Math.floor(x + width));
    const endY = Math.min(buffer.height, Math.floor(y + height));

    for (let row = startY; row < endY; row++) {
      for (let col = startX; col < endX; col++) {
        const color = GradientUtils.sampleFill(fill, col + 0.5, row + 0.5);
        buffer.setPixel(col, row, color);
      }
    }
  }

  // Antialiased rectangle using supersampling
  static fillRectangleAntialiased(buffer: PixelBuffer, x: number, y: number, width: number, height: number, fill: Fill, sampleRate: number): void {
    const samplesPerAxis = Math.sqrt(sampleRate);
    const sampleOffset = 1.0 / samplesPerAxis;

    const minX = Math.max(0, Math.floor(x) - 1);
    const maxX = Math.min(buffer.width - 1, Math.ceil(x + width) + 1);
    const minY = Math.max(0, Math.floor(y) - 1);
    const maxY = Math.min(buffer.height - 1, Math.ceil(y + height) + 1);

    for (let py = minY; py <= maxY; py++) {
      for (let px = minX; px <= maxX; px++) {
        let coverage = 0;
        let totalColor = Color.argb(0, 0, 0, 0);

        for (let sy = 0; sy < samplesPerAxis; sy++) {
          for (let sx = 0; sx < samplesPerAxis; sx++) {
            const sampleX = px + (sx + 0.5) * sampleOffset;
            const sampleY = py + (sy + 0.5) * sampleOffset;

            if (sampleX >= x && sampleX < x + width && sampleY >= y && sampleY < y + height) {
              const sampleColor = GradientUtils.sampleFill(fill, sampleX, sampleY);
              totalColor = GradientUtils.interpolateColor(totalColor, sampleColor, 1.0 / (coverage + 1));
              coverage++;
            }
          }
        }

        if (coverage > 0) {
          const alpha = coverage / sampleRate;
          const blendColor = totalColor.opacity(totalColor.opacity() * alpha);
          buffer.blendPixel(px, py, blendColor);
        }
      }
    }
  }
}

// ============================================================================
// POLYGON RASTERIZATION
// ============================================================================

interface EdgeBucket {
  yMax: number;
  xOfYmin: number;
  slopeInverse: number;
}

class PolygonRenderer {
  // Scanline polygon filling with Active Edge Table
  static fillPolygon(buffer: PixelBuffer, vertices: Point[], fill: Fill): void {
    if (vertices.length < 3) return;

    // Find Y bounds
    let minY = vertices[0].y;
    let maxY = vertices[0].y;
    for (let i = 1; i < vertices.length; i++) {
      minY = Math.min(minY, vertices[i].y);
      maxY = Math.max(maxY, vertices[i].y);
    }

    minY = Math.max(0, Math.floor(minY));
    maxY = Math.min(buffer.height - 1, Math.floor(maxY));

    // Build Global Edge Table
    const globalEdgeTable = new Map<number, EdgeBucket[]>();

    for (let i = 0; i < vertices.length; i++) {
      const v1 = vertices[i];
      const v2 = vertices[(i + 1) % vertices.length];

      if (Math.floor(v1.y) !== Math.floor(v2.y)) {
        const yMin = Math.min(v1.y, v2.y);
        const yMax = Math.max(v1.y, v2.y);
        const xOfYmin = v1.y < v2.y ? v1.x : v2.x;
        const slopeInverse = (v2.x - v1.x) / (v2.y - v1.y);

        const bucket: EdgeBucket = {
          yMax: Math.floor(yMax),
          xOfYmin: xOfYmin,
          slopeInverse: slopeInverse
        };

        const y = Math.floor(yMin);
        if (!globalEdgeTable.has(y)) {
          globalEdgeTable.set(y, []);
        }
        globalEdgeTable.get(y)!.push(bucket);
      }
    }

    // Process scanlines
    const activeEdgeTable: EdgeBucket[] = [];

    for (let y = minY; y <= maxY; y++) {
      // Add new edges from GET
      if (globalEdgeTable.has(y)) {
        activeEdgeTable.push(...globalEdgeTable.get(y)!);
      }

      // Remove edges that end at this scanline
      for (let i = activeEdgeTable.length - 1; i >= 0; i--) {
        if (activeEdgeTable[i].yMax <= y) {
          activeEdgeTable.splice(i, 1);
        }
      }

      // Sort by x coordinate
      activeEdgeTable.sort((a, b) => a.xOfYmin - b.xOfYmin);

      // Fill spans
      for (let i = 0; i < activeEdgeTable.length; i += 2) {
        if (i + 1 < activeEdgeTable.length) {
          const x1 = Math.max(0, Math.floor(activeEdgeTable[i].xOfYmin));
          const x2 = Math.min(buffer.width - 1, Math.floor(activeEdgeTable[i + 1].xOfYmin));

          for (let x = x1; x <= x2; x++) {
            const color = GradientUtils.sampleFill(fill, x + 0.5, y + 0.5);
            buffer.setPixel(x, y, color);
          }
        }
      }

      // Update x coordinates for next scanline
      for (const edge of activeEdgeTable) {
        edge.xOfYmin += edge.slopeInverse;
      }
    }
  }

  // Antialiased polygon using supersampling
  static fillPolygonAntialiased(buffer: PixelBuffer, vertices: Point[], fill: Fill, sampleRate: number): void {
    if (vertices.length < 3) return;

    const samplesPerAxis = Math.sqrt(sampleRate);
    const sampleOffset = 1.0 / samplesPerAxis;

    // Find bounding box
    let minX = vertices[0].x, maxX = vertices[0].x;
    let minY = vertices[0].y, maxY = vertices[0].y;

    for (let i = 1; i < vertices.length; i++) {
      minX = Math.min(minX, vertices[i].x);
      maxX = Math.max(maxX, vertices[i].x);
      minY = Math.min(minY, vertices[i].y);
      maxY = Math.max(maxY, vertices[i].y);
    }

    minX = Math.max(0, Math.floor(minX) - 1);
    maxX = Math.min(buffer.width - 1, Math.ceil(maxX) + 1);
    minY = Math.max(0, Math.floor(minY) - 1);
    maxY = Math.min(buffer.height - 1, Math.ceil(maxY) + 1);

    // Point-in-polygon test using ray casting
    const isPointInPolygon = (px: number, py: number): boolean => {
      let inside = false;
      for (let i = 0, j = vertices.length - 1; i < vertices.length; j = i++) {
        const xi = vertices[i].x, yi = vertices[i].y;
        const xj = vertices[j].x, yj = vertices[j].y;

        if (((yi > py) !== (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi)) {
          inside = !inside;
        }
      }
      return inside;
    };

    for (let py = minY; py <= maxY; py++) {
      for (let px = minX; px <= maxX; px++) {
        let coverage = 0;
        let totalColor = Color.argb(0, 0, 0, 0);

        for (let sy = 0; sy < samplesPerAxis; sy++) {
          for (let sx = 0; sx < samplesPerAxis; sx++) {
            const sampleX = px + (sx + 0.5) * sampleOffset;
            const sampleY = py + (sy + 0.5) * sampleOffset;

            if (isPointInPolygon(sampleX, sampleY)) {
              const sampleColor = GradientUtils.sampleFill(fill, sampleX, sampleY);
              totalColor = GradientUtils.interpolateColor(totalColor, sampleColor, 1.0 / (coverage + 1));
              coverage++;
            }
          }
        }

        if (coverage > 0) {
          const alpha = coverage / sampleRate;
          const blendColor = totalColor.opacity(totalColor.opacity() * alpha);
          buffer.blendPixel(px, py, blendColor);
        }
      }
    }
  }
}

// ============================================================================
// BEZIER CURVE RASTERIZATION
// ============================================================================

class BezierRenderer {
  // De Casteljau's algorithm for curve evaluation
  static evaluateCubicBezier(t: number, p0: Point, p1: Point, p2: Point, p3: Point): Point {
    const u = 1 - t;
    const tt = t * t;
    const uu = u * u;
    const uuu = uu * u;
    const ttt = tt * t;

    return {
      x: uuu * p0.x + 3 * uu * t * p1.x + 3 * u * tt * p2.x + ttt * p3.x,
      y: uuu * p0.y + 3 * uu * t * p1.y + 3 * u * tt * p2.y + ttt * p3.y
    };
  }

  // Check if curve is flat enough (flatness test)
  static isFlatEnough(curve: BezierCurve, tolerance: number): boolean {
    // Calculate maximum distance from control points to chord
    const chordLength = Math.sqrt(
      (curve.p3.x - curve.p0.x) ** 2 + (curve.p3.y - curve.p0.y) ** 2
    );

    if (chordLength < tolerance) return true;

    // Distance from p1 to chord
    const dist1 = Math.abs(
      (curve.p3.y - curve.p0.y) * curve.p1.x -
      (curve.p3.x - curve.p0.x) * curve.p1.y +
      curve.p3.x * curve.p0.y - curve.p3.y * curve.p0.x
    ) / chordLength;

    // Distance from p2 to chord
    const dist2 = Math.abs(
      (curve.p3.y - curve.p0.y) * curve.p2.x -
      (curve.p3.x - curve.p0.x) * curve.p2.y +
      curve.p3.x * curve.p0.y - curve.p3.y * curve.p0.x
    ) / chordLength;

    return Math.max(dist1, dist2) < tolerance;
  }

  // Subdivide cubic Bezier curve at parameter t
  static subdivideCubicBezier(curve: BezierCurve, t: number): [BezierCurve, BezierCurve] {
    const { p0, p1, p2, p3 } = curve;

    // First level of subdivision
    const q0 = { x: p0.x + t * (p1.x - p0.x), y: p0.y + t * (p1.y - p0.y) };
    const q1 = { x: p1.x + t * (p2.x - p1.x), y: p1.y + t * (p2.y - p1.y) };
    const q2 = { x: p2.x + t * (p3.x - p2.x), y: p2.y + t * (p3.y - p2.y) };

    // Second level
    const r0 = { x: q0.x + t * (q1.x - q0.x), y: q0.y + t * (q1.y - q0.y) };
    const r1 = { x: q1.x + t * (q2.x - q1.x), y: q1.y + t * (q2.y - q1.y) };

    // Final point
    const s = { x: r0.x + t * (r1.x - r0.x), y: r0.y + t * (r1.y - r0.y) };

    const left: BezierCurve = { p0, p1: q0, p2: r0, p3: s };
    const right: BezierCurve = { p0: s, p1: r1, p2: q2, p3 };

    return [left, right];
  }

  // Adaptive subdivision algorithm
  static tessellateAdaptive(curve: BezierCurve, tolerance: number): Point[] {
    if (this.isFlatEnough(curve, tolerance)) {
      return [curve.p0, curve.p3];
    }

    const [left, right] = this.subdivideCubicBezier(curve, 0.5);
    const leftPoints = this.tessellateAdaptive(left, tolerance);
    const rightPoints = this.tessellateAdaptive(right, tolerance);

    // Remove duplicate point at subdivision
    return [...leftPoints.slice(0, -1), ...rightPoints];
  }

  // Draw cubic Bezier curve
  static drawCubicBezier(buffer: PixelBuffer, curve: BezierCurve, color: Color, antialiasing: AntiAliasingLevel): void {
    // Calculate tolerance based on antialiasing level
    const tolerance = antialiasing === 'off' ? 1.0 :
      antialiasing === 's' ? 0.5 :
        antialiasing === 'm' ? 0.25 : 0.125;

    const points = this.tessellateAdaptive(curve, tolerance);

    // Draw lines between consecutive points
    for (let i = 0; i < points.length - 1; i++) {
      const p1 = points[i];
      const p2 = points[i + 1];

      if (antialiasing === 'off') {
        LineRenderer.drawLineBresenham(buffer, p1.x, p1.y, p2.x, p2.y, color);
      } else if (antialiasing === 's') {
        LineRenderer.drawLineWu(buffer, p1.x, p1.y, p2.x, p2.y, color);
      } else {
        const sampleRate = antialiasing === 'm' ? 4 : 16;
        LineRenderer.drawLineSupersampled(buffer, p1.x, p1.y, p2.x, p2.y, color, sampleRate);
      }
    }
  }

  // Draw quadratic Bezier curve (special case)
  static drawQuadraticBezier(buffer: PixelBuffer, p0: Point, p1: Point, p2: Point, color: Color, antialiasing: AntiAliasingLevel): void {
    // Convert quadratic to cubic Bezier
    const cubic: BezierCurve = {
      p0: p0,
      p1: { x: p0.x + 2 / 3 * (p1.x - p0.x), y: p0.y + 2 / 3 * (p1.y - p0.y) },
      p2: { x: p2.x + 2 / 3 * (p1.x - p2.x), y: p2.y + 2 / 3 * (p1.y - p2.y) },
      p3: p2
    };

    this.drawCubicBezier(buffer, cubic, color, antialiasing);
  }
}

// ============================================================================
// CIRCLE FILL RASTERIZATION
// ============================================================================

class CircleFillRenderer {
  // Fill circle using scanline algorithm
  static fillCircle(buffer: PixelBuffer, centerX: number, centerY: number, radius: number, fill: Fill): void {
    const minX = Math.max(0, Math.floor(centerX - radius));
    const maxX = Math.min(buffer.width - 1, Math.ceil(centerX + radius));
    const minY = Math.max(0, Math.floor(centerY - radius));
    const maxY = Math.min(buffer.height - 1, Math.ceil(centerY + radius));

    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        const dx = x + 0.5 - centerX;
        const dy = y + 0.5 - centerY;
        const distance = Math.sqrt(dx * dx + dy * dy);

        if (distance <= radius) {
          const color = GradientUtils.sampleFill(fill, x + 0.5, y + 0.5);
          buffer.setPixel(x, y, color);
        }
      }
    }
  }

  // Fill circle with antialiasing
  static fillCircleAntialiased(buffer: PixelBuffer, centerX: number, centerY: number, radius: number, fill: Fill): void {
    const minX = Math.max(0, Math.floor(centerX - radius - 1));
    const maxX = Math.min(buffer.width - 1, Math.ceil(centerX + radius + 1));
    const minY = Math.max(0, Math.floor(centerY - radius - 1));
    const maxY = Math.min(buffer.height - 1, Math.ceil(centerY + radius + 1));

    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        const dx = x + 0.5 - centerX;
        const dy = y + 0.5 - centerY;
        const distance = Math.sqrt(dx * dx + dy * dy) - radius;

        let alpha = 0;
        if (distance <= -0.5) {
          alpha = 1;
        } else if (distance <= 0.5) {
          alpha = 0.5 - distance;
        }

        if (alpha > 0) {
          const color = GradientUtils.sampleFill(fill, x + 0.5, y + 0.5);
          const blendColor = color.opacity(color.opacity() * alpha);
          buffer.blendPixel(x, y, blendColor);
        }
      }
    }
  }
}

// ============================================================================
// SHAPE BUILDERS (FLUENT API)
// ============================================================================

abstract class ShapeBuilder {
  protected antiAliasingOverride: AntiAliasingLevel | null = null;

  constructor(protected graphics: RasterGraphics) { }

  // Antialiasing override methods
  setAntiAliasing(level: AntiAliasingLevel): this {
    this.antiAliasingOverride = level;
    return this;
  }

  antiAliasingOff(): this {
    return this.setAntiAliasing('off');
  }

  antiAliasingS(): this {
    return this.setAntiAliasing('s');
  }

  antiAliasingM(): this {
    return this.setAntiAliasing('m');
  }

  antiAliasingL(): this {
    return this.setAntiAliasing('l');
  }

  // Get effective antialiasing level (override or graphics context default)
  protected getAntiAliasing(): AntiAliasingLevel {
    return this.antiAliasingOverride ?? this.graphics['antialiasing'];
  }

  abstract stroke(color: Color): this;
}

abstract class FillableShapeBuilder extends ShapeBuilder {
  abstract fill(fill: Fill): this;
}

class CircleBuilder extends FillableShapeBuilder {
  constructor(
    graphics: RasterGraphics,
    private centerX: number,
    private centerY: number,
    private radius: number
  ) {
    super(graphics);
  }

  stroke(color: Color): this {
    const antialiasing = this.getAntiAliasing();

    if (antialiasing === 'off') {
      CircleRenderer.drawCircleBresenham(this.graphics.buffer, this.centerX, this.centerY, this.radius, color);
    } else {
      CircleRenderer.drawCircleSDF(this.graphics.buffer, this.centerX, this.centerY, this.radius, color);
    }
    return this;
  }

  fill(fill: Fill): this {
    const antialiasing = this.getAntiAliasing();

    if (antialiasing === 'off') {
      CircleFillRenderer.fillCircle(this.graphics.buffer, this.centerX, this.centerY, this.radius, fill);
    } else {
      CircleFillRenderer.fillCircleAntialiased(this.graphics.buffer, this.centerX, this.centerY, this.radius, fill);
    }
    return this;
  }
}

class RectangleBuilder extends FillableShapeBuilder {
  constructor(
    graphics: RasterGraphics,
    private x: number,
    private y: number,
    private width: number,
    private height: number
  ) {
    super(graphics);
  }

  stroke(color: Color): this {
    const antialiasing = this.getAntiAliasing();

    if (antialiasing === 'off') {
      // Top line
      LineRenderer.drawLineBresenham(this.graphics.buffer, this.x, this.y, this.x + this.width, this.y, color);
      // Right line
      LineRenderer.drawLineBresenham(this.graphics.buffer, this.x + this.width, this.y, this.x + this.width, this.y + this.height, color);
      // Bottom line
      LineRenderer.drawLineBresenham(this.graphics.buffer, this.x + this.width, this.y + this.height, this.x, this.y + this.height, color);
      // Left line
      LineRenderer.drawLineBresenham(this.graphics.buffer, this.x, this.y + this.height, this.x, this.y, color);
    } else if (antialiasing === 's') {
      LineRenderer.drawLineWu(this.graphics.buffer, this.x, this.y, this.x + this.width, this.y, color);
      LineRenderer.drawLineWu(this.graphics.buffer, this.x + this.width, this.y, this.x + this.width, this.y + this.height, color);
      LineRenderer.drawLineWu(this.graphics.buffer, this.x + this.width, this.y + this.height, this.x, this.y + this.height, color);
      LineRenderer.drawLineWu(this.graphics.buffer, this.x, this.y + this.height, this.x, this.y, color);
    } else {
      const sampleRate = antialiasing === 'm' ? 4 : 16;
      LineRenderer.drawLineSupersampled(this.graphics.buffer, this.x, this.y, this.x + this.width, this.y, color, sampleRate);
      LineRenderer.drawLineSupersampled(this.graphics.buffer, this.x + this.width, this.y, this.x + this.width, this.y + this.height, color, sampleRate);
      LineRenderer.drawLineSupersampled(this.graphics.buffer, this.x + this.width, this.y + this.height, this.x, this.y + this.height, color, sampleRate);
      LineRenderer.drawLineSupersampled(this.graphics.buffer, this.x, this.y + this.height, this.x, this.y, color, sampleRate);
    }
    return this;
  }

  fill(fill: Fill): this {
    const antialiasing = this.getAntiAliasing();

    if (antialiasing === 'off') {
      RectangleRenderer.fillAxisAlignedRect(this.graphics.buffer, this.x, this.y, this.width, this.height, fill);
    } else {
      const sampleRate = antialiasing === 's' ? 4 :
        antialiasing === 'm' ? 9 : 16;
      RectangleRenderer.fillRectangleAntialiased(this.graphics.buffer, this.x, this.y, this.width, this.height, fill, sampleRate);
    }
    return this;
  }
}

class PolygonBuilder extends FillableShapeBuilder {
  constructor(
    graphics: RasterGraphics,
    private vertices: Point[]
  ) {
    super(graphics);
  }

  stroke(color: Color): this {
    const antialiasing = this.getAntiAliasing();

    // Draw polygon outline by connecting vertices
    for (let i = 0; i < this.vertices.length; i++) {
      const p1 = this.vertices[i];
      const p2 = this.vertices[(i + 1) % this.vertices.length];

      if (antialiasing === 'off') {
        LineRenderer.drawLineBresenham(this.graphics.buffer, p1.x, p1.y, p2.x, p2.y, color);
      } else if (antialiasing === 's') {
        LineRenderer.drawLineWu(this.graphics.buffer, p1.x, p1.y, p2.x, p2.y, color);
      } else {
        const sampleRate = antialiasing === 'm' ? 4 : 16;
        LineRenderer.drawLineSupersampled(this.graphics.buffer, p1.x, p1.y, p2.x, p2.y, color, sampleRate);
      }
    }
    return this;
  }

  fill(fill: Fill): this {
    const antialiasing = this.getAntiAliasing();

    if (antialiasing === 'off') {
      PolygonRenderer.fillPolygon(this.graphics.buffer, this.vertices, fill);
    } else {
      const sampleRate = antialiasing === 's' ? 4 :
        antialiasing === 'm' ? 9 : 16;
      PolygonRenderer.fillPolygonAntialiased(this.graphics.buffer, this.vertices, fill, sampleRate);
    }
    return this;
  }
}

class LineBuilder extends ShapeBuilder {
  constructor(
    graphics: RasterGraphics,
    private x0: number,
    private y0: number,
    private x1: number,
    private y1: number
  ) {
    super(graphics);
  }

  stroke(color: Color): this {
    const antialiasing = this.getAntiAliasing();

    if (antialiasing === 'off') {
      LineRenderer.drawLineBresenham(this.graphics.buffer, this.x0, this.y0, this.x1, this.y1, color);
    } else if (antialiasing === 's') {
      LineRenderer.drawLineWu(this.graphics.buffer, this.x0, this.y0, this.x1, this.y1, color);
    } else {
      const sampleRate = antialiasing === 'm' ? 4 : 16;
      LineRenderer.drawLineSupersampled(this.graphics.buffer, this.x0, this.y0, this.x1, this.y1, color, sampleRate);
    }
    return this;
  }
}

class ArcBuilder extends ShapeBuilder {
  constructor(
    graphics: RasterGraphics,
    private centerX: number,
    private centerY: number,
    private radius: number,
    private startAngle: number,
    private endAngle: number
  ) {
    super(graphics);
  }

  stroke(color: Color): this {
    const antialiasing = this.getAntiAliasing();
    const arc: Arc = {
      centerX: this.centerX,
      centerY: this.centerY,
      radius: this.radius,
      startAngle: this.startAngle,
      endAngle: this.endAngle
    };
    CircleRenderer.drawArc(this.graphics.buffer, arc, color, antialiasing);
    return this;
  }
}

class CubicBezierBuilder extends ShapeBuilder {
  constructor(
    graphics: RasterGraphics,
    private p0: Point,
    private p1: Point,
    private p2: Point,
    private p3: Point
  ) {
    super(graphics);
  }

  stroke(color: Color): this {
    const antialiasing = this.getAntiAliasing();
    const curve: BezierCurve = { p0: this.p0, p1: this.p1, p2: this.p2, p3: this.p3 };
    BezierRenderer.drawCubicBezier(this.graphics.buffer, curve, color, antialiasing);
    return this;
  }
}

class QuadraticBezierBuilder extends ShapeBuilder {
  constructor(
    graphics: RasterGraphics,
    private p0: Point,
    private p1: Point,
    private p2: Point
  ) {
    super(graphics);
  }

  stroke(color: Color): this {
    const antialiasing = this.getAntiAliasing();
    BezierRenderer.drawQuadraticBezier(this.graphics.buffer, this.p0, this.p1, this.p2, color, antialiasing);
    return this;
  }
}

// ============================================================================
// MAIN GRAPHICS CONTEXT (FLUENT API)
// ============================================================================

class RasterGraphics {
  public buffer: PixelBuffer;
  private antialiasing: AntiAliasingLevel = 'off';

  constructor(width: number, height: number) {
    this.buffer = new PixelBuffer(width, height);
  }

  // Configuration methods
  setAntiAliasing(level: AntiAliasingLevel): this {
    this.antialiasing = level;
    return this;
  }

  clear(color: Color = Color.argb(0, 0, 0, 0)): this {
    this.buffer.clear(color);
    return this;
  }

  // Fluent API shape methods
  circle(centerX: number, centerY: number, radius: number): CircleBuilder {
    return new CircleBuilder(this, centerX, centerY, radius);
  }

  rectangle(x: number, y: number, width: number, height: number): RectangleBuilder {
    return new RectangleBuilder(this, x, y, width, height);
  }

  polygon(vertices: Point[]): PolygonBuilder {
    return new PolygonBuilder(this, vertices);
  }

  line(x0: number, y0: number, x1: number, y1: number): LineBuilder {
    return new LineBuilder(this, x0, y0, x1, y1);
  }

  arc(centerX: number, centerY: number, radius: number, startAngle: number, endAngle: number): ArcBuilder {
    return new ArcBuilder(this, centerX, centerY, radius, startAngle, endAngle);
  }

  cubicBezier(p0: Point, p1: Point, p2: Point, p3: Point): CubicBezierBuilder {
    return new CubicBezierBuilder(this, p0, p1, p2, p3);
  }

  quadraticBezier(p0: Point, p1: Point, p2: Point): QuadraticBezierBuilder {
    return new QuadraticBezierBuilder(this, p0, p1, p2);
  }

  // Convenience method for shapes that only make sense as strokes
  path(): PathBuilder {
    return new PathBuilder(this);
  }

  // Output methods
  getRGBAArray(): Uint8ClampedArray {
    return this.buffer.getRGBAArray();
  }

  getWidth(): number {
    return this.buffer.width;
  }

  getHeight(): number {
    return this.buffer.height;
  }
}

// ============================================================================
// PATH BUILDER (FOR COMPLEX PATHS)
// ============================================================================

class PathBuilder extends ShapeBuilder {
  private commands: Array<() => void> = [];

  constructor(graphics: RasterGraphics) {
    super(graphics);
  }

  moveTo(x: number, y: number): this {
    // Path building would be implemented here for complex paths
    return this;
  }

  lineTo(x: number, y: number): this {
    // Path building would be implemented here
    return this;
  }

  curveTo(cp1x: number, cp1y: number, cp2x: number, cp2y: number, x: number, y: number): this {
    // Path building would be implemented here
    return this;
  }

  stroke(color: Color): this {
    // Execute all path commands as stroke
    this.commands.forEach(cmd => cmd());
    return this;
  }

  fill(fill: Fill): this {
    // Execute all path commands as fill
    this.commands.forEach(cmd => cmd());
    return this;
  }
}

// Export the main classes
export {
  ArcBuilder, BezierRenderer,
  // Builder classes
  CircleBuilder, CircleFillRenderer, CircleRenderer, CubicBezierBuilder, Gradients,
  GradientUtils, LineBuilder, LineRenderer, PathBuilder, PixelBuffer, PolygonBuilder, PolygonRenderer, QuadraticBezierBuilder, RasterGraphics, RectangleBuilder, RectangleRenderer
};

export type { AntiAliasingLevel, Arc, BezierCurve, ColorStop, Fill, LinearGradient, Point, RadialGradient };

