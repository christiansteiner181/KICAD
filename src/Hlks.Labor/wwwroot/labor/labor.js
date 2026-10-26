// Labor-Seite (XR-Labor L0): Bereich «Brille» mit dem Teil «Quest App» aus KICAD (Netzzugang, App installieren, Kopplung, Ersatzkopplung), mit der Marke des
// Labors, und die Teilnehmer im Kanal. Keine Logik hier: alles läuft über /api/xr/… und /api/quest/app/… des Labor-Servers (LaborDienste.cs).
// Die Module aus KICAD (../js/…) liefert der Labor-Server aus src/Hlks.Web/wwwroot aus (LaborOberflaeche.cs) – dieselben Dateien wie in KICAD.

import { holen } from '../js/api.js';
import { questAppTeil } from '../js/oberflaeche/quest-app-dialog.js';
import { LABOR_MARKE } from './labor-marke.js';
import { TEILNEHMER_TAKT_MS, questAusApp, standText, teilnehmerZeilen } from './labor-text.js';

let app = null;   // letzte Antwort von GET /api/quest/app (für den Zustand «Brille per adb»)

const teil = questAppTeil({ quest: () => questAusApp(app), beiGelesen: (a) => { app = a; }, marke: LABOR_MARKE });
document.getElementById('labor-quest-app').appendChild(teil.knoten);
teil.zustandNeu({ netzNeu: true });

async function stand() {
  try { document.getElementById('labor-stand').textContent = standText(await holen('/api/status')); }
  catch { document.getElementById('labor-stand').textContent = standText(null); }
}

let teilnehmerStand = '';
async function teilnehmer() {
  let zeilen;
  try { zeilen = teilnehmerZeilen(await holen('/api/xr/teilnehmer')); } catch { return; }
  const neu = JSON.stringify(zeilen);
  if (neu === teilnehmerStand) return;
  teilnehmerStand = neu;
  const ziel = document.getElementById('labor-teilnehmer');
  ziel.replaceChildren();
  if (zeilen.length === 0) {
    const leer = document.createElement('div');
    leer.className = 'leise';
    leer.textContent = 'Keine Brille verbunden. Die App «XR-Labor» verbindet sich, sobald der Netzzugang an und die Brille gekoppelt ist.';
    ziel.appendChild(leer);
    return;
  }
  for (const z of zeilen) {
    const d = document.createElement('div');
    d.className = 'labor-teilnehmer-zeile';
    d.dataset.id = z.id;
    d.textContent = z.text;   // Namen aus dem Netz nur als Text
    ziel.appendChild(d);
  }
}

stand();
teilnehmer();
setInterval(teilnehmer, TEILNEHMER_TAKT_MS);
