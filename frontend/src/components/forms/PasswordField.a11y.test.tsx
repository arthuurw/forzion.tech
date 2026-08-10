import { render, screen, fireEvent } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import { useForm, FormProvider } from "react-hook-form";
import type { ReactNode } from "react";
import PasswordField from "./PasswordField";

type Values = { senha: string };

function Harness({
  children,
  errors,
}: {
  children: ReactNode;
  errors?: Record<string, { type: string; message: string }>;
}) {
  const methods = useForm<Values>({ defaultValues: { senha: "minhasenha123" }, errors });
  return <FormProvider {...methods}>{children}</FormProvider>;
}

describe("PasswordField a11y", () => {
  it("estado default (senha oculta) sem violações", async () => {
    const { container } = render(
      <Harness>
        <PasswordField name="senha" label="Senha" />
      </Harness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("botão de alternar visibilidade tem aria-label dinâmico e nenhuma violação após alternar", async () => {
    const { container } = render(
      <Harness>
        <PasswordField name="senha" label="Senha" />
      </Harness>,
    );

    const toggle = screen.getByRole("button", { name: "Mostrar senha" });
    fireEvent.click(toggle);
    expect(screen.getByRole("button", { name: "Ocultar senha" })).toBeInTheDocument();
    expect(screen.getByLabelText("Senha")).toHaveAttribute("type", "text");

    expect(await axe(container)).toHaveNoViolations();
  });

  it("estado de erro: campo aria-invalid e mensagem associada por aria-describedby, sem violações", async () => {
    const { container } = render(
      <Harness errors={{ senha: { type: "manual", message: "Senha muito curta." } }}>
        <PasswordField name="senha" label="Senha" />
      </Harness>,
    );

    const input = screen.getByLabelText("Senha");
    expect(input).toHaveAttribute("aria-invalid", "true");

    const describedBy = input.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    // eslint-disable-next-line testing-library/no-node-access
    const helperEl = document.getElementById(describedBy!);
    expect(helperEl).toHaveTextContent("Senha muito curta.");

    expect(await axe(container)).toHaveNoViolations();
  });
});
