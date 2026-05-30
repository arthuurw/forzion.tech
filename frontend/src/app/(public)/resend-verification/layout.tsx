import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Reenviar verificação",
  robots: { index: false, follow: true },
};

export default function ResendVerificationLayout({ children }: { children: React.ReactNode }) {
  return children;
}
