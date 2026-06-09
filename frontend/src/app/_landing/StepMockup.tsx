// Mockups ilustrativos da UI pra seção "Como funciona". SVG vetorial inline (não print
// real): sempre nítido/cheio, escala sem perda e sem asset em /public — evita o cache
// stale de screenshots (browser/next-image/CDN) que travava a troca de imagem.
// Estilo: "cards flutuantes" (glass/profundidade) — cards sobrepostos com sombra.
import { Box } from "@mui/material";

export type StepVariant = "ficha" | "alunos" | "historico";

const C = {
  ink: "#1A1A1A",
  primary: "#F5C400",
  surface: "#FFFFFF",
  bg: "#F2F4F8",
  bgTop: "#FAFBFD",
  subtle: "#F7F8FA",
  line: "#E5E7EB",
  text: "#111827",
  muted: "#9CA3AF",
  faint: "#D1D5DB",
  green: "#2E7D32",
};

// Moldura: bg suave + banda superior clara (profundidade) + filtro de sombra único por
// variant (ids globais no DOM colidiriam entre os 3 SVGs na mesma página).
function Frame({ variant, label, children }: { variant: StepVariant; label: string; children: (sh: string) => React.ReactNode }) {
  const sh = `mock-sh-${variant}`;
  return (
    <Box
      component="svg"
      viewBox="0 0 1280 900"
      role="img"
      aria-label={label}
      sx={{ width: "100%", height: "100%", display: "block" }}
    >
      <defs>
        <filter id={sh} x="-20%" y="-20%" width="140%" height="140%">
          <feDropShadow dx="0" dy="18" stdDeviation="28" floodColor={C.ink} floodOpacity="0.13" />
        </filter>
      </defs>
      <rect width={1280} height={900} fill={C.bg} />
      <rect width={1280} height={430} fill={C.bgTop} />
      {children(sh)}
    </Box>
  );
}

function Bar({ x, y, w, h = 16, fill = C.text, rx = 8 }: { x: number; y: number; w: number; h?: number; fill?: string; rx?: number }) {
  return <rect x={x} y={y} width={w} height={h} rx={rx} fill={fill} />;
}

function Shadowed({ id, x, y, w, h, rx = 24 }: { id: string; x: number; y: number; w: number; h: number; rx?: number }) {
  return (
    <g filter={`url(#${id})`}>
      <rect x={x} y={y} width={w} height={h} rx={rx} fill={C.surface} />
    </g>
  );
}

function Ficha() {
  const rows = [
    { name: 220, obs: true },
    { name: 300, obs: false },
    { name: 200, obs: true },
    { name: 260, obs: false },
    { name: 240, obs: true },
  ];
  const series = [0, 1, 2];
  return (
    <Frame variant="ficha" label="Ficha de treino: exercícios, séries e observações">
      {(sh: string) => (
        <>
          {/* card de trás: lista de exercícios */}
          <Shadowed id={sh} x={80} y={120} w={820} h={680} rx={28} />
          <Bar x={128} y={168} w={300} h={26} fill={C.text} />
          <rect x={128} y={212} width={150} height={28} rx={14} fill={C.subtle} stroke={C.line} strokeWidth={2} />
          {rows.map((r, i) => {
            const y = 270 + i * 96;
            return (
              <g key={i}>
                <rect x={120} y={y} width={740} height={84} rx={14} fill={C.subtle} />
                <Bar x={152} y={y + 34} w={r.name} h={16} fill={C.text} />
                <rect x={520} y={y + 24} width={104} height={36} rx={18} fill={C.surface} stroke={C.line} strokeWidth={2} />
                <Bar x={548} y={y + 36} w={48} h={12} fill={C.ink} rx={6} />
                {r.obs ? <Bar x={668} y={y + 35} w={130} h={14} fill={C.faint} /> : null}
                <rect x={792} y={y + 28} width={28} height={28} rx={7} fill={C.line} />
                <rect x={830} y={y + 28} width={28} height={28} rx={7} fill={C.primary} />
              </g>
            );
          })}
          {/* card da frente: painel de séries (sobreposto à direita) */}
          <Shadowed id={sh} x={860} y={300} w={360} h={420} rx={26} />
          <Bar x={900} y={348} w={200} h={18} fill={C.text} />
          {series.map((i) => {
            const y = 404 + i * 76;
            return (
              <g key={i}>
                <rect x={900} y={y} width={52} height={40} rx={9} fill={C.primary} />
                <Bar x={972} y={y + 8} w={180} h={12} fill={C.text} rx={6} />
                <Bar x={972} y={y + 28} w={110} h={8} fill={C.muted} rx={4} />
              </g>
            );
          })}
          <rect x={900} y={636} width={172} height={48} rx={12} fill={C.primary} />
          <Bar x={930} y={654} w={112} h={12} fill={C.ink} rx={6} />
        </>
      )}
    </Frame>
  );
}

