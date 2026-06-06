#!/usr/bin/env bash
# Avalia thresholds de cobertura a partir de UM ReportGenerator JsonSummary (Summary.json),
# sem re-rodar os testes. Substitui as multiplas execucoes de `dotnet test` que so existiam
# para aplicar /p:Threshold diferentes por assembly.
#
# Uso:
#   check-coverage.sh <Summary.json> <CHECK>...
# CHECK = "<Assembly>:<metric>=<thr>[,<metric>=<thr>...]"  ou  "__overall__:branch=50"
#   <Assembly>: nome exato do assembly no relatorio, ou __overall__ para o total.
#   <metric>: line | branch | method   (thr = percentual minimo)
# Exemplos:
#   check-coverage.sh Summary.json "forzion.tech.Domain:branch=75,line=85,method=85"
#   check-coverage.sh Summary.json "__overall__:branch=50" "forzion.tech.Infrastructure:branch=35"
#
# Exit: 0 = todos OK; 1 = alguma metrica abaixo do threshold; 2 = erro de uso/relatorio.
set -euo pipefail

SUMMARY="${1:?uso: check-coverage.sh <Summary.json> <check>...}"
shift
[ -f "$SUMMARY" ] || { echo "::error::Summary.json nao encontrado: $SUMMARY"; exit 2; }
command -v jq >/dev/null || { echo "::error::jq nao encontrado"; exit 2; }

fail=0

# Retorna "covered total" do assembly (ou __overall__) para os campos cov/tot dados,
# ou "MISSING" se o assembly nao existe no relatorio.
get_pair() {
  local asm="$1" cov="$2" tot="$3"
  if [ "$asm" = "__overall__" ]; then
    jq -r --arg c "$cov" --arg t "$tot" '"\(.summary[$c]) \(.summary[$t])"' "$SUMMARY"
  else
    jq -r --arg n "$asm" --arg c "$cov" --arg t "$tot" '
      [.coverage.assemblies[] | select(.name == $n)] as $m
      | if ($m | length) == 0 then "MISSING" else "\($m[0][$c]) \($m[0][$t])" end' "$SUMMARY"
  fi
}

for check in "$@"; do
  asm="${check%%:*}"
  spec="${check#*:}"
  IFS=',' read -ra parts <<< "$spec"
  for p in "${parts[@]}"; do
    metric="${p%%=*}"
    thr="${p#*=}"
    case "$metric" in
      line)   cov=coveredlines;    tot=coverablelines;;
      branch) cov=coveredbranches; tot=totalbranches;;
      method) cov=coveredmethods;  tot=totalmethods;;
      *) echo "::error::metrica invalida '$metric' em '$check'"; exit 2;;
    esac
    pair="$(get_pair "$asm" "$cov" "$tot")"
    if [ "$pair" = "MISSING" ]; then
      echo "::error::assembly nao encontrado no relatorio: $asm"
      fail=1; continue
    fi
    read -r c t <<< "$pair"
    # total 0 (ex.: assembly sem branches) -> 100% para nao falsear FAIL.
    actual="$(awk -v c="$c" -v t="$t" 'BEGIN{ if (t+0==0) printf "100.00"; else printf "%.2f",(c/t)*100 }')"
    ok="$(awk -v a="$actual" -v thr="$thr" 'BEGIN{ print (a+0 >= thr+0) ? 1 : 0 }')"
    if [ "$ok" = "1" ]; then
      printf '  OK   %-30s %-7s %7s%% >= %s%%\n' "$asm" "$metric" "$actual" "$thr"
    else
      printf '  FAIL %-30s %-7s %7s%% <  %s%%\n' "$asm" "$metric" "$actual" "$thr"
      fail=1
    fi
  done
done

if [ "$fail" = "0" ]; then
  echo "Cobertura: todos os thresholds atingidos."
else
  echo "::error::Cobertura abaixo do threshold (ver FAIL acima)."
fi
exit "$fail"
