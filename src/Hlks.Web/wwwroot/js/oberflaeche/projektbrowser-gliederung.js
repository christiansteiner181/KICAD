// Gliederungen des Projektbrowsers (G1, entwurf/projektbrowser-gliederung.md; Entscheide Christian 19.10.2026): Gebäude, Sanitär, Heizung, Lüftung,
// Koordination, Energie und Umwelt. Rein (ohne DOM, in Node getestet: projektbrowser.test.mjs).
//
// EINE Tabelle (GLIEDERUNGEN) beschreibt alle Gliederungen: je Gliederung die Äste in ihrer Reihenfolge. Ein Ast nennt seine QUELLE (was er zeigt:
// Ansichten, Pläne, Systeme …) und eine AUSWAHL (welche Elemente der Quelle in diesen Ast kommen). «Weitere» wird NICHT von Hand beschrieben, sondern
// aus der Tabelle abgeleitet: alles, was kein Ast der Gliederung aufgenommen hat, steht unter «Weitere» – nichts verschwindet ganz, und die Summe
// aller Elemente ist in jeder Gliederung dieselbe wie in «Gebäude». Eine weitere Gliederung (z. B. Elektro/MSRL) ist ein neuer Eintrag in der
// Tabelle, ohne neuen Code. Kälte hat keine eigene Gliederung: «Kälte ist Teil der Heizung mit anderem Medium» (Entscheid Christian 20.10.2026) – die
// Gliederung «Heizung» zeigt die Kältesysteme (ihr Systemtyp hat das Gewerk heizung); eine gespeicherte Kennung «kaelte» (Benutzereinstellung) wird als
// «heizung» gelesen.
//
// Auswahl eines Asts (fehlt sie: alle Elemente der Quelle; eine Liste von Auswahlen: eine davon genügt):
//   { gewerk: 'sanitaer' | '$' }   das Gewerk gehört zu den Gewerken des Elements ($ = das Gewerk der Gliederung)
//   { thema: 'koordination' }      Thema der Ansicht bzw. des Plans (Darstellung aussparungen, einlagen, grundleitungen; Liste kollisionen …)
//   { art: ['ifc'] }               Art der Unterlage
//
// Zuordnung zum Gewerk (Spezifikation «Zuordnung zum Gewerk»), Elemente ohne Gewerk erscheinen unter «Weitere»:
//   Klasse    → gewerk aus dem Katalog (allgemein bzw. leer = ohne Gewerk)
//   System    → Systemtyp (systemtypen.json), sonst Medium (medien.json); Strang → sein System; Anlage → die Gewerke ihrer Systeme
//   Ansicht   → Eigenschaft «gewerke» plus die Gewerke der Systeme der Ansicht («systeme»)
//   Planblatt → Gewerk des Plansatzes (Abfrage plansaetze: gesetzt oder Vorschlag), sonst Eigenschaft «gewerk»
//   Familie   → Gewerk ihrer Kategorie (Klasse)

import { gewerkGruppen, blattGewerk } from './serienpdf-text.js';
import { gewerkAlias, gewerkeAlias } from '../gewerk-alias.js';

const lc = (t) => String(t ?? '').toLowerCase();

// ---------------------------------------------------------------------------------------------- Tabelle der Gliederungen

/** Quellen in der Reihenfolge von «Gebäude»; «Weitere» zeigt die Reste in dieser Reihenfolge. `katalog` steht in «Gebäude» unter Familien. */
export const QUELLEN_REIHE = ['ansichten', 'vorlagen', 'plaene', 'gebaeude', 'huelle', 'systeme', 'unterlagen', 'familien', 'katalog'];
/** Quellen, deren Reste unter «Weitere» nach Gewerk gruppiert werden (jede Gruppe in der Farbe ihres Gewerks). */
export const QUELLEN_NACH_GEWERK = ['ansichten', 'plaene', 'systeme', 'familien'];
/** Quellen, die es nur in einer Fachgliederung gibt (Koordination): sie gehören nicht zur Summe der Elemente von «Gebäude». */
export const QUELLEN_ZUSATZ = ['kollisionen', 'aussparungen', 'typenliste'];

