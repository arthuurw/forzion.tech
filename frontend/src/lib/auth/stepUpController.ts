type StepUpHandler = () => Promise<string | null>;

let current: StepUpHandler | null = null;
let inFlight: Promise<string | null> | null = null;

export function registerStepUpHandler(handler: StepUpHandler): () => void {
  current = handler;
  return () => {
    if (current === handler) current = null;
  };
}

export function requestStepUp(): Promise<string | null> {
  if (!current) return Promise.resolve(null);
  if (!inFlight) {
    inFlight = current().finally(() => {
      inFlight = null;
    });
  }
  return inFlight;
}
