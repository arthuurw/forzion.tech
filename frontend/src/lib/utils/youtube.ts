const ID_PURO = /^[A-Za-z0-9_-]{11}$/;
const DE_URL = /(?:youtu\.be\/|\/shorts\/|\/embed\/|[?&]v=)([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])/i;

export function parseYouTubeId(entrada: string | null | undefined): string | null {
  if (!entrada) return null;
  const valor = entrada.trim();
  if (ID_PURO.test(valor)) return valor;
  const match = DE_URL.exec(valor);
  return match ? match[1] : null;
}

export function youtubeThumb(videoIdOuUrl: string | null | undefined): string | null {
  const id = parseYouTubeId(videoIdOuUrl);
  return id ? `https://i.ytimg.com/vi/${id}/hqdefault.jpg` : null;
}

export function youtubeEmbedUrl(videoIdOuUrl: string | null | undefined): string | null {
  const id = parseYouTubeId(videoIdOuUrl);
  return id ? `https://www.youtube-nocookie.com/embed/${id}?rel=0` : null;
}
