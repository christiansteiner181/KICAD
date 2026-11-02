# Server fuer die Herstellerbibliothek (entwurf/herstellerbibliothek.md, 02.11.2026): KI-Auftraege «Familie aus Unterlagen»
# fuer Pumpen, Ventile, Antriebe und Klappen. Quelle: Arbeitskopie der Sitzung Herstellerbibliothek (Parameter -Arbeitskopie),
# Build %USERPROFILE%\HLKS_CAD_build\herstellerbibliothek, Port 5154, Arbeitsprojekt lokal.
# Familienbibliothek und Warteschlange lokal: nie W: (Uebernahme in die Buerobibliothek macht Christian nach der Freigabe).
# Mit werkzeuge\losgeloest.ps1 starten (absoluter Pfad). Datei bewusst nur ASCII (PowerShell 5.1).
param([Parameter(Mandatory = $true)][string]$Arbeitskopie, [int]$Port = 5154)
$wt = $Arbeitskopie
$hb = "$env:USERPROFILE\HLKS_CAD_build\herstellerbibliothek_ki"
$bau = "$env:USERPROFILE\HLKS_CAD_build\herstellerbibliothek"
New-Item -ItemType Directory -Force "$hb\bibliothek", "$hb\ausstehend", "$hb\zuletzt" | Out-Null
$env:HLKS_BUILD = $bau
$env:HLKS_HKXR = "$env:USERPROFILE\HK_XR_fuer_kicad"
$env:HLKS_WEBROOT = "$wt\src\Hlks.Web\wwwroot"
$env:HLKS_KATALOG = "$wt\kataloge"
$env:HLKS_KI = "$wt\ki"
$env:HLKS_MCP_DLL = "$bau\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll"
$env:HLKS_FAMILIENBIBLIOTHEK = "$hb\bibliothek"
$env:HLKS_BIBLIOTHEK_AUSSTEHEND = "$hb\ausstehend"
$env:HLKS_ZULETZT = "$hb\zuletzt"
$env:HLKS_KI_ZEITLIMIT_MIN = '90'
Remove-Item Env:HLKS_TEST -ErrorAction SilentlyContinue
Remove-Item Env:HLKS_CLAUDE_EXE -ErrorAction SilentlyContinue
$a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$wt\werkzeuge\start.ps1", '-Port', "$Port", '-Akteur', 'CS', '-Geraet', 'herstellerbibliothek', '-OhneBau')
if (Test-Path "$hb\e_KICAD\projekt.json") { $a += @('-Projekt', "$hb\e_KICAD") }
& powershell @a
