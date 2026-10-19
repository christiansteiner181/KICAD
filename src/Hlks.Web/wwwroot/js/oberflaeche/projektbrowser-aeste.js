// Äste des Projektbrowsers (G1): baut aus den Daten des Projekts (ctx) die Knoten der gewählten Gliederung – Fachäste, «Weitere» und die Äste der Koordination.
// Rein (ohne DOM, ohne zustand.js; alles kommt in ctx), deshalb in Node getestet (projektbrowser.test.mjs). Die Tabelle der Gliederungen und die Zuordnung
// zum Gewerk stehen in projektbrowser-gliederung.js; hier stehen die Knoten der einzelnen Quellen (Ansichten, Pläne, Systeme …), wie sie der Projektbrowser seit Modul 1 zeigt.
//
// ctx = {
//   klassen, systemtypen, medien, gewerke       aus dem Katalog (Abfrage katalog)
//   derKlasse(klasse), element(kennzeichen|id)  das Modell
//   plansaetze                                  Antwort der Abfrage plansaetze (oder null)
//   familien, bibliothek                        Antworten von familien und familien_bibliothek
//   katalogKlassen                              Katalogklassen für den Knoten «Katalog»
//   koordination { kollisionen, aussparungen, typen, fehler }   Antworten der Abfragen kollisionen (mit Sicht der Palette), aussparungsliste, typmaterial
//   idVon(kz)                                   Id eines Elements zum Kennzeichen
// }
// Knoten: { key, label, sub, zahl, kinder, sym, art, id, kz, klasse, gewerk, quelle, kontext, … } (die Felder, die der Projektbrowser zeichnet).

import { symbolFuerKlasse, symbolFuerAnsichtsart } from './symbole.js';
import { vergleichen, ANSICHT_ARTEN, SCHICHT_NAMEN, anzeigeName } from './werkzeug.js';
import { unterlageKurz, unterlageName } from './einfuegen-text.js';
import { istNichtPlatziert, nichtPlatzierte, raumTitel } from './hkap-text.js';
import { familienAst as familienAstBauen } from '../familien/browser-knoten.js';
import { statusText, paarText, KOLLISION_STATUS, AUSSPARUNG_STATUS } from './koordination-text.js';
import {
  MERKMALE, QUELLEN_NACH_GEWERK, QUELLEN_REIHE, astPasst, restPasst, weitereQuellen, klassenGewerk, systemGewerk, gewerkGruppen, blattGewerk,
  gewerkTitel, gewerkeSortiert, praefix, blattZahl,
} from './projektbrowser-gliederung.js';

const lc = (t) => String(t ?? '').toLowerCase();

// ---------------------------------------------------------------------------------------------- Grundknoten

function elementKnoten(e, extra = {}) {
  return { key: 'e:' + e.id, label: anzeigeName(e), sub: e.name ? e.kennzeichen : '', sym: symbolFuerKlasse(e.klasse, e.basis), art: 'element', id: e.id, kz: e.kennzeichen, klasse: e.klasse, ...extra };
}
function ansichtKnoten(a) {
  return { ...elementKnoten(a), sym: symbolFuerAnsichtsart(a.werte?.art), art: 'ansicht', vorlage: a.werte?.vorlage === true };
}
function astKnoten(key, label, kinder, extra = {}) {
  return { key, label: `${label}`, zahl: kinder.length, kinder, sym: 'ordner', art: 'ast', ...extra };
}
const nachKz = (a, b) => vergleichen(a.kennzeichen, b.kennzeichen);

// ---------------------------------------------------------------------------------------------- Ansichten, Vorlagen

