import type { Meta, StoryObj } from "@storybook/nextjs";
import LoadingSpinner from "./LoadingSpinner";

const meta: Meta<typeof LoadingSpinner> = {
  title: "UI/LoadingSpinner",
  component: LoadingSpinner,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof LoadingSpinner>;

export const Inline: Story = {
  args: { fullPage: false },
};

export const FullPage: Story = {
  args: { fullPage: true },
  parameters: { layout: "fullscreen" },
};
