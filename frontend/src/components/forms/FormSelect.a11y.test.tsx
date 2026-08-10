import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import { RhfHarness } from "@/test/rhfHarness";
import FormSelect from "./FormSelect";

type Values = { plano: string };

const OPTIONS = [
  { value: "mensal", label: "Mensal" },
  { value: "trimestral", label: "Trimestral" },
  { value: "anual", label: "Anual", disabled: true },
];

describe("FormSelect a11y", () => {
  it("estado default sem violações", async () => {
    const { container } = render(
      <RhfHarness<Values> defaultValues={{ plano: "" }}>
        <FormSelect name="plano" label="Plano" options={OPTIONS} />
      </RhfHarness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("estado de erro: combobox aria-invalid e mensagem associada por aria-describedby, sem violações", async () => {
    const { container } = render(
      <RhfHarness<Values>
        defaultValues={{ plano: "" }}
        errors={{ plano: { type: "manual", message: "Selecione um plano." } }}
      >
        <FormSelect name="plano" label="Plano" options={OPTIONS} />
      </RhfHarness>,
    );

    const combobox = screen.getByRole("combobox", { name: "Plano" });
    expect(combobox).toHaveAttribute("aria-invalid", "true");

    const describedBy = combobox.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    // eslint-disable-next-line testing-library/no-node-access
    const helperEl = document.getElementById(describedBy!);
    expect(helperEl).toHaveTextContent("Selecione um plano.");

    expect(await axe(container)).toHaveNoViolations();
  });

  it("InputLabel associado ao Select via labelId (nome acessível)", () => {
    render(
      <RhfHarness<Values> defaultValues={{ plano: "" }}>
        <FormSelect name="plano" label="Plano" options={OPTIONS} />
      </RhfHarness>,
    );
    expect(screen.getByRole("combobox", { name: "Plano" })).toBeInTheDocument();
  });
});
