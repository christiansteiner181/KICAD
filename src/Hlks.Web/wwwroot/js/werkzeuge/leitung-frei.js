// Freie Grösse (Kanalserien) und Abwasser-Serien (Nachtrag Modul 3, entwurf/freie-groesse-kanal.md und entwurf/abwasser-serie.md) –
// reine Logik ohne Browser, in Node getestet (frei-abwasser.test.mjs). Die Oberfläche rechnet nichts Fachliches:
//   - ob eine Grösse innerhalb von Grenzen und Schritt liegt, welcher Formteil-Typ passt und wie eine Leitung beschriftet wird, sagt der
//     Kern (Trockenlauf der Befehle, Prüfungen P29/P30, Abfrage leitungsserie);
//   - hier stehen nur das Lesen der Zahlenfelder, der Aufbau des Parameters «groesse» ({ b, h } bzw. { d } statt Typname), die leichte
//     Serie aus der Abfrage `familie` (siehe unten) und Texte zur Anzeige.
//
// Leichte Serie: Die Abfrage `leitungsserie` rechnet alle Grössen mit allen Formteilen (bei einer grossen freien Kanalserie bis 155 s).
// Für das Zeichnen und die Eigenschaften einer freien Serie genügt der Block `leitung` und die Werte der Typen aus der Abfrage `familie`
// (schnell): Grössen des Rasters (Vorschlag), `frei` (Vorlage, Grenzen, Schritt), Einsatz und Winkelregel. Die leichte Serie hat dieselbe
// Form wie die Antwort von `leitungsserie` (groessen, winkel, winkelRegel …), dazu `leicht: true`; Masse (aussen …), Formteilverfügbarkeit
// und Probe fehlen – die holt der Familieneditor auf Knopfdruck.

const zahlOK = (x) => typeof x === 'number' && Number.isFinite(x);

/** Zahl in der Schreibweise der Meldungen: ganze Zahl ohne Nachkommastellen, sonst bis 3 Stellen. */
export const zahlText = (x) => (zahlOK(x) ? String(Math.round(x * 1000) / 1000) : '');

// ------------------------------------------------------------------------------------------------ Eingabe

/**
 * Zahlenfeld lesen (Breite, Höhe, Durchmesser): Komma oder Punkt, grösser als 0.
 * → { leer: true } | { wert } | { fehler }. Grenzen und Schritt prüft der Kern, nicht diese Funktion.
 */
export function masseLesen(text, name = 'Wert') {
  const t = String(text ?? '').trim().replace(/\s*mm$/i, '').replace(',', '.');
  if (t === '') return { leer: true };
  if (!/^\d+(\.\d+)?$/.test(t)) return { fehler: `${name}: «${String(text).trim()}» ist keine Zahl (mm).` };
  const w = Number(t);
  if (!(w > 0)) return { fehler: `${name} muss grösser als 0 sein.` };
  return { wert: w };
}

// ------------------------------------------------------------------------------------------------ Leichte Serie (Abfrage familie)

/** Ist es eine freie Serie (leitung.frei)? */
export const istFrei = (serie) => !!serie?.frei;

/** Grössenschlüssel der Serie in der Reihenfolge des Blocks: ['b', 'h'] | ['d'] | ['dn']. */
export const groessenSchluessel = (serie) => Object.keys(serie?.groessenparameter ?? {});

/** Rechteckkanal (b × h)? */
export const istRechteck = (serie) => groessenSchluessel(serie).includes('b');

/** Abwasser-Serie nach Aussendurchmesser (Grösse d, Rohr)? */
export const nachAussendurchmesser = (serie) => !!serie?.nachAussendurchmesser;

/** Serie mit Einsatz «abwasser»? */
export const istAbwasser = (serie) => serie?.einsatz === 'abwasser';

