export const REPLAY_DENYLIST = ["/admin/saude", "/cadastro/aluno"] as const;

export function isReplayDenied(pathname: string): boolean {
  return REPLAY_DENYLIST.some((d) => {
    if (!pathname.startsWith(d)) return false;
    const next = pathname.charAt(d.length);
    return next === "" || next === "/" || next === "?" || next === "#";
  });
}
