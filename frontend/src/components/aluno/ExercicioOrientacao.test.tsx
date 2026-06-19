import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ExercicioOrientacao from "./ExercicioOrientacao";

describe("ExercicioOrientacao", () => {
  it("não renderiza quando texto e vídeo ausentes", () => {
    const { container } = render(<ExercicioOrientacao nomeExercicio="Supino" />);
    expect(container).toBeEmptyDOMElement();
  });

  it("renderiza o texto de execução", () => {
    render(<ExercicioOrientacao nomeExercicio="Supino" comoExecutar="Mantenha a postura." />);
    expect(screen.getByText("Mantenha a postura.")).toBeInTheDocument();
  });

  it("não monta iframe antes do clique (facade)", () => {
    render(<ExercicioOrientacao nomeExercicio="Supino" videoId="dQw4w9WgXcQ" />);
    expect(screen.queryByTitle(/Vídeo de execução/i)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Assistir vídeo de execução: Supino/i })).toBeInTheDocument();
  });

  it("monta iframe nocookie após o clique", async () => {
    render(<ExercicioOrientacao nomeExercicio="Supino" videoId="dQw4w9WgXcQ" />);
    await userEvent.click(screen.getByRole("button", { name: /Assistir vídeo/i }));
    const iframe = screen.getByTitle("Vídeo de execução: Supino");
    expect(iframe).toHaveAttribute("src", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?rel=0");
  });

  it("ignora videoId inválido (sem facade, sem iframe)", () => {
    render(<ExercicioOrientacao nomeExercicio="Supino" comoExecutar="Texto" videoId="lixo" />);
    expect(screen.getByText("Texto")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Assistir/i })).not.toBeInTheDocument();
  });
});
