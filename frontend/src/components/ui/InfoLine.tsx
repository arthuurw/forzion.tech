import { Typography } from "@mui/material";

interface Props {
  label: string;
  value: string | number;
}

export default function InfoLine({ label, value }: Props) {
  return (
    <Typography variant="body2">
      <strong>{label}:</strong> {value}
    </Typography>
  );
}
