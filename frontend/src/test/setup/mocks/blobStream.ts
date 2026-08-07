/**
 * Polyfill Blob.prototype.stream() para jsdom.
 * O interceptor XHR do MSW embrulha o `xhr.response` (Blob, quando
 * responseType="blob") num Response da Fetch API; o undici do Node 22 chama
 * stream() nesse Blob e, sem o metodo, a promise do axios nunca resolve.
 */
export function installBlobStreamPolyfill(): void {
  if (typeof Blob === "undefined") return;
  if (typeof Blob.prototype.stream === "function") return;

  Blob.prototype.stream = function stream(this: Blob): ReadableStream<Uint8Array<ArrayBuffer>> {
    const bytes = this.arrayBuffer();

    return new ReadableStream<Uint8Array<ArrayBuffer>>({
      async start(controller) {
        controller.enqueue(new Uint8Array(await bytes));
        controller.close();
      },
    });
  };
}
