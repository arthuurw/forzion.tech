import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import SocialProof, { type Testimonial } from "../SocialProof";

const TESTIMONIAL: Testimonial = {
  text: "Plataforma excelente para gerenciar meus alunos.",
  name: "João Silva",
  city: "São Paulo",
};

describe("SocialProof", () => {
  it("renderiza null quando testimonials está vazio e count não fornecido", () => {
    const { container } = render(<SocialProof testimonials={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("renderiza null quando testimonials está vazio e count é 0", () => {
    const { container } = render(<SocialProof testimonials={[]} count={0} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("renderiza N cards quando recebe N depoimentos", () => {
    const second: Testimonial = { text: "Ótimo suporte!", name: "Maria Souza", city: "Rio de Janeiro" };
    render(<SocialProof testimonials={[TESTIMONIAL, second]} />);

    expect(screen.getByText(TESTIMONIAL.name)).toBeInTheDocument();
    expect(screen.getByText(TESTIMONIAL.city)).toBeInTheDocument();
    expect(screen.getByText(second.name)).toBeInTheDocument();
    expect(screen.getByText(second.city)).toBeInTheDocument();
  });

  it("exibe contador quando fornecido com valor positivo", () => {
    render(<SocialProof testimonials={[]} count={150} />);
    expect(screen.getByText("+150 treinadores na plataforma")).toBeInTheDocument();
  });
});
