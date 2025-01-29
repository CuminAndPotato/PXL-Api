import * as net from 'net';
import { Canvas, CanvasMetadata } from './canvas.js';
import { Color } from './color.js';

// --------------------------------------------------------------------------
// All types and signatures have to be mirrored
// in the dotnet / node implementations.
//
// see:
//   src/Pxl.BufferedHttpCanvas/BufferedHttpCanvas.fs
//   src/Pxl.Ui/Canvas.fs
//   src/pxl-local-display/src/domain.ts
//   src/pxl-local-display/src/server.ts
// --------------------------------------------------------------------------

type Ports = {
  tcp: number,
  http: number
};

const invariantServicePorts: Ports = {
  http: 5001,
  tcp: 5002
};

async function getClientMetadata(host: string, ports: Ports): Promise<CanvasMetadata> {
  const url = `http://${host}:${ports.http}/metadata`;
  const res = await fetch(url);
  if (!res.ok)
    throw new Error(`HTTP request failed with status code: ${res.status}`);
  const obj = await res.json();
  return {
    width: obj.width,
    height: obj.height,
    fps: obj.fps
  };
}

export async function create(
  host: string,
  sendBufferSize: number,
  ports = invariantServicePorts,
): Promise<Canvas> {
  const clientMetadata = await getClientMetadata(host, ports);
  const client = net.connect(invariantServicePorts.tcp, host);
  const sendFrame = (pixels: readonly Color[]) => {
    const bytes = Buffer.alloc(pixels.length * 3);
    for (let i = 0; i < pixels.length; i++) {
      const c = pixels[i];
      bytes.writeUInt8(c.r, i * 3);
      bytes.writeUInt8(c.g, i * 3 + 1);
      bytes.writeUInt8(c.b, i * 3 + 2);
    }
    client.write(bytes);
  };

  const metadata = {
    width: clientMetadata.width,
    height: clientMetadata.height,
    fps: clientMetadata.fps
  };

  const canvas = new Canvas(metadata, sendBufferSize);

  const pollCanvas = () => {
    for (const frame of canvas.popAllFrames()) {
      sendFrame(frame);
    }
  };

  const intervalId = setInterval(pollCanvas, 1000 / metadata.fps * 3);

  const dispose = () => {
    clearInterval(intervalId);
    client.end();
    client.destroy();
  };

  return canvas;
}