const fachAeste = [
  { quelle: 'ansichten', auswahl: { gewerk: '$' }, farbe: true },
  { quelle: 'plaene', auswahl: { gewerk: '$' }, farbe: true },
  { quelle: 'systeme', titel: 'Anlagen', auswahl: { gewerk: '$' }, farbe: true },
  { quelle: 'familien', auswahl: { gewerk: '$' }, farbe: true },
  { quelle: 'gebaeude', titel: 'Räume' },
];

export const GLIEDERUNGEN = [
  { id: 'gebaeude', titel: 'Gebäude', sym: 'gebaeude', gewerk: null, exklusiv: true, aeste: [
    { quelle: 'ansichten' }, { quelle: 'vorlagen' }, { quelle: 'plaene' }, { quelle: 'gebaeude' }, { quelle: 'huelle' },
    { quelle: 'systeme' }, { quelle: 'unterlagen' }, { quelle: 'familien', mitKatalog: true },
  ] },
  { id: 'sanitaer', titel: 'Sanitär', sym: 'apparat', gewerk: 'sanitaer', aeste: fachAeste },
  { id: 'heizung', titel: 'Heizung', sym: 'rohr', gewerk: 'heizung', aeste: fachAeste },
  { id: 'lueftung', titel: 'Lüftung', sym: 'kanal', gewerk: 'lueftung', aeste: fachAeste },
  { id: 'koordination', titel: 'Koordination', sym: 'koPruefen', gewerk: 'koordination', aeste: [
    { quelle: 'kollisionen', farbe: true }, { quelle: 'aussparungen', farbe: true }, { quelle: 'typenliste', farbe: true },
    { quelle: 'unterlagen', auswahl: { art: ['ifc'] }, farbe: true },
    { quelle: 'plaene', auswahl: [{ gewerk: '$' }, { thema: 'koordination' }], farbe: true },
    { quelle: 'ansichten', auswahl: [{ gewerk: '$' }, { thema: 'koordination' }], farbe: true },
  ] },
  { id: 'energie', titel: 'Energie und Umwelt', sym: 'huellbauteile', gewerk: 'energie', aeste: [
    { quelle: 'huelle', farbe: true }, { quelle: 'gebaeude', farbe: true },
    { quelle: 'ansichten', auswahl: [{ gewerk: '$' }, { thema: 'energie' }], farbe: true },
  ] },
];

export const GLIEDERUNG_VORGABE = 'gebaeude';
export const EINSTELLUNG_GLIEDERUNG = 'projektbrowser.gliederung';

/** Die Gliederung zur Kennung; unbekannt oder leer: «Gebäude» (Vorgabe, auch für Tests ohne Einstellung). */
export function gliederung(id, tabelle = GLIEDERUNGEN) {
  return tabelle.find((g) => g.id === id) ?? tabelle.find((g) => g.id === GLIEDERUNG_VORGABE) ?? tabelle[0];
}
export const gliederungGueltig = (id, tabelle = GLIEDERUNGEN) => tabelle.some((g) => g.id === id);

// ---------------------------------------------------------------------------------------------- Mehrere Gliederungen zugleich (G2)

/** Kennung für den gemerkten Aufklappzustand, wenn mehrere Gliederungen zugleich gewählt sind (die Schlüssel der Blöcke tragen die Kennung des Blocks, ein Satz genügt). */
export const GLIEDERUNG_MEHRERE = 'mehrere';

/** Eine Gliederung gilt als «exklusiv», wenn sie ohnehin alles zeigt («Gebäude»): sie lässt sich nicht mit anderen zusammen wählen. */
const exklusiv = (id, tabelle) => !!tabelle.find((g) => g.id === id)?.exklusiv;

/** Macht aus dem gespeicherten Wert die Liste der gewählten Kennungen: Einzelwert (alte Einstellung) → Liste mit einem Eintrag; unbekannte Kennungen und Doppelte entfallen,
 *  die Reihenfolge ist die der Tabelle (= der Auswahlliste); eine exklusive Gliederung neben anderen entfällt; nichts Gültiges → die Vorgabe («Gebäude»). */
