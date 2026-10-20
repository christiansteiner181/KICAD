// Koordinationsplan per Knopf (Modul 7 Teil B3): reine Bausteine der Oberfläche für die Knöpfe «Koordinationsplan» und «Aussparungsboxen» im Register «Koordination».
// Die Oberfläche rechnet und färbt nichts selbst: die Gewerkefarben, die Boxen und der Koordinationsplan selbst kommen vom Kern (Ansichtseigenschaften darstellung =
// koordination und aussparungsboxen, Grafik KoordinationGrafik/AussparungGrafik). Hier stehen nur der Kennzeichen- und Namensvorschlag, die Suche nach einem schon
// vorhandenen Koordinationsplan des Geschosses und die Parameter der Befehle element_anlegen und element_aendern. Node-testbar (koordinationsplan.test.mjs).

const klein = (t) => String(t ?? '').toLowerCase();

/** Wert der Ansichtseigenschaft darstellung für den Koordinationsplan (Katalog klassen.json, Ansicht.darstellung). */
export const DARSTELLUNG = 'koordination';
/** Massstab, wenn die aktive Ansicht keinen Grundriss-Massstab liefert (Vorgabe der Grundrisse im Programm). */
export const MASSSTAB_VORGABE = 50;
const MASSSTAEBE = [10, 20, 50, 100, 200, 500];

/** Kennzeichen des Koordinationsplans eines Geschosses, z. B. «AN-KO-EG». */
export const kennzeichen = (geschoss) => `AN-KO-${geschoss}`;
/** Name des Koordinationsplans eines Geschosses, z. B. «Koordination EG». */
export const name = (geschoss) => `Koordination ${geschoss}`;

const istGrundriss = (a) => klein(a?.werte?.art) === 'grundriss';
const istVorlage = (a) => a?.werte?.vorlage === true;

/**
 * Geschoss, für das der Knopf «Koordinationsplan» gilt: das Geschoss der aktiven Ansicht (Kennzeichen), sofern es unter den Geschossen vorkommt; sonst null.
 * aktiveAnsicht: Element der Klasse Ansicht (oder null), geschosse: Elemente der Klasse Geschoss.
 */
export function aktivesGeschoss(aktiveAnsicht, geschosse) {
  if (!aktiveAnsicht || aktiveAnsicht.klasse !== 'Ansicht' || istVorlage(aktiveAnsicht)) return null;
  const g = (geschosse ?? []).find((x) => klein(x.kennzeichen) === klein(aktiveAnsicht.geschoss));
  return g ? g.kennzeichen : null;
}

/** Hinweis, wenn der Knopf «Koordinationsplan» nicht möglich ist (sonst ''). */
export function planHinweis({ projektOffen, aktiveAnsicht, geschosse }) {
  if (!projektOffen) return 'Zuerst ein Projekt öffnen.';
  if ((geschosse ?? []).length === 0) return 'Im Modell gibt es noch kein Geschoss.';
  if (!aktiveAnsicht) return 'Zuerst einen Grundriss öffnen: der Koordinationsplan gilt für das Geschoss der aktiven Ansicht.';
  return aktivesGeschoss(aktiveAnsicht, geschosse) ? '' : 'Die aktive Ansicht gehört zu keinem Geschoss: zuerst einen Grundriss öffnen.';
}

/**
 * Vorhandener Koordinationsplan des Geschosses: Grundriss mit darstellung = koordination (Vorlagen zählen nicht). Gibt es mehrere, gilt der mit dem Standardkennzeichen,
 * sonst der erste nach Kennzeichen; sonst null.
 */
export function vorhandener(ansichten, geschoss) {
  const passend = (ansichten ?? []).filter((a) => a.klasse === 'Ansicht' && istGrundriss(a) && !istVorlage(a) && klein(a.werte?.darstellung) === DARSTELLUNG && klein(a.geschoss) === klein(geschoss));
  return passend.find((a) => klein(a.kennzeichen) === klein(kennzeichen(geschoss))) ?? passend.sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen), 'de', { numeric: true }))[0] ?? null;
}

/** Massstab des neuen Koordinationsplans: der des aktiven Grundrisses, sofern es ein üblicher Massstab ist, sonst 50. */
export function massstabFuer(aktiveAnsicht) {
  const m = Number(aktiveAnsicht?.werte?.massstab);
  return istGrundriss(aktiveAnsicht) && MASSSTAEBE.includes(m) ? m : MASSSTAB_VORGABE;
}

/**
 * Parameter von element_anlegen für den Koordinationsplan eines Geschosses: Grundriss mit darstellung = koordination und aussparungsboxen = true; alle Gewerke sichtbar
 * (die Eigenschaft gewerke bleibt leer). kz und anzeigeName kommen vom Aufrufer, wenn die Vorgaben schon vergeben sind.
 */
export function anlegenParameter({ geschoss, massstab = MASSSTAB_VORGABE, kz, anzeigeName }) {
  return {
    klasse: 'Ansicht', kennzeichen: kz ?? kennzeichen(geschoss), name: anzeigeName ?? name(geschoss), geschoss,
    eigenschaften: { art: 'grundriss', massstab: Number(massstab), detailgrad: 'mittel', darstellung: DARSTELLUNG, aussparungsboxen: true },
  };
}

// ---------------------------------------------------------------------------------------------- Schalter «Aussparungsboxen»

/** Zeichnet die Ansicht Aussparungsboxen? Grundriss mit Option aussparungsboxen oder im Koordinationsplan (dort immer); nie im Aussparungsplan (wie der Kern, AussparungGrafik.Boxen). */
export function boxenAn(a) {
  if (!a || !istGrundriss(a)) return false;
  const d = klein(a.werte?.darstellung);
  if (d === 'aussparungen') return false;
  return d === DARSTELLUNG || a.werte?.aussparungsboxen === true;
}

/** Hinweis, wenn der Schalter «Aussparungsboxen» nicht möglich ist (sonst ''). */
export function boxenHinweis(a) {
  if (!a || a.klasse !== 'Ansicht') return 'Zuerst einen Grundriss öffnen oder im Projektbrowser wählen.';
  if (!istGrundriss(a)) return 'Die Aussparungsboxen gibt es nur im Grundriss.';
  if (istVorlage(a)) return 'Eine Ansichtsvorlage hat keine Boxen: den Schalter an der Ansicht setzen.';
  const d = klein(a.werte?.darstellung);
  if (d === 'aussparungen') return 'Im Aussparungsplan gibt es keine Boxen (dort stehen die Massketten).';
  if (d === DARSTELLUNG) return 'Im Koordinationsplan sind die Aussparungsboxen immer eingeschaltet.';
  return '';
}

/** Parameter von element_aendern für den Schalter: ein → aussparungsboxen = true, aus → Eigenschaft entfernt (Vorgabe aus). */
export function boxenParameter(a) {
  return { element: a.kennzeichen, eigenschaften: { aussparungsboxen: a.werte?.aussparungsboxen === true ? null : true } };
}

/** Meldung nach dem Umschalten (der Zustand danach steht in der Ansicht vor dem Befehl: war sie an, ist sie jetzt aus). */
export function boxenMeldung(a) {
  const an = a.werte?.aussparungsboxen !== true;
  return `Aussparungsboxen ${an ? 'eingeschaltet' : 'ausgeschaltet'} (${a.name || a.kennzeichen}).`;
}
