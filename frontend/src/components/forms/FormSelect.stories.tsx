import type { Meta, StoryObj } from "@storybook/nextjs";
import { useForm, FormProvider } from "react-hook-form";
import FormSelect from "./FormSelect";

const meta: Meta<typeof FormSelect> = {
  title: "Forms/FormSelect",
  component: FormSelect,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof FormSelect>;

function Wrapper(props: React.ComponentProps<typeof FormSelect>) {
  const methods = useForm({ defaultValues: { [props.name]: "" } });
  return (
    <FormProvider {...methods}>
      <FormSelect {...props} />
    </FormProvider>
  );
}

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