export function gliederungenNormieren(wert, tabelle = GLIEDERUNGEN) {
  const roh = (Array.isArray(wert) ? wert : (wert === undefined || wert === null || wert === '' ? [] : [wert])).map(gewerkAlias);   // «kaelte» (alt) = heizung
  const ids = tabelle.map((g) => g.id).filter((id) => roh.includes(id));
  if (ids.length === 0) return [GLIEDERUNG_VORGABE];
  if (ids.length > 1) return ids.filter((id) => !exklusiv(id, tabelle));
  return ids;
}

/** Schaltet eine Gliederung in der Auswahl um (Häkchen setzen oder entfernen) und liefert die neue Liste: «Gebäude» ersetzt alles, eine Fachgliederung hebt «Gebäude» auf,
 *  und mindestens eine bleibt gewählt (das letzte Häkchen lässt sich nicht entfernen). */
export function gliederungUmschalten(aktuell, id, tabelle = GLIEDERUNGEN) {
  const jetzt = gliederungenNormieren(aktuell, tabelle);
  if (!gliederungGueltig(id, tabelle)) return jetzt;
  if (exklusiv(id, tabelle)) return [id];
  const fach = jetzt.filter((x) => !exklusiv(x, tabelle));
  const neu = fach.includes(id) ? fach.filter((x) => x !== id) : [...fach, id];
  return neu.length === 0 ? jetzt : gliederungenNormieren(neu, tabelle);
}

/** Text im Auswahlfeld: «Sanitär», «Sanitär + Heizung», bei mehr als zwei «Sanitär + 2». */
export function gliederungAnzeige(ids, tabelle = GLIEDERUNGEN) {
  const t = ids.map((id) => gliederung(id, tabelle).titel);
  return t.length <= 2 ? t.join(' + ') : `${t[0]} + ${t.length - 1}`;
}

export const gleicheListe = (a, b) => a.length === b.length && a.every((x, i) => x === b[i]);

/** Schlüssel für den Aufklappzustand: bei einer Gliederung ihre Kennung, bei mehreren «mehrere». */
export const offenKennung = (ids) => (ids.length === 1 ? ids[0] : GLIEDERUNG_MEHRERE);

const loesen = (auswahl, gewerk) => (Array.isArray(auswahl) ? auswahl.map((a) => loesen(a, gewerk))
  : Object.fromEntries(Object.entries(auswahl).map(([k, v]) => [k, k === 'gewerk' && v === '$' ? gewerk : v])));

/** Zerlegt mehrere Fachgliederungen (in Listenreihenfolge) in Blöcke und Gemeinsames:
 *  - bloecke: je Gliederung ihre Äste ohne das Gemeinsame;
 *  - gemeinsam: Äste ohne Auswahl (sie zeigen die ganze Quelle, z. B. «Räume»), die nicht an ein Gewerk gebunden sind – entweder haben mehrere Blöcke einen solchen Ast derselben
 *    Quelle, oder der Ast trägt keine Gewerkfarbe. Sie stehen einmal nach den Blöcken, ohne Farbe (ein Ast je Quelle, bevorzugt der ohne Farbe);
 *  - gesamt: eine Gliederung mit allen Ästen (Auswahl `$` aufgelöst) – daraus leitet «Weitere» ab, was in keinem Block vorkommt. */
export function aufteilen(defs) {
  const ohneAuswahl = (a) => a.auswahl === undefined;
  const zaehler = new Map();
  for (const d of defs) for (const q of new Set(d.aeste.filter(ohneAuswahl).map((a) => a.quelle))) zaehler.set(q, (zaehler.get(q) ?? 0) + 1);
  const gemeinsamQuelle = (a) => ohneAuswahl(a) && (zaehler.get(a.quelle) > 1 || !a.farbe);
  const gemeinsam = [];
  for (const d of defs) {
    for (const a of d.aeste.filter(gemeinsamQuelle)) {
      const i = gemeinsam.findIndex((x) => x.quelle === a.quelle);
      if (i < 0) gemeinsam.push(a);
      else if (gemeinsam[i].farbe && !a.farbe) gemeinsam[i] = a;
    }
  }
  const bloecke = defs.map((d) => ({ def: d, aeste: d.aeste.filter((a) => !gemeinsamQuelle(a)) }));
  const gesamt = { id: GLIEDERUNG_MEHRERE, titel: defs.map((d) => d.titel).join(' + '), sym: defs[0]?.sym, gewerk: null,
    aeste: defs.flatMap((d) => d.aeste.map((a) => (a.auswahl === undefined ? a : { ...a, auswahl: loesen(a.auswahl, d.gewerk) }))) };
  return { bloecke, gemeinsam: gemeinsam.map((a) => ({ ...a, farbe: false })), gesamt };
}