function ansichtenAst(ctx, passt, opt = {}) {
  const alle = ctx.derKlasse('Ansicht').filter((a) => a.werte?.vorlage !== true && (!passt || passt(MERKMALE.ansichten(ctx, a))))
    .sort((a, b) => vergleichen(anzeigeName(a), anzeigeName(b)));
  const nachArt = new Map();
  for (const a of alle) {
    const art = a.werte?.art in ANSICHT_ARTEN ? a.werte.art : '?';
    if (!nachArt.has(art)) nachArt.set(art, []);
    nachArt.get(art).push(a);
  }
  const gruppen = [];
  for (const art of [...Object.keys(ANSICHT_ARTEN), '?']) {
    const l = nachArt.get(art);
    if (l?.length) gruppen.push(astKnoten('g:' + art, ANSICHT_ARTEN[art] ?? 'Weitere', l.map(ansichtKnoten), { sym: symbolFuerAnsichtsart(art), kontext: art === 'liste' ? 'liste' : undefined }));
  }
  return astKnoten('w:ansichten', opt.titel ?? 'Ansichten', gruppen, { zahl: alle.length, quelle: 'ansichten' });
}

function vorlagenAst(ctx, passt, opt = {}) {
  const l = ctx.derKlasse('Ansicht').filter((a) => a.werte?.vorlage === true && (!passt || passt(MERKMALE.vorlagen(ctx, a)))).sort((a, b) => vergleichen(anzeigeName(a), anzeigeName(b)));
  return astKnoten('w:vorlagen', opt.titel ?? 'Ansichtsvorlagen', l.map((a) => ({ ...ansichtKnoten(a), sym: 'vorlage' })), { quelle: 'vorlagen' });
}

// ---------------------------------------------------------------------------------------------- Pläne

/** Pläne nach Gewerk gruppiert (Serien-PDF, entwurf/serien-pdf.md), wenn alle gezeigt werden («Gebäude»); in einer Fachgliederung und unter «Weitere» flach.
 *  Das Gewerk (gesetzt oder Vorschlag) kommt vom Kern (Abfrage plansaetze); bis die Antwort da ist, steht die Liste ungruppiert. */
function plaeneAst(ctx, passt, opt = {}) {
  const nummerTitel = (p) => [p.werte?.nummer, p.werte?.titel].filter(Boolean).join(' ') || anzeigeName(p);
  const l = ctx.derKlasse('Planblatt').filter((p) => !passt || passt(MERKMALE.plaene(ctx, p)))
    .sort((a, b) => vergleichen(a.werte?.nummer ?? a.kennzeichen, b.werte?.nummer ?? b.kennzeichen));
  const fenster = ctx.derKlasse('Ansichtsfenster');
  const titel = opt.titel ?? 'Pläne';
  const blattKnoten = (p, info) => {
    const kinder = fenster.filter((f) => lc(f.teilVon) === lc(p.kennzeichen)).sort(nachKz).map((f) => {
      const a = ctx.element(f.bezuege?.ansicht);
      return { ...elementKnoten(f), label: a ? anzeigeName(a) : f.kennzeichen, sub: a ? a.kennzeichen : '', sym: symbolFuerAnsichtsart(a?.werte?.art), art: 'ansichtsfenster', ansichtKz: a?.kennzeichen ?? '' };
    });
    const vorschlag = info?.gewerkHerkunft === 'vorschlag';
    return { ...elementKnoten(p), label: nummerTitel(p), sub: p.kennzeichen + (vorschlag ? ' · Gewerk: Vorschlag' : ''), sym: 'planblatt', art: 'planblatt', kinder, zahl: kinder.length,
      css: vorschlag ? 'pb-vorschlag' : '', tipp: info ? `${p.kennzeichen} · Gewerk ${info.gewerkName} (${vorschlag ? 'Vorschlag: ' + info.gewerkQuelle : 'von Hand gesetzt'})` : undefined };
  };
  const gruppen = passt ? [] : gewerkGruppen(ctx.plansaetze);
  if (gruppen.length === 0) {
    return astKnoten('w:plaene', titel, l.map((p) => blattKnoten(p, blattGewerk(ctx.plansaetze, p.kennzeichen))), { quelle: 'plaene', kontext: 'plaene' });
  }
  const nachKennz = new Map(l.map((p) => [lc(p.kennzeichen), p]));
  const verwendet = new Set();
  const knoten = gruppen.map((g) => {
    const kinder = g.blaetter.map((b) => {
      const p = nachKennz.get(lc(b.planblatt));
      if (!p || verwendet.has(p.id)) return null;
      verwendet.add(p.id);
      return blattKnoten(p, b);
    }).filter(Boolean);
    return astKnoten('p:gewerk:' + g.gewerk, g.name, kinder, { kz: g.schluessel, tipp: `Plansatz ${g.name} · Rechtsklick: Serien-PDF …`, gewerk: g.gewerk, kontext: 'plaene', anfangsOffen: true });
  }).filter((g) => g.kinder.length > 0);
  const lose = l.filter((p) => !verwendet.has(p.id));   // gerade angelegt, der Kern hat sie noch nicht gemeldet
  if (lose.length) knoten.push(astKnoten('p:gewerk:neu', 'Weitere', lose.map((p) => blattKnoten(p, null)), { kontext: 'plaene' }));
  return astKnoten('w:plaene', titel, knoten, { zahl: l.length, quelle: 'plaene', kontext: 'plaene' });
}

