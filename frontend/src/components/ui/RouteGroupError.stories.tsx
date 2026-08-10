import type { Meta, StoryObj } from "@storybook/nextjs";
import RouteGroupError from "./RouteGroupError";

const meta: Meta<typeof RouteGroupError> = {
  title: "UI/RouteGroupError",
  component: RouteGroupError,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof RouteGroupError>;

export const Default: Story = {
  args: {
    error: new Error("Falha ao carregar a página"),
    reset: () => {},
    homeHref: "/aluno",
    homeLabel: "Voltar ao início",
    bodyText: "Tente novamente ou volte para o início.",
  },
};
