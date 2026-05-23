import type { Meta, StoryObj } from "@storybook/nextjs";
import AlertBanner from "./AlertBanner";

const meta: Meta<typeof AlertBanner> = {
  title: "UI/AlertBanner",
  component: AlertBanner,
  tags: ["autodocs"],
  argTypes: {
    severity: {
      control: "select",
      options: ["error", "warning", "info", "success"],
    },
  },
};

export default meta;

type Story = StoryObj<typeof AlertBanner>;

export const Error: Story = {
  args: {
    open: true,
    severity: "error",
    title: "Falha no envio",
    message: "Nao foi possivel concluir a operacao. Tente novamente.",
  },
};

export const Warning: Story = {
  args: {
    open: true,
    severity: "warning",
    message: "Atencao: dados nao salvos serao perdidos.",
  },
};

export const Info: Story = {
  args: {
    open: true,
    severity: "info",
    message: "Sua sessao expira em 5 minutos.",
  },
};

export const Success: Story = {
  args: {
    open: true,
    severity: "success",
    title: "Sucesso",
    message: "Aluno cadastrado com sucesso.",
  },
};

export const SemTitulo: Story = {
  args: {
    open: true,
    severity: "info",
    message: "Mensagem informativa sem titulo.",
  },
};

export const Fechado: Story = {
  args: {
    open: false,
    severity: "error",
    message: "Este alerta nao eh visivel.",
  },
};
