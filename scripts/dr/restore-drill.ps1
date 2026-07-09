# scripts/dr/restore-drill.ps1 — drill de restore do backup REAL (R2 -> age -d -> pg_restore).
# Prova ponta-a-ponta que o artefato .dump.age mais recente do R2 volta: download -> decifra com
# identity age OFFLINE do dono -> restaura num postgres:17 efêmero -> valida counts-âncora -> teardown.
# Runbook: specs/specification-dr.md §2. Segredos (identity, creds R2) só via param/env — nunca aqui.
#
# Uso:
#   $env:AWS_ACCESS_KEY_ID="..."; $env:AWS_SECRET_ACCESS_KEY="..."
#   .\scripts\dr\restore-drill.ps1 -Bucket <bucket> -AccountId <r2-account-id> -AgeIdentity C:\caminho\identity.txt
#
# Params opcionais: -Key (default: mais recente forzion-db-*.dump.age do bucket), -Port (default 55432).

param(
    [string]$Bucket = $env:R2_BUCKET,
    [string]$AccountId = $env:R2_ACCOUNT_ID,
    [Parameter(Mandatory = $true)][string]$AgeIdentity,
    [string]$Key,
    [int]$Port = 55432
)

$ErrorActionPreference = "Stop"
$ContainerName = "drill-pg17"

if (-not $Bucket) { throw "Bucket ausente (param -Bucket ou env R2_BUCKET)." }
if (-not $AccountId) { throw "AccountId ausente (param -AccountId ou env R2_ACCOUNT_ID)." }
if (-not (Test-Path $AgeIdentity)) { throw "AgeIdentity não encontrada: $AgeIdentity" }
if (-not $env:AWS_ACCESS_KEY_ID -or -not $env:AWS_SECRET_ACCESS_KEY) {
    throw "Creds R2 ausentes — sete AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY (escopo leitura no bucket) antes de rodar."
}
foreach ($bin in @("age", "aws", "docker")) {
    if (-not (Get-Command $bin -ErrorAction SilentlyContinue)) { throw "$bin não encontrado no PATH." }
}

# E5: R2 rejeita os checksums novos default do aws-cli v2.
$env:AWS_REQUEST_CHECKSUM_CALCULATION = "when_required"
$env:AWS_RESPONSE_CHECKSUM_VALIDATION = "when_required"
$Endpoint = "https://$AccountId.r2.cloudflarestorage.com"

if (-not $Key) {
    $listing = & aws s3 ls "s3://$Bucket/" --endpoint-url $Endpoint 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "ListBucket falhou (E6 — token pode ser object-scope sem List) — informe -Key explicitamente.`n$listing"
    }
    $Key = ($listing | Select-String 'forzion-db-.*\.dump\.age' |
        ForEach-Object { ($_ -split '\s+')[-1] } |
        Sort-Object | Select-Object -Last 1)
    if (-not $Key) { throw "Nenhum forzion-db-*.dump.age encontrado em s3://$Bucket/." }
}

$DownloadedFile = $Key
$DecryptedFile = "restore.dump"
$Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    Write-Output "[1/5] Download s3://$Bucket/$Key"
    & aws s3 cp "s3://$Bucket/$Key" $DownloadedFile --endpoint-url $Endpoint --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw "Download falhou." }

    Write-Output "[2/5] Decifrando com identity offline"
    & age -d -i $AgeIdentity -o $DecryptedFile $DownloadedFile
    if ($LASTEXITCODE -ne 0) { throw "age -d falhou (E1 — identity pode não ser a par de BACKUP_AGE_PUBLIC_KEY)." }

    Write-Output "[3/5] Subindo postgres:17 efêmero (porta $Port)"
    & docker run -d --name $ContainerName -e POSTGRES_PASSWORD=drill -p "${Port}:5432" postgres:17 | Out-Null
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        & docker exec $ContainerName pg_isready -U postgres 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw "postgres:17 não ficou pronto em 30s." }

    Write-Output "[4/5] Restaurando (--no-owner --no-privileges, via binário do próprio container — E3)"
    & docker cp $DecryptedFile "${ContainerName}:/restore.dump"
    & docker exec $ContainerName pg_restore -U postgres -d postgres --no-owner --no-privileges /restore.dump
    if ($LASTEXITCODE -gt 1) { throw "pg_restore falhou fatalmente (exit $LASTEXITCODE)." }

    Write-Output "[5/5] Validando counts-âncora (E4 — via stdin, identificador case-sensitive)"
    $migrations = ('SELECT count(*) FROM homolog."__EFMigrationsHistory";' | & docker exec -i $ContainerName psql -U postgres -d postgres -t).Trim()
    $contas = ('SELECT count(*) FROM homolog.contas;' | & docker exec -i $ContainerName psql -U postgres -d postgres -t).Trim()
    $tabelas = ("SELECT count(*) FROM information_schema.tables WHERE table_schema='homolog' AND table_type='BASE TABLE';" | & docker exec -i $ContainerName psql -U postgres -d postgres -t).Trim()

    $Stopwatch.Stop()
    $tempo = "{0:N1}s" -f $Stopwatch.Elapsed.TotalSeconds

    Write-Output ""
    Write-Output "Linha pronta pro DRILL LOG (specs/specification-dr.md §DRILL LOG):"
    Write-Output "| <data> | homolog · R2 real \`$Key\` (age -d) -> restore em postgres:17 | $migrations | $contas | $tabelas | $tempo | <preencher> | <operador> |"
}
finally {
    Write-Output "Teardown: removendo dump claro + artefato baixado + container"
    Remove-Item -Force -ErrorAction SilentlyContinue $DecryptedFile
    Remove-Item -Force -ErrorAction SilentlyContinue $DownloadedFile
    & docker rm -f $ContainerName 2>&1 | Out-Null
}
