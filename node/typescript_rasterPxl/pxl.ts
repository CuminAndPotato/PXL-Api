
/*
a: 0..1
r,g,b: 0..255
*/
type Color = { r: number; g: number; b: number; a: number; }

/*
h: Hue (0-360 degrees)
s: Saturation (0-1)
v: Value/Brightness (0-1)
a: Alpha (preserved from input)
*/
type HsvColor = { h: number; s: number; v: number; a: number }

type LinearGradientBrush = {
    tag: 'LinearGradient';

    // Gradient vector - defines direction and extent
    start?: { x: number; y: number };
    end?: { x: number; y: number };

    // Color stops - positions along the gradient (0.0 = start, 1.0 = end)
    stops: Array<{
        offset: number;  // 0.0 to 1.0
        color: Color;
    }>;

    // Optional properties for advanced control
    spreadMethod?: 'pad' | 'reflect' | 'repeat';  // How to handle areas outside gradient
    colorSpace?: 'sRGB' | 'linearRGB' | 'HSV';    // Color interpolation space

    // Optional coordinate system
    coordinateSystem?: 'objectBoundingBox' | 'userSpaceOnUse';
}

type Brush = Color | LinearGradientBrush;

const color = {
    fromRgba: (r: number, g: number, b: number, a: number = 1): Color => ({ r, g, b, a }),

    // fromRgbaString: (rgba: string): Color => { PERF

    fromHsv: (h: number, s: number, v: number, a: number = 1): Color => {
        // Optimized HSV to RGB conversion - branchless with lookup table approach
        const c = v * s;
        const hSector = (h / 60) % 6;
        const x = c * (1 - Math.abs((hSector % 2) - 1));
        const m = v - c;

        // Use integer sector and array lookup instead of branches
        const sector = Math.floor(hSector);
        const rgbValues = [
            [c, x, 0], [x, c, 0], [0, c, x],
            [0, x, c], [x, 0, c], [c, 0, x]
        ][sector]!; // Non-null assertion - sector is always 0-5

        // Avoid Math.round by using bit operations for better performance
        const r = ((rgbValues[0]! + m) * 255 + 0.5) | 0;
        const g = ((rgbValues[1]! + m) * 255 + 0.5) | 0;
        const b = ((rgbValues[2]! + m) * 255 + 0.5) | 0;

        return { r, g, b, a };
    },

    toHsv: (color: Color): HsvColor => {
        // Optimized RGB to HSV conversion - high performance
        const r = color.r / 255;
        const g = color.g / 255;
        const b = color.b / 255;

        // Fast min/max without Math.min/Math.max
        let max = r;
        let min = r;
        let maxChannel = 0; // 0=R, 1=G, 2=B

        if (g > max) { max = g; maxChannel = 1; }
        if (b > max) { max = b; maxChannel = 2; }
        if (g < min) min = g;
        if (b < min) min = b;

        const delta = max - min;
        const v = max;
        const s = max === 0 ? 0 : delta / max;

        let h = 0;
        if (delta !== 0) {
            // Branchless hue calculation using lookup
            const hueCalcs = [
                (g - b) / delta + (g < b ? 6 : 0), // Red is max
                (b - r) / delta + 2,               // Green is max  
                (r - g) / delta + 4                // Blue is max
            ];
            h = hueCalcs[maxChannel]! * 60;
        }

        return { h, s, v, a: color.a };
    }
}

const colors = {
    transparent: { r: 0, g: 0, b: 0, a: 0 },
    transparentBlack: { r: 0, g: 0, b: 0, a: 0 },
    transparentWhite: { r: 255, g: 255, b: 255, a: 0 },
    black: { r: 0, g: 0, b: 0, a: 1 },
    white: { r: 255, g: 255, b: 255, a: 1 },
    red: { r: 255, g: 0, b: 0, a: 1 },
    green: { r: 0, g: 255, b: 0, a: 1 },
    blue: { r: 0, g: 0, b: 255, a: 1 },
}

// Minimal interface for direct pixel access - for high-performance drawing
interface IPixelBuffer {
    readonly data: Uint8ClampedArray;
    readonly width: number;
    readonly height: number;
}

interface IDrawOperation {
    draw: (buffer: IPixelBuffer) => void;
}

type AntiAliasLevel = 0 | 1 | 2 | 3;

type ImageData = { data: Uint8ClampedArray; width: number; height: number; };

class DrawingContext {
    public readonly data: Uint8ClampedArray;
    public readonly totalBytes: number;
    public readonly width: number;
    public readonly height: number;

    private _currentDrawOperation: IDrawOperation | null = null;

    constructor(width: number, height: number) {
        this.width = width;
        this.height = height;

        // RGBA format: 4 bytes per pixel
        this.totalBytes = width * height * 4;
        this.data = new Uint8ClampedArray(this.totalBytes);
    }

    // Get raw index for manual data manipulation
    getIndex(x: number, y: number): number {
        return (y * this.width + x) * 4;
    }

