"use client";
import RouteGroupError from "@/components/ui/RouteGroupError";

export default function AlunoError(props: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <RouteGroupError
      {...props}
      homeHref="/aluno"
      homeLabel="Meu painel"
      bodyText="Um erro inesperado ocorreu. Se o problema persistir, volte ao seu painel."
    />
  );
}