// ---------------------------------------------------------------------------------------------- Gebäude, Hülle

/** Gebäude → Geschoss → Räume, dazu «Nicht platzierte Räume» (HKAP, Modul 1d). */
function gebaeudeAst(ctx, passt, opt = {}) {
  const geschosse = ctx.derKlasse('Geschoss');
  const nichtPlatziert = nichtPlatzierte(ctx.derKlasse('Raum'));
  const raeume = ctx.derKlasse('Raum').filter((r) => !istNichtPlatziert(r));
  const raumLabel = (r) => (r.name ? `${r.kennzeichen} ${r.name}` : r.kennzeichen);
  const raumKnoten = (g) => raeume.filter((r) => lc(r.geschoss) === lc(g.kennzeichen)).sort(nachKz).map((r) => elementKnoten(r, { label: raumLabel(r), sub: '' }));
  const geschossKnoten = (g) => { const r = raumKnoten(g); return { ...elementKnoten(g), kinder: r, zahl: r.length, art: 'element-ast', sub: g.name ? g.kennzeichen : '' }; };
  const nachKote = (a, b) => (b.werte?.kote ?? 0) - (a.werte?.kote ?? 0);
  const gebaeude = ctx.derKlasse('Gebaeude').sort(nachKz);
  const verwendet = new Set();
  const knoten = gebaeude.map((g) => {
    const gs = geschosse.filter((s) => lc(s.teilVon) === lc(g.kennzeichen)).sort(nachKote);
    gs.forEach((s) => verwendet.add(s.id));
    const k = gs.map(geschossKnoten);
    return { ...elementKnoten(g), kinder: k, zahl: k.length, art: 'element-ast' };
  });
  const lose = geschosse.filter((s) => !verwendet.has(s.id)).sort(nachKote);
  if (lose.length) knoten.push(astKnoten('g:ohne-gebaeude', '(ohne Gebäude)', lose.map(geschossKnoten)));
  const bekannt = new Set(geschosse.map((g) => lc(g.kennzeichen)));
  const heimatlos = raeume.filter((r) => !bekannt.has(lc(r.geschoss))).sort(nachKz);
  if (heimatlos.length) knoten.push(astKnoten('g:ohne-geschoss', '(Räume ohne Geschoss)', heimatlos.map((r) => elementKnoten(r, { label: raumLabel(r), sub: '' }))));
  if (nichtPlatziert.length) {
    knoten.push(astKnoten('g:nicht-platziert', 'Nicht platzierte Räume', nichtPlatziert.map((r) => elementKnoten(r, {
      art: 'raum-offen', sym: 'raumOffen', label: raumTitel(r), sub: r.geschoss ?? '', tipp: `${r.kennzeichen} · nicht platziert (aus HKAP, noch ohne Geometrie)\nRechtsklick: Platzieren … oder einem gezeichneten Raum zuordnen …`,
    })), { sym: 'raumOffen' }));
  }
  return astKnoten('w:gebaeude', opt.titel ?? 'Gebäude', knoten, { quelle: 'gebaeude' });
}

