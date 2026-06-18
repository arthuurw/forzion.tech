"use client";
import RouteGroupError from "@/components/ui/RouteGroupError";

export default function AdminError(props: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <RouteGroupError
      {...props}
      homeHref="/admin"
      homeLabel="Painel"
      bodyText="Um erro inesperado ocorreu. Se o problema persistir, volte ao painel."
    />
  );
}
