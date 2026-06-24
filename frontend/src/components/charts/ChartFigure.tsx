import type { ReactNode } from "react";
import { srOnly } from "@/lib/utils/a11y";

interface Props {
  label: string;
  summary: string;
  children: ReactNode;
}

export default function ChartFigure({ label, summary, children }: Props) {
  return (
    <figure aria-label={label} style={{ margin: 0 }}>
      <span style={srOnly}>{summary}</span>
      {children}
    </figure>
  );
}