function bereichText(g) {
  if (g && zahlOK(g.min) && zahlOK(g.max)) return `${zahlText(g.min)} … ${zahlText(g.max)} mm`;
  if (g && zahlOK(g.min)) return `ab ${zahlText(g.min)} mm`;
  if (g && zahlOK(g.max)) return `bis ${zahlText(g.max)} mm`;
  return 'jede Grösse > 0';
}

/** «frei b × h (145 … 2500 mm, Schritt 1 mm)» – wie der Text des Kerns (Leitungsserie.FreiText). */
export function freiText(frei, schluessel) {
  if (!frei) return null;
  const je = schluessel.map((k) => [k, bereichText(frei.grenzen?.[k])]);
  const gleich = new Set(je.map(([, b]) => b)).size === 1;
  const bereich = gleich ? je[0][1] : je.map(([k, b]) => `${k} ${b}`).join(', ');
  return `frei ${schluessel.includes('b') ? 'b × h' : 'd'} (${bereich}, Schritt ${zahlText(frei.schritt ?? 1)} mm)`;
}

/** Leichte Serie aus der Antwort der Abfrage `familie` (Definition + aufgelöste Werte je Typ) oder null (keine Leitungsserie). */
export function serieLeichtAus(f) {
  const l = f?.definition?.leitung;
  if (!l || typeof l !== 'object') return null;
  const gp = l.groesse ?? {};
  const schl = Object.keys(gp);
  const kategorie = f.kategorie ?? null;
  const groessen = (f.typen ?? []).map((t) => {
    const g = { typ: t.name, gueltig: t.gueltig !== false, aussen: null, innen: null, wand: null };
    for (const k of schl) { const w = t.werte?.[gp[k]]; g[k] = zahlOK(w) ? w : null; }
    if (schl.some((k) => g[k] === null)) g.gueltig = false;
    return g;
  });
  const w = l.regeln?.winkel;
  const frei = l.frei ? {
    vorlage: l.frei.vorlage, grenzen: l.frei.grenzen ?? {}, schritt: zahlOK(l.frei.schritt) ? l.frei.schritt : 1,
    quellen: Array.isArray(l.frei.quellen) ? l.frei.quellen : [],
  } : null;
  if (frei) frei.text = freiText(frei, schl);
  return {
    leicht: true, familie: f.kennzeichen, name: f.name, id: f.id, kategorie,
    querschnitt: l.querschnitt ?? null, kuerzel: l.kuerzel ?? null, einsatz: l.einsatz ?? null,
    nachAussendurchmesser: l.einsatz === 'abwasser' && schl.length === 1 && schl[0] === 'd',   // Grösse d gibt es bei Rohren nur mit Einsatz abwasser (Kern)
    groessenparameter: gp,
    winkel: Array.isArray(w) ? w : 'frei', winkelRegel: Array.isArray(w) ? 'liste' : w === 'frei' ? 'frei' : 'keine',
    groessen, frei, probe: null,
  };
}

// ------------------------------------------------------------------------------------------------ Grössen anzeigen

/** Text einer Grösse { b, h } | { d } | { dn } (auch ein Eintrag aus groessen): «430 × 270», «Ø 250», «d 110» (Abwasser-Serie), «DN 20». */
export function groesseText(g, serie = null) {
  if (!g) return '–';
  if (zahlOK(g.b) && zahlOK(g.h)) return `${zahlText(g.b)} × ${zahlText(g.h)}`;
  if (zahlOK(g.d)) return nachAussendurchmesser(serie) ? `d ${zahlText(g.d)}` : `Ø ${zahlText(g.d)}`;
  if (zahlOK(g.dn)) return `DN ${zahlText(g.dn)}`;
  return '–';
}

const gleich = (a, b) => zahlOK(a) && zahlOK(b) && Math.abs(a - b) <= 1e-6;
const gleicheGroesse = (g, h) => (zahlOK(g.b) || zahlOK(h.b) ? gleich(g.b, h.b) && gleich(g.h, h.h) : zahlOK(g.d) || zahlOK(h.d) ? gleich(g.d, h.d) : gleich(g.dn, h.dn));

