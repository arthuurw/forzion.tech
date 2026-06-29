"use client";
import { Controller, useFormContext } from "react-hook-form";
import {
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText,
} from "@mui/material";

interface Option {
  value: string | number;
  label: string;
  disabled?: boolean;
}

interface FormSelectProps {
  name: string;
  label: string;
  options: Option[];
  required?: boolean;
}

export default function FormSelect({ name, label, options, required }: FormSelectProps) {
  const { control } = useFormContext();
  const labelId = `${name}-label`;

  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <FormControl fullWidth size="small" error={!!fieldState.error} required={required}>
          <InputLabel id={labelId}>{label}</InputLabel>
          <Select {...field} labelId={labelId} label={label}>
            {options.map((opt) => (
              <MenuItem key={opt.value} value={opt.value} disabled={opt.disabled}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
          {fieldState.error && (
            <FormHelperText>{fieldState.error.message}</FormHelperText>
          )}
        </FormControl>
      )}
    />
  );
}
