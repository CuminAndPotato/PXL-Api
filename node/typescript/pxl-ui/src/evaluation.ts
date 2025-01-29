import { Canvas } from "./canvas.js";
import { Color } from "./color.js";
import { RenderCtx } from "./renderCtx.js";
import { ResumableStateHack, SceneFunc } from "./vide.js";

// Reality class and module
export class Reality {
  constructor(
    public readonly onCycleFinished: (nextPlannedEvaluation: Date) => Promise<void>,
    private readonly getNow: () => Date
  ) { }

  get now(): Date {
    return this.getNow();
  }

  static forRealTime(): Reality {
    const getNow = () => new Date();

    // Instead of Thread.Sleep, we return a promise that resolves after the given delay.
    const onCycleFinished = async (nextPlannedEvaluation: Date) => {
      const now = getNow().getTime();
      const target = nextPlannedEvaluation.getTime();
      const timeToWait = target - now;
      if (timeToWait > 0) {
        await new Promise<void>((resolve) => setTimeout(resolve, timeToWait));
      }
    };

    return new Reality(onCycleFinished, getNow);
  }
}

// Evaluation module
let startedTimes = 0;
const hangDetectionTimeSpan = 5000; // 5 seconds

let stop: () => void = () => { }

// The 'start' function has been adapted to use setTimeout and async loops instead of threads.
export function startEx<S>(
  canvas: Canvas,
  onEvalError: (ex: Error) => void,
  reality: Reality,
  // readButtons: () => Buttons,
  scene: SceneFunc<S>
)
  : { stop: () => void } {

  stop();

  startedTimes++;
  let shouldEvaluate = true;
  const isRunning = () => shouldEvaluate && !canvas.isCancellationRequested;

  let lastEvaluationTime: Date | null = null;

  // Prepare frames
  const frameArrays: Color[][] = [];
  for (let i = 0; i < canvas.sendFrameBufferSize; i++) {
    frameArrays.push(new Array<Color>(canvas.metadata.width * canvas.metadata.height));
  }

  const renderCtx = new RenderCtx(canvas.metadata.width, canvas.metadata.height);
  const durationForOneFrame = 1.0 / canvas.metadata.fps;
  const sceneStartTime = reality.now;

  let lastSceneState: readonly any[] | undefined = undefined;
  let completeCycleCount = 0;

  const calcTimeForCycle = (cycleNr: number) =>
    new Date(sceneStartTime.getTime() + (durationForOneFrame * cycleNr * 1000));

  // The main evaluation loop as an async function that schedules itself
  async function evaluationLoop() {
    if (!isRunning()) return;

    try {
      lastEvaluationTime = reality.now;

      // Compute frame time based on ideal schedule
      const frameNow = calcTimeForCycle(completeCycleCount);

      renderCtx.prepareCycle(frameNow);
      ResumableStateHack.popStack = lastSceneState;
      ResumableStateHack.pushStack = [];
      ResumableStateHack.ctx = renderCtx;

      await scene();

      lastSceneState = ResumableStateHack.pushStack;
      ResumableStateHack.popStack = undefined;
      ResumableStateHack.pushStack = [];
      ResumableStateHack.ctx = undefined;

      const frame = renderCtx.endCycle();

      canvas.pushFrame(frame);
      completeCycleCount++;

      const nextPlannedEvaluation = calcTimeForCycle(completeCycleCount);
      await reality.onCycleFinished(nextPlannedEvaluation);

    } catch (ex: any) {
      console.log(`Error in evaluation: ${ex.message}`);
      onEvalError(ex);
    }

    if (isRunning()) {
      // Schedule next frame evaluation using setTimeout with minimal delay
      // Actual timing is managed by reality.OnCycleFinished
      setTimeout(evaluationLoop, 0);
    }
  }

  // Hang detection loop: checks periodically if the evaluation is hanging
  const hangDetectionInterval = setInterval(() => {
    if (!isRunning()) return;
    if (lastEvaluationTime) {
      const now = reality.now.getTime();
      const lastEval = lastEvaluationTime.getTime();
      if (now - lastEval > hangDetectionTimeSpan) {
        onEvalError(new Error("Evaluation hanging"));
      }
    }
  }, 100);

  // Start the evaluation loop
  setTimeout(evaluationLoop, 0);

  // Return a function that stops the evaluation
  stop = () => {
    shouldEvaluate = false;
    clearInterval(hangDetectionInterval);
  };

  return { stop };
}

export function start(canvas: Canvas, scene: SceneFunc<void>) {
  const reality = Reality.forRealTime();
  return startEx(
    canvas,
    err => console.log("Error in evaluation", err.message),
    reality,
    scene);
}
