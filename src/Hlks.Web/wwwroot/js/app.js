// Start der Oberfläche: Status holen, Projekt (falls offen) und Katalog laden, Teile einrichten, Ereignisstrom
// verbinden, zuletzt offene Ansichten wiederherstellen. Ohne offenes Projekt erscheint der Startbildschirm (oberflaeche/startbildschirm.js).

import { holen, abfrage } from './api.js';
import { zustand, melden, modellLaden, derKlasse } from './zustand.js';
import { verbinden } from './ereignisse.js';
import * as fenster from './fenster.js';
import * as menueband from './oberflaeche/menueband.js';
import * as hilfe from './oberflaeche/hilfe-oberflaeche.js';
import * as zweitfenster from './oberflaeche/zweitfenster.js';
import * as projektbrowser from './oberflaeche/projektbrowser.js';
import * as eigenschaften from './oberflaeche/eigenschaften.js';
import * as statusleiste from './oberflaeche/statusleiste.js';
import * as kuerzel from './oberflaeche/kuerzel.js';
import * as dialoge from './oberflaeche/dialoge.js';
import * as startbildschirm from './oberflaeche/startbildschirm.js';
import * as vorschau from './oberflaeche/vorschau.js';
import * as paletten from './oberflaeche/paletten.js';
import { designEinrichten } from './oberflaeche/design.js';

async function start() {
  if (zweitfenster.istZweitfenster()) zweitfensterKopf();   // vor allem anderen: Modus «nur Ansicht» (W2)
  designEinrichten();   // Design (hell/dunkel) steht schon durch design-start.js; hier nur absichern
  paletten.einrichten();   // Eigenschaften, Projektbrowser (und bei Bedarf Koordination) in ihre Bereiche setzen (W1); die Paletten richten sich dabei selbst ein
  fenster.einrichten();
  menueband.einrichten(document.getElementById('menueband'), document.getElementById('optionsleiste'), document.getElementById('schnellzugriff'));
  hilfe.einrichten();   // Hilfe-Symbol «?» in der Kopfleiste und F1 (Bedienungsanleitung); nach dem Kopf des Zweitfensters
  statusleiste.einrichten(document.getElementById('statusleiste'));
  kuerzel.einrichten();
  verbinden();

  // Startbildschirm (S1): ohne offenes Projekt der Start; mit ?start=1 (Desktop-Icon) liegt er über dem offenen Projekt
  startbildschirm.einrichten();
  vorschau.einrichten();
  const zweit = zweitfenster.istZweitfenster();   // eigenes Fenster einer Ansicht (W2): ?ansicht=…&fenster=2, Modus «nur Ansicht»
  const startVerlangt = !zweit && new URLSearchParams(location.search).get('start') === '1';
  if (startVerlangt) { try { history.replaceState(null, '', location.pathname); } catch { /* egal */ } }   // ein Neuladen zeigt ihn nicht erneut

  zustand.status = await holen('/api/status');
  melden('status', zustand.status);
  if (!zustand.status.offen) { if (zweit) { try { window.close(); } catch { /* egal */ } return; } await startbildschirm.zeigen(); return; }
  if (startVerlangt) startbildschirm.zeigen().catch((e) => console.error('Startbildschirm', e));   // nicht warten: das Projekt lädt dahinter
  await projektLaden();
}

/** Lädt Katalog und Modell des offenen Projekts und stellt die Ansichten wieder her. */
export async function projektLaden() {
  zustand.geladen = zustand.status?.ort ?? null;
  zustand.katalog = await abfrage('katalog');
  await modellLaden();
  document.getElementById('titel').textContent = `HLKS-CAD – ${zustand.status.projekt ?? ''}`;
  const geoeffnet = await fenster.wiederherstellen();   // gemerkte Anordnung des Projekts (W2); im eigenen Fenster die Ansicht aus der Adresse
  if (geoeffnet === 0 && !zweitfenster.istZweitfenster()) {
    const erste = derKlasse('Ansicht').filter(a => a.werte?.art === 'grundriss' && !a.werte?.vorlage)
      .sort((a, b) => a.kennzeichen.localeCompare(b.kennzeichen))[0];
    if (erste) await fenster.ansichtOeffnen(erste.kennzeichen);
  }
  if (!zweitfenster.istZweitfenster()) vorschau.nachLaden();   // 3D-Vorschaubild für den Startbildschirm (höchstens alle 10 min, nur bei geändertem Stand)
}

/** Eigenes Fenster: Knopf «Zurück ins Hauptfenster» im Kopf. */
function zweitfensterKopf() {
  document.body.dataset.fenster = String(zweitfenster.fensterNummer());   // blendet Menüband und Seitenleiste aus (css/anordnung.css)
  const ort = document.getElementById('kopf-rechts');
  if (!ort) return;
  const b = document.createElement('button');
  b.type = 'button'; b.className = 'ag-zurueck'; b.textContent = '↩ Zurück ins Hauptfenster';
  b.title = 'Die Ansicht wieder im Hauptfenster öffnen und dieses Fenster schliessen';
  b.onclick = () => fenster.zurueckInsHauptfenster();
  ort.appendChild(b);
}

window.addEventListener('error', (e) => console.error(e.error ?? e.message));
start().catch((e) => {
  console.error(e);
  document.getElementById('ansichtsflaeche').innerHTML = `<div class="leer-hinweis">Start fehlgeschlagen: ${e.message}</div>`;
});
