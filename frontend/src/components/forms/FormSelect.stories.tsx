import type { Meta, StoryObj } from "@storybook/nextjs";
import { rhfStoryWrapper } from "@/test/rhfStoryWrapper";
import FormSelect from "./FormSelect";

const meta: Meta<typeof FormSelect> = {
  title: "Forms/FormSelect",
  component: FormSelect,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof FormSelect>;

const Wrapper = rhfStoryWrapper(FormSelect);

export const Default: Story = {
  render: (args) => <Wrapper {...args} />,
  args: {
    name: "plano",
    label: "Plano",
    options: [
      { value: "mensal", label: "Mensal" },
      { value: "trimestral", label: "Trimestral" },
      { value: "anual", label: "Anual", disabled: true },
    ],
  },
};