/** Schlüssel im Browser-Speicher für die aufgeklappten Äste: «Gebäude» wie bisher (browserOffen), die übrigen je Gliederung. */
export const offenSchluessel = (id) => (id === GLIEDERUNG_VORGABE ? 'browserOffen' : `browserOffen.${id}`);

/** Anfangs aufgeklappt (solange der Benutzer nichts anderes gemerkt hat): bei «Gebäude» wie bisher, bei Fachgliederungen alle Fachäste; «Weitere» ist zu. */
export function offenVorgabe(def) {
  if (def.id === GLIEDERUNG_VORGABE) return ['w:ansichten', 'w:gebaeude', 'w:systeme', 'w:unterlagen'];
  return def.aeste.map((a) => 'w:' + a.quelle);
}

// ---------------------------------------------------------------------------------------------- Gewerke

/** Gewerk einer Klasse aus dem Katalog; «allgemein» und leer bedeuten: ohne Gewerk (''). */
export function klassenGewerk(ctx, klasse) {
  const g = (ctx.klassen ?? []).find((k) => k.name === klasse)?.gewerk;
  return !g || g === 'allgemein' ? '' : g;
}

/** Gewerk eines Systems: Systemtyp, sonst Medium; '' wenn keins bekannt. */
export function systemGewerk(ctx, system) {
  if (!system) return '';
  const typ = (ctx.systemtypen ?? []).find((s) => s.name === system.werte?.systemtyp);
  if (typ?.gewerk && typ.gewerk !== 'allgemein') return typ.gewerk;
  const medium = (ctx.medien ?? []).find((m) => m.kennung === system.werte?.medium || (typ && m.kennung === typ.medium));
  return medium?.gewerk && medium.gewerk !== 'allgemein' ? medium.gewerk : '';
}

/** Gewerk eines Strangs: das seines Systems. */
export function strangGewerk(ctx, strang) { return systemGewerk(ctx, ctx.element(strang?.teilVon)); }

/** Der Hauptvertreter einer Gewerkliste: das erste Fachgewerk (nicht Architektur), sonst das erste; '' ohne Gewerk. */
export function hauptGewerk(liste) {
  const l = (liste ?? []).filter(Boolean);
  return l.find((g) => g !== 'architektur') ?? l[0] ?? '';
}

const DARSTELLUNG_THEMA = { aussparungen: 'koordination', einlagen: 'koordination', grundleitungen: 'koordination', huelle: 'energie' };
const LISTE_THEMA = { kollisionen: 'koordination', aussparungsliste: 'koordination', raumgrenzen: 'energie', huellbauteile: 'energie' };

/** Thema einer Ansicht aus Darstellung bzw. Liste: koordination | energie | ''. */
export function ansichtThema(a) {
  const w = a?.werte ?? {};
  return (w.art === 'liste' ? LISTE_THEMA[w.liste] : undefined) ?? DARSTELLUNG_THEMA[w.darstellung] ?? '';
}

/** Gewerke einer Ansicht: «gewerke» und die Gewerke der Systeme der Ansicht (Kennzeichen in «systeme»), ohne Doppelte. */
export function ansichtGewerke(ctx, a) {
  const w = a?.werte ?? {};
  const aus = gewerkeAlias((Array.isArray(w.gewerke) ? w.gewerke : []).map(String));
  const sys = (Array.isArray(w.systeme) ? w.systeme : []).map((kz) => systemGewerk(ctx, ctx.element(kz)));
  return [...new Set([...aus, ...sys].filter((g) => g && g !== 'allgemein'))];
}

/** Gewerk eines Planblatts: Plansatz (gesetzt oder Vorschlag, Abfrage plansaetze), sonst die Eigenschaft «gewerk»; '' = ohne. */
export function planGewerk(ctx, p) {
  const g = gewerkAlias(blattGewerk(ctx.plansaetze, p.kennzeichen)?.gewerk ?? p.werte?.gewerk);
  return !g || g === 'allgemein' ? '' : g;
}

