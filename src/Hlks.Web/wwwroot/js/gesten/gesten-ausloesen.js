// Gesten am Arbeitsplatz (Modul G): führt die Schritt-Gesten aus (Ereignis { art: 'schritt', geste: 'G2' | 'G3' | 'G6', richtung }, Schnittstellen im Bau,
// entwurf/modul-gesten.md). Die Entscheidung steht rein in gesten-ausloesen-logik.js (schrittPlan); hier wird nur ausgeführt und danach eine kurze schwebende
// Meldung gezeigt («Reiter: 3D Lüftung»). Schritt-Gesten lösen nur Aktionen des Aktionsregisters aus, wie Tastenkürzel (nur aus der Liste BELEGBAR, gesten-text.js);
// die fliessenden Gesten (drehen, rollen, zoomen, verschieben) löst gesten.js selbst über navigieren() der aktiven Ansicht aus.
// Solange das Testfeld des Dialogs «Gesten» offen ist, löst nichts aus.
//
// Dieses Modul greift nicht auf das Dokument zu: die Umgebung (umg) wird übergeben – die echte steht in gesten-umgebung.js, Tests geben eine Attrappe.
//   umg.kontext()        → { dialog: null | { seiten, rollbar }, ansicht: null | { seiten, rollbar } }
//   umg.seite(schritt)   → { text } | null      blättert im obersten Dialog bzw. im aktiven Ansichtsfenster
//   umg.rollen(schritt)  → bool                 rollt eine Bildschirmseite im obersten Dialog bzw. im aktiven Ansichtsfenster
//   umg.aktion(id)       → Promise<{ ok, ergebnis, titel }>   führt die Aktion über das Register aus (ok = sie war verfügbar)
//   umg.meldung(text)    schwebende Kurzmeldung

import { schrittPlan, meldungText } from './gesten-ausloesen-logik.js';

/** Führt ein Ereignis aus. Ergebnis { plan, text } (für Tests); wirft nie. */
export async function schrittAusfuehren(ereignis, steuerung, umg) {
  let plan = { art: 'nichts', grund: 'fehler' };
  let text = null;
  try {
    plan = schrittPlan(ereignis, umg.kontext(), steuerung.einstellung(), { test: steuerung.istTest() });
    let info = {};
    if (plan.art === 'seite') {
      const r = umg.seite(plan.schritt);
      if (!r) return { plan: { art: 'nichts', grund: 'seitenende' }, text: null };
      info = r;
    } else if (plan.art === 'rollen') {
      info = { bewegt: umg.rollen(plan.schritt) };
    } else if (plan.art === 'aktion') {
      const r = await umg.aktion(plan.id);
      if (!r?.ok) return { plan, text: null };   // die Aktion war nicht verfügbar: das Register hat seinen Hinweis schon gezeigt
      info = { reiter: r.ergebnis?.titel, aktionTitel: r.titel };
      if (plan.id.startsWith('ansicht.reiter-') && !info.reiter) info = {};   // kein Reiter gewechselt: keine Meldung
    }
    text = meldungText(plan, info);
    if (text) umg.meldung(text);
  } catch (e) {
    console.error('Geste', e);
  }
  return { plan, text };
}

/** Meldet sich bei der Steuerung an und führt Schritt-Gesten aus. Gibt die Abmeldefunktion zurück. */
export function ausloesenEinrichten(steuerung, umg) {
  return steuerung.abonnieren((e) => { if (e?.art === 'schritt') schrittAusfuehren(e, steuerung, umg); });
}
