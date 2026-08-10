import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import { useForm, FormProvider } from "react-hook-form";
import type { ReactNode } from "react";
import FormTextField from "./FormTextField";

type Values = { nome: string };

function Harness({
  children,
  errors,
}: {
  children: ReactNode;
  errors?: Record<string, { type: string; message: string }>;
}) {
  const methods = useForm<Values>({ defaultValues: { nome: "" }, errors });
  return <FormProvider {...methods}>{children}</FormProvider>;
}

describe("FormTextField a11y", () => {
  it("estado default sem violações", async () => {
    const { container } = render(
      <Harness>
        <FormTextField name="nome" label="Nome" />
      </Harness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("estado de erro: campo aria-invalid e mensagem associada por aria-describedby, sem violações", async () => {
    const { container } = render(
      <Harness errors={{ nome: { type: "manual", message: "Nome obrigatório." } }}>
        <FormTextField name="nome" label="Nome" />
      </Harness>,
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
      <Harness>
        <FormTextField name="nome" label="Nome" />
      </Harness>,
    );
    expect(screen.getByRole("textbox", { name: "Nome" })).toBeInTheDocument();
  });
});
