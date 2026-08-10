import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import { useForm, FormProvider } from "react-hook-form";
import type { ReactNode } from "react";
import FormSelect from "./FormSelect";

type Values = { plano: string };

const OPTIONS = [
  { value: "mensal", label: "Mensal" },
  { value: "trimestral", label: "Trimestral" },
  { value: "anual", label: "Anual", disabled: true },
];

function Harness({
  children,
  errors,
}: {
  children: ReactNode;
  errors?: Record<string, { type: string; message: string }>;
}) {
  const methods = useForm<Values>({ defaultValues: { plano: "" }, errors });
  return <FormProvider {...methods}>{children}</FormProvider>;
}

describe("FormSelect a11y", () => {
  it("estado default sem violações", async () => {
    const { container } = render(
      <Harness>
        <FormSelect name="plano" label="Plano" options={OPTIONS} />
      </Harness>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("estado de erro: combobox aria-invalid e mensagem associada por aria-describedby, sem violações", async () => {
    const { container } = render(
      <Harness errors={{ plano: { type: "manual", message: "Selecione um plano." } }}>
        <FormSelect name="plano" label="Plano" options={OPTIONS} />
      </Harness>,
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
      <Harness>
        <FormSelect name="plano" label="Plano" options={OPTIONS} />
      </Harness>,
    );
    expect(screen.getByRole("combobox", { name: "Plano" })).toBeInTheDocument();
  });
});
