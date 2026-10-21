# Abnahmeserver 5150-5153 starten, wenn sie nicht laufen (Christian 21.10.2026: "das KICAD-Icon soll die fehlenden
# Abnahmeserver mitstarten"). Anlass: Nach einem Neustart des Rechners (21.10.2026 00:09) liefen sie nicht mehr, bis jemand sie
# von Hand startete. kicad_starten.ps1 ruft dieses Skript bei jedem Klick auf das Icon losgeloest auf; es blockiert den
# Desktop-Start nicht.
# Je Server: laeuft der Port schon, passiert nichts. Fehlt der Build, wird nichts gebaut (Abnahmestaende bleiben, wie sie sind),
# nur protokolliert. Protokoll: %USERPROFILE%\HLKS_CAD_build\losgeloest_logs\abnahmeserver.log, Ausgabe je Server server_<Port>.log.
# Aufruf: powershell -File werkzeuge\abnahmeserver_starten.ps1 [-Nur 5150,5153] [-Liste]
# Datei bewusst nur ASCII (PowerShell 5.1).
param([string]$Nur = '', [switch]$Liste)
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
$b = "$env:USERPROFILE\HLKS_CAD_build"
$logs = "$b\losgeloest_logs"
New-Item -ItemType Directory -Force $logs | Out-Null
$log = "$logs\abnahmeserver.log"
function Log([string]$t) { Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $t) -Encoding UTF8 }

# Abnahmeserver wie bisher gestartet (Abnahmen 3, 3b, 3c, Grundausstattung der Leitungsserien)
$server = @(
  @{ Port = 5150; Name = 'Abnahme 3 (Bernstrasse, Leitungen)'; Bau = "$b\abn3"; Skript = "$repo\werkzeuge\start.ps1"
     Args = @('-Port', '5150', '-Akteur', 'CS', '-Geraet', 'abnahme3', '-OhneBau', '-Projekt', "`"$b\abnahme3\e_KICAD`"") },
  @{ Port = 5151; Name = 'Abnahme 3b (Kollision, Aussparungen)'; Bau = "$b\abn3b"; Skript = "$repo\werkzeuge\start.ps1"
     Args = @('-Port', '5151', '-Akteur', 'CS', '-Geraet', 'abnahme3b', '-OhneBau') },
  @{ Port = 5152; Name = 'Abnahme 3c (KI-Palette)'; Bau = "$b\endstand"; Skript = "$repo\werkzeuge\start.ps1"
     Args = @('-Port', '5152', '-Akteur', 'CS', '-Geraet', 'abnahme3c', '-OhneBau', '-Projekt', "`"$b\abnahme3c\e_KICAD`"") },
  @{ Port = 5153; Name = 'Grundausstattung Leitungsserien'; Bau = "$b\grundausstattung_bin"; Skript = "$repo\werkzeuge\grundausstattung_server.ps1"
     Args = @() }
)
$nurPorts = @($Nur -split '[,; ]+' | Where-Object { $_ } | ForEach-Object { [int]$_ })

foreach ($s in $server) {
  if ($nurPorts.Count -gt 0 -and $nurPorts -notcontains $s.Port) { continue }
  $laeuft = $null -ne (Get-NetTCPConnection -State Listen -LocalPort $s.Port -ErrorAction SilentlyContinue | Select-Object -First 1)
  $dll = "$($s.Bau)\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"
  if ($Liste) { "{0} {1,-40} laeuft={2} build={3}" -f $s.Port, $s.Name, $laeuft, (Test-Path $dll); continue }
  if ($laeuft) { continue }
  if (-not (Test-Path $dll)) { Log "$($s.Port) $($s.Name): Build fehlt ($dll) - nicht gestartet"; continue }
  # Befehl fuer einen eigenen, versteckten PowerShell-Prozess: Umgebung setzen (start.ps1 nimmt Build und MCP daraus), Skript
  # starten, Ausgabe ins Protokoll. Start-Process ohne Umleitung (ShellExecute) - so ueberlebte der Desktop-Server auch einen
  # Start aus einem WMI-Prozess (losgeloest.ps1), siehe dort.
  $zeilen = @('$ErrorActionPreference = ''Continue''')
  if ($s.Port -ne 5153) {
    $zeilen += ('$env:HLKS_BUILD = ''{0}''' -f $s.Bau)
    $zeilen += ('$env:HLKS_MCP_DLL = ''{0}\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll''' -f $s.Bau)
  }
  $zeilen += ("& '{0}' {1} *> '{2}\server_{3}.log'" -f $s.Skript, ($s.Args -join ' ').Replace('"', "'"), $logs, $s.Port)
  $enc = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes(($zeilen -join "`n")))
  try {
    $p = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-EncodedCommand', $enc) `
      -WindowStyle Hidden -WorkingDirectory (Split-Path $s.Skript) -PassThru
    Log "$($s.Port) $($s.Name): gestartet (PID $($p.Id))"
  } catch { Log "$($s.Port) $($s.Name): Start fehlgeschlagen - $($_.Exception.Message)" }
}
