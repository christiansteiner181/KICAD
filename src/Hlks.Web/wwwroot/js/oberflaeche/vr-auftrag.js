// «An die Brille senden» (Modul XR, Christian Steiner 21.10.2026: «ev. ist es einfacher die am pc eingestellten befehle … vom VR gerät aufzurufen»): setzt den VR-Auftrag des Servers
// (POST /api/vr/auftrag, VrAuftrag.cs); die VR-Startseite /vr in der Quest liest ihn und führt ihn aus. Eine Ansichtsfunktion ohne Fachlogik: der Auftrag ändert das Modell nicht.
// Aufrufer: Kontextmenü eines Raums im Grundriss (aktionen-vr-auftrag.js) und der Knopf «An die Brille senden» in der 3D-Ansicht (ansichten/vr3d.js, dynamischer Import).

import { senden } from '../api.js';
import { element, zustand } from '../zustand.js';
import { meldung } from './dialoge.js';
import { benutzerLaden, benutzerWert } from './benutzer.js';
import * as T from './quest-text.js';

/** Text der Rückmeldung zu einem Auftrag: «Begehen · EG · E.01 Wohnen/Küche» bzw. «Sitzungsraum · 1.OG». */
export function auftragBeschreibung(modus, raum, geschoss, ausschnitt = false, ansicht = null) {
  if (modus === 'sitzungsraum' && ansicht) return ['Sitzungsraum', '3D-Ansicht', ansicht.name || ansicht.kennzeichen].filter(Boolean).join(' · ');
  if (modus === 'sitzungsraum' && ausschnitt) return ['Raum zeigen', geschoss?.kennzeichen, [raum?.werte?.nummer, raum?.name].filter(Boolean).join(' ') || raum?.kennzeichen].filter(Boolean).join(' · ');
  if (modus === 'sitzungsraum') return ['Sitzungsraum', geschoss?.kennzeichen].filter(Boolean).join(' · ');
  const name = [raum?.werte?.nummer, raum?.name].filter(Boolean).join(' ') || raum?.kennzeichen;
  return ['Begehen', geschoss?.kennzeichen, name].filter(Boolean).join(' · ');
}

/**
 * Setzt den Auftrag. par: { modus, raum?, geschoss?, ausschnitt?, ansicht? } mit Elementen (Id oder Kennzeichen). Meldet das Ergebnis; true bei Erfolg.
 * ausschnitt (Sitzungsraum mit Raum): «Raum zeigen (VR)» – auf dem Tisch nur der Raum plus Rand; ansicht (Sitzungsraum): eine 3D-Ansicht, mit Schnittbereich und Filtern.
 */
export async function auftragSenden({ modus, raum = null, geschoss = null, ausschnitt = false, ansicht = null }) {
  const r = raum ? element(raum) : null;
  const an = ansicht ? element(ansicht) : null;
  const g = ausschnitt || an ? null : geschoss ? element(geschoss) : r?.geschoss ? element(r.geschoss) : null;   // ein Ausschnitt oder eine 3D-Ansicht ersetzt die Etagenwahl
  const koerper = { modus };
  if (r && (modus === 'begehen' || ausschnitt)) koerper.raum = r.id;
  if (ausschnitt && modus === 'sitzungsraum' && r) koerper.ausschnitt = true;
  if (an && modus === 'sitzungsraum') koerper.ansicht = an.id;
  if (modus === 'sitzungsraum' && g) koerper.geschoss = g.id;
  // Begehung beim Aufsetzen automatisch starten (QuestStarter.cs): nur bei eingeschalteter Einstellung quest.autostart (Vorgabe ja); der Server entscheidet am Ende
  try { await benutzerLaden(); } catch { /* ohne Einstellungen gilt die Vorgabe */ }
  if (T.autostartAn(benutzerWert(T.AUTOSTART_SCHLUESSEL, true))) koerper.autostart = true;
  try {
    const antwort = await senden('/api/vr/auftrag', koerper);
    if (antwort?.autostart === 'gestartet') {
      meldung(`Auftrag an die Brille: ${auftragBeschreibung(modus, r, g ?? (r?.geschoss ? element(r.geschoss) : null), ausschnitt, an)} – Brille aufsetzen, die Begehung startet automatisch.`, 'ok');
      import('./quest-status.js').then((m) => m.autostartVerfolgen()).catch(() => { /* die Anzeige ist nur eine Zugabe */ });
    } else {
      meldung(`Auftrag an die Brille: ${auftragBeschreibung(modus, r, g ?? (r?.geschoss ? element(r.geschoss) : null), ausschnitt, an)} – in der Quest KICAD VR öffnen.`, 'ok');
    }
    return true;
  } catch (e) { meldung(e.message ?? String(e), 'warnung', { vorschlag: e.vorschlag }); return false; }
}

/**
 * Die 3D-Ansicht, in der der Knopf «In VR begehen» geklickt wurde: die aktive Ansicht, wenn sie eine 3D-Ansicht ist (der Klick macht sie aktiv), sonst null.
 * Christian Steiner 21.10.2026: «die 3dAnzeige fenster auch in VR möglich machen.»
 */
export function aktive3dAnsicht() {
  const e = zustand.aktiveAnsicht ? element(zustand.aktiveAnsicht) : null;
  return e?.klasse === 'Ansicht' && e.werte?.art === '3d' ? e : null;
}

/**
 * 3D-Ansicht: läuft das Begehen, geht dessen Raum an die Brille (Begehen); sonst die 3D-Ansicht als Sitzungsraum (Modell auf dem Tisch, mit dem Schnittbereich und den Filtern
 * dieser Ansicht), ohne aktive 3D-Ansicht das ganze Modell.
 */
export function anBrilleSenden() {
  const w = document.querySelector('.kachel.aktiv .d3-wurzel[data-begehen]') ?? document.querySelector('.d3-wurzel[data-begehen]');
  let raum = null;
  try { raum = w?.dataset.begehen ? JSON.parse(w.dataset.begehen).raum ?? null : null; } catch { /* kein Raum */ }
  if (raum) return auftragSenden({ modus: 'begehen', raum });
  const an = aktive3dAnsicht();
  return an ? auftragSenden({ modus: 'sitzungsraum', ansicht: an.id }) : auftragSenden({ modus: 'sitzungsraum' });
}
