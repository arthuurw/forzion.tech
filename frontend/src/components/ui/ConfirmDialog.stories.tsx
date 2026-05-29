// F24 (Fase 5 test remediation) — stateful component story.
// Estados cobertos: idle (default), destructive, loading, com children custom.
import type { Meta, StoryObj } from "@storybook/nextjs";
import { useState } from "react";
import { Button, TextField } from "@mui/material";
import ConfirmDialog from "./ConfirmDialog";

const meta: Meta<typeof ConfirmDialog> = {
  title: "UI/ConfirmDialog",
  component: ConfirmDialog,
  tags: ["autodocs"],
  argTypes: {
    destructive: { control: "boolean" },
    loading: { control: "boolean" },
  },
};

export default meta;

type Story = StoryObj<typeof ConfirmDialog>;

const noop = () => {};

export const Default: Story = {
  args: {
    open: true,
    title: "Salvar alterações?",
    description: "Os dados serão atualizados no sistema.",
    confirmLabel: "Salvar",
    cancelLabel: "Cancelar",
    onConfirm: noop,
    onClose: noop,
  },
};

export const Destructive: Story = {
  args: {
    open: true,
    title: "Excluir treinador?",
    description: "Esta ação é PERMANENTE e não pode ser desfeita.",
    confirmLabel: "Excluir definitivamente",
    cancelLabel: "Cancelar",
    destructive: true,
    onConfirm: noop,
    onClose: noop,
  },
};

export const Loading: Story = {
  args: {
    open: true,
    title: "Processando…",
    description: "Aguarde enquanto salvamos os dados.",
    loading: true,
    onConfirm: noop,
    onClose: noop,
  },
};

export const ComChildrenForm: Story = {
  render: (args) => {
    function Wrapper() {
      const [observacao, setObservacao] = useState("");
      return (
        <ConfirmDialog
          {...args}
          onConfirm={noop}
          onClose={noop}
        >
          <TextField
            autoFocus
            margin="dense"
            label="Observação"
            fullWidth
            value={observacao}
            onChange={(e) => setObservacao(e.target.value)}
          />
        </ConfirmDialog>
      );
    }
    return <Wrapper />;
  },
  args: {
    open: true,
    title: "Reprovar treinador",
    description: "Adicione uma observação opcional.",
    confirmLabel: "Reprovar",
    destructive: true,
  },
};

export const Fechado: Story = {
  args: {
    open: false,
    title: "Não visível",
    description: "Este dialog está fechado.",
    onConfirm: noop,
    onClose: noop,
  },
};

// Interativo via toggle pra testar manualmente o ciclo open/close.
export const Interativo: Story = {
  render: () => {
    function Wrapper() {
      const [open, setOpen] = useState(false);
      const [loading, setLoading] = useState(false);

      const handleConfirm = () => {
        setLoading(true);
        setTimeout(() => {
          setLoading(false);
          setOpen(false);
        }, 1500);
      };

      return (
        <>
          <Button variant="contained" onClick={() => setOpen(true)}>
            Abrir dialog
          </Button>
          <ConfirmDialog
            open={open}
            title="Confirmar ação"
            description="Simula um confirm com loading async."
            loading={loading}
            onConfirm={handleConfirm}
            onClose={() => setOpen(false)}
          />
        </>
      );
    }
    return <Wrapper />;
  },
};