/** Hülle (Modul 1c): Materialien, Aufbauten und Wärmebrücken des Projekts. Doppelklick öffnet Material- bzw. Aufbau-Dialog, Kontextmenü beim Aufbau: Zuweisen …. */
function huelleAst(ctx, passt, opt = {}) {
  const mit = (e, extra) => elementKnoten(e, { label: e.name ? `${e.kennzeichen} ${e.name}` : e.kennzeichen, sub: '', ...extra });
  const material = ctx.derKlasse('Material').sort(nachKz).map((m) => mit(m, { art: 'huelle-material', sub: m.werte?.kategorie ?? '' }));
  const aufbauten = ctx.derKlasse('Aufbau').sort(nachKz).map((a) => mit(a, { art: 'huelle-aufbau', sub: a.werte?.art ?? '' }));
  const bruecken = ctx.derKlasse('Waermebruecke').sort(nachKz).map((b) => mit(b, { sub: b.werte?.art ?? '' }));
  return astKnoten('w:huelle', opt.titel ?? 'Hülle', [
    astKnoten('h:materialien', 'Materialien', material, { sym: 'material' }),
    astKnoten('h:aufbauten', 'Aufbauten', aufbauten, { sym: 'aufbau' }),
    astKnoten('h:waermebruecken', 'Wärmebrücken', bruecken, { sym: 'waermebruecke' }),
  ], { sym: 'huellbauteile', zahl: material.length + aufbauten.length + bruecken.length, quelle: 'huelle' });
}

// ---------------------------------------------------------------------------------------------- Systeme

/** Anlage → System → Strang. Mit Auswahl (Gewerk): nur Systeme, die passen; eine Anlage steht da, wo mindestens eins ihrer Systeme steht (eine Anlage mit Systemen
 *  mehrerer Gewerke steht in mehreren Gewerken, jedes System nur einmal). Ohne Auswahl («Gebäude»): wie bisher; eine Anlage, deren Systeme alle zu einem Gewerk gehören, trägt dessen Farbe. */
function systemeAst(ctx, passt, opt = {}) {
  const alleSysteme = ctx.derKlasse('System');
  const straenge = ctx.derKlasse('Strang');
  const systeme = alleSysteme.filter((s) => !passt || passt(MERKMALE.systeme(ctx, s)));
  const sysKnoten = (s) => {
    const k = straenge.filter((x) => lc(x.teilVon) === lc(s.kennzeichen)).sort(nachKz).map((x) => elementKnoten(x));
    return { ...elementKnoten(s), kinder: k, zahl: k.length, art: 'element-ast' };
  };
  const leer = !passt || passt({ gewerke: [], haupt: '' });   // Anlage ohne System gehört zu «ohne Gewerk»
  const anlagen = ctx.derKlasse('Anlage').sort(nachKz);
  const verwendet = new Set();
  const knoten = [];
  for (const a of anlagen) {
    const alle = alleSysteme.filter((s) => lc(s.teilVon) === lc(a.kennzeichen));
    const ss = systeme.filter((s) => lc(s.teilVon) === lc(a.kennzeichen)).sort(nachKz);
    ss.forEach((s) => verwendet.add(s.id));
    if (ss.length === 0 && !(alle.length === 0 && leer)) continue;
    const k = ss.map(sysKnoten);
    const gewerke = new Set(alle.map((s) => systemGewerk(ctx, s)));
    knoten.push({ ...elementKnoten(a), kinder: k, zahl: k.length, art: 'element-ast', gewerk: gewerke.size === 1 && [...gewerke][0] ? [...gewerke][0] : undefined });
  }
  const lose = systeme.filter((s) => !verwendet.has(s.id) && !anlagen.some((a) => lc(a.kennzeichen) === lc(s.teilVon))).sort(nachKz);
  if (lose.length) knoten.push(astKnoten('g:ohne-anlage', '(ohne Anlage)', lose.map(sysKnoten)));
  return astKnoten('w:systeme', opt.titel ?? 'Systeme', knoten, { quelle: 'systeme' });
}

// ---------------------------------------------------------------------------------------------- Unterlagen, Familien, Katalog

