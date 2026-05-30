import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Recuperar acesso",
  robots: { index: false, follow: true },
};

export default function ForgotPasswordLayout({ children }: { children: React.ReactNode }) {
  return children;
}
