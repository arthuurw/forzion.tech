import type { Meta, StoryObj } from "@storybook/nextjs";
import { useForm, FormProvider } from "react-hook-form";
import PasswordField from "./PasswordField";

const meta: Meta<typeof PasswordField> = {
  title: "Forms/PasswordField",
  component: PasswordField,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof PasswordField>;

function Wrapper(
  props: React.ComponentProps<typeof PasswordField> & { initialValue?: string },
) {
  const { initialValue, ...rest } = props;
  const methods = useForm({ defaultValues: { [props.name]: initialValue ?? "" } });
  return (
    <FormProvider {...methods}>
      <PasswordField {...rest} />
    </FormProvider>
  );
}

export const Default: Story = {
  render: (args) => <Wrapper {...args} />,
  args: {
    name: "senha",
    label: "Senha",
  },
};

export const Preenchido: Story = {
  render: (args) => <Wrapper {...args} initialValue="minhasenha123" />,
  args: {
    name: "senha",
    label: "Senha",
  },
};
