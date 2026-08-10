import {
  useForm,
  FormProvider,
  type FieldValues,
  type DefaultValues,
  type FieldErrors,
} from "react-hook-form";
import type { ReactNode } from "react";

export function RhfHarness<TValues extends FieldValues>({
  children,
  defaultValues,
  errors,
}: {
  children: ReactNode;
  defaultValues: DefaultValues<TValues>;
  errors?: FieldErrors<TValues>;
}) {
  const methods = useForm<TValues>({ defaultValues, errors });
  return <FormProvider {...methods}>{children}</FormProvider>;
}
