# HLKS-CAD Modul 2 (Auftrag B3): richtet occt-import-js 0.0.23 (LGPL-2.1) fuer den STEP/IGES-Leser ein.
# Installiert mit "npm ci" genau die Fassung aus package-lock.json nach %USERPROFILE%\HLKS_CAD_build\step\
# (node_modules nie in OneDrive). Die Lizenztexte des Pakets bleiben im Installationsordner
# (node_modules\occt-import-js\LICENSE.md, dist\license.occt-import-js.txt, dist\license.occt.txt).
# Nur ASCII, laeuft unter Windows PowerShell 5.1.
#
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\step\einrichten.ps1 [-Ziel <ordner>]

param([string]$Ziel = (Join-Path $env:USERPROFILE 'HLKS_CAD_build\step'))

$ErrorActionPreference = 'Stop'
$quelle = $PSScriptRoot

$node = Get-Command node.exe -ErrorAction SilentlyContinue
if (-not $node) { throw 'Node.js fehlt. Node.js (v18 oder neuer) von https://nodejs.org installieren und das Skript erneut ausfuehren.' }
# npm.cmd statt npm.ps1: die Ausfuehrungsrichtlinie sperrt npm.ps1 unter PowerShell 5.1 oft.
$npm = Get-Command npm.cmd -ErrorAction SilentlyContinue
if (-not $npm) { throw 'npm fehlt (gehoert zu Node.js). Node.js neu installieren.' }

New-Item -ItemType Directory -Force -Path $Ziel | Out-Null
Copy-Item -LiteralPath (Join-Path $quelle 'package.json') -Destination $Ziel -Force
Copy-Item -LiteralPath (Join-Path $quelle 'package-lock.json') -Destination $Ziel -Force

Push-Location $Ziel
try {
    & $npm.Source ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw "npm ci ist fehlgeschlagen (Exit-Code $LASTEXITCODE)." }
}
finally { Pop-Location }

$modul = Join-Path $Ziel 'node_modules\occt-import-js'
if (-not (Test-Path -LiteralPath (Join-Path $modul 'dist\occt-import-js.wasm'))) { throw "occt-import-js ist unvollstaendig: dist\occt-import-js.wasm fehlt in $modul" }
if (-not (Test-Path -LiteralPath (Join-Path $modul 'LICENSE.md'))) { throw "Lizenztext LICENSE.md fehlt in $modul" }
$version = (Get-Content -LiteralPath (Join-Path $modul 'package.json') -Raw | ConvertFrom-Json).version
if ($version -ne '0.0.23') { throw "Falsche Fassung von occt-import-js: $version (erwartet 0.0.23)." }

# Probelauf: Paket laden wie der Leser
$probe = & $node.Source (Join-Path $quelle 'step_lesen.mjs') --pruefen --paket $Ziel
if ($LASTEXITCODE -ne 0) { throw "Probelauf fehlgeschlagen: $probe" }
Write-Output "occt-import-js $version eingerichtet in $Ziel"
Write-Output $probe