/** Der Rastertyp (Normgrösse) mit genau dieser Grösse oder null. */
export function rasterTyp(serie, g) {
  return (serie?.groessen ?? []).find((r) => r.gueltig !== false && gleicheGroesse(r, g))?.typ ?? null;
}

/** Vorschlagsliste: die Rastertypen der Serie, nach Grösse sortiert. [{ typ, text, b, h, d }]. */
export function rasterVorschlag(serie) {
  return (serie?.groessen ?? []).filter((g) => g.gueltig !== false)
    .map((g) => ({ typ: g.typ, text: groesseText(g, serie), b: g.b ?? null, h: g.h ?? null, d: g.d ?? null }))
    .sort((a, b) => (a.b ?? a.d ?? 0) - (b.b ?? b.d ?? 0) || (a.h ?? 0) - (b.h ?? 0));
}

/** Optionen der Grössenwahl [[typ, Text]]: bei einer freien Serie «430 × 270» (Normgrössen als Vorschlag), bei einer Abwasser-Serie nach
 *  Aussendurchmesser «d 110» (der Typname folgt, wenn er anders heisst), sonst der Typname. */
export function groessenOptionen(serie) {
  if (istFrei(serie)) return rasterVorschlag(serie).map((r) => [r.typ, r.text]);
  return (serie?.groessen ?? []).filter((g) => g.gueltig !== false).map((g) => {
    if (!nachAussendurchmesser(serie) || !zahlOK(g.d)) return [g.typ, g.typ];
    const t = groesseText(g, serie);
    return [g.typ, g.typ === t ? t : `${t} (${g.typ})`];
  });
}

/** Vorbelegung der Zahlenfelder mit der Vorlage der freien Serie: { freiB, freiH } | { freiD } | null. */
export function vorbelegung(serie) {
  if (!istFrei(serie)) return null;
  const v = (serie.groessen ?? []).find((g) => g.typ === serie.frei.vorlage && g.gueltig !== false);
  if (!v) return null;
  return istRechteck(serie) ? { freiB: v.b, freiH: v.h } : { freiD: v.d };
}

/** Freie Grösse eines Elements (Kanal: breite/hoehe bzw. durchmesser aus werte) als { b, h } | { d } oder null. */
export function elementGroesse(serie, werte) {
  if (istRechteck(serie)) return zahlOK(werte?.breite) && zahlOK(werte?.hoehe) ? { b: werte.breite, h: werte.hoehe } : null;
  if (groessenSchluessel(serie).includes('d')) return zahlOK(werte?.durchmesser) ? { d: werte.durchmesser } : null;
  return null;
}

/**
 * Anzeige der Grösse einer Leitung mit freier Serie: { text, frei, typ }. Eine Rastergrösse bleibt ein normaler Typ («400 × 200»,
 * typ = Rastertyp, frei false); jede andere Grösse gilt als «frei» (typ = null). Ohne Grösse am Element: der Typ (familientyp).
 */
export function groesseAnzeige(serie, werte) {
  const g = elementGroesse(serie, werte);
  if (!g) return { text: werte?.familientyp ?? '–', frei: false, typ: werte?.familientyp ?? null };
  const typ = rasterTyp(serie, g);
  return { text: groesseText(g, serie), frei: typ === null, typ };
}

// ------------------------------------------------------------------------------------------------ Befehlsparameter

/** Die Felder der Optionsleiste für die Grösse einer freien Serie: [opt-Schlüssel …]. */
export const freiFelder = (serie) => (istRechteck(serie) ? ['freiB', 'freiH'] : ['freiD']);

/** Werte der Zahlenfelder (Optionsleiste) aus den Eigenschaften einer bestehenden Leitung, beim Start eines Abzweigs: [[feld, wert]]. */
export function abzweigGroesse(werte) {
  const z = (x) => (zahlOK(x) && x > 0 ? x : null);
  return [['freiB', z(werte?.breite)], ['freiH', z(werte?.hoehe)], ['freiD', z(werte?.durchmesser)]];
}

