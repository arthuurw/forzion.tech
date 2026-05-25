import type { Meta, StoryObj } from "@storybook/nextjs";
import EmptyState from "./EmptyState";

const meta: Meta<typeof EmptyState> = {
  title: "UI/EmptyState",
  component: EmptyState,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof EmptyState>;

export const SemAcao: Story = {
  args: {
    message: "Nenhum aluno cadastrado ainda.",
  },
};

export const ComAcao: Story = {
  args: {
    message: "Voce ainda nao tem treinos.",
    actionLabel: "Criar treino",
  },
};
