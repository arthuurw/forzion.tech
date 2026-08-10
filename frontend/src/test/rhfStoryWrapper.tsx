import { useForm, FormProvider } from "react-hook-form";
import type { ComponentType } from "react";

export function rhfStoryWrapper<P extends { name: string }>(Component: ComponentType<P>) {
  return function Wrapper(props: P & { defaultValue?: unknown }) {
    const { defaultValue, ...rest } = props;
    const methods = useForm({ defaultValues: { [props.name]: defaultValue ?? "" } });
    return (
      <FormProvider {...methods}>
        <Component {...(rest as P)} />
      </FormProvider>
    );
  };
}
