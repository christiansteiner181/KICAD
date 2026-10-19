# KI-Verstaendnistest (Abnahme Modul 0a)
# Eine KI ohne Vorwissen bekommt nur die MCP-Werkzeuge des HLKS-CAD und beantwortet 10 Fragen zum Beispielprojekt.
# Die richtigen Antworten rechnet der Kern selbst; das Ergebnis landet in ki-test\ergebnisse\.
#
# Aufruf:  powershell -ExecutionPolicy Bypass -File ki-test\ki_test.ps1 [-Modell claude-sonnet-5-5]
# (Datei bewusst nur ASCII: PowerShell 5.1 liest Skripte ohne BOM sonst falsch.)

param(
  [string]$Modell = 'claude-sonnet-5-5',
  [int]$ZeitlimitMinuten = 20
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8   # Ausgaben von Hlks.Mcp sind UTF-8 (Umlaute)
$root =(Resolve-Path "$PSScriptRoot\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$stempel = Get-Date -Format 'yyyyMMdd_HHmmss'
$buildRoot = "$env:USERPROFILE\HLKS_CAD_build\kitest"
$env:HLKS_BUILD = $buildRoot
$arbeit = "$buildRoot\lauf_$stempel"                       # ausserhalb von OneDrive: keine CLAUDE.md, kein Gedaechtnis
$ae = [char]0x00E4
$projekt = "$arbeit\projekt\c_Pl${ae}ne und Modelle\e_KICAD"  # wie auf dem NAS, mit Umlaut und Leerzeichen
$katalog = "$root\kataloge"
New-Item -ItemType Directory -Force $arbeit | Out-Null

Write-Host "1. Bauen"
& $dotnet build "$root\src\Hlks.Mcp\Hlks.Mcp.csproj" -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen" }
$dll = "$buildRoot\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll"

function Hlks([string[]]$a) {
  # stdout lesen; stderr nicht umleiten (PowerShell 5.1 macht daraus sonst einen Abbruch)
  $o = & $dotnet $dll @a
  if ($LASTEXITCODE -gt 2) { throw "Hlks.Mcp $($a -join ' ') endete mit Code $LASTEXITCODE" }
  return ($o -join "`n")
}

Write-Host "2. Beispielprojekt anlegen: $projekt"
Hlks @('--beispiel', $projekt, '--katalog', $katalog) | Out-Null

Write-Host "3. Auftrag und MCP-Konfiguration"
$auftrag = Hlks @('--ki-auftrag')
[IO.File]::WriteAllText("$arbeit\prompt.txt", $auftrag, [Text.UTF8Encoding]::new($false))
$cfg = @{ mcpServers = @{ hlks = @{ command = $dotnet; args = @($dll, '--projekt', $projekt, '--akteur', 'KI-Test', '--art', 'ki', '--geraet', 'kitest', '--katalog', $katalog) } } } |
  ConvertTo-Json -Depth 6
[IO.File]::WriteAllText("$arbeit\mcp.json", $cfg, [Text.UTF8Encoding]::new($false))

Write-Host "4. KI fragen ($Modell)"
$claude = Get-ChildItem "$env:APPDATA\Claude\claude-code" -Recurse -Filter claude.exe |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $claude) { throw "claude.exe nicht gefunden" }
$env:CLAUDECODE = $null
$env:MAX_MCP_OUTPUT_TOKENS = '60000'
$argumente = "-p --mcp-config `"$arbeit\mcp.json`" --strict-mcp-config --tools `"`" --allowedTools mcp__hlks " +
  "--permission-mode dontAsk --model $Modell --output-format json --no-session-persistence"
$start = Get-Date
$p = Start-Process -FilePath $claude -ArgumentList $argumente -WorkingDirectory $arbeit -WindowStyle Hidden -PassThru `
  -RedirectStandardInput "$arbeit\prompt.txt" -RedirectStandardOutput "$arbeit\ausgabe.json" -RedirectStandardError "$arbeit\stderr.txt"
if (-not $p.WaitForExit($ZeitlimitMinuten * 60000)) { $p.Kill(); throw "Zeitlimit ueberschritten" }
$dauer = [int]((Get-Date) - $start).TotalSeconds

Write-Host "5. Antwort auswerten"
$roh = [IO.File]::ReadAllText("$arbeit\ausgabe.json", [Text.Encoding]::UTF8)
$aus = $roh | ConvertFrom-Json
$text = [string]$aus.result
$m = [regex]::Match($text, '```json\s*(\{[\s\S]*\})\s*```')
$json = if ($m.Success) { $m.Groups[1].Value } else { $i = $text.IndexOf('{'); $j = $text.LastIndexOf('}'); if ($i -ge 0 -and $j -gt $i) { $text.Substring($i, $j - $i + 1) } else { '{}' } }
[IO.File]::WriteAllText("$arbeit\antwort.json", $json, [Text.UTF8Encoding]::new($false))
$bewertung = Hlks @('--projekt', $projekt, '--katalog', $katalog, '--akteur', 'Pruefer', '--ki-bewerten', "$arbeit\antwort.json")
[IO.File]::WriteAllText("$arbeit\bewertung.json", $bewertung, [Text.UTF8Encoding]::new($false))
$b = $bewertung | ConvertFrom-Json

$ziel = "$root\ki-test\ergebnisse"
New-Item -ItemType Directory -Force $ziel | Out-Null
$zusammenfassung = [ordered]@{
  zeit = (Get-Date -Format 's'); modell = $Modell; richtig = $b.richtig; fragen = $b.fragen; bestanden = $b.bestanden
  dauer_s = $dauer; runden = $aus.num_turns; kosten_usd = $aus.total_cost_usd; arbeitsordner = $arbeit
}
$ergebnis = [ordered]@{ zusammenfassung = $zusammenfassung; bewertung = $b; antworttext = $text }
[IO.File]::WriteAllText("$ziel\${stempel}_$Modell.json", ($ergebnis | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host ("Ergebnis: {0} von {1} richtig, bestanden: {2}, Dauer {3} s, Runden {4}" -f $b.richtig, $b.fragen, $b.bestanden, $dauer, $aus.num_turns)
foreach ($e in $b.ergebnisse) { Write-Host ("  {0} {1}" -f $e.frage, $(if ($e.richtig) { 'richtig' } else { 'FALSCH' })) }
Write-Host "Bericht: $ziel\${stempel}_$Modell.json"
