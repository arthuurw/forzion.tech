"use client";
import { useEffect, useState } from "react";
import { pagamentoApi } from "@/lib/api/pagamento";
import AlunoInadimplenteBanner from "./AlunoInadimplenteBanner";

/**
 * Wrapper client-side que consulta a assinatura do aluno logado e exibe o
 * banner persistente quando status === "Inadimplente".
 *
 * Comportamento:
 * - Fetch on-mount (sem polling agressivo).
 * - Falha silenciosa: erros nao bloqueiam a UI (banner apenas nao aparece).
 * - Re-render dos children nao reinicia o fetch (efeito depende de [] estavel).
 */
export default function AlunoInadimplenteGate({
  children,
}: {
  children: React.ReactNode;
}) {
  const [inadimplente, setInadimplente] = useState(false);

  useEffect(() => {
    let active = true;
    pagamentoApi
      .obterMinhaAssinatura()
      .then((res) => {
        if (active && res.data?.status === "Inadimplente") {
          setInadimplente(true);
        }
      })
      .catch(() => {
        // Silent fail — banner so aparece com confirmacao positiva do backend.
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <>
      {inadimplente && <AlunoInadimplenteBanner />}
      {children}
    </>
  );
}
