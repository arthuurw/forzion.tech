"use client";
import { Controller, useFormContext } from "react-hook-form";
import { TextField } from "@mui/material";
import type { TextFieldProps } from "@mui/material";

type FormTextFieldProps = TextFieldProps & {
  name: string;
};

export default function FormTextField({ name, ...props }: FormTextFieldProps) {
  const { control } = useFormContext();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <TextField
          {...field}
          {...props}
          error={!!fieldState.error}
          helperText={fieldState.error?.message ?? props.helperText}
        />
      )}
    />
  );
}