const UNTERLAGE_SYMBOL = { ifc: 'gebaeude', dwg: 'grundriss', dxf: 'grundriss', pdf: 'planblatt', bild: 'planblatt' };
/** Unterlagen (Modul 1b): IFC, DWG/DXF, PDF und Bilder des Projekts mit Art und Geschoss; Kontextmenü: anzeigen, Deckkraft, Lagebericht, Entfernen. */
function unterlagenAst(ctx, passt, opt = {}) {
  const l = ctx.derKlasse('Unterlage').filter((u) => !passt || passt(MERKMALE.unterlagen(ctx, u))).sort((a, b) => vergleichen(unterlageName(a), unterlageName(b)));
  return astKnoten('w:unterlagen', opt.titel ?? 'Unterlagen', l.map((u) => ({
    ...elementKnoten(u), art: 'unterlage', label: unterlageName(u), sym: UNTERLAGE_SYMBOL[u.werte?.art] ?? 'planblatt',
    sub: unterlageKurz(u) + (u.werte?.anzeigen === false ? ' · ausgeblendet' : ''),
  })), { quelle: 'unterlagen' });
}

/** Katalogklassen nach Schicht → Klasse (nur lesen; Klick zeigt die Beschreibung in den Eigenschaften). */
function katalogAst(ctx) {
  const nachSchicht = new Map();
  for (const k of [...(ctx.katalogKlassen ?? [])].sort((a, b) => vergleichen(a.name, b.name))) {
    if (!nachSchicht.has(k.schicht)) nachSchicht.set(k.schicht, []);
    nachSchicht.get(k.schicht).push(k);
  }
  const gruppen = [...nachSchicht].map(([s, ks]) => astKnoten('f:' + s, SCHICHT_NAMEN[s] ?? s, ks.map((k) => ({
    key: 'k:' + k.name, label: k.name, sub: k.basis ?? '', sym: symbolFuerKlasse(k.name, k.basis), art: 'familie', klasse: k.name, tipp: k.beschreibung,
  }))));
  return astKnoten('f:katalog', 'Katalog', gruppen, { zahl: (ctx.katalogKlassen ?? []).length, quelle: 'katalog' });
}

/** Familien des Projekts je Kategorie (Abfrage familien) plus die Bibliotheksfamilien nach der Familienauswahl (familien_bibliothek, Modul 2, C3); in «Gebäude» am Ende der Knoten «Katalog».
 *  Die Kategorien tragen das Gewerk ihrer Klasse (Farbe). */
function familienAst(ctx, passt, opt = {}) {
  const ok = (f) => !passt || passt(MERKMALE.familien(ctx, f));
  const liste = (ctx.familien ?? []).filter(ok);
  const bib = (ctx.bibliothek ?? []).filter(ok);
  const n = familienAstBauen(liste, opt.mitKatalog ? katalogAst(ctx) : null, ctx.idVon, bib);
  return { ...n, label: opt.titel ?? n.label, quelle: 'familien', kontext: 'familien',
    kinder: n.kinder.map((k) => (k.key.startsWith('fk:') ? { ...k, gewerk: klassenGewerk(ctx, k.label) || undefined } : k)) };
}

// ---------------------------------------------------------------------------------------------- Koordination (Abfragen kollisionen, aussparungsliste, typmaterial)

const kurz = (t, n = 60) => { const s = String(t ?? '').replace(/\s+/g, ' ').trim(); return s.length > n ? s.slice(0, n - 1) + '…' : s; };
const ladeKnoten = (key, text) => ({ key, label: text, sym: 'info', art: 'hinweis', zahl: undefined });

function koordinationLeer(key, label, sym, quelle, kontext, fehler) {
  const hinweis = fehler ? `Fehler: ${fehler}` : 'Wird geladen …';
  return astKnoten(key, label, [ladeKnoten(key + ':hinweis', hinweis)], { sym, quelle, kontext, zahl: undefined });
}