/** Thema eines Planblatts: koordination/energie, wenn eine seiner Ansichten es hat. */
export function planThema(ctx, p) {
  for (const f of ctx.derKlasse('Ansichtsfenster')) {
    if (lc(f.teilVon) !== lc(p.kennzeichen)) continue;
    const t = ansichtThema(ctx.element(f.bezuege?.ansicht));
    if (t) return t;
  }
  return '';
}

// ---------------------------------------------------------------------------------------------- Merkmale und Auswahl

/** Merkmale eines Elements der Quelle für die Auswahl: { gewerke[], haupt, thema, art }. */
export const MERKMALE = {
  ansichten: (ctx, a) => { const g = ansichtGewerke(ctx, a); return { gewerke: g, haupt: hauptGewerk(g), thema: ansichtThema(a) }; },
  vorlagen: () => ({ gewerke: [], haupt: '' }),
  plaene: (ctx, p) => { const g = planGewerk(ctx, p); return { gewerke: g ? [g] : [], haupt: g, thema: planThema(ctx, p) }; },
  systeme: (ctx, s) => { const g = systemGewerk(ctx, s); return { gewerke: g ? [g] : [], haupt: g }; },
  unterlagen: (ctx, u) => ({ gewerke: [], haupt: '', art: u.werte?.art ?? '' }),
  familien: (ctx, f) => { const g = klassenGewerk(ctx, f.kategorie); return { gewerke: g ? [g] : [], haupt: g }; },
};

/** Passt ein Element (seine Merkmale m) zu einer Auswahl der Gliederung def? Ohne Auswahl: ja; Liste: eine davon. */
export function passt(auswahl, m, def) {
  if (auswahl === undefined || auswahl === null) return true;
  if (Array.isArray(auswahl)) return auswahl.some((a) => passt(a, m, def));
  return Object.entries(auswahl).every(([schluessel, soll]) => {
    if (schluessel === 'gewerk') return (m.gewerke ?? []).includes(soll === '$' ? def.gewerk : soll);
    if (schluessel === 'thema') return m.thema === soll;
    if (schluessel === 'art') return (Array.isArray(soll) ? soll : [soll]).includes(m.art);
    return false;
  });
}

/** Ast der Gliederung → Prüffunktion der Merkmale; null = alle (keine Einschränkung). */
export const astPasst = (def, ast) => (ast.auswahl === undefined ? null : (m) => passt(ast.auswahl, m, def));

/** Was von einer Quelle in keinen Ast der Gliederung kam («Weitere»). null = nichts übrig (ein Ast nimmt alles); (m) => true bei Quellen ohne Ast. */
export function restPasst(def, quelle) {
  const aeste = def.aeste.filter((a) => a.quelle === quelle);
  if (aeste.length === 0) return () => true;
  if (aeste.some((a) => a.auswahl === undefined)) return null;
  return (m) => !aeste.some((a) => passt(a.auswahl, m, def));
}

/** Welche Quellen unter «Weitere» Reste haben können (in der Reihenfolge von «Gebäude»). Der Katalog fehlt, wenn ein Familienast ihn enthält. */
export function weitereQuellen(def) {
  const katalogDrin = def.aeste.some((a) => a.quelle === 'familien' && a.mitKatalog);
  return QUELLEN_REIHE.filter((q) => (q === 'katalog' ? !katalogDrin && !def.aeste.some((a) => a.quelle === 'katalog') : restPasst(def, q) !== null));
}

// ---------------------------------------------------------------------------------------------- Farben der Gewerke (Katalog gewerke.json)