/** Fehlende oder ungültige Angabe der freien Grösse vor dem ersten Klick: { feld, text, vorschlag } oder null. */
export function freiPflichtFehlt(serie, opt) {
  if (!istFrei(serie)) return null;
  const namen = { freiB: 'Breite', freiH: 'Höhe', freiD: 'Durchmesser' };
  for (const f of freiFelder(serie)) {
    if (!zahlOK(opt?.[f]) || !(opt[f] > 0))
      return { feld: f, text: `${namen[f]} der Leitung fehlt.`, vorschlag: `In der Optionsleiste ${istRechteck(serie) ? 'Breite und Höhe' : 'den Durchmesser'} in mm eintragen oder eine Normgrösse wählen (${serie.frei.text ?? 'freie Grösse'}).` };
  }
  return null;
}

/** Parameter «groesse» der Befehle aus der Optionsleiste: bei einer freien Serie { b, h } bzw. { d } (der Kern nimmt bei einer Rastergrösse den Typ),
 *  sonst der Typname; null, wenn die Angabe fehlt. */
export function groesseAusOptionen(serie, opt) {
  if (istFrei(serie)) {
    if (freiPflichtFehlt(serie, opt)) return null;
    return istRechteck(serie) ? { b: opt.freiB, h: opt.freiH } : { d: opt.freiD };
  }
  return opt?.groesse ? opt.groesse : null;
}

/** Eingabe der Palette (Texte der Felder) → Parameter «groesse» für leitung_groesse: { groesse } | { fehler }. */
export function freiEingabe(serie, texte) {
  const felder = istRechteck(serie) ? [['b', 'Breite'], ['h', 'Höhe']] : [['d', 'Durchmesser']];
  const g = {};
  for (const [k, name] of felder) {
    const r = masseLesen(texte?.[k], name);
    if (r.leer) return { fehler: `${name} fehlt – ${istRechteck(serie) ? 'Breite und Höhe' : 'der Durchmesser'} in mm eintragen.` };
    if (r.fehler) return { fehler: r.fehler };
    g[k] = r.wert;
  }
  return { groesse: g };
}

// ------------------------------------------------------------------------------------------------ Texte

/** Hinweis zur freien Grösse für die Optionsleiste und die Palette (Grenzen und Schritt; geprüft wird im Kern). */
export function freiHinweis(serie) {
  if (!istFrei(serie)) return '';
  return `${serie.frei.text ?? 'freie Grösse'} – die Normgrössen sind Vorschläge; der Kern prüft Grenzen und Schritt.`;
}

/** Hinweis zur Abwasser-Serie: Grösse und Beschriftung nach Aussendurchmesser. */
export function abwasserHinweis(serie, beschriftung = null) {
  if (!istAbwasser(serie)) return '';
  return `Abwasser-Serie: Grösse nach Aussendurchmesser${beschriftung ? `, Beschriftung «${beschriftung}»` : ''}; nur für Schmutz- und Regenwasser.`;
}

/** Beschriftung (Kern) der Grösse mit diesem Typ aus der Antwort leitungsserie oder null (leichte Serie hat sie nicht). */
export const beschriftungVon = (serie, typ) => serie?.groessen?.find((g) => g.typ === typ)?.beschriftung ?? null;

/** Prüfbefunde der freien Grösse und der Abwasser-Serie (P29, P30) eines Elements aus der Liste der Befunde → [{ code, schwere, text, vorschlag }]. */
export function leitungsBefunde(befunde, kennzeichen) {
  return (befunde ?? []).filter((b) => /^P(29|30)$/.test(b?.code ?? '') && String(b.element ?? '').toLowerCase() === String(kennzeichen ?? '').toLowerCase())
    .map((b) => ({ code: b.code, schwere: b.schwere, text: b.text, vorschlag: b.vorschlag ?? null }));
}