    beginDraw<T extends IDrawOperation>(op: T): T {
        if (this._currentDrawOperation)
            this._currentDrawOperation.draw(this);
        this._currentDrawOperation = op;
        return op;
    }

    endDraw(): void {
        if (this._currentDrawOperation) {
            this._currentDrawOperation.draw(this);
            this._currentDrawOperation = null;
        }
    }

    // High-performance pixel setting
    setPxl(x: number, y: number, color: Color): void {
        if (x < 0 || x >= this.width || y < 0 || y >= this.height)
            return;

        const index = (y * this.width + x) * 4;
        this.data[index] = color.r;         // Red
        this.data[index + 1] = color.g;     // Green  
        this.data[index + 2] = color.b;     // Blue
        this.data[index + 3] = color.a * 255; // Alpha (convert 0-1 to 0-255)
    }

    // High-performance pixel getting
    getPxl(x: number, y: number): Color {
        if (x < 0 || x >= this.width || y < 0 || y >= this.height)
            return { r: 0, g: 0, b: 0, a: 0 }; // Transparent black for out-of-bounds

        const index = (y * this.width + x) * 4;
        return {
            r: this.data[index]!,
            g: this.data[index + 1]!,
            b: this.data[index + 2]!,
            a: this.data[index + 3]! / 255 // Convert 0-255 to 0-1
        };
    }

    // TODO: later - setPxls() + section

    // TODO - later, we want to get a section of pixels only (optional)
    getPxls(): Color[] {
        const pixels: Color[] = [];
        for (let y = 0; y < this.height; y++) {
            for (let x = 0; x < this.width; x++) {
                pixels.push(this.getPxl(x, y));
            }
        }
        return pixels;
    }

    // Utility methods for common operations
    clear(color: Color = { r: 0, g: 0, b: 0, a: 0 }): void {
        const r = color.r;
        const g = color.g;
        const b = color.b;
        const a = color.a * 255;

        for (let i = 0; i < this.data.length; i += 4) {
            this.data[i] = r;
            this.data[i + 1] = g;
            this.data[i + 2] = b;
            this.data[i + 3] = a;
        }
    }

    drawLine(this: DrawingContext, x1: number, y1: number, x2: number, y2: number): LineDrawOperation {
        const op = new LineDrawOperation(x1, y1, x2, y2);
        return this.beginDraw(op);
    }

    render(scaledWidth?: number, scaledHeight?: number): ImageData {
        this.endDraw();

        // If no scaling requested, return original data
        if (!scaledWidth && !scaledHeight) {
            return {
                data: this.data,
                width: this.width,
                height: this.height
            };
        }

        // Calculate target dimensions
        const targetWidth = scaledWidth || this.width;
        const targetHeight = scaledHeight || this.height;
        
        // If same dimensions, return original data
        if (targetWidth === this.width && targetHeight === this.height) {
            return {
                data: this.data,
                width: this.width,
                height: this.height
            };
        }

        // Perform high-performance nearest neighbor scaling
        const scaledData = new Uint8ClampedArray(targetWidth * targetHeight * 4);
        const scaleX = this.width / targetWidth;
        const scaleY = this.height / targetHeight;

        // Optimized scaling loop
        for (let y = 0; y < targetHeight; y++) {
            const sourceY = Math.floor(y * scaleY);
            if (sourceY >= this.height) continue;
            
            for (let x = 0; x < targetWidth; x++) {
                const sourceX = Math.floor(x * scaleX);
                if (sourceX >= this.width) continue;
                
                const sourceIndex = (sourceY * this.width + sourceX) * 4;
                const targetIndex = (y * targetWidth + x) * 4;
                
                // Direct RGBA copy for performance
                scaledData[targetIndex] = this.data[sourceIndex]!;         // R
                scaledData[targetIndex + 1] = this.data[sourceIndex + 1]!; // G
                scaledData[targetIndex + 2] = this.data[sourceIndex + 2]!; // B
                scaledData[targetIndex + 3] = this.data[sourceIndex + 3]!; // A
            }
        }

        return {
            data: scaledData,
            width: targetWidth,
            height: targetHeight
        };
    }
}


/* -----------------------------------
 * BEGIN Line Drawing Extension
 * -------------------------------- */

class LineDrawOperation implements IDrawOperation {
    private _x1: number;
    private _y1: number;
    private _x2: number;
    private _y2: number;
    private _color: Color;
    private _thickness: number;
    private _antiAliasLevel: AntiAliasLevel;

    constructor(x1: number, y1: number, x2: number, y2: number) {
        // Initialize core properties
        this._x1 = x1;
        this._y1 = y1;
        this._x2 = x2;
        this._y2 = y2;
        this._thickness = 1; // default

        // Initialize mixin properties
        this._color = colors.black;
        this._antiAliasLevel = 0;
    }

    // Builder-style setters for core properties
    start(x: number, y: number): this {
        this._x1 = x;
        this._y1 = y;
        return this;
    }

    end(x: number, y: number): this {
        this._x2 = x;
        this._y2 = y;
        return this;
    }

