import type { Meta, StoryObj } from "@storybook/nextjs";
import StatusChip from "./StatusChip";

const meta: Meta<typeof StatusChip> = {
  title: "UI/StatusChip",
  component: StatusChip,
  tags: ["autodocs"],
  argTypes: {
    status: {
      control: "select",
      options: ["AguardandoAprovacao", "Ativo", "Inativo"],
    },
    size: {
      control: "radio",
      options: ["small", "medium"],
    },
  },
};

export default meta;

type Story = StoryObj<typeof StatusChip>;

export const AguardandoAprovacao: Story = {
  args: { status: "AguardandoAprovacao", size: "small" },
};

export const Ativo: Story = {
  args: { status: "Ativo", size: "small" },
};

export const Inativo: Story = {
  args: { status: "Inativo", size: "small" },
};

export const Medium: Story = {
  args: { status: "Ativo", size: "medium" },
};
