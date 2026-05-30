import { ImageResponse } from "next/og";

export const alt = "forzion.tech — Gestão para Personal Trainers";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";
// nodejs runtime: compatível com `output: "standalone"` (next.config.ts).
export const runtime = "nodejs";

export default function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          backgroundColor: "#1A1A1A",
        }}
      >
        <div
          style={{
            fontSize: 120,
            fontWeight: 800,
            color: "#F5C400",
            letterSpacing: "-0.04em",
          }}
        >
          forzion.tech
        </div>
        <div
          style={{
            marginTop: 24,
            fontSize: 44,
            fontWeight: 500,
            color: "rgba(255,255,255,0.85)",
          }}
        >
          Gestão para Personal Trainers
        </div>
      </div>
    ),
    { ...size },
  );
}
