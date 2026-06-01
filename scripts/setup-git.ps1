# scripts/setup-git.ps1 — aplica configs git recomendadas para este repo (solo dev).
# Idempotente: re-executar é seguro. Reflete o que está documentado em
# specs/specification-git.md.
#
# Uso:
#   .\scripts\setup-git.ps1            # aplica em --global (default)
#   .\scripts\setup-git.ps1 -Local     # aplica em --local (só este clone)
#
# Após rodar, valide:
#   git config --get push.autoSetupRemote   # → true
#   git config --get push.default           # → current

param([switch]$Local)

$Scope = if ($Local) { "--local" } else { "--global" }

# core.autocrlf=true alinha com o pre-commit dotnet format ENDOFLINE check (Windows).
# Em POSIX (setup-git.sh), o equivalente é "input".
$configs = @(
    @("init.defaultBranch",   "main"),
    @("push.autoSetupRemote", "true"),
    @("push.default",         "current"),
    @("pull.rebase",          "true"),
    @("rebase.autoStash",     "true"),
    @("fetch.prune",          "true"),
    @("core.autocrlf",        "true")
)

Write-Output "Aplicando configs git em $Scope..."
Write-Output ""

foreach ($cfg in $configs) {
    $key, $value = $cfg
    & git config $Scope $key $value
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Falha ao setar $key=$value"
        exit 1
    }
    Write-Output "[OK] $key = $value"
}

Write-Output ""
Write-Output "Configs aplicadas. Veja specs/specification-git.md para racional e troubleshooting."
Write-Output ""
Write-Output "Validação rápida:"
Write-Output "  git config --get push.autoSetupRemote"
Write-Output "  git config --get push.default"
Write-Output "  git config --get pull.rebase"
