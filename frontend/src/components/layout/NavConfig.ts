import PeopleIcon from "@mui/icons-material/People";
import CardMembershipIcon from "@mui/icons-material/CardMembership";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import ListAltIcon from "@mui/icons-material/ListAlt";
import InventoryIcon from "@mui/icons-material/Inventory";
import AssignmentIcon from "@mui/icons-material/Assignment";
import HistoryIcon from "@mui/icons-material/History";
import type { ElementType } from "react";
import type { TipoConta } from "@/types";

export interface NavItem {
  label: string;
  href: string;
  Icon: ElementType;
}

const adminNav: NavItem[] = [
  { label: "Treinadores", href: "/admin/treinadores", Icon: PeopleIcon },
  { label: "Planos", href: "/admin/planos", Icon: CardMembershipIcon },
  { label: "Exercícios", href: "/admin/exercicios", Icon: FitnessCenterIcon },
  { label: "Grupos Musculares", href: "/admin/grupos-musculares", Icon: ListAltIcon },
];

const treinadorNav: NavItem[] = [
  { label: "Alunos", href: "/treinador/alunos", Icon: PeopleIcon },
  { label: "Fichas", href: "/treinador/treinos", Icon: ListAltIcon },
  { label: "Exercícios", href: "/treinador/exercicios", Icon: FitnessCenterIcon },
  { label: "Pacotes", href: "/treinador/pacotes", Icon: InventoryIcon },
];

const alunoNav: NavItem[] = [
  { label: "Minhas Fichas", href: "/aluno/fichas", Icon: AssignmentIcon },
  { label: "Histórico", href: "/aluno/historico", Icon: HistoryIcon },
];

export const NAV_BY_TIPO: Record<TipoConta, NavItem[]> = {
  SystemAdmin: adminNav,
  Treinador: treinadorNav,
  Aluno: alunoNav,
};
