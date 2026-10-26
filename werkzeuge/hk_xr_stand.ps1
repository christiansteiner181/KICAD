# HK_XR fuer KICAD (Umzug U3, 26.10.2026; Christian: "das xr labor [soll] nichts mit KICAD zutun haben", "fuer jedes VR Modul [ist] HKXR die VR app").
# KICAD verweist auf die Bibliothek HkXr.Verbindung im Projekt HK_XR (nur lokal, Claude_Code\HK_XR, kein GitHub). Welcher Stand von HK_XR zu einem
# KICAD-Stand gehoert, steht in extern\hk_xr.commit. Pruefung und Desktop bauen gegen eine eigene Arbeitskopie von HK_XR auf genau diesem Stand,
# nie gegen den Ordner, in dem die HK_XR-Sitzung arbeitet.
# Eingebunden mit ". werkzeuge\hk_xr_stand.ps1" (stand_pruefen.ps1, kicad_starten.ps1). Datei bewusst nur ASCII (PowerShell 5.1).

# Bereitet die Arbeitskopie $ziel auf dem Stand aus <kicad>\extern\hk_xr.commit vor und setzt HLKS_HKXR (MSBuild HkXrOrdner) und HK_XR_BUILD.
# Gibt den Stand (SHA) zurueck, $null fuer KICAD-Staende vor U3 (Datei fehlt). Wirft bei Fehlern (Repository oder Stand fehlt).
function HkXr-Bereitstellen([string]$kicad, [string]$ziel, [string]$bau) {
  $datei = Join-Path $kicad 'extern\hk_xr.commit'
  if (-not (Test-Path $datei)) { Remove-Item Env:HLKS_HKXR -ErrorAction SilentlyContinue; return $null }
  $sha = ([string](Get-Content $datei -TotalCount 1)).Trim()
  if ($sha -notmatch '^[0-9a-f]{40}$') { throw "extern\hk_xr.commit enthaelt keinen vollstaendigen Commit: $sha" }
  $quelle = (Resolve-Path (Join-Path $PSScriptRoot '..\..\HK_XR')).Path
  if (-not (Test-Path "$quelle\.git")) { throw "HK_XR fehlt: $quelle" }
  $o = & git.exe -C $quelle cat-file -t $sha 2>&1
  if ($LASTEXITCODE -ne 0) { throw "Stand $sha fehlt in HK_XR ($quelle)" }
  if (-not (Test-Path "$ziel\.git")) {
    if (Test-Path $ziel) { Remove-Item $ziel -Recurse -Force }
    $o = & git.exe -C $quelle worktree add --detach $ziel $sha 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git worktree add ${ziel}: $o" }
  } else {
    $ist = ([string](& git.exe -C $ziel rev-parse HEAD 2>$null)).Trim()
    if ($ist -ne $sha) {
      $o = & git.exe -C $ziel checkout -q --detach $sha 2>&1
      if ($LASTEXITCODE -ne 0) { throw "git checkout $sha in ${ziel}: $o" }
    }
    & git.exe -C $ziel reset -q --hard $sha 2>&1 | Out-Null
    & git.exe -C $ziel clean -qfd 2>&1 | Out-Null
  }
  $env:HLKS_HKXR = $ziel
  $env:HK_XR_BUILD = $bau
  return $sha
}
