import type { Meta, StoryObj } from "@storybook/nextjs";
import { useForm, FormProvider } from "react-hook-form";
import FormSwitch from "./FormSwitch";

const meta: Meta<typeof FormSwitch> = {
  title: "Forms/FormSwitch",
  component: FormSwitch,
  tags: ["autodocs"],
};

export default meta;

type Story = StoryObj<typeof FormSwitch>;

function Wrapper(
  props: React.ComponentProps<typeof FormSwitch> & { defaultChecked?: boolean },
) {
  const { defaultChecked, ...rest } = props;
  const methods = useForm({ defaultValues: { [props.name]: defaultChecked ?? false } });
  return (
    <FormProvider {...methods}>
      <FormSwitch {...rest} />
    </FormProvider>
  );
}

export const Desligado: Story = {
  render: (args) => <Wrapper {...args} defaultChecked={false} />,
  args: {
    name: "notificacoes",
    label: "Receber notificações por e-mail",
  },
};

export const Ligado: Story = {
  render: (args) => <Wrapper {...args} defaultChecked />,
  args: {
    name: "notificacoes",
    label: "Receber notificações por e-mail",
  },
};
