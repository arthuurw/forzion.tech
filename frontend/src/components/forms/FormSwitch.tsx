"use client";
import { Controller, useFormContext } from "react-hook-form";
import { FormControlLabel, Switch } from "@mui/material";

interface FormSwitchProps {
  name: string;
  label: string;
}

export default function FormSwitch({ name, label }: FormSwitchProps) {
  const { control } = useFormContext();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field }) => (
        <FormControlLabel
          control={<Switch checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />}
          label={label}
        />
      )}
    />
  );
}
