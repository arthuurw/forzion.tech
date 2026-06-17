"use client";
import { useEffect, useRef, useState } from "react";
import StepUpDialog from "./StepUpDialog";
import { registerStepUpHandler } from "@/lib/auth/stepUpController";

export default function StepUpProvider() {
  const [open, setOpen] = useState(false);
  const resolverRef = useRef<((token: string | null) => void) | null>(null);

  useEffect(() => {
    return registerStepUpHandler(
      () =>
        new Promise<string | null>((resolve) => {
          resolverRef.current = resolve;
          setOpen(true);
        }),
    );
  }, []);

  const settle = (token: string | null) => {
    setOpen(false);
    resolverRef.current?.(token);
    resolverRef.current = null;
  };

  return (
    <StepUpDialog
      open={open}
      description="Esta ação exige verificação adicional. Confirme sua identidade para continuar."
      onClose={() => settle(null)}
      onVerified={(token) => settle(token)}
    />
  );
}
