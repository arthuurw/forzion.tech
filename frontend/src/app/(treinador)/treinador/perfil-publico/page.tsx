"use client";
import { useEffect, useState } from "react";
import {
  Box, Card, CardContent, Button, Grid, TextField, Stack, Typography,
  Switch, FormControlLabel, IconButton, MenuItem, Divider, Chip,
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import PageHeader from "@/components/ui/PageHeader";
import { extractApiError } from "@/lib/api/extractApiError";
import {
  perfilPublicoApi,
  type HorarioFuncionamentoPayload,
} from "@/lib/api/perfilPublico";

const DIAS_SEMANA = [
  { value: 0, label: "Domingo" },
  { value: 1, label: "Segunda" },
  { value: 2, label: "Terça" },
  { value: 3, label: "Quarta" },
  { value: 4, label: "Quinta" },
  { value: 5, label: "Sexta" },
  { value: 6, label: "Sábado" },
];

const nomeDiaSemana = (dia: number) => DIAS_SEMANA.find((d) => d.value === dia)?.label ?? String(dia);

interface PoliticaLinha {
  chave: string;
  valor: string;
}

export default function PerfilPublicoTreinadorPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [nomeFantasia, setNomeFantasia] = useState("");
  const [rua, setRua] = useState("");
  const [numero, setNumero] = useState("");
  const [complemento, setComplemento] = useState("");
  const [bairro, setBairro] = useState("");
  const [cidade, setCidade] = useState("");
  const [estado, setEstado] = useState("");
  const [cep, setCep] = useState("");
  const [publicado, setPublicado] = useState(false);

  const [politicas, setPoliticas] = useState<PoliticaLinha[]>([]);
  const [novaPoliticaChave, setNovaPoliticaChave] = useState("");
  const [novaPoliticaValor, setNovaPoliticaValor] = useState("");

  const [horarios, setHorarios] = useState<HorarioFuncionamentoPayload[]>([]);
  const [novoDiaSemana, setNovoDiaSemana] = useState(1);
  const [novoAbreAs, setNovoAbreAs] = useState("");
  const [novoFechaAs, setNovoFechaAs] = useState("");
  const [horarioError, setHorarioError] = useState("");

  const publicadoBloqueado = publicado && !nomeFantasia.trim();

  useEffect(() => {
    perfilPublicoApi
      .getPerfilPublico()
      .then((res) => {
        const d = res.data;
        if (!d) return;
        setNomeFantasia(d.nomeFantasia ?? "");
        setPublicado(d.isPublicado);
        if (d.endereco) {
          setRua(d.endereco.rua);
          setNumero(d.endereco.numero ?? "");
          setComplemento(d.endereco.complemento ?? "");
          setBairro(d.endereco.bairro);
          setCidade(d.endereco.cidade);
          setEstado(d.endereco.estado);
          setCep(d.endereco.cep);
        }
        setHorarios(d.horariosFuncionamento.map((h) => ({
          diaSemana: h.diaSemana, abreAs: h.abreAs, fechaAs: h.fechaAs,
        })));
        setPoliticas(Object.entries(d.politicas ?? {}).map(([chave, valor]) => ({ chave, valor })));
      })
      .catch(() => setError("Não foi possível carregar o perfil público."))
      .finally(() => setLoading(false));
  }, []);

  const handleAdicionarPolitica = () => {
    if (!novaPoliticaChave.trim() || !novaPoliticaValor.trim()) return;
    setPoliticas((prev) => [...prev, { chave: novaPoliticaChave.trim(), valor: novaPoliticaValor.trim() }]);
    setNovaPoliticaChave("");
    setNovaPoliticaValor("");
  };

  const handleRemoverPolitica = (index: number) => {
    setPoliticas((prev) => prev.filter((_, i) => i !== index));
  };

  const handleAdicionarHorario = () => {
    setHorarioError("");
    if (!novoAbreAs || !novoFechaAs) return;
    if (novoAbreAs >= novoFechaAs) {
      setHorarioError("O horário de abertura deve ser antes do horário de fechamento.");
      return;
    }
    setHorarios((prev) => [...prev, { diaSemana: novoDiaSemana, abreAs: novoAbreAs, fechaAs: novoFechaAs }]);
    setNovoAbreAs("");
    setNovoFechaAs("");
  };

  const handleRemoverHorario = (index: number) => {
    setHorarios((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSalvar = async () => {
    if (publicadoBloqueado) return;
    setError("");
    setSuccess("");
    setSaving(true);
    try {
      const enderecoPreenchido = rua.trim() || bairro.trim() || cidade.trim() || estado.trim() || cep.trim();
      await perfilPublicoApi.salvarPerfilPublico({
        nomeFantasia: nomeFantasia.trim() || null,
        endereco: enderecoPreenchido
          ? {
            rua: rua.trim(),
            numero: numero.trim() || null,
            complemento: complemento.trim() || null,
            bairro: bairro.trim(),
            cidade: cidade.trim(),
            estado: estado.trim().toUpperCase(),
            cep: cep.trim(),
          }
          : null,
        politicas: politicas.length > 0
          ? Object.fromEntries(politicas.map((p) => [p.chave, p.valor]))
          : null,
        horarios,
        isPublicado: publicado,
      });
      setSuccess("Perfil público salvo.");
    } catch (err) {
      setError(extractApiError(err, "Não foi possível salvar o perfil público."));
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 720 } }}>
      <PageHeader
        title="Perfil Público"
        subtitle="Dados exibidos ao agente conversacional do seu negócio quando o perfil está publicado."
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Card sx={{ border: "1px solid", borderColor: "divider", mb: 2 }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12 }}>
              <TextField
                label="Nome fantasia"
                value={nomeFantasia}
                onChange={(e) => setNomeFantasia(e.target.value)}
                size="small"
                fullWidth
                required
                error={publicadoBloqueado}
                helperText={publicadoBloqueado ? "Nome fantasia é obrigatório para publicar o perfil." : undefined}
              />
            </Grid>

            <Grid size={{ xs: 12 }}>
              <Typography variant="subtitle2" color="text.secondary">Endereço (opcional)</Typography>
            </Grid>
            <Grid size={{ xs: 12, sm: 8 }}>
              <TextField label="Rua" value={rua} onChange={(e) => setRua(e.target.value)} size="small" fullWidth />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField label="Número" value={numero} onChange={(e) => setNumero(e.target.value)} size="small" fullWidth />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <TextField label="Complemento" value={complemento} onChange={(e) => setComplemento(e.target.value)} size="small" fullWidth />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <TextField label="Bairro" value={bairro} onChange={(e) => setBairro(e.target.value)} size="small" fullWidth />
            </Grid>
            <Grid size={{ xs: 12, sm: 5 }}>
              <TextField label="Cidade" value={cidade} onChange={(e) => setCidade(e.target.value)} size="small" fullWidth />
            </Grid>
            <Grid size={{ xs: 12, sm: 3 }}>
              <TextField
                label="UF"
                value={estado}
                onChange={(e) => setEstado(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ htmlInput: { maxLength: 2, style: { textTransform: "uppercase" } } }}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField label="CEP" value={cep} onChange={(e) => setCep(e.target.value)} size="small" fullWidth />
            </Grid>
          </Grid>
        </CardContent>
      </Card>

      <Card sx={{ border: "1px solid", borderColor: "divider", mb: 2 }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
            Horários de funcionamento
          </Typography>

          <Stack spacing={1} sx={{ mb: 2 }}>
            {horarios.map((h, index) => (
              <Stack key={`${h.diaSemana}-${h.abreAs}-${h.fechaAs}-${index}`} direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Chip label={nomeDiaSemana(h.diaSemana)} size="small" />
                <Typography variant="body2">{h.abreAs} — {h.fechaAs}</Typography>
                <IconButton size="small" onClick={() => handleRemoverHorario(index)} aria-label={`Remover horário ${nomeDiaSemana(h.diaSemana)} ${h.abreAs}`}>
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Stack>
            ))}
            {horarios.length === 0 && (
              <Typography variant="body2" color="text.secondary">Nenhum horário cadastrado.</Typography>
            )}
          </Stack>

          <Grid container spacing={2} sx={{ alignItems: "flex-start" }}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField
                select
                label="Dia da semana"
                value={novoDiaSemana}
                onChange={(e) => setNovoDiaSemana(Number(e.target.value))}
                size="small"
                fullWidth
              >
                {DIAS_SEMANA.map((d) => (
                  <MenuItem key={d.value} value={d.value}>{d.label}</MenuItem>
                ))}
              </TextField>
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <TextField
                label="Abre às"
                type="time"
                value={novoAbreAs}
                onChange={(e) => setNovoAbreAs(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <TextField
                label="Fecha às"
                type="time"
                value={novoFechaAs}
                onChange={(e) => setNovoFechaAs(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 2 }}>
              <Button
                startIcon={<AddIcon />}
                onClick={handleAdicionarHorario}
                disabled={!novoAbreAs || !novoFechaAs}
                fullWidth
              >
                Adicionar horário
              </Button>
            </Grid>
            {horarioError && (
              <Grid size={{ xs: 12 }}>
                <Typography variant="caption" color="error">{horarioError}</Typography>
              </Grid>
            )}
          </Grid>
        </CardContent>
      </Card>

      <Card sx={{ border: "1px solid", borderColor: "divider", mb: 2 }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
            Políticas públicas
          </Typography>

          <Stack spacing={1} sx={{ mb: 2 }}>
            {politicas.map((p, index) => (
              <Stack key={`${p.chave}-${index}`} direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Chip label={p.chave} size="small" />
                <Typography variant="body2">{p.valor}</Typography>
                <IconButton size="small" onClick={() => handleRemoverPolitica(index)} aria-label={`Remover política ${p.chave}`}>
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Stack>
            ))}
            {politicas.length === 0 && (
              <Typography variant="body2" color="text.secondary">Nenhuma política cadastrada.</Typography>
            )}
          </Stack>

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField
                label="Nome da política"
                value={novaPoliticaChave}
                onChange={(e) => setNovaPoliticaChave(e.target.value)}
                size="small"
                fullWidth
                placeholder="Ex: cancelamento"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <TextField
                label="Descrição"
                value={novaPoliticaValor}
                onChange={(e) => setNovaPoliticaValor(e.target.value)}
                size="small"
                fullWidth
                placeholder="Ex: cancelamento com 24h de antecedência"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 2 }}>
              <Button
                startIcon={<AddIcon />}
                onClick={handleAdicionarPolitica}
                disabled={!novaPoliticaChave.trim() || !novaPoliticaValor.trim()}
                fullWidth
              >
                Adicionar política
              </Button>
            </Grid>
          </Grid>
        </CardContent>
      </Card>

      <Card sx={{ border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <FormControlLabel
            control={<Switch checked={publicado} onChange={(e) => setPublicado(e.target.checked)} />}
            label="Publicado (visível ao agente conversacional)"
          />
          {publicadoBloqueado && (
            <Typography variant="caption" color="error" sx={{ display: "block" }}>
              Preencha o nome fantasia para publicar o perfil.
            </Typography>
          )}

          <Divider sx={{ my: 2 }} />

          <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              variant="contained"
              onClick={handleSalvar}
              disabled={saving || publicadoBloqueado}
            >
              Salvar
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
