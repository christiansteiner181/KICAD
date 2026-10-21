// Statusanzeige «Brille» in der Statusleiste (Modul XR, «Brille automatisch verbinden», Christian 21.10.2026: «ist die brillen Mac gespeichert, könnte man nach jedem
// programmstart neu suchen. automatisiert oder ein icon ohne manuellen eingriff»). Sichtbar nur, wenn die Benutzereinstellung quest.automatisch an ist oder schon eine
// Brille gemerkt ist. grün = bereit, gelb = sucht, grau = nicht gefunden. Klick = ein Schritt mit Ping-Durchlauf, Rechtsklick öffnet den Dialog «Quest verbinden».
//
// Die Oberfläche ruft den Schritt (POST /api/quest/automatik) auf: beim Start, beim Öffnen einer 3D-Ansicht und, solange die Brille nicht grün ist, alle 10 s (Anlass «takt»:
// der Server liest dann nur «adb devices» und die Nachbartabelle, nie ein Ping-Durchlauf). Die Entscheidungen stehen in quest-text.js; hier nur Verdrahtung. Keine Logik der
// Brille: alles macht der Server (QuestDienst.cs). Ist die Automatik aus, läuft hier nichts im Hintergrund.

import { senden } from '../api.js';
import { abonnieren, element } from '../zustand.js';
import { benutzerLaden, benutzerWert } from './benutzer.js';
import * as T from './quest-text.js';

let knopf = null;
let antwort = null;        // letzte Antwort des Servers (oder null)
let laeuft = false;
let automatisch = false;
let brillen = [];
let timer = null;

const sichtbar = () => T.statusSichtbar(automatisch, brillen);

function zeichnen() {
  if (!knopf) return;
  knopf.hidden = !sichtbar();
  if (knopf.hidden) return;
  const a = T.brilleAnzeige(antwort, laeuft, automatisch);
  knopf.className = `sl-brille ${a.stufe}`;
  knopf.dataset.stufe = a.stufe;
  knopf.textContent = `● ${a.text}`;
  knopf.title = a.titel;
}

/** Ein Schritt: nur wenn erlaubt (Automatik an, oder ausdrücklicher Klick) und keiner läuft. */
export async function questSchritt(anlass) {
  if (laeuft || !T.schrittErlaubt(anlass, automatisch)) return null;
  laeuft = true;
  zeichnen();
  try {
    antwort = await senden('/api/quest/automatik', { anlass });
  } catch (e) {
    antwort = { status: 'fehler', meldung: e?.message || 'Der Schritt ist fehlgeschlagen.', hinweis: e?.vorschlag || null };
  } finally {
    laeuft = false;
  }
  if (antwort?.brille && brillen.length === 0) await aktualisieren(false);   // die Brille wurde eben gemerkt: Sichtbarkeit nachziehen
  zeichnen();
  return antwort;
}

async function aktualisieren(anstossen) {
  await benutzerLaden();
  const vorher = automatisch;
  automatisch = benutzerWert(T.AUTOMATIK_SCHLUESSEL, false) === true;
  brillen = T.brillenListe(benutzerWert(T.BRILLEN_SCHLUESSEL, null));
  zeichnen();
  if (anstossen && automatisch && !vorher) questSchritt('start');
}

/** Für den Dialog: nach einer Änderung der Einstellungen neu lesen; wird die Automatik eben eingeschaltet, folgt ein Schritt. */
export const questStatusAktualisieren = () => aktualisieren(true);

/** Verdrahtet den Knopf der Statusleiste (versteckt, bis die Einstellungen gelesen sind). */
export function questStatusEinrichten(k) {
  knopf = k;
  knopf.hidden = true;
  knopf.onclick = () => questSchritt('klick');
  knopf.oncontextmenu = (e) => {
    e.preventDefault();
    import('./quest-dialog.js').then((m) => m.questDialogOeffnen());
  };
  abonnieren('ansicht', (kz) => { if (automatisch && element(kz)?.werte?.art === '3d') questSchritt('ansicht3d'); });
  clearInterval(timer);
  timer = setInterval(() => { if (T.taktNoetig({ automatisch, antwort, laeuft })) questSchritt('takt'); }, T.TAKT_MS);
  aktualisieren(true);
}
