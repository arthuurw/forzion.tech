import type { Meta, StoryObj } from "@storybook/nextjs";
import DetalheErro from "./DetalheErro";

const meta: Meta<typeof DetalheErro> = {
  title: "UI/DetalheErro",
  component: DetalheErro,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof DetalheErro>;

const noop = () => {};

export const Default: Story = {
  args: {
    mensagem: "Não foi possível carregar os detalhes.",
  },
};

export const ComAcoes: Story = {
  args: {
    mensagem: "Não foi possível carregar os detalhes.",
    onRetry: noop,
    onVoltar: noop,
  },
};
