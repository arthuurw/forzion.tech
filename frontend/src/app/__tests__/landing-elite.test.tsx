import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import { buildPlano } from "@/test/factories/plano";
import { renderLanding, setupLandingTest } from "@/test/helpers/landing";

setupLandingTest();

describe("LandingPage — plano Elite", () => {
  // First render pays the cold module-import cost (page.tsx + its deps) which can
  // exceed the 5s default under parallel load — give this one an explicit timeout.
  it("exibe badge 'Em breve' para plano com tier Elite", async () => {
    const elite = buildPlano({ tier: "Elite", nome: "Elite" });
    await renderLanding([elite]);
    expect(screen.getByText("Em breve")).toBeInTheDocument();
  }, 20000);

  it("plano Elite NÃO está envolto em link para /cadastro/treinador", async () => {
    const elite = buildPlano({ tier: "Elite", nome: "Elite" });
    await renderLanding([elite]);

    // No <a> with href /cadastro/treinador should wrap the Elite "Em breve" chip.
    const eliteBadge = screen.getByText("Em breve");
    let node: HTMLElement | null = eliteBadge;
    while (node && node !== document.body) {
      if (node.tagName === "A" && (node as HTMLAnchorElement).href?.includes("/cadastro/treinador")) {
        throw new Error("Elite card is wrapped in a /cadastro/treinador link");
      }
      node = node.parentElement;
    }
    expect(eliteBadge).toBeInTheDocument();
  });

  it("plano não-Elite continua com link para /cadastro/treinador", async () => {
    const pro = buildPlano({ tier: "Pro", nome: "Pro" });
    await renderLanding([pro]);
    // Plan card name should be inside a link to /cadastro/treinador
    const proText = screen.getByText("Pro");
    let found = false;
    let node: HTMLElement | null = proText;
    while (node && node !== document.body) {
      if (node.tagName === "A" && (node as HTMLAnchorElement).getAttribute("href") === "/cadastro/treinador") {
        found = true;
        break;
      }
      node = node.parentElement;
    }
    expect(found).toBe(true);
  });

  it("plano Elite não exibe badge 'Em breve' quando tier não é Elite", async () => {
    const basic = buildPlano({ tier: "Basic", nome: "Basic" });
    await renderLanding([basic]);
    expect(screen.queryByText("Em breve")).not.toBeInTheDocument();
  });
});
