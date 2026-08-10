import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import { useForm, FormProvider } from "react-hook-form";
import type { ReactNode } from "react";
import FormSwitch from "./FormSwitch";

type Values = { notificacoes: boolean };

function Harness({
  children,
  defaultChecked,
}: {
  children: ReactNode;
  defaultChecked: boolean;
}) {
  const methods = useForm<Values>({ defaultValues: { notificacoes: defaultChecked } });
  return <FormProvider {...methods}>{children}</FormProvider>;
}

describe("FormSwitch a11y", () => {
  it("estado desligado sem violações", async () => {
    const { container } = render(
      <Harness defaultChecked={false}>
        <FormSwitch name="notificacoes" label="Receber notificações por e-mail" />
      </Harness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("estado ligado sem violações", async () => {
    const { container } = render(
      <Harness defaultChecked>
        <FormSwitch name="notificacoes" label="Receber notificações por e-mail" />
      </Harness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("FormControlLabel associa o rótulo ao Switch por nome acessível", () => {
    render(
      <Harness defaultChecked={false}>
        <FormSwitch name="notificacoes" label="Receber notificações por e-mail" />
      </Harness>,
    );
    expect(
      screen.getByRole("switch", { name: "Receber notificações por e-mail" }),
    ).toBeInTheDocument();
  });
});
