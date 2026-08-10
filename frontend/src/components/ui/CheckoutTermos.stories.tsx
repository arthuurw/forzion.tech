import type { Meta, StoryObj } from "@storybook/nextjs";
import CheckoutTermos from "./CheckoutTermos";

const meta: Meta<typeof CheckoutTermos> = {
  title: "UI/CheckoutTermos",
  component: CheckoutTermos,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof CheckoutTermos>;

export const Default: Story = {
  args: {
    valor: 120,
  },
};

export const Dense: Story = {
  args: {
    valor: 99.9,
    dense: true,
  },
};
