/**
 * Forca prefers-reduced-motion: reduce no matchMedia global.
 * Elimina flake de animacoes MUI/CSS em testes.
 *
 * Pre-requisito: mock global de matchMedia ja instalado (mocks/matchMedia.ts).
 */
export function forceReducedMotion(): void {
  const originalMatchMedia = window.matchMedia;
  window.matchMedia = ((query: string) => {
    const result = originalMatchMedia(query);
    if (query.includes("prefers-reduced-motion")) {
      return {
        ...result,
        matches: query.includes("reduce"),
        media: query,
      };
    }
    return result;
  }) as typeof window.matchMedia;
}
