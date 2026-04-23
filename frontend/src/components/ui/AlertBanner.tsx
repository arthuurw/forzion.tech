"use client";
import { Alert, AlertTitle, Collapse } from "@mui/material";

interface AlertBannerProps {
  open: boolean;
  severity?: "error" | "warning" | "info" | "success";
  title?: string;
  message: string;
  onClose?: () => void;
}

export default function AlertBanner({
  open,
  severity = "error",
  title,
  message,
  onClose,
}: AlertBannerProps) {
  return (
    <Collapse in={open}>
      <Alert severity={severity} onClose={onClose} sx={{ mb: 2 }}>
        {title && <AlertTitle>{title}</AlertTitle>}
        {message}
      </Alert>
    </Collapse>
  );
}
