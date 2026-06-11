import type { CSSProperties } from "react";

/**
 * srOnly — esconde o conteúdo visualmente mas o mantém acessível a leitores de tela
 * (padrão "sr-only"/"visually hidden"). Usado p/ descrever gráficos a tecnologias assistivas.
 */
export const srOnly: CSSProperties = {
  position: "absolute",
  width: 1,
  height: 1,
  padding: 0,
  margin: -1,
  overflow: "hidden",
  clip: "rect(0,0,0,0)",
  whiteSpace: "nowrap",
  borderWidth: 0,
};
