import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { useQueryClient } from "@tanstack/react-query";
import { QueryProvider } from "./QueryProvider";

function Probe() {
  const client = useQueryClient();
  return <div data-testid="probe">{client ? "tem-client" : "sem-client"}</div>;
}

describe("QueryProvider", () => {
  it("monta children e expõe QueryClient no contexto", () => {
    render(
      <QueryProvider>
        <Probe />
      </QueryProvider>,
    );

    expect(screen.getByTestId("probe")).toHaveTextContent("tem-client");
  });
});
