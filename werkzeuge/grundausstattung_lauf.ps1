# Ablauf der Grundausstattung ausserhalb der Claude-Sitzung (mit werkzeuge\losgeloest.ps1 starten):
# 1. laufenden KI-Auftrag auf Port 5153 abwarten, 2. den Server dort beenden und losgeloest neu starten
# (werkzeuge\grundausstattung_server.ps1), 3. die Auftraege nacheinander (werkzeuge\grundausstattung_auftraege.ps1 -Nur ...).
# Datei bewusst nur ASCII (PowerShell 5.1).
param([string]$Nur = '', [int]$Port = 5153)
$ErrorActionPreference = 'Continue'
$hier = $PSScriptRoot
$ga = "$env:USERPROFILE\HLKS_CAD_build\grundausstattung"
$log = "$ga\lauf.log"
function Log($t) { Add-Content -Path $log -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" -Encoding UTF8 }
$u = "http://localhost:$Port"

Log "Ablauf gestartet (Nur: $Nur)"
# 1. laufenden Auftrag abwarten
do {
  $laeuft = $false
  try { $l = Invoke-RestMethod "$u/api/auftraege" -TimeoutSec 10; $laeuft = @($l.auftraege | Where-Object { $_.status -in @('wartet', 'laeuft') }).Count -gt 0 } catch { }
  if ($laeuft) { Start-Sleep 30 }
} while ($laeuft)
Log 'kein Auftrag mehr aktiv'

# 2. Server neu starten (losgeloest)
$c = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
if ($c) { Stop-Process -Id $c.OwningProcess -Force; Log "Server PID $($c.OwningProcess) beendet"; Start-Sleep 3 }
$spid = & powershell -NoProfile -ExecutionPolicy Bypass -File "$hier\losgeloest.ps1" -Skript "$hier\grundausstattung_server.ps1" -Log "$ga\server.log"
Log "Server losgeloest gestartet (PID $spid)"
$ok = $false
for ($i = 0; $i -lt 90 -and -not $ok; $i++) { try { $s = Invoke-RestMethod "$u/api/status" -TimeoutSec 5; $ok = $s.offen } catch { Start-Sleep 2 } }
if (-not $ok) { Log 'Server antwortet nicht - Abbruch'; exit 1 }
Log "Server bereit: $($s.projekt)"

# 3. Auftraege
& powershell -NoProfile -ExecutionPolicy Bypass -File "$hier\grundausstattung_auftraege.ps1" -Nur $Nur -Port $Port
Log 'Ablauf fertig'
