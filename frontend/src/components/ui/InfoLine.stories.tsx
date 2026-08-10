import type { Meta, StoryObj } from "@storybook/nextjs";
import InfoLine from "./InfoLine";

const meta: Meta<typeof InfoLine> = {
  title: "UI/InfoLine",
  component: InfoLine,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof InfoLine>;

export const Default: Story = {
  args: {
    label: "E-mail",
    value: "maria@forzion.tech",
  },
};
