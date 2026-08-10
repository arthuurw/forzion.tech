import type { Meta, StoryObj } from "@storybook/nextjs";
import { rhfStoryWrapper } from "@/test/rhfStoryWrapper";
import PasswordField from "./PasswordField";

const meta: Meta<typeof PasswordField> = {
  title: "Forms/PasswordField",
  component: PasswordField,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof PasswordField>;

const Wrapper = rhfStoryWrapper(PasswordField);

export const Default: Story = {
  render: (args) => <Wrapper {...args} />,
  args: {
    name: "senha",
    label: "Senha",
  },
};

export const Preenchido: Story = {
  render: (args) => <Wrapper {...args} defaultValue="minhasenha123" />,
  args: {
    name: "senha",
    label: "Senha",
  },
};
