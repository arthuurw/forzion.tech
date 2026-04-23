"use client";
import { useState } from "react";
import { Controller, useFormContext } from "react-hook-form";
import {
  TextField,
  InputAdornment,
  IconButton,
} from "@mui/material";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import type { TextFieldProps } from "@mui/material";

type PasswordFieldProps = Omit<TextFieldProps, "type"> & {
  name: string;
};

export default function PasswordField({ name, ...props }: PasswordFieldProps) {
  const { control } = useFormContext();
  const [show, setShow] = useState(false);

  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <TextField
          {...field}
          {...props}
          type={show ? "text" : "password"}
          error={!!fieldState.error}
          helperText={fieldState.error?.message ?? props.helperText}
          slotProps={{
            input: {
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton
                    onClick={() => setShow((v) => !v)}
                    edge="end"
                    size="small"
                    aria-label={show ? "Ocultar senha" : "Mostrar senha"}
                  >
                    {show ? <VisibilityOff /> : <Visibility />}
                  </IconButton>
                </InputAdornment>
              ),
            },
          }}
        />
      )}
    />
  );
}
