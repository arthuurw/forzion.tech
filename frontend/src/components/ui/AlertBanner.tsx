"use client";
import { ReactNode } from "react";
import { Alert, AlertTitle, Collapse } from "@mui/material";

interface AlertBannerProps {
  open: boolean;
  severity?: "error" | "warning" | "info" | "success";
  title?: string;
  message: string;
  onClose?: () => void;
  action?: ReactNode;
}

export default function AlertBanner({
  open,
  severity = "error",
  title,
  message,
  onClose,
  action,
}: AlertBannerProps) {
  return (
    <Collapse in={open}>
      <Alert severity={severity} onClose={onClose} action={action} sx={{ mb: 2 }}>
        {title && <AlertTitle>{title}</AlertTitle>}
        {message}
      </Alert>
    </Collapse>
  );
}
