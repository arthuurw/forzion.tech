"use client";
import dynamic from "next/dynamic";

// Dynamic import to avoid SSR hydration mismatch (reads document.cookie)
const ConsentBanner = dynamic(() => import("./ConsentBanner"), { ssr: false });

export default function ConsentProvider() {
  return <ConsentBanner />;
}
