import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect, vi } from "vitest";
import RouteGroupError from "./RouteGroupError";

vi.mock("@sentry/nextjs", () => ({ captureException: vi.fn() }));

describe("RouteGroupError a11y", () => {
  it("sem violações", async () => {
    const { container } = render(
      <RouteGroupError
        error={new Error("Falha ao carregar a página")}
        reset={() => undefined}
        homeHref="/aluno"
        homeLabel="Voltar ao início"
        bodyText="Tente novamente ou volte para o início."
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