    x1(x: number): this {
        this._x1 = x;
        return this;
    }

    y1(y: number): this {
        this._y1 = y;
        return this;
    }

    x2(x: number): this {
        this._x2 = x;
        return this;
    }

    y2(y: number): this {
        this._y2 = y;
        return this;
    }

    color(color: Color): this {
        this._color = color;
        return this;
    }

    thickness(thickness: number): this {
        this._thickness = thickness;
        return this;
    }

    antiAlias(level: AntiAliasLevel): this {
        this._antiAliasLevel = level;
        return this;
    }

    // Getters for properties
    getX1(): number { return this._x1; }
    getY1(): number { return this._y1; }
    getX2(): number { return this._x2; }
    getY2(): number { return this._y2; }
    getThickness(): number { return this._thickness; }
    getColor(): Color { return this._color; }
    getAntiAlias(): AntiAliasLevel { return this._antiAliasLevel; }

    draw(buffer: IPixelBuffer): void {
        const color = this.getColor();
        const thickness = this._thickness;
        const antiAlias = this.getAntiAlias();

        if (thickness === 1 && antiAlias === 0) {
            this.drawSimpleLine(buffer, color);
        } else {
            this.drawComplexLine(buffer, color, thickness, antiAlias);
        }
    }

    private drawSimpleLine(buffer: IPixelBuffer, color: Color): void {
        // Bresenham's line algorithm - OPTIMIZED for direct pixel access
        let x0 = Math.floor(this._x1);
        let y0 = Math.floor(this._y1);
        let x1 = Math.floor(this._x2);
        let y1 = Math.floor(this._y2);

        const dx = Math.abs(x1 - x0);
        const dy = Math.abs(y1 - y0);
        const sx = x0 < x1 ? 1 : -1;
        const sy = y0 < y1 ? 1 : -1;
        let err = dx - dy;

        // Pre-calculate color components for performance
        const r = color.r;
        const g = color.g;
        const b = color.b;
        const a = (color.a * 255) | 0; // Convert to 0-255 and ensure integer

        // Direct access to pixel data
        const data = buffer.data;
        const width = buffer.width;
        const height = buffer.height; while (true) {
            // Bounds check - only once per pixel
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height) {
                const index = (y0 * width + x0) * 4;
                data[index] = r;         // Red
                data[index + 1] = g;     // Green
                data[index + 2] = b;     // Blue
                data[index + 3] = a;     // Alpha
            }

            if (x0 === x1 && y0 === y1) break;

            const e2 = 2 * err;
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

    private drawComplexLine(buffer: IPixelBuffer, color: Color, thickness: number, antiAlias: AntiAliasLevel): void {
        // Use Bresenham's algorithm with perpendicular offset for thick lines
        const dx = Math.abs(this._x2 - this._x1);
        const dy = Math.abs(this._y2 - this._y1);
        const sx = this._x1 < this._x2 ? 1 : -1;
        const sy = this._y1 < this._y2 ? 1 : -1;
        let err = dx - dy;

        let x = this._x1;
        let y = this._y1;

        // Pre-calculate constants for performance
        const data = buffer.data;
        const width = buffer.width;
        const height = buffer.height;
        const baseR = color.r;
        const baseG = color.g;
        const baseB = color.b;
        const baseA = (color.a * 255) | 0;

        // Calculate perpendicular offsets for thickness
        const lineLength = Math.sqrt(dx * dx + dy * dy);
        const offsetX = lineLength > 0 ? (this._y2 - this._y1) / lineLength : 0;
        const offsetY = lineLength > 0 ? (this._x1 - this._x2) / lineLength : 0;
        
        const halfThickness = thickness / 2;

        // Bresenham's line algorithm with thickness
        while (true) {
            // Draw perpendicular line at current point
            for (let t = -halfThickness; t <= halfThickness; t += 0.5) {
                const px = Math.round(x + offsetX * t);
                const py = Math.round(y + offsetY * t);

                // Bounds check
                if (px >= 0 && px < width && py >= 0 && py < height) {
                    const index = (py * width + px) * 4;
                    data[index] = baseR;
                    data[index + 1] = baseG;
                    data[index + 2] = baseB;
                    data[index + 3] = baseA;
                }
            }

            // Check if we've reached the end point
            if (x === this._x2 && y === this._y2) break;

            // Bresenham's step
            const e2 = 2 * err;
            if (e2 > -dy) {
                err -= dy;
                x += sx;
            }
            if (e2 < dx) {
                err += dx;
                y += sy;
            }
        }
    }
}

/* -----------------------------------
 * END Line Drawing Extension
 * -------------------------------- */


/* -----------------------------------
 * BEGIN Rasterizing
 * -------------------------------- */



// Export all types
export type {
    Color,
    HsvColor,
    Brush,
    LinearGradientBrush,
    AntiAliasLevel,
    IPixelBuffer,
    IDrawOperation
};

// Export utility objects and functions
export {
    color,
    colors
};

// Export main classes
export {
    DrawingContext,
    LineDrawOperation
};
