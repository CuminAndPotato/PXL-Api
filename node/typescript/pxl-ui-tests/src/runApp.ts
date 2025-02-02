import { default as env } from 'env-var';
import minimist from 'minimist';
import { create as createProxy } from 'pxl-ui/src/canvasProxy';
import { start as startEvaluation } from 'pxl-ui/src/evaluation';

const argv = minimist(process.argv.slice(2));

// provide the name of the app as a command line argument,
// defaults to "03_sefa"
const app = argv['app'] || '03_sefa';
const { scene } = await import(`./${app}`);

// provide the IP address of the device as an environment variable,
// defaults to "localhost" for running on the simulator
const host = env.get('PXL_HOST').default('localhost').asString();

const sendBufferSize = env.get('PXL_SEND_BUFFER_SIZE').default(20).asIntPositive();

const { canvas } = await createProxy(host, sendBufferSize);

await startEvaluation(canvas, scene);
