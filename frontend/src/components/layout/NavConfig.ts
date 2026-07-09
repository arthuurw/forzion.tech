import PeopleIcon from "@mui/icons-material/People";
import CardMembershipIcon from "@mui/icons-material/CardMembership";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import ListAltIcon from "@mui/icons-material/ListAlt";
import InventoryIcon from "@mui/icons-material/Inventory";
import AssignmentIcon from "@mui/icons-material/Assignment";
import HistoryIcon from "@mui/icons-material/History";
import MonitorHeartIcon from "@mui/icons-material/MonitorHeart";
import SupportAgentIcon from "@mui/icons-material/SupportAgent";
import PaymentsIcon from "@mui/icons-material/Payments";
import type { ElementType } from "react";
import type { TipoConta } from "@/types";

export interface NavItem {
  label: string;
  href: string;
  Icon: ElementType;
  drawerOnly?: boolean;
}

const adminNav: NavItem[] = [
  { label: "Treinadores", href: "/admin/treinadores", Icon: PeopleIcon },
  { label: "Alunos", href: "/admin/alunos", Icon: AssignmentIcon },
  { label: "Planos", href: "/admin/planos", Icon: CardMembershipIcon },
  { label: "Exercícios", href: "/admin/exercicios", Icon: FitnessCenterIcon },
  { label: "Grupos Musculares", href: "/admin/grupos-musculares", Icon: ListAltIcon },
  { label: "Saúde", href: "/admin/saude", Icon: MonitorHeartIcon },
];

const treinadorNav: NavItem[] = [
  { label: "Alunos", href: "/treinador/alunos", Icon: PeopleIcon },
  { label: "Fichas", href: "/treinador/treinos", Icon: ListAltIcon },
  { label: "Exercícios", href: "/treinador/exercicios", Icon: FitnessCenterIcon },
  { label: "Pacotes", href: "/treinador/pacotes", Icon: InventoryIcon },
  { label: "Recebimentos", href: "/treinador/pagamentos", Icon: PaymentsIcon, drawerOnly: true },
  { label: "Plano", href: "/treinador/plano", Icon: CardMembershipIcon, drawerOnly: true },
  { label: "Suporte", href: "/treinador/suporte", Icon: SupportAgentIcon },
];

const alunoNav: NavItem[] = [
  { label: "Minhas Fichas", href: "/aluno/fichas", Icon: AssignmentIcon },
  { label: "Histórico", href: "/aluno/historico", Icon: HistoryIcon },
  { label: "Suporte", href: "/aluno/suporte", Icon: SupportAgentIcon },
];

export const NAV_BY_TIPO: Record<TipoConta, NavItem[]> = {
  SystemAdmin: adminNav,
  Treinador: treinadorNav,
  Aluno: alunoNav,
};
