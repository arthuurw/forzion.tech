import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import { Button } from "@mui/material";
import PageHeader from "./PageHeader";

describe("PageHeader a11y", () => {
  it("só título", async () => {
    const { container } = render(<PageHeader title="Painel" />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("título + subtítulo + ação + voltar", async () => {
    const { container } = render(
      <PageHeader
        title="Meus alunos"
        subtitle="Gerencie seus vínculos"
        action={<Button variant="contained">Novo aluno</Button>}
        backHref="/treinador"
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