function Alunos() {
  const rows = [
    { name: 240, ativo: true },
    { name: 300, ativo: true },
    { name: 200, ativo: false },
    { name: 280, ativo: true },
  ];
  return (
    <Frame variant="alunos" label="Carteira de alunos com status e ações">
      {(sh: string) => (
        <>
          {/* card de trás: tabela de alunos */}
          <Shadowed id={sh} x={80} y={130} w={860} h={660} rx={28} />
          <Bar x={128} y={178} w={240} h={26} fill={C.text} />
          <rect x={128} y={222} width={210} height={42} rx={10} fill={C.surface} stroke={C.line} strokeWidth={2} />
          <Bar x={152} y={238} w={120} h={12} fill={C.muted} rx={6} />
          {rows.map((r, i) => {
            const y = 312 + i * 108;
            return (
              <g key={i}>
                <rect x={120} y={y} width={780} height={88} rx={14} fill={i % 2 ? C.subtle : C.surface} stroke={C.line} strokeWidth={2} />
                <circle cx={170} cy={y + 44} r={26} fill={C.subtle} stroke={C.line} strokeWidth={2} />
                <Bar x={216} y={y + 36} w={r.name} h={16} fill={C.text} />
                <rect x={560} y={y + 26} width={112} height={36} rx={18} fill={r.ativo ? C.green : C.line} />
                <Bar x={586} y={y + 38} w={60} h={12} fill={r.ativo ? "#FFFFFF" : C.muted} rx={6} />
                <Bar x={720} y={y + 38} w={130} h={14} fill={C.faint} />
              </g>
            );
          })}
          {/* card da frente: stat "alunos ativos" (donut) */}
          <Shadowed id={sh} x={900} y={250} w={300} h={300} rx={26} />
          <Bar x={940} y={298} w={170} h={14} fill={C.muted} rx={7} />
          <circle cx={1050} cy={420} r={72} fill="none" stroke={C.subtle} strokeWidth={18} />
          <path d="M1050 348 a72 72 0 1 1 -50 124" fill="none" stroke={C.primary} strokeWidth={18} strokeLinecap="round" />
          <Bar x={1018} y={406} w={64} h={26} fill={C.text} rx={6} />
        </>
      )}
    </Frame>
  );
}

function Historico() {
  const bars = [120, 92, 150, 112, 168, 134, 158];
  const linePts = "640,690 720,650 800,662 880,610 960,560 1040,520 1120,476";
  const dots = [
    [640, 690],
    [800, 662],
    [960, 560],
    [1120, 476],
  ];
  return (
    <Frame variant="historico" label="Histórico de execuções: frequência e progressão">
      {(sh: string) => (
        <>
          {/* card de trás: frequência (barras) */}
          <Shadowed id={sh} x={80} y={110} w={780} h={430} rx={28} />
          <Bar x={128} y={158} w={240} h={16} fill={C.muted} rx={8} />
          {bars.map((h, i) => {
            const x = 140 + i * 96;
            const baseY = 470;
            return <rect key={i} x={x} y={baseY - h} width={64} height={h} rx={8} fill={C.primary} />;
          })}
          <line x1={128} y1={470} x2={812} y2={470} stroke={C.line} strokeWidth={2} />
          {/* card da frente: progressão (linha) sobreposto */}
          <Shadowed id={sh} x={560} y={380} w={640} h={420} rx={26} />
          <Bar x={600} y={430} w={220} h={14} fill={C.muted} rx={7} />
          {[0, 1, 2, 3].map((i) => (
            <line key={i} x1={600} y1={500 + i * 70} x2={1160} y2={500 + i * 70} stroke={C.subtle} strokeWidth={2} />
          ))}
          <polyline points={linePts} fill="none" stroke={C.primary} strokeWidth={6} strokeLinejoin="round" strokeLinecap="round" />
          {dots.map((p, i) => (
            <circle key={i} cx={p[0]} cy={p[1]} r={9} fill={C.surface} stroke={C.primary} strokeWidth={5} />
          ))}
        </>
      )}
    </Frame>
  );
}

export default function StepMockup({ variant }: { variant: StepVariant }) {
  if (variant === "ficha") return <Ficha />;
  if (variant === "alunos") return <Alunos />;
  return <Historico />;
}