/** Kollisionen nach Status, darunter das Paar (HLKS gegen Architektur …); die Liste ist die der Palette Koordination (Sicht ohne nicht geprüfte Paare). */
function kollisionenAst(ctx) {
  const d = ctx.koordination?.kollisionen;
  if (!d) return koordinationLeer('w:kollisionen', 'Kollisionen', 'koListe', 'kollisionen', 'kollisionen', ctx.koordination?.fehler?.kollisionen);
  const zeilen = d.zeilen ?? [];
  const nachStatus = new Map();
  for (const z of zeilen) { const s = z.status ?? '?'; if (!nachStatus.has(s)) nachStatus.set(s, []); nachStatus.get(s).push(z); }
  const reihenfolge = [...KOLLISION_STATUS, ...[...nachStatus.keys()].filter((s) => !KOLLISION_STATUS.includes(s))];
  const zeile = (z) => ({ key: 'ko:' + z.kollision, label: z.kollision, sub: kurz(z.beteiligte, 50), sym: 'koListe', art: 'kollision', kz: z.kollision, klasse: 'Kollision', id: ctx.idVon?.(z.kollision) ?? undefined,
    tipp: `${z.kollision} · ${z.art ?? ''} ${z.typ ?? ''} · ${statusText(z.status)}${z.geschoss ? ' · ' + z.geschoss : ''}\n${z.beteiligte ?? ''}\nDoppelklick zeigt die Kollision im Grundriss.` });
  const gruppen = reihenfolge.filter((s) => nachStatus.has(s)).map((s) => {
    const l = nachStatus.get(s);
    const nachPaar = new Map();
    for (const z of l) { const p = z.paar ?? ''; if (!nachPaar.has(p)) nachPaar.set(p, []); nachPaar.get(p).push(z); }
    const paare = [...nachPaar].sort((a, b) => vergleichen(paarText(a[0]), paarText(b[0]))).map(([p, zs]) =>
      astKnoten(`ko:${s}:${p}`, p ? paarText(p) : '(ohne Paar)', zs.map(zeile), { sym: 'ordner' }));
    return astKnoten(`ko:${s}`, statusText(s), paare, { zahl: l.length, sym: 'ordner' });
  });
  return astKnoten('w:kollisionen', 'Kollisionen', gruppen, { sym: 'koListe', quelle: 'kollisionen', kontext: 'kollisionen', zahl: zeilen.length,
    tipp: 'Kollisionen des letzten Prüflaufs nach Status, darunter das Paar. Rechtsklick: Kollisionsliste, Kollisionen prüfen …' });
}

/** Aussparungen nach Geschoss (von oben nach unten), darunter der Status. */
function aussparungenAst(ctx) {
  const d = ctx.koordination?.aussparungen;
  if (!d) return koordinationLeer('w:aussparungen', 'Aussparungen', 'koAusListe', 'aussparungen', 'aussparungen', ctx.koordination?.fehler?.aussparungen);
  const zeilen = d.zeilen ?? [];
  const nachGeschoss = new Map();
  for (const z of zeilen) { const g = z.geschoss ?? ''; if (!nachGeschoss.has(g)) nachGeschoss.set(g, []); nachGeschoss.get(g).push(z); }
  const kote = (kz) => ctx.element(kz)?.werte?.kote ?? -Infinity;
  const zeile = (z) => ({ key: 'au:' + z.aussparung, label: z.nummer ? `${z.nummer} · ${z.aussparung}` : z.aussparung, sub: z.masse ?? '', sym: 'koAusListe', art: 'aussparung', kz: z.aussparung, klasse: 'Aussparung',
    id: ctx.idVon?.(z.aussparung) ?? undefined, tipp: `${z.aussparung} · ${z.art ?? ''} · ${statusText(z.status)}\nDoppelklick zeigt die Aussparung im Grundriss.` });
  const gruppen = [...nachGeschoss].sort((a, b) => (a[0] === '' ? 1 : b[0] === '' ? -1 : kote(b[0]) - kote(a[0]) || vergleichen(a[0], b[0]))).map(([g, l]) => {
    const nachStatus = new Map();
    for (const z of l) { const s = z.status ?? '?'; if (!nachStatus.has(s)) nachStatus.set(s, []); nachStatus.get(s).push(z); }
    const reihenfolge = [...AUSSPARUNG_STATUS, ...[...nachStatus.keys()].filter((s) => !AUSSPARUNG_STATUS.includes(s))];
    const stati = reihenfolge.filter((s) => nachStatus.has(s)).map((s) =>
      astKnoten(`au:${g}:${s}`, statusText(s), nachStatus.get(s).sort((a, b) => vergleichen(a.nummer ?? a.aussparung, b.nummer ?? b.aussparung)).map(zeile), { sym: 'ordner' }));
    const ge = ctx.element(g);
    return astKnoten(`au:${g}`, g === '' ? '(ohne Geschoss)' : (ge?.name ? `${g} ${ge.name}` : g), stati, { zahl: l.length, sym: 'geschoss', kz: g || undefined });
  });
  return astKnoten('w:aussparungen', 'Aussparungen', gruppen, { sym: 'koAusListe', quelle: 'aussparungen', kontext: 'aussparungen', zahl: zeilen.length,
    tipp: 'Aussparungen nach Geschoss, darunter der Status. Rechtsklick: Aussparungsliste, Aussparungen vorschlagen …' });
}

