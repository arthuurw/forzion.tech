import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import { RhfHarness } from "@/test/rhfHarness";
import FormTextField from "./FormTextField";

type Values = { nome: string };

describe("FormTextField a11y", () => {
  it("estado default sem violações", async () => {
    const { container } = render(
      <RhfHarness<Values> defaultValues={{ nome: "" }}>
        <FormTextField name="nome" label="Nome" />
      </RhfHarness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("estado de erro: campo aria-invalid e mensagem associada por aria-describedby, sem violações", async () => {
    const { container } = render(
      <RhfHarness<Values>
        defaultValues={{ nome: "" }}
        errors={{ nome: { type: "manual", message: "Nome obrigatório." } }}
      >
        <FormTextField name="nome" label="Nome" />
      </RhfHarness>,
    );

    const input = screen.getByLabelText("Nome");
    expect(input).toHaveAttribute("aria-invalid", "true");

    const describedBy = input.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    // eslint-disable-next-line testing-library/no-node-access
    const helperEl = document.getElementById(describedBy!);
    expect(helperEl).toHaveTextContent("Nome obrigatório.");

    expect(await axe(container)).toHaveNoViolations();
  });

  it("rótulo associado ao campo por nome acessível", () => {
    render(
      <RhfHarness<Values> defaultValues={{ nome: "" }}>
        <FormTextField name="nome" label="Nome" />
      </RhfHarness>,
    );
    expect(screen.getByRole("textbox", { name: "Nome" })).toBeInTheDocument();
  });
});
