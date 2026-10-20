# Server fuer die Grundausstattung Rohr- und Kanalserien (Hauptlauf, 20.10.2026)
# Quelle: Arbeitskopie %USERPROFILE%\HLKS_CAD_grundausstattung (6e4e75b), Build grundausstattung_bin, Port 5153.
# Familienbibliothek und Warteschlange lokal: nie W: (Uebernahme in die Buerobibliothek macht Christian).
$wt = "$env:USERPROFILE\HLKS_CAD_grundausstattung"
$ga = "$env:USERPROFILE\HLKS_CAD_build\grundausstattung"
$bau = "$env:USERPROFILE\HLKS_CAD_build\grundausstattung_bin"
New-Item -ItemType Directory -Force "$ga\bibliothek", "$ga\ausstehend", "$ga\zuletzt" | Out-Null
$env:HLKS_BUILD = $bau
$env:HLKS_WEBROOT = "$wt\src\Hlks.Web\wwwroot"
$env:HLKS_KATALOG = "$wt\kataloge"
$env:HLKS_KI = "$wt\ki"
$env:HLKS_MCP_DLL = "$bau\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll"
$env:HLKS_FAMILIENBIBLIOTHEK = "$ga\bibliothek"
$env:HLKS_BIBLIOTHEK_AUSSTEHEND = "$ga\ausstehend"
$env:HLKS_ZULETZT = "$ga\zuletzt"
$env:HLKS_KI_ZEITLIMIT_MIN = '90'
Remove-Item Env:HLKS_TEST -ErrorAction SilentlyContinue
Remove-Item Env:HLKS_CLAUDE_EXE -ErrorAction SilentlyContinue
$a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$wt\werkzeuge\start.ps1", '-Port', '5153', '-Akteur', 'CS', '-Geraet', 'grundausstattung', '-OhneBau')
if (Test-Path "$ga\e_KICAD") { $a += @('-Projekt', "$ga\e_KICAD") }
& powershell @a
