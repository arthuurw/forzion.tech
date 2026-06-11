export function proximaCobranca(hoje: Date = new Date()): string {
  // setMonth(+1) faz overflow em meses curtos (31/jan → 03/mar); clampa ao último dia do mês alvo.
  const alvo = new Date(hoje.getFullYear(), hoje.getMonth() + 1, 1);
  const ultimoDia = new Date(alvo.getFullYear(), alvo.getMonth() + 1, 0).getDate();
  alvo.setDate(Math.min(hoje.getDate(), ultimoDia));
  return alvo.toLocaleDateString("pt-BR");
}
