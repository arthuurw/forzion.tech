import type { Meta, StoryObj } from "@storybook/nextjs";
import { useForm, FormProvider } from "react-hook-form";
import FormTextField from "./FormTextField";

const meta: Meta<typeof FormTextField> = {
  title: "Forms/FormTextField",
  component: FormTextField,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof FormTextField>;

function Wrapper(props: React.ComponentProps<typeof FormTextField>) {
  const methods = useForm({ defaultValues: { [props.name]: "" } });
  return (
    <FormProvider {...methods}>
      <FormTextField {...props} />
    </FormProvider>
  );
}

export const Default: Story = {
  render: (args) => <Wrapper {...args} />,
  args: {
    name: "nome",
    label: "Nome",
  },
};
