import type { Meta, StoryObj } from "@storybook/nextjs";
import { rhfStoryWrapper } from "@/test/rhfStoryWrapper";
import FormSwitch from "./FormSwitch";

const meta: Meta<typeof FormSwitch> = {
  title: "Forms/FormSwitch",
  component: FormSwitch,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof FormSwitch>;

const Wrapper = rhfStoryWrapper(FormSwitch);

export const Desligado: Story = {
  render: (args) => <Wrapper {...args} defaultValue={false} />,
  args: {
    name: "notificacoes",
    label: "Receber notificações por e-mail",
  },
};

export const Ligado: Story = {
  render: (args) => <Wrapper {...args} defaultValue />,
  args: {
    name: "notificacoes",
    label: "Receber notificações por e-mail",
  },
};
