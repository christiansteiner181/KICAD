// Bestimmung der «aktiven Anlage» und der Listenansicht dazu (Modul 4 Teil A-O) – rein, ohne Browser, in Node getestet (sanitaer-belastung.test.mjs).
// Aktive Anlage in dieser Reihenfolge: (1) die Anlage des gewählten Elements – die Anlage selbst, sonst über system und teilVon aufwärts bis zur Klasse Anlage;
// (2) die Anlage der aktiven Ansicht, wenn es eine Sanitär-Zusammenstellung ist (Eigenschaft anlage); (3) die einzige Anlage des Projekts; sonst null (der Aufrufer fragt).

const lc = (t) => String(t ?? '').toLowerCase();

/** Anlage des Elements: aufwärts über system und teilVon (höchstens 8 Schritte, gegen Kreise); null, wenn keine erreicht wird. */
export function anlageVon(e, element) {
  let x = e;
  for (let i = 0; i < 8 && x; i++) {
    if (x.klasse === 'Anlage') return x;
    x = element(x.system) ?? element(x.teilVon) ?? null;
  }
  return null;
}

/**
 * ctx = { auswahl (Element oder null), aktiveAnsicht (Element oder null), anlagen ([Anlage]), element (kz|id → Element) }
 * → Anlage oder null (mehrere Anlagen, keine bestimmbar)
 */
export function aktiveAnlage({ auswahl, aktiveAnsicht, anlagen, element }) {
  const ausAuswahl = anlageVon(auswahl, element);
  if (ausAuswahl) return ausAuswahl;
  const w = aktiveAnsicht?.werte ?? {};
  if (aktiveAnsicht?.klasse === 'Ansicht' && w.art === 'liste' && w.liste === 'sanitaer_zusammenstellung' && w.anlage) {
    const a = (anlagen ?? []).find((x) => lc(x.kennzeichen) === lc(w.anlage));
    if (a) return a;
  }
  return (anlagen ?? []).length === 1 ? anlagen[0] : null;
}

/** Vorhandene Listenansicht (keine Vorlage) der Sanitär-Zusammenstellung für die Anlage (Kennzeichen); null, wenn es keine gibt. */
export function listeFuerAnlage(ansichten, anlageKz) {
  const liste = (ansichten ?? []).filter((a) => a.werte?.art === 'liste' && a.werte?.liste === 'sanitaer_zusammenstellung' && a.werte?.vorlage !== true);
  return liste.find((a) => lc(a.werte?.anlage) === lc(anlageKz)) ?? null;
}

/** Kennzeichen- und Namensvorschlag der neuen Liste (gleich wie im Dialog «Liste anlegen»). */
export const listenKennzeichen = (anlage) => `AN-LI-SANITAER-${anlage.kennzeichen}`;
export const listenName = (anlage) => `Sanitär-Zusammenstellung (W3) – ${anlage.kennzeichen}`;
