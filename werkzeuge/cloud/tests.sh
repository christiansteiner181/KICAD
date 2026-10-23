#!/usr/bin/env bash
# Tests in Cloud-Sitzungen (Linux), Gegenstück zu werkzeuge/stand_pruefen.ps1: gleiche Umgebungsvariablen, gleiche Testprojekte und
# Node-Tests, aber im ausgecheckten Stand und ohne Zweige oder Marken zu setzen. «geprueft» setzt nur der Hauptlauf am Windows-PC.
# Browserreihen (*.browser.mjs) und die PowerShell-Werkzeuge laufen hier nicht; Echt-Tests überspringen sich ohne echte Dateien.
#
# Aufruf:
#   bash werkzeuge/cloud/tests.sh                         alles (kern, mcp, web, xr, node)
#   bash werkzeuge/cloud/tests.sh kern node               nur diese Teile
#   bash werkzeuge/cloud/tests.sh kern -- --filter "FullyQualifiedName~Stuetze"    weitere Argumente an dotnet test
# Exit 0 = grün, 1 = rot, 2 = Aufruffehler. Ergebnisse (trx) unter $HLKS_BUILD/ergebnisse.
set -uo pipefail
repo="$(cd "$(dirname "$0")/../.." && pwd)"
export HLKS_BUILD="${HLKS_BUILD:-/tmp/hlks_build}"
export HLKS_KATALOG="$repo/kataloge" HLKS_KI="$repo/ki" HLKS_WEBROOT="$repo/src/Hlks.Web/wwwroot" HLKS_TEST=1
unset HLKS_FAMILIENBIBLIOTHEK HLKS_BIBLIOTHEK_AUSSTEHEND HLKS_PROJEKTABLAGE HLKS_ZULETZT HLKS_BENUTZER HLKS_ANLEITUNG \
  HLKS_ANLEITUNG_PRUEFUNG HLKS_NORMEN HLKS_MCP_DLL
export HLKS_TEST_SERVER="" HLKS_TESTSERVER=""   # nie einen fremden Server abfragen
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
[ -f /etc/profile.d/dotnet.sh ] && . /etc/profile.d/dotnet.sh

teile=(); extra=()
while [ $# -gt 0 ]; do
  case "$1" in --) shift; extra=("$@"); break ;; *) teile+=("$1"); shift ;; esac
done
[ ${#teile[@]} -eq 0 ] && teile=(kern mcp web xr node)
erg="$HLKS_BUILD/ergebnisse"; mkdir -p "$erg"
rot=0
for t in "${teile[@]}"; do
  case "$t" in
    kern) p=Hlks.Kern.Tests ;;
    mcp) p=Hlks.Mcp.Tests ;;
    web) p=Hlks.Web.Tests ;;
    xr) p=Hlks.Xr.Tests ;;
    node)
      mapfile -t dateien < <(find "$repo/src/Hlks.Web/wwwroot/js" -name '*.test.mjs' | sort)
      echo "== Node (${#dateien[@]} Dateien)"
      aus=$(node --test --test-reporter=tap "${dateien[@]}" 2>&1)
      echo "$aus" | grep -E '^not ok' | head -n 40
      echo "$aus" | grep -E '^# (tests|pass|fail|skipped) '
      echo "$aus" | grep -qE '^# fail 0$' || rot=1
      continue ;;
    *) echo "Unbekannter Teil: $t (kern | mcp | web | xr | node)"; exit 2 ;;
  esac
  echo "== $p"
  dotnet test "$repo/tests/$p" --nologo --logger "trx;LogFileName=$p.trx" --results-directory "$erg" "${extra[@]}" || rot=1
done
if [ $rot -eq 0 ]; then echo "GRUEN"; else echo "ROT"; fi
exit $rot
