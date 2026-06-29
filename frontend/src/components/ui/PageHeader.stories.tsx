import type { Meta, StoryObj } from "@storybook/nextjs";
import { Button } from "@mui/material";
import PageHeader from "./PageHeader";

const meta: Meta<typeof PageHeader> = {
  title: "UI/PageHeader",
  component: PageHeader,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof PageHeader>;

export const SoTitulo: Story = {
  args: {
    title: "Painel",
  },
};

export const ComSubtitulo: Story = {
  args: {
    title: "Meus alunos",
    subtitle: "Gerencie seus vínculos ativos.",
  },
};

export const ComAcao: Story = {
  args: {
    title: "Meus treinos",
    action: <Button variant="contained">Novo treino</Button>,
  },
};

export const ComVoltar: Story = {
  args: {
    title: "Detalhe do aluno",
    backHref: "/treinador/alunos",
  },
};
