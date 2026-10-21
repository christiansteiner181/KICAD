// «An die Brille senden» (Modul XR, Christian Steiner 21.10.2026: «ev. ist es einfacher die am pc eingestellten befehle … vom VR gerät aufzurufen»): setzt den VR-Auftrag des Servers
// (POST /api/vr/auftrag, VrAuftrag.cs); die VR-Startseite /vr in der Quest liest ihn und führt ihn aus. Eine Ansichtsfunktion ohne Fachlogik: der Auftrag ändert das Modell nicht.
// Aufrufer: Kontextmenü eines Raums im Grundriss (aktionen-vr-auftrag.js) und der Knopf «An die Brille senden» in der 3D-Ansicht (ansichten/vr3d.js, dynamischer Import).

import { senden } from '../api.js';
import { element } from '../zustand.js';
import { meldung } from './dialoge.js';

/** Text der Rückmeldung zu einem Auftrag: «Begehen · EG · E.01 Wohnen/Küche» bzw. «Sitzungsraum · 1.OG». */
export function auftragBeschreibung(modus, raum, geschoss) {
  if (modus === 'sitzungsraum') return ['Sitzungsraum', geschoss?.kennzeichen].filter(Boolean).join(' · ');
  const name = [raum?.werte?.nummer, raum?.name].filter(Boolean).join(' ') || raum?.kennzeichen;
  return ['Begehen', geschoss?.kennzeichen, name].filter(Boolean).join(' · ');
}

/** Setzt den Auftrag. par: { modus, raum?, geschoss? } mit Elementen (Id oder Kennzeichen). Meldet das Ergebnis; true bei Erfolg. */
export async function auftragSenden({ modus, raum = null, geschoss = null }) {
  const r = raum ? element(raum) : null;
  const g = geschoss ? element(geschoss) : r?.geschoss ? element(r.geschoss) : null;
  const koerper = { modus };
  if (r && modus === 'begehen') koerper.raum = r.id;
  if (modus === 'sitzungsraum' && g) koerper.geschoss = g.id;
  try {
    await senden('/api/vr/auftrag', koerper);
    meldung(`Auftrag an die Brille: ${auftragBeschreibung(modus, r, g)} – in der Quest KICAD VR öffnen.`, 'ok');
    return true;
  } catch (e) { meldung(e.message ?? String(e), 'warnung', { vorschlag: e.vorschlag }); return false; }
}

/** 3D-Ansicht: läuft das Begehen, geht dessen Raum an die Brille (Begehen), sonst die 3D-Gesamtansicht als Sitzungsraum (Modell auf dem Tisch). */
export function anBrilleSenden() {
  const w = document.querySelector('.kachel.aktiv .d3-wurzel[data-begehen]') ?? document.querySelector('.d3-wurzel[data-begehen]');
  let raum = null;
  try { raum = w?.dataset.begehen ? JSON.parse(w.dataset.begehen).raum ?? null : null; } catch { /* kein Raum */ }
  return raum ? auftragSenden({ modus: 'begehen', raum }) : auftragSenden({ modus: 'sitzungsraum' });
}
