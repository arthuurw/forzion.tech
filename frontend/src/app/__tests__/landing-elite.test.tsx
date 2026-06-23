import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import { buildPlano } from "@/test/factories/plano";
import { renderLanding, setupLandingTest } from "@/test/helpers/landing";

setupLandingTest();

describe("LandingPage — plano inativo", () => {
  // First render pays the cold module-import cost (page.tsx + its deps) which can
  // exceed the 5s default under parallel load — give this one an explicit timeout.
  it("exibe badge 'Em breve' para plano inativo", async () => {
    const inativo = buildPlano({ tier: "Elite", nome: "Elite", isAtivo: false });
    await renderLanding([inativo]);
    expect(screen.getByText("Em breve")).toBeInTheDocument();
  }, 20000);

  it("plano inativo NÃO está envolto em link para /cadastro/treinador", async () => {
    const inativo = buildPlano({ tier: "Elite", nome: "Elite", isAtivo: false });
    await renderLanding([inativo]);

    const badge = screen.getByText("Em breve");
    let node: HTMLElement | null = badge;
    while (node && node !== document.body) {
      if (node.tagName === "A" && (node as HTMLAnchorElement).href?.includes("/cadastro/treinador")) {
        throw new Error("Inactive card is wrapped in a /cadastro/treinador link");
      }
      // eslint-disable-next-line testing-library/no-node-access
      node = node.parentElement;
    }
    expect(badge).toBeInTheDocument();
  });

  it("plano ativo continua com link para /cadastro/treinador", async () => {
    const pro = buildPlano({ tier: "Pro", nome: "Pro", isAtivo: true });
    await renderLanding([pro]);
    const proText = screen.getByText("Pro");
    let found = false;
    let node: HTMLElement | null = proText;
    while (node && node !== document.body) {
      if (node.tagName === "A" && (node as HTMLAnchorElement).getAttribute("href") === "/cadastro/treinador") {
        found = true;
        break;
      }
      // eslint-disable-next-line testing-library/no-node-access
      node = node.parentElement;
    }
    expect(found).toBe(true);
  });

  it("plano ativo não exibe badge 'Em breve'", async () => {
    const basic = buildPlano({ tier: "Basic", nome: "Basic", isAtivo: true });
    await renderLanding([basic]);
    expect(screen.queryByText("Em breve")).not.toBeInTheDocument();
  });
});
