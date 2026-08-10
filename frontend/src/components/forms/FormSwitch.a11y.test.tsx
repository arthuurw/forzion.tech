import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import { RhfHarness } from "@/test/rhfHarness";
import FormSwitch from "./FormSwitch";

type Values = { notificacoes: boolean };

describe("FormSwitch a11y", () => {
  it("estado desligado sem violações", async () => {
    const { container } = render(
      <RhfHarness<Values> defaultValues={{ notificacoes: false }}>
        <FormSwitch name="notificacoes" label="Receber notificações por e-mail" />
      </RhfHarness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("estado ligado sem violações", async () => {
    const { container } = render(
      <RhfHarness<Values> defaultValues={{ notificacoes: true }}>
        <FormSwitch name="notificacoes" label="Receber notificações por e-mail" />
      </RhfHarness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("FormControlLabel associa o rótulo ao Switch por nome acessível", () => {
    render(
      <RhfHarness<Values> defaultValues={{ notificacoes: false }}>
        <FormSwitch name="notificacoes" label="Receber notificações por e-mail" />
      </RhfHarness>,
    );
    expect(
      screen.getByRole("switch", { name: "Receber notificações por e-mail" }),
    ).toBeInTheDocument();
  });
});