/** Typenliste «Typname → Material»: unbekannte Typen (zu entscheiden) und die Einträge. Doppelklick auf den Ast öffnet den Dialog der Typenliste. */
function typenlisteAst(ctx) {
  const d = ctx.koordination?.typen;
  if (!d) return koordinationLeer('w:typenliste', 'Typenliste', 'koTypenliste', 'typenliste', 'typenliste', ctx.koordination?.fehler?.typen);
  const unbekannt = (d.unbekannt ?? []).map((u) => ({ key: 'ty:u:' + u.schluessel, label: u.schluessel, sub: kurz(u.entitaeten, 40), sym: 'koTypenliste', art: 'typ-unbekannt', tipp: `Unbekannter Typ «${u.schluessel}»: Doppelklick auf «Typenliste» öffnet den Dialog zum Festlegen.` }));
  const eintraege = (d.typen ?? []).map((t) => ({ key: 'ty:' + t.element, label: t.typname, sub: t.kategorie ?? '', sym: 'koTypenliste', art: 'typ', kz: t.element, klasse: 'Typmaterial', id: ctx.idVon?.(t.element) ?? undefined }));
  const kinder = [];
  if (unbekannt.length) kinder.push(astKnoten('ty:unbekannt', 'Unbekannte Typen', unbekannt, { sym: 'ordner' }));
  kinder.push(astKnoten('ty:eintraege', 'Einträge', eintraege, { sym: 'ordner' }));
  return astKnoten('w:typenliste', 'Typenliste', kinder, { sym: 'koTypenliste', quelle: 'typenliste', kontext: 'typenliste', zahl: eintraege.length + unbekannt.length,
    tipp: 'Typenliste «Typname → Material». Doppelklick öffnet den Dialog.' });
}

// ---------------------------------------------------------------------------------------------- Quellen

/** Die Quellen: Titel, Merkmale-Liste (für die Gruppen unter «Weitere») und Bauer (ctx, passt, opt) → Ast. */
export const QUELLEN = {
  ansichten: { titel: 'Ansichten', bauen: ansichtenAst, elemente: (ctx) => ctx.derKlasse('Ansicht').filter((a) => a.werte?.vorlage !== true).map((a) => MERKMALE.ansichten(ctx, a)) },
  vorlagen: { titel: 'Ansichtsvorlagen', bauen: vorlagenAst },
  plaene: { titel: 'Pläne', bauen: plaeneAst, elemente: (ctx) => ctx.derKlasse('Planblatt').map((p) => MERKMALE.plaene(ctx, p)) },
  gebaeude: { titel: 'Gebäude', bauen: gebaeudeAst },
  huelle: { titel: 'Hülle', bauen: huelleAst },
  systeme: {
    titel: 'Systeme', bauen: systemeAst,
    elemente: (ctx) => [...ctx.derKlasse('System').map((s) => MERKMALE.systeme(ctx, s)),
      ...ctx.derKlasse('Anlage').filter((a) => !ctx.derKlasse('System').some((s) => lc(s.teilVon) === lc(a.kennzeichen))).map(() => ({ gewerke: [], haupt: '' }))],
  },
  unterlagen: { titel: 'Unterlagen', bauen: unterlagenAst },
  familien: { titel: 'Familien', bauen: familienAst, elemente: (ctx) => [...(ctx.familien ?? []), ...(ctx.bibliothek ?? [])].map((f) => MERKMALE.familien(ctx, f)) },
  katalog: { titel: 'Katalog', bauen: (ctx) => katalogAst(ctx) },
  kollisionen: { titel: 'Kollisionen', bauen: kollisionenAst },
  aussparungen: { titel: 'Aussparungen', bauen: aussparungenAst },
  typenliste: { titel: 'Typenliste', bauen: typenlisteAst },
};

