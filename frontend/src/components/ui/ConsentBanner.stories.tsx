import type { Meta, StoryObj } from "@storybook/nextjs";
import ConsentBanner from "./ConsentBanner";

const meta: Meta<typeof ConsentBanner> = {
  title: "UI/ConsentBanner",
  component: ConsentBanner,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof ConsentBanner>;

export const Aberto: Story = {
  args: {
    forceOpen: true,
  },
};
