// N12 – IFC: Original oder Nachbau als Unterlage zeigen, je eigene Farbe (transparent). Reine Funktionen ohne DOM und ohne three.js
// (in Node testbar); der Kern rechnet dieselben Regeln für Grundriss, Schnitt und Ansicht (Hlks.Kern/Grafik/IfcDarstellung.cs).
//
// Ansichtseigenschaft ifcDarstellung:
//   nachbau  (Vorgabe) die übernommenen Elemente plus das nicht übernommene Original (Hintergrund)
//   original das Original aller Produkte; die aus der Unterlage übernommenen Elemente entfallen in dieser Ansicht
//   beide    übereinander, zum Vergleichen
// Farben an der Unterlage: farbeOriginal + deckkraft (Original), farbeNachbau + deckkraftNachbau (Nachbau; leer = normale Darstellung).
// Die Einstellung gilt nur in der Darstellung «standard» und für Unterlagen, die angezeigt werden (anzeigen ≠ false).

export const MODI = Object.freeze(['nachbau', 'original', 'beide']);
export const MODUS_NAMEN = Object.freeze({
  nachbau: 'Nachbau (übernommene Elemente, dazu das nicht übernommene Original als Hintergrund)',
  original: 'Original (alle Produkte der IFC-Datei; der Nachbau entfällt in dieser Ansicht)',
  beide: 'Beide übereinander (zum Vergleichen)',
});
/** Vorgaben des Originals (wie der Kern: GrundlagenIfc.Farbe, VorgabeDeckkraft). */
export const ORIGINAL_FARBE = '#8a8a8a';
export const ORIGINAL_DECKKRAFT = 0.5;
/** Ansichtsarten, in denen die Einstellung wirkt. */
export const ARTEN = Object.freeze(['grundriss', 'schnitt', 'ansicht', '3d']);

const HEX = /^#[0-9a-fA-F]{6}$/;

/** Farbe #rrggbb (klein) oder null. */
export function gueltigeFarbe(wert) {
  return typeof wert === 'string' && HEX.test(wert.trim()) ? wert.trim().toLowerCase() : null;
}

/** Zahl 0…1 oder null. */
export function gueltigeDeckkraft(wert) {
  if (wert === null || wert === undefined || wert === '') return null;
  const n = Number(wert);
  return Number.isFinite(n) ? Math.min(Math.max(n, 0), 1) : null;
}

/** Darstellungsart der Ansicht; andere Darstellungen als «standard» (Hülle, Aussparungen …) und unbekannte Werte gelten als nachbau. */
export function ifcModus(ansicht) {
  const w = ansicht?.werte ?? {};
  if (w.darstellung && w.darstellung !== 'standard') return 'nachbau';
  return MODI.includes(w.ifcDarstellung) ? w.ifcDarstellung : 'nachbau';
}

/** Welche Produkte der Netzdatei zeigt das 3D? (leseNetz, Option nur): nachbau → nur den Hintergrund, sonst alle Produkte. */
export const netzAuswahl = (ansicht) => (ifcModus(ansicht) === 'nachbau' ? 'hintergrund' : 'alle');

/** Zeigt die Ansicht das Original (Netz aller Produkte)? */
export const zeigtOriginal = (ansicht) => ifcModus(ansicht) !== 'nachbau';

/** Die angezeigte IFC-Unterlage, aus der ein Element übernommen wurde (kennungen.quelle), sonst null. nachId(id) → Element oder undefined. */
export function ifcUnterlageVon(el, nachId) {
  const q = el?.kennungen?.quelle;
  if (!q) return null;
  const u = nachId(q);
  return u && u.klasse === 'Unterlage' && u.werte?.art === 'ifc' && u.werte?.anzeigen !== false ? u : null;
}

/** Entfällt das Element in dieser Ansicht, weil sie nur das Original zeigt? */
export const nachbauEntfaellt = (el, ansicht, nachId) => ifcModus(ansicht) === 'original' && ifcUnterlageVon(el, nachId) !== null;

/** Übersteuerung des Nachbaus für ein Element: { farbe, deckkraft } (je null, wo nichts gesetzt ist) oder null. */
export function nachbauStil(el, ansicht, nachId) {
  if (ansicht?.werte?.darstellung && ansicht.werte.darstellung !== 'standard') return null;
  const u = ifcUnterlageVon(el, nachId);
  if (!u) return null;
  const farbe = gueltigeFarbe(u.werte?.farbeNachbau), deckkraft = gueltigeDeckkraft(u.werte?.deckkraftNachbau);
  return farbe === null && deckkraft === null ? null : { farbe, deckkraft };
}

/** Werte des Originals einer Unterlage (mit Vorgaben). */
export function originalStil(u) {
  return { farbe: gueltigeFarbe(u?.werte?.farbeOriginal) ?? ORIGINAL_FARBE, deckkraft: gueltigeDeckkraft(u?.werte?.deckkraft) ?? ORIGINAL_DECKKRAFT };
}