// ---------------------------------------------------------------------------------------------- Baum einer Gliederung

/** Ast aus einem Eintrag der Tabelle; farbe: der Ast trägt das Gewerk der Gliederung (Hinterlegung). */
function hauptAst(def, a, ctx) {
  const q = QUELLEN[a.quelle];
  const k = q.bauen(ctx, astPasst(def, a), { titel: a.titel, mitKatalog: a.mitKatalog });
  return a.farbe && def.gewerk ? { ...k, gewerk: def.gewerk } : k;
}

/** «Weitere»: erst die Reste der nach Gewerk gruppierten Quellen, je Gewerk eine Gruppe (in der Farbe des Gewerks, «Ohne Gewerk» zuletzt), dann die übrigen Quellen. */
function weitere(def, ctx) {
  const quellen = weitereQuellen(def);
  const kinder = [];
  const gruppenQuellen = quellen.filter((q) => QUELLEN_NACH_GEWERK.includes(q));
  const rest = Object.fromEntries(quellen.map((q) => [q, restPasst(def, q)]));
  const haupts = new Set();
  for (const q of gruppenQuellen) for (const m of QUELLEN[q].elemente(ctx)) if (rest[q](m)) haupts.add(m.haupt ?? '');
  for (const g of gewerkeSortiert(ctx, haupts)) {
    const aeste = [];
    for (const q of gruppenQuellen) {
      const k = QUELLEN[q].bauen(ctx, (m) => rest[q](m) && (m.haupt ?? '') === g, { mitKatalog: false });
      if (blattZahl(k) > 0) aeste.push(praefix(k, `x:${g || '-'}:`));
    }
    if (aeste.length === 0) continue;
    kinder.push({ key: `x:gw:${g || '-'}`, label: g ? gewerkTitel(ctx, g) : 'Ohne Gewerk', kinder: aeste, zahl: aeste.reduce((s, k) => s + blattZahl(k), 0), sym: 'ordner', art: 'ast', gewerk: g || undefined,
      tipp: g ? `Weitere ${gewerkTitel(ctx, g)}: gehört nicht zu dieser Gliederung` : 'Elemente ohne Gewerk' });
  }
  for (const q of quellen.filter((x) => !QUELLEN_NACH_GEWERK.includes(x))) {
    const k = QUELLEN[q].bauen(ctx, rest[q] ? (m) => rest[q](m) : null, {});
    if (blattZahl(k) > 0) kinder.push(praefix(k, 'x:'));
  }
  if (kinder.length === 0) return null;
  return { key: 'x:weitere', label: 'Weitere', kinder, zahl: kinder.reduce((s, k) => s + blattZahl(k), 0), sym: 'ordner', art: 'ast', quelle: 'weitere',
    tipp: 'Alles, was nicht zu dieser Gliederung gehört – nichts verschwindet ganz.' };
}

/** Die Wurzelknoten einer Gliederung: die Äste der Tabelle in ihrer Reihenfolge, am Ende «Weitere» (nur wenn etwas darin steht). */
export function baumBauen(def, ctx) {
  const haupt = def.aeste.map((a) => hauptAst(def, a, ctx));
  const w = weitere(def, ctx);
  return w ? [...haupt, w] : haupt;
}