/** Farben eines Gewerks aus dem Katalog { id, titel, koordination, pastell } oder null (nicht im Katalog: keine Hinterlegung). */
export const gewerkFarben = (ctx, id) => (ctx.gewerke ?? []).find((g) => g.id === id && /^#[0-9a-fA-F]{6}$/.test(g.pastell ?? '') && /^#[0-9a-fA-F]{6}$/.test(g.koordination ?? '')) ?? null;
export const gewerkTitel = (ctx, id) => (ctx.gewerke ?? []).find((g) => g.id === id)?.titel ?? id;

/** Reihenfolge der Gewerke für die Gruppen unter «Weitere»: Katalog (gewerke.json), danach unbekannte, zuletzt «ohne Gewerk» (''). */
export function gewerkeSortiert(ctx, vorhanden) {
  const kat = (ctx.gewerke ?? []).map((g) => g.id);
  const l = [...vorhanden].filter((g) => g !== '');
  l.sort((a, b) => { const i = kat.indexOf(a), j = kat.indexOf(b); return (i < 0 ? 999 : i) - (j < 0 ? 999 : j) || a.localeCompare(b); });
  return vorhanden.has('') ? [...l, ''] : l;
}

// ---------------------------------------------------------------------------------------------- Farbe und Kontrast (Hinterlegung in hell und dunkel)

export const hexRgb = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
const lin = (c) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
/** Relative Leuchtdichte nach WCAG 2.x. */
export const leuchtdichte = ([r, g, b]) => 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
/** Kontrastverhältnis zweier Farben (RGB-Tripel), 1 … 21. */
export function kontrast(a, b) { const [x, y] = [leuchtdichte(a), leuchtdichte(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); }
/** Farbe v mit Deckkraft d über dem Grund. */
export const ueberlagern = (v, d, grund) => v.map((c, i) => Math.round(c * d + grund[i] * (1 - d)));

// ---------------------------------------------------------------------------------------------- Baumwerkzeuge

const istBlatt = (k) => k.art !== 'ast' && !(Array.isArray(k.kinder) && k.kinder.length > 0);

/** Alle Blattknoten (Elemente, nicht Gruppen) eines Baums. */
export function blaetter(k, aus = []) {
  if (!k) return aus;
  if (istBlatt(k) && k.art !== 'ast') aus.push(k);
  for (const x of k.kinder ?? []) blaetter(x, aus);
  return aus;
}
export const blattZahl = (k) => blaetter(k).length;

/** Bezeichner eines Knotens unabhängig vom Präfix, das «Weitere» den Schlüsseln voranstellt. */
export const identitaet = (k) => k.ursprung ?? k.key;

/** Menge der Bezeichner aller Elementknoten eines Baums (für den Vergleich: die Summe in jeder Gliederung ist die von «Gebäude»). */
export function elementMenge(wurzeln) {
  const m = new Set();
  const lauf = (k) => { if (k.art !== 'ast') m.add(identitaet(k)); for (const x of k.kinder ?? []) lauf(x); };
  for (const w of wurzeln) lauf(w);
  return m;
}

/** Stellt allen Schlüsseln des Teilbaums ein Präfix voran (die Ursprungsschlüssel bleiben in `ursprung`). */
export function praefix(k, p) {
  return { ...k, ursprung: k.ursprung ?? k.key, key: p + k.key, kinder: k.kinder ? k.kinder.map((x) => praefix(x, p)) : k.kinder };
}

/** Sucht im Baum: behält Treffer und ihre Vorfahren (Text: Beschriftung, Zusatz, Klasse). Ohne Suchtext unverändert. */
export function filtern(k, suche) {
  if (!suche) return k;
  const text = lc(`${k.label} ${k.sub ?? ''} ${k.klasse ?? ''}`);
  const treffer = text.includes(suche);
  if (!k.kinder) return treffer ? k : null;
  const kinder = k.kinder.map((x) => filtern(x, suche)).filter(Boolean);
  if (kinder.length === 0 && !treffer) return null;
  return { ...k, kinder: treffer && kinder.length === 0 ? k.kinder : kinder, zahl: kinder.length || k.zahl };
}

/** Schlüssel aller Äste (mit Kindern) im Baum – so viele Zeilen klappt die Suche auf. */
export function astSchluessel(wurzeln) {
  const s = new Set();
  const lauf = (k) => { if (Array.isArray(k.kinder)) s.add(k.key); for (const x of k.kinder ?? []) lauf(x); };
  for (const w of wurzeln) lauf(w);
  return s;
}

// ---------------------------------------------------------------------------------------------- Plansatz-Gruppen (für den Baum «Pläne»)
export { gewerkGruppen, blattGewerk };
