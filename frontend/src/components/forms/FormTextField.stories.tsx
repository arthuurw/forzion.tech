import type { Meta, StoryObj } from "@storybook/nextjs";
import { rhfStoryWrapper } from "@/test/rhfStoryWrapper";
import FormTextField from "./FormTextField";

const meta: Meta<typeof FormTextField> = {
  title: "Forms/FormTextField",
  component: FormTextField,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof FormTextField>;

const Wrapper = rhfStoryWrapper(FormTextField);

export const Default: Story = {
  render: (args) => <Wrapper {...args} />,
  args: {
    name: "nome",
    label: "Nome",
  },
};
