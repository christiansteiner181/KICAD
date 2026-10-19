// Reine Funktionen für das Register «Koordination» (Modul 3b, Auftrag C1): Texte, HTML und Parameter der Dialoge Projektvorgaben, Kollisionen prüfen,
// Aussparungen vorschlagen / nummerieren / abgleichen, Aussparungsplan, IFC für den Architekten und BCF sowie der Tabellen der Palette «Koordination»
// (Kollisionsliste, Aussparungsliste). Kein Browser, keine Fachlogik: gezeigt wird, was der Kern liefert (Abfragen vorgaben, kollisionen, aussparungsliste,
// bcf_lesen; Trockenläufe der Befehle); gerechnet und entschieden wird im Kern.
// Grundsatz «keine Vorgabewerte in der Oberfläche»: die Felder der Projektvorgaben sind beim Öffnen leer, wenn der Kern keinen Wert hat; jeder Wert geht mit
// seiner Quelle (Pflicht). In Node getestet (koordination.test.mjs).
// Nachtrag C1b (Entscheide Christian 19.10.2026): Form der Aussparung rund | rechteckig (aussparung_form), keine Vorgabe grenze_kernbohrung mehr, Abstandsprüfung eingeklappt und aus,
// Paar «Bekleidungen» (hlks-bekleidung), Koordinaten für BCF und IFC wählbar (Vorgabe sendet nichts). Felder, die ein älterer Kern nicht liefert (formWaehlbar, material), werden nur gezeigt, wenn sie da sind.
// Nachtrag C1c (Kern B3d, Entscheide Christian 19.10.2026): Typenliste «Typname → Material» (Abfrage typmaterial, Befehl typmaterial_setzen: Dialog mit unbekannten Typen, Material aus Typname zum Bestätigen und den Einträgen),
// Paar «Installationsvorwände» (hlks-vorwand), Kollisionsliste mit Paar-Filter und standardmässig ohne Kollisionen der Paare, die der letzte Prüflauf nicht geprüft hat. Auch hier keine Fachlogik: Kategorien, Wirkung, Zahlen
// und Hinweise kommen vom Kern; hier stehen nur Anzeige, Auswahl und die Parameter des Befehls (jede Festlegung mit Quelle).

import { esc } from './einfuegen-text.js';
import { zahlLesen, zahlAnzeige, zahlFeld } from './huelle-text.js';

export { esc, zahlLesen, zahlAnzeige, zahlFeld };

const leer = (x) => x === null || x === undefined || (typeof x === 'string' && x.trim() === '');
const istObjekt = (x) => x !== null && typeof x === 'object' && !Array.isArray(x);
const text = (x) => (leer(x) ? '' : String(x).trim());
const gleicheMenge = (a, b) => a.length === b.length && a.every((x) => b.includes(x));

// ---------------------------------------------------------------------------------------------- Texte des Kerns für Menschen

/** Knöpfe der Oberfläche zu den Befehlen und Abfragen des Kerns (die Texte des Kerns sind für MCP geschrieben). */
export const KNOEPFE = {
  vorgaben_setzen: 'Projektvorgaben', kollision_pruefen: 'Kollisionen prüfen', kollision_status: 'Kollisionsliste', aussparungen_vorschlagen: 'Aussparungen vorschlagen',
  aussparungen_nummerieren: 'Aussparungen nummerieren', aussparung_status: 'Aussparungsliste', aussparungen_abgleichen: 'Abgleich', aussparungen_ifc: 'IFC für Architekt',
  bcf_import: 'BCF importieren', bcf_export: 'BCF exportieren', bcf_lesen: 'BCF importieren', ifc_uebernehmen: 'IFC übernehmen', element_loeschen: 'Löschen',
  element_aendern: 'Eigenschaften', elemente_suchen: 'Projektbrowser', aussparung_form: 'Als Kernbohrung / Als Rechteck', typmaterial_setzen: 'Typenliste',
};

/** Ein Text des Kerns für Menschen: «(vorgaben_setzen)» wird zu «(Dialog «Projektvorgaben …»)», nackte Namen heissen wie die Knöpfe. */
export const fuerMenschen = (t) => String(t ?? '')
  .replace(/\s*\(((?:[a-z0-9]+_)+[a-z0-9]+)[^)]*\)/g, (m, name) => (KNOEPFE[name] ? ` («${KNOEPFE[name]} …»)` : m))
  .replace(/\b((?:[a-z0-9]+_)+[a-z0-9]+)\b/g, (m, name) => (KNOEPFE[name] ? `«${KNOEPFE[name]} …»` : m))
  .replace(/\bAbfrage typmaterial\b/g, 'die Typenliste')
  .trim();

/** Text einer Ablehnung: Meldung und Vorschlag des Kerns. */
export const ablehnung = (r) => fuerMenschen([r?.fehler || r?.meldung || 'Abgelehnt.', r?.vorschlag ? `Vorschlag: ${r.vorschlag}` : ''].filter(Boolean).join(' – '));

export const STATUS_TEXT = { bestaetigt: 'bestätigt', ausgefuehrt: 'ausgeführt' };
export const statusText = (s) => STATUS_TEXT[s] ?? s ?? '';

// ---------------------------------------------------------------------------------------------- Listen der Auswahlen (Spiegel des Katalogs und des Kerns, vom Test geprüft)

/** Paare der Kollisionsprüfung (KollisionTexte.Paare). «hlks-bekleidung» (Teil B3c) und «hlks-vorwand» (Teil B3d, Entscheide Christian 19.10.2026) sind zuschaltbar und nie in der Vorgabe;
 *  kennt ein älterer Kern ein Paar noch nicht, meldet er es mit einem Fehler, den der Dialog zeigt. */
export const PAARE = [
  { id: 'hlks-architektur', titel: 'HLKS gegen Architektur und Tragwerk', hinweis: 'Wände, Decken, Stützen, Unterzüge – in KICAD gezeichnet oder aus IFC-Unterlagen' },
  { id: 'hlks-hlks', titel: 'HLKS untereinander', hinweis: 'Leitungen, Formteile, Geräte' },
  { id: 'hlks-fremd', titel: 'HLKS gegen fremde IFC-Modelle', hinweis: 'Produkte anderer Fachplaner' },
  { id: 'hlks-bekleidung', titel: 'Bekleidungen (Unterdecken, Verkleidungen, Bodenbeläge, Dämmungen)', hinweis: 'IfcCovering – nicht in der Vorgabe: Bekleidungen sind weder Kollision noch Aussparung, solange das Paar aus ist' },
  { id: 'hlks-vorwand', titel: 'Installationsvorwände (z. B. Geberit GIS)', hinweis: 'nicht in der Vorgabe: Leitungen in Vorwänden sind weder Kollision noch Aussparung, solange das Paar aus ist' },
];
/** Kurznamen der Paare für Filter und Spalte der Kollisionsliste (Titel und Hinweis sind für den Dialog zu lang). */
export const PAAR_KURZ = { 'hlks-architektur': 'Architektur', 'hlks-hlks': 'HLKS', 'hlks-fremd': 'Fremd-IFC', 'hlks-bekleidung': 'Bekleidungen', 'hlks-vorwand': 'Vorwände' };
export const paarText = (id) => PAAR_KURZ[id] ?? id ?? '';
/** Paare, die der Kern ohne Angabe prüft (KollisionTexte.PaareVorgabe) – die Oberfläche sendet `paare` nur bei Abweichung. */
export const PAARE_VORGABE = ['hlks-architektur', 'hlks-hlks'];
export const KOLLISION_STATUS = ['neu', 'bestaetigt', 'akzeptiert', 'erledigt', 'behoben'];
export const KOLLISION_ARTEN = ['hart', 'weich'];
export const KOLLISION_TYPEN = ['kontakt', 'durchdringung'];
export const AUSSPARUNG_STATUS = ['vorgeschlagen', 'freigegeben', 'versandt', 'angekommen', 'ausgefuehrt', 'veraltet', 'fehlt'];
export const IFC_FASSUNGEN = ['IFC4', 'IFC2X3'];

export const GEWERK_TEXT = { sanitaer: 'Sanitär', heizung: 'Heizung', lueftung: 'Lüftung', kaelte: 'Kälte', elektro: 'Elektro', msrl: 'MSRL', architektur: 'Architektur', fremd: 'Fremd-IFC', hlks: 'alle HLKS-Gewerke' };
export const seitenText = (s) => GEWERK_TEXT[s] ?? s;

export function geschossOptionenHtml(geschosse, gewaehlt = '', alle = '(alle)') {
  return `<option value="">${esc(alle)}</option>` + geschosse.map((g) => `<option value="${esc(g.kennzeichen)}"${g.kennzeichen === gewaehlt ? ' selected' : ''}>${esc(g.kennzeichen)}${g.name ? ' – ' + esc(g.name) : ''}</option>`).join('');
}

// ---------------------------------------------------------------------------------------------- Projektvorgaben

/**
 * Die Projektvorgaben (Namen wie im Katalog, Klasse Vorgaben). Labels und Einheiten sind Beschriftung, keine Werte.
 * gruppe: «pflicht» (Spiel und Zusammenfassen: nötig für Aussparungen), «wahl» (toleranz_hart: wie bisher wählbar), «abstand» (Abstandsprüfung, eingeklappt, Entscheid Christian 19.10.2026).
 */
export const VORGABEN = [
  { name: 'spiel_aussparung', label: 'Spiel um Leitungen bei Aussparungen', einheit: 'mm', art: 'zahl', gruppe: 'pflicht' },
  { name: 'abstand_zusammenfassen', label: 'Abstand zum Zusammenfassen benachbarter Aussparungen', einheit: 'mm', art: 'zahl', gruppe: 'pflicht' },
  { name: 'toleranz_hart', label: 'Toleranz hart (Durchdringung bis zu dieser Tiefe gilt als Berührung)', einheit: 'mm', art: 'zahl', gruppe: 'wahl' },
  { name: 'abstand_leitungen', label: 'Mindestabstand Leitung – Leitung (weiche Kollision)', einheit: 'mm', art: 'zahl', gruppe: 'abstand' },
  { name: 'abstand_weich', label: 'Abstand weich je Gewerkpaar', einheit: 'mm', art: 'objekt', gruppe: 'abstand' },
];
export const VORGABEN_ZAHL = VORGABEN.filter((v) => v.art === 'zahl');
/** Vorgaben, die im Katalog stehen, aber nichts mehr bewirken (Entscheid Christian 19.10.2026: Kernbohrungen nur von Hand). Kein Feld im Dialog, nur ein Hinweis mit «entfernen». */
export const VORGABEN_NICHT_MEHR = [{ name: 'grenze_kernbohrung', label: 'Grenze Kernbohrung / Aussparung (Durchmesser)' }];
export const NICHT_MEHR_VERWENDET_TEXT = 'nicht mehr verwendet (Entscheid Christian 19.10.2026)';
export const ABSTAND_ERKLAERUNG = 'Entscheid Christian 19.10.2026: keine Abstandsprüfung (keine Mindestabstände Leitung–Leitung bzw. Dämmung–Dämmung). Die weiche Prüfung bleibt aus; '
  + 'die Werte bleiben setzbar, falls später gewünscht. Sie wirken nur, wenn im Dialog «Kollisionen prüfen» die weiche Prüfung eingeschaltet ist.';
export const VORGABEN_QUELLENSATZ = 'Vorgaben nur mit Quelle (Bürostandard, Projektfestlegung mit Datum)';
const ABSTAND_NAMEN = VORGABEN.filter((v) => v.gruppe === 'abstand').map((v) => v.name);

/** Sind Abstände gesetzt (abstand_weich oder abstand_leitungen mit Quelle)? Dann ist die Abstandsprüfung wirksam, sobald die weiche Prüfung läuft. */
export const abstaendeGesetzt = (daten) => ABSTAND_NAMEN.some((n) => !!daten?.werte?.[n]);

/** Ist die nicht mehr verwendete Vorgabe in diesem Projekt gesetzt (mit oder ohne Quelle)? Quelle: `nichtMehrVerwendet` des Kerns, sonst Werte und `ohneQuelle`. */
export function nichtMehrGesetzt(daten, name) {
  const k = (daten?.nichtMehrVerwendet ?? []).find((x) => x.name === name);
  return !!(k?.gesetzt || daten?.werte?.[name] || (daten?.ohneQuelle ?? []).includes(name));
}

/** Schlüssel eines Gewerkpaars: klein, die Seiten alphabetisch (wie `Projektvorgaben.Schluessel` im Kern). */
export function paarSchluessel(a, b) {
  const x = text(a).toLowerCase(), y = text(b).toLowerCase();
  return x <= y ? `${x}-${y}` : `${y}-${x}`;
}

/** Formular der Projektvorgaben aus der Antwort der Abfrage `vorgaben` (leer, wo der Kern keinen Wert mit Quelle hat). */
export function vorgabenFormular(daten) {
  const werte = daten?.werte ?? {};
  const felder = {};
  for (const v of VORGABEN_ZAHL) {
    const w = werte[v.name];
    felder[v.name] = { wert: w ? zahlFeld(w.wert) : '', quelle: w?.quelle ?? '' };
  }
  const roh = werte.abstand_weich?.wert;
  const zeilen = istObjekt(roh) ? Object.entries(roh).map(([k, v]) => { const [a = '', b = ''] = k.split('-'); return { a, b, wert: zahlFeld(v) }; }) : [];
  // nicht mehr verwendete Vorgaben (grenze_kernbohrung): kein Feld, nur «gesetzt?» und die Vormerkung «beim Speichern entfernen»
  const alt = {};
  for (const v of VORGABEN_NICHT_MEHR) alt[v.name] = { gesetzt: nichtMehrGesetzt(daten, v.name), wert: werte[v.name] ? zahlFeld(werte[v.name].wert) : '', quelle: werte[v.name]?.quelle ?? '', entfernen: false };
  return { felder, weich: { zeilen, quelle: werte.abstand_weich?.quelle ?? '' }, alt };
}

const weichObjekt = (zeilen) => {
  const o = {};
  for (const z of zeilen) { const w = zahlLesen(z.wert); if (text(z.a) && text(z.b) && w !== null) o[paarSchluessel(z.a, z.b)] = w; }
  return o;
};
const objektGleich = (a, b) => { const ka = Object.keys(a).sort(), kb = Object.keys(b).sort(); return ka.length === kb.length && ka.every((k, i) => k === kb[i] && a[k] === b[k]); };

/**
 * Prüft das Formular gegen den Stand beim Öffnen und liefert { fehler[], warnungen[], werte } – `werte` ist der Inhalt von `vorgaben_setzen`:
 * nur Geändertes, ein geleerter Wert wird null (entfernt). Jeder Wert braucht eine Quelle.
 */
export function vorgabenAuswertung(form, bisher) {
  const fehler = [], warnungen = [], werte = {};
  for (const v of VORGABEN_ZAHL) {
    const f = form.felder[v.name], b = bisher.felder[v.name];
    const w = text(f.wert), q = text(f.quelle);
    if (w === '') {
      if (text(b.wert) !== '') werte[v.name] = null;
      else if (q !== '') fehler.push(`${v.label}: Quelle ohne Wert – Wert eintragen oder die Quelle leeren.`);
      continue;
    }
    const z = zahlLesen(w);
    if (z === null) { fehler.push(`${v.label}: «${w}» ist keine Zahl (${v.einheit}).`); continue; }
    if (z < 0) { fehler.push(`${v.label}: ${w} ist negativ.`); continue; }
    if (q === '') { fehler.push(`${v.label}: Quelle fehlt (Bürostandard, Projektfestlegung mit Datum …).`); continue; }
    const bz = zahlLesen(b.wert);
    if (bz === z && text(b.quelle) === q) continue;
    werte[v.name] = { wert: z, quelle: q };
    if (bz !== null && bz !== z && text(b.quelle) === q) warnungen.push(`${v.label}: Wert geändert, Quelle gleich geblieben – gilt «${q}» auch für ${zahlAnzeige(z)} ${v.einheit}?`);
  }
  // nicht mehr verwendete Vorgaben: nur das vorgemerkte Entfernen geht an den Kern (null); neu gesetzt wird so ein Wert nie
  for (const v of VORGABEN_NICHT_MEHR) if (form.alt?.[v.name]?.gesetzt && form.alt[v.name].entfernen === true) werte[v.name] = null;
  // Abstand weich: ein Objekt je Gewerkpaar mit einer Quelle für das Ganze
  const zeilen = form.weich.zeilen.filter((z) => !(text(z.a) === '' && text(z.b) === '' && text(z.wert) === ''));
  const neu = {};
  let ok = true;
  zeilen.forEach((z, i) => {
    const nr = `Abstand weich, Zeile ${i + 1}`;
    const w = zahlLesen(z.wert);
    if (!text(z.a) || !text(z.b)) { fehler.push(`${nr}: beide Seiten des Gewerkpaars wählen.`); ok = false; return; }
    if (w === null) { fehler.push(`${nr}: «${text(z.wert)}» ist keine Zahl (mm).`); ok = false; return; }
    if (w < 0) { fehler.push(`${nr}: ${text(z.wert)} ist negativ.`); ok = false; return; }
    const k = paarSchluessel(z.a, z.b);
    if (k in neu) { fehler.push(`${nr}: das Paar ${k} steht doppelt (die Reihenfolge der Seiten zählt nicht).`); ok = false; return; }
    neu[k] = w;
  });
  if (ok) {
    const alt = weichObjekt(bisher.weich.zeilen), q = text(form.weich.quelle), qAlt = text(bisher.weich.quelle);
    if (Object.keys(neu).length === 0) {
      if (Object.keys(alt).length > 0) werte.abstand_weich = null;
      else if (q !== '') fehler.push('Abstand weich: Quelle ohne Paar – ein Gewerkpaar eintragen oder die Quelle leeren.');
    } else if (q === '') fehler.push('Abstand weich: Quelle fehlt (eine Quelle für alle Gewerkpaare).');
    else if (!objektGleich(neu, alt) || q !== qAlt) {
      werte.abstand_weich = { wert: neu, quelle: q };
      if (q === qAlt && Object.keys(alt).length > 0) warnungen.push(`Abstand weich: Paare geändert, Quelle gleich geblieben – gilt «${q}» auch für die neuen Werte?`);
    }
  }
  return { fehler, warnungen, werte };
}

/** Parameter von `vorgaben_setzen`; null, wenn nichts zu senden ist. */
export function vorgabenParameter(form, bisher) {
  const a = vorgabenAuswertung(form, bisher);
  return Object.keys(a.werte).length === 0 ? null : { werte: a.werte };
}

/** Kopiert die erste ausgefüllte Quelle in alle Zeilen mit Wert und leerer Quelle. Gibt die Zahl der gefüllten Zeilen zurück. */
export function quelleUebernehmen(form) {
  const erste = [...VORGABEN_ZAHL.map((v) => form.felder[v.name]).filter((f) => text(f.wert) !== '' && text(f.quelle) !== '').map((f) => text(f.quelle)),
    ...(form.weich.zeilen.length > 0 && text(form.weich.quelle) !== '' ? [text(form.weich.quelle)] : [])][0];
  if (!erste) return 0;
  let n = 0;
  for (const v of VORGABEN_ZAHL) { const f = form.felder[v.name]; if (text(f.wert) !== '' && text(f.quelle) === '') { f.quelle = erste; n++; } }
  if (form.weich.zeilen.some((z) => text(z.wert) !== '') && text(form.weich.quelle) === '') { form.weich.quelle = erste; n++; }
  return n;
}

const wirkungVon = (daten, name) => (daten?.fehlend ?? []).find((f) => f.name === name)?.wirkung ?? null;

/** Seiten für die Auswahl der Gewerkpaare (Kern: abstandWeichSeiten); fehlt die Liste, zeigt die Auswahl nur die gesetzten. */
export function weichSeiten(daten, form) {
  const aus = new Set(daten?.abstandWeichSeiten ?? []);
  for (const z of form?.weich?.zeilen ?? []) { if (text(z.a)) aus.add(z.a); if (text(z.b)) aus.add(z.b); }
  return [...aus];
}

export function seitenOptionenHtml(seiten, gewaehlt) {
  return `<option value="">(wählen)</option>` + seiten.map((s) => `<option value="${esc(s)}"${s === gewaehlt ? ' selected' : ''}>${esc(seitenText(s))}</option>`).join('');
}

export function weichZeileHtml(i, z, seiten) {
  return `<tr data-i="${i}"><td><select class="w-a" aria-label="Seite A">${seitenOptionenHtml(seiten, z.a)}</select></td>`
    + `<td><select class="w-b" aria-label="Seite B">${seitenOptionenHtml(seiten, z.b)}</select></td>`
    + `<td><input type="text" class="w-wert zahl" inputmode="decimal" value="${esc(z.wert)}" aria-label="Abstand mm" data-kein-enter="1"> <span class="leise">mm</span></td>`
    + `<td><button type="button" class="chip w-weg" title="Zeile entfernen">✕</button></td></tr>`;
}

/** Zustandstext einer Vorgabe: gesetzt (mit Herkunft) oder fehlt (mit Wirkung des Kerns). */
export function vorgabeZustandHtml(daten, name) {
  const w = daten?.werte?.[name];
  if (w) return `<span class="huelle-ok">gesetzt</span> <span class="leise">${esc(w.gesetzt ?? '')}</span>`;
  if ((daten?.ohneQuelle ?? []).includes(name)) return '<span class="rot">Wert ohne Quelle – gilt nicht. Neu setzen mit Quelle.</span>';
  // Abstände sind kein Mangel: ohne sie bleibt die Abstandsprüfung aus (Entscheid Christian 19.10.2026)
  if (ABSTAND_NAMEN.includes(name)) return '<span class="leise kp-aus">nicht gesetzt – Abstandsprüfung aus</span>';
  const wirkung = wirkungVon(daten, name);
  return `<span class="kp-fehlt">fehlt</span>${wirkung ? ` <span class="leise">– ${esc(fuerMenschen(wirkung))}</span>` : ''}`;
}

/** Wie viele der Vorgaben für Aussparungen und harte Kollisionen (Pflicht und wählbar, ohne Abstände) sind gesetzt? { gesetzt, gesamt }. */
export function vorgabenZaehlung(daten) {
  const kern = VORGABEN.filter((v) => v.gruppe !== 'abstand');
  return { gesetzt: kern.filter((v) => !!daten?.werte?.[v.name]).length, gesamt: kern.length };
}

const vorgabeZeileHtml = (daten, form, v) => {
  const f = form.felder[v.name];
  const pflicht = v.gruppe === 'pflicht' ? '<div class="leise kp-pflicht">Pflicht für Aussparungen</div>' : v.gruppe === 'wahl' ? '<div class="leise kp-pflicht">wählbar</div>' : '';
  return `<tr data-name="${v.name}"><td title="${esc(daten?.werte?.[v.name]?.beschreibung ?? '')}">${esc(v.label)}<div class="leise kp-name">${v.name}</div>${pflicht}</td>`
    + `<td class="v-wertzelle"><input type="text" class="v-wert zahl" inputmode="decimal" value="${esc(f.wert)}" aria-label="${esc(v.label)}" data-kein-enter="1"> <span class="leise">${v.einheit}</span></td>`
    + `<td><input type="text" class="v-quelle" value="${esc(f.quelle)}" placeholder="Bürostandard, Projektfestlegung …" aria-label="Quelle ${esc(v.name)}" data-kein-enter="1"></td>`
    + `<td class="v-zustand">${vorgabeZustandHtml(daten, v.name)}</td></tr>`;
};
const vorgabenTabelle = (zeilen, klasse = '') => `<table class="tab huelle-kennwerte kp-vorgaben${klasse ? ' ' + klasse : ''}"><thead><tr><th>Vorgabe</th><th>Wert</th><th>Quelle *</th><th>Zustand</th></tr></thead><tbody>${zeilen}</tbody></table>`;

/** Hinweis auf eine nicht mehr verwendete Vorgabe (nur wenn sie im Projekt gesetzt ist) mit Knopf «entfernen» (bzw. «doch behalten», solange nicht gespeichert). */
export function nichtMehrHtml(daten, form) {
  return VORGABEN_NICHT_MEHR.filter((v) => form.alt?.[v.name]?.gesetzt).map((v) => {
    const a = form.alt[v.name];
    const wert = text(a.wert) !== '' ? ` Gesetzt ist ${esc(a.wert)} mm${a.quelle ? ` (Quelle: ${esc(a.quelle)})` : ''}.` : '';
    const grund = (daten?.nichtMehrVerwendet ?? []).find((x) => x.name === v.name)?.hinweis;
    return `<div class="hkap-kasten kp-nichtmehr" data-alt="${v.name}"><b>${esc(v.name)}</b> (${esc(v.label)}): <b>${esc(NICHT_MEHR_VERWENDET_TEXT)}</b>.${wert} `
      + `${esc(fuerMenschen(grund ? grund.replace(/^nicht mehr verwendet \(Entscheid Christian 19\.10\.2026\):\s*/, '') : 'Kernbohrungen wählt nur ein Mensch (Aussparungsliste, «Als Kernbohrung»).'))} `
      + (a.entfernen ? '<span class="kp-wird-entfernt">Wird beim Speichern entfernt.</span> <button type="button" class="chip v-alt-behalten" title="Den Wert doch stehen lassen">doch behalten</button>'
        : '<button type="button" class="chip v-alt-weg" title="Den Wert beim Speichern aus den Projektvorgaben entfernen">entfernen</button>')
      + '</div>';
  }).join('');
}

/**
 * HTML der Projektvorgaben: Pflicht (Spiel, Zusammenfassen) und toleranz_hart mit Quelle und Zustand; darunter der eingeklappte Bereich «Abstandsprüfung (aus)» mit
 * abstand_leitungen und den Gewerkpaaren (abstand_weich). grenze_kernbohrung hat kein Feld mehr: ist sie im Projekt gesetzt, steht ein Hinweis mit «entfernen» da.
 * opt.abstandOffen: ob der Bereich aufgeklappt ist (die Oberfläche merkt es über das Neuzeichnen hinweg).
 */
export function vorgabenHtml(daten, form, { abstandOffen = false } = {}) {
  const kopf = `<p class="huelle-regel"><b>${esc(VORGABEN_QUELLENSATZ)}.</b> KICAD hat keine eingebauten Werte: was fehlt, fehlt – der Kern nennt dann bei der Prüfung, was ausfällt. `
    + `<button type="button" class="chip v-quelle-alle" title="Die erste ausgefüllte Quelle in alle Zeilen mit Wert und leerer Quelle übernehmen">Quelle in leere Zeilen übernehmen</button></p>`;
  const z = vorgabenZaehlung(daten);
  const nAbstand = ABSTAND_NAMEN.filter((n) => !!daten?.werte?.[n]).length;
  const zusammen = daten?.element
    ? `<p class="leise">${esc(daten.element)} · ${z.gesetzt} von ${z.gesamt} Vorgaben gesetzt${z.gesetzt < z.gesamt ? `, ${z.gesamt - z.gesetzt} fehlen` : ''} · Abstandsprüfung ${nAbstand ? `mit ${nAbstand} Wert(en)` : 'aus'}.</p>`
    : '<p class="leise">Noch keine Projektvorgaben angelegt – der erste gespeicherte Wert legt sie an (Element VG-1).</p>';
  const zahlen = VORGABEN_ZAHL.filter((v) => v.gruppe !== 'abstand').map((v) => vorgabeZeileHtml(daten, form, v)).join('');
  const seiten = weichSeiten(daten, form);
  const leitungen = VORGABEN_ZAHL.filter((v) => v.gruppe === 'abstand').map((v) => vorgabeZeileHtml(daten, form, v)).join('');
  const weich = `<h3>Abstand weich je Gewerkpaar <span class="leise kp-name">abstand_weich</span></h3>`
    + `<p class="leise">Mindestabstand (mm) zwischen den Dämmungshüllen, je Paar von Gewerken; «Architektur» gilt für Wände, Decken und Tragwerk, «alle HLKS-Gewerke» für jedes HLKS-Gewerk. Die Reihenfolge der Seiten zählt nicht, eine Seite muss ein HLKS-Gewerk sein (prüft der Kern).</p>`
    + `<table class="tab huelle-kennwerte kp-weich"><thead><tr><th>Seite A</th><th>Seite B</th><th>Abstand</th><th></th></tr></thead><tbody class="v-weich">`
    + form.weich.zeilen.map((zl, i) => weichZeileHtml(i, zl, seiten)).join('') + `</tbody></table>`
    + `<p class="huelle-leiste"><button type="button" class="chip v-weich-neu">＋ Gewerkpaar</button>`
    + `<label class="fz kp-weichquelle"><span>Quelle (für alle Paare) *</span><input type="text" class="v-weich-quelle" value="${esc(form.weich.quelle)}" placeholder="Bürostandard, Projektfestlegung …" data-kein-enter="1"></label></p>`
    + `<p class="v-zustand-weich">${vorgabeZustandHtml(daten, 'abstand_weich')}</p>`;
  const abstand = `<details class="kp-abstand"${abstandOffen ? ' open' : ''}><summary>Abstandsprüfung (${nAbstand ? `${nAbstand} Wert(e) gesetzt` : 'aus'})</summary>`
    + `<p class="leise kp-abstand-text">${esc(ABSTAND_ERKLAERUNG)}</p>${vorgabenTabelle(leitungen, 'kp-vorgaben-abstand')}${weich}</details>`;
  const nichtMehr = VORGABEN_NICHT_MEHR.map((v) => v.name);
  const ohneListe = (daten?.ohneQuelle ?? []).filter((n) => !nichtMehr.includes(n));
  const ohne = ohneListe.length
    ? `<div class="fehlerkasten" role="alert"><b>Ohne Quelle (gelten nicht): ${esc(ohneListe.join(', '))}</b><div>Neu setzen, mit Quelle.</div></div>` : '';
  return `${kopf}${zusammen}${ohne}${nichtMehrHtml(daten, form)}${vorgabenTabelle(zahlen)}${abstand}`;
}

/** Vorschau des Trockenlaufs von `vorgaben_setzen` (Hinweise des Kerns oder Ablehnung). */
export function vorgabenVorschauHtml(r, auswertung) {
  const warn = (auswertung?.warnungen ?? []).length ? `<ul class="hinweisliste warnung">${auswertung.warnungen.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : '';
  if (!r.angenommen) return `${warn}<div class="fehlerkasten" role="alert"><b>${esc(fuerMenschen(r.fehler || 'Abgelehnt.'))}</b>${r.vorschlag ? `<div>Vorschlag: ${esc(fuerMenschen(r.vorschlag))}</div>` : ''}</div>`;
  const h = (r.hinweise ?? []).filter(Boolean);
  return `${warn}<p class="leise">Vorschau des Kerns (Trockenlauf – nichts wurde geändert):</p>${h.length ? `<ul class="hinweisliste huelle-neu">${h.map((t) => `<li>${esc(fuerMenschen(t))}</li>`).join('')}</ul>` : ''}`;
}

// ---------------------------------------------------------------------------------------------- Läufe: Hinweise des Kerns

const WARNT = /fehlt|fehlen|übersprungen|entfällt|Keine Projektvorgaben|unbestimmter Tiefe|Als erledigt markiert|nicht nachgeführt|nicht abgeglichen|veraltet|unter abstand_zusammenfassen|nicht automatisch zusammengefasst|nicht mehr verwendet|passt auf kein Bauteil|Schreibweise prüfen|gilt nicht|steht mehrfach|Netzdatei fehlt/;

/** Hinweise des Kerns als Liste: Zeilen mit zwei Leerzeichen am Anfang sind eingerückt, Zeilen über Fehlendes in Warnfarbe. */
export function hinweiseListeHtml(hinweise, { max = 200 } = {}) {
  const h = (hinweise ?? []).filter(Boolean);
  if (h.length === 0) return '';
  const zeilen = h.slice(0, max).map((t, i) => {
    const eingerueckt = /^\s{2,}/.test(t);
    return `<li class="${[eingerueckt ? 'kp-ein' : '', WARNT.test(t) ? 'kp-warn' : '', i === 0 ? 'kp-kopf-zeile' : ''].filter(Boolean).join(' ')}">${esc(fuerMenschen(t))}</li>`;
  });
  if (h.length > max) zeilen.push(`<li class="leise">… und ${h.length - max} weitere</li>`);
  return `<ul class="hinweisliste kp-hinweise">${zeilen.join('')}</ul>`;
}

/** Ergebnis eines (Trocken-)Laufs: Titel, Hinweise des Kerns; abgelehnt als roter Kasten mit Vorschlag. */
export function laufHtml(r, { trocken = false, titel = '' } = {}) {
  if (!r) return '';
  if (!r.angenommen) return `<div class="fehlerkasten" role="alert"><b>${esc(fuerMenschen(r.fehler || r.meldung || 'Abgelehnt.'))}</b>${r.vorschlag ? `<div>Vorschlag: ${esc(fuerMenschen(r.vorschlag))}</div>` : ''}</div>`;
  const t = titel || (trocken ? 'Vorschau (Trockenlauf – nichts wurde geändert)' : 'Ergebnis');
  const n = (r.aenderungen ?? []).length;
  return `<h4 class="${trocken ? 'kp-vorschau' : 'kp-ergebnis'}">${esc(t)}</h4>${hinweiseListeHtml(r.hinweise)}${hinweiseListeHtml(r.hinweise) === '' ? `<p class="leise">${n ? `${n} Änderung(en).` : 'Keine Änderung.'}</p>` : ''}`;
}

// ---------------------------------------------------------------------------------------------- Kollisionen prüfen

/** Prüfung des Formulars «Kollisionen prüfen»: true oder ein Text. */
export function pruefenPruefen(f) {
  if (!f.paare || f.paare.length === 0) return 'Mindestens ein Paar wählen.';
  return true;
}

/**
 * Parameter von `kollision_pruefen`: `paare`, `unterlagen`, `geschoss` nur bei Abweichung von den Vorgaben des Kerns; `weich` immer ausdrücklich (true oder false), weil die
 * Abstandsprüfung aus ist (Entscheid Christian 19.10.2026) und die Vorgabe des Kerns sich damit ändern kann – so hängt der Lauf nicht von ihr ab.
 * f = { paare[], unterlagen[], alleUnterlagen[], geschoss, weich }. Ohne Angabe `weich` (undefined) geht nichts mit.
 */
export function pruefenParameter(f) {
  const p = {};
  if (!gleicheMenge(f.paare, PAARE_VORGABE)) p.paare = PAARE.map((x) => x.id).filter((id) => f.paare.includes(id));
  const alle = f.alleUnterlagen ?? [];
  if ((f.unterlagen ?? []).length > 0 && f.unterlagen.length < alle.length) p.unterlagen = [...f.unterlagen];
  if (text(f.geschoss)) p.geschoss = text(f.geschoss);
  if (f.weich === true || f.weich === false) p.weich = f.weich;
  return p;
}

/** Vorgabe des Dialogs für «weiche Prüfung»: aus, solange keine Abstände gesetzt sind (Entscheid Christian 19.10.2026: keine Abstandsprüfung); sind Abstände gesetzt, an. */
export const weichVorgabe = (daten) => abstaendeGesetzt(daten);
export const WEICH_HINWEIS_OHNE = 'Keine Abstände in den Projektvorgaben gesetzt – die Abstandsprüfung ist aus (Entscheid Christian 19.10.2026), die weiche Prüfung hätte nichts zu prüfen. '
  + 'Abstände lassen sich in «Projektvorgaben …» unter «Abstandsprüfung» setzen.';

/** Vorgaben, die der Prüflauf braucht und die fehlen (mit Wirkung des Kerns). */
export const PRUEF_VORGABEN = ['toleranz_hart', 'abstand_weich', 'abstand_leitungen'];
/** Fehlende Vorgaben des Prüflaufs. Die Abstände zählen nur bei eingeschalteter weicher Prüfung (opt.weich); ohne Angabe wie bisher alle. */
export function fehlendeFuerPruefung(daten, { weich = true } = {}) {
  return (daten?.fehlend ?? []).filter((f) => PRUEF_VORGABEN.includes(f.name) && (weich || !ABSTAND_NAMEN.includes(f.name)));
}
export function fehlendeHtml(liste, titel = 'Fehlende Projektvorgaben') {
  if (!liste || liste.length === 0) return '';
  return `<div class="hkap-kasten kp-fehlende"><b>${esc(titel)}</b><ul class="hinweisliste">${liste.map((f) => `<li><b>${esc(f.name)}</b> – ${esc(fuerMenschen(f.wirkung ?? f.beschreibung ?? ''))}</li>`).join('')}</ul></div>`;
}

// ---------------------------------------------------------------------------------------------- Kollisionsliste (Palette)

/** Parameter der Abfrage `kollisionen` aus dem Filter; null, wenn kein Status gewählt ist (dann ist die Liste leer). f = { status[], art, typ, geschoss, element }. */
export function kollisionFilterParameter(f) {
  if (!f.status || f.status.length === 0) return null;
  const p = {};
  if (f.status.length < KOLLISION_STATUS.length) p.status = KOLLISION_STATUS.filter((s) => f.status.includes(s)).join(',');
  if (text(f.art)) p.art = text(f.art);
  if (text(f.typ)) p.typ = text(f.typ);
  if (text(f.geschoss)) p.geschoss = text(f.geschoss);
  if (text(f.element)) p.element = text(f.element);
  return p;
}

/** Filter der Kollisionsliste. status, art, typ, geschoss, element gehen an die Abfrage `kollisionen`; paar und auchNichtGeprueft wirken im Browser (kollisionSicht). */
export const KOLLISION_FILTER_VORGABE = () => ({ status: [...KOLLISION_STATUS], art: '', typ: '', geschoss: '', element: '', paar: '', auchNichtGeprueft: false });

// ---- Paare des letzten Prüflaufs (Entscheid Hauptlauf C1c): Kollisionen eines Paars, das der letzte Lauf nicht geprüft hat (z. B. alte Vorwand- oder Bekleidungs-Kollisionen mit Status «neu»,
// die kein Lauf mehr nachführt), sind in der Liste ausgeblendet; ein Schalter blendet sie ein.

/**
 * Paare des letzten Prüflaufs aus `kollisionslauf` der Kollisionsliste (Projektinformation.kollisionslauf = «JJJJ-MM-TT hh:mm · Lauf n · hlks-architektur, hlks-hlks»).
 * Null, wenn der Text keine Paare nennt (kein Lauf, älteres Format «… · 120 Paare»): dann gilt die Vorgabe des Kerns plus die zuletzt im Dialog gewählten (geprueftePaare).
 */
export function laufPaare(kollisionslauf) {
  const teile = text(kollisionslauf).split(' · ');
  if (teile.length < 3) return null;
  const ids = teile[teile.length - 1].split(',').map((s) => s.trim()).filter(Boolean);
  return ids.length > 0 && ids.every((s) => /^hlks-[a-z]+$/.test(s)) ? ids : null;
}

/** Geprüfte Paare für die Liste: die des letzten Laufs, sonst PAARE_VORGABE und die zuletzt im Dialog gewählten Paare. */
export const geprueftePaare = (daten, zuletzt = []) => laufPaare(daten?.kollisionslauf) ?? [...new Set([...PAARE_VORGABE, ...(zuletzt ?? [])])];

const zaehle = (zeilen, feld) => {
  const o = {};
  for (const z of zeilen) { const k = z[feld] ?? ''; o[k] = (o[k] ?? 0) + 1; }
  return Object.fromEntries(Object.entries(o).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)));
};

/**
 * Die Liste, wie sie die Palette zeigt: Antwort der Abfrage `kollisionen` mit Paar-Filter (f.paar) und ohne Kollisionen nicht geprüfter Paare, solange f.auchNichtGeprueft aus ist.
 * Wer ein Paar ausdrücklich wählt, sieht es auch, wenn es nicht geprüft wurde. Zeilen ohne `paar` (älterer Kern) bleiben stehen.
 * Ergebnis: dieselbe Form wie die Antwort (zeilen, anzahl, zusammenfassung neu gezählt) plus geprueftePaare, nichtGeprueft (alle dieser Paare in der Antwort), ausgeblendet (gerade nicht gezeigt) und paarHerkunft ('lauf' | 'vorgabe').
 */
export function kollisionSicht(daten, f = {}, { zuletzt = [] } = {}) {
  if (!daten) return daten;
  const alle = daten.zeilen ?? [];
  const geprueft = geprueftePaare(daten, zuletzt);
  const nicht = (z) => !!z.paar && !geprueft.includes(z.paar);
  const einblenden = f.auchNichtGeprueft === true || !!f.paar;
  const gewaehlt = f.paar ? alle.filter((z) => z.paar === f.paar) : alle;
  const zeilen = einblenden ? gewaehlt : gewaehlt.filter((z) => !nicht(z));
  const z0 = daten.zusammenfassung ?? {};
  return {
    ...daten, zeilen, anzahl: zeilen.length, geprueftePaare: geprueft, paarHerkunft: laufPaare(daten.kollisionslauf) ? 'lauf' : 'vorgabe',
    nichtGeprueft: alle.filter(nicht).length, ausgeblendet: gewaehlt.length - zeilen.length, jePaarAntwort: zaehle(alle, 'paar'),
    // nichts ausgeblendet: die Zahlen des Kerns bleiben, wie sie sind; sonst neu gezählt
    zusammenfassung: zeilen.length === alle.length ? z0 : { ...z0, jeStatus: zaehle(zeilen, 'status'), jeArt: zaehle(zeilen, 'art'), jeTyp: zaehle(zeilen, 'typ'), jePaar: zaehle(zeilen, 'paar') },
  };
}

/** Beschriftung des Schalters «auch nicht geprüfte Paare» mit der Zahl der ausgeblendeten bzw. eingeblendeten Kollisionen. */
export function ungeprueftText(sicht, an) {
  const n = sicht?.nichtGeprueft ?? 0;
  if (n === 0) return 'auch nicht geprüfte Paare (keine Kollision betroffen)';
  if (an) return `auch nicht geprüfte Paare (${n} eingeblendet)`;
  return (sicht?.ausgeblendet ?? 0) > 0 ? `auch nicht geprüfte Paare (${sicht.ausgeblendet} ausgeblendet)` : `auch nicht geprüfte Paare (${n} vorhanden, mit dem gewählten Paar sichtbar)`;
}

/** Hinweis zum Schalter: welche Paare der letzte Lauf geprüft hat und woher das bekannt ist. */
export function ungeprueftTitel(sicht) {
  const p = (sicht?.geprueftePaare ?? []).map(paarText).join(', ');
  return sicht?.paarHerkunft === 'lauf'
    ? `Der letzte Prüflauf hat geprüft: ${p}. Kollisionen anderer Paare führt kein Lauf mehr nach; sie sind ausgeblendet, solange der Schalter aus ist.`
    : `Der Kern nennt die Paare des letzten Laufs nicht – angenommen werden die Vorgabe und die zuletzt im Dialog gewählten Paare: ${p}.`;
}

/** Auswahlliste des Paar-Filters: «(alle Paare)» und jedes Paar mit der Zahl seiner Kollisionen in der Antwort; nicht geprüfte tragen den Vermerk. */
export function paarOptionenHtml(sicht, gewaehlt = '') {
  const je = sicht?.jePaarAntwort ?? {};
  const geprueft = sicht?.geprueftePaare ?? [];
  return `<option value="">(alle Paare)</option>` + PAARE.map((p) => {
    const n = je[p.id] ?? 0;
    const vermerk = n > 0 && sicht?.geprueftePaare && !geprueft.includes(p.id) ? ', nicht geprüft' : '';
    return `<option value="${esc(p.id)}"${p.id === gewaehlt ? ' selected' : ''}>${esc(paarText(p.id))}${sicht ? ` (${n}${vermerk})` : ''}</option>`;
  }).join('');
}

const kurz = (t, n = 70) => { const s = text(t).replace(/\s+/g, ' '); return s.length > n ? s.slice(0, n - 1) + '…' : s; };

/** Kennzahlen über der Liste: «12 Kollisionen · neu 10, bestätigt 2 · hart 9, weich 3 · letzter Prüflauf …». */
export function kollisionKopf(daten) {
  if (!daten) return '';
  const je = (o) => Object.entries(o ?? {}).map(([k, v]) => `${statusText(k)} ${v}`).join(', ');
  const z = daten.zusammenfassung ?? {};
  const teile = [`${daten.anzahl ?? (daten.zeilen ?? []).length} Kollisionen`];
  if (daten.ausgeblendet > 0) teile.push(`${daten.ausgeblendet} nicht geprüfte ausgeblendet`);
  if (Object.keys(z.jeStatus ?? {}).length) teile.push(je(z.jeStatus));
  if (Object.keys(z.jeArt ?? {}).length) teile.push(je(z.jeArt));
  if (daten.kollisionslauf) teile.push(`letzter Prüflauf: ${daten.kollisionslauf}`);
  return teile.join(' · ');
}

export function kollisionTabelleHtml(daten, { auswahl = new Set(), aktiv = null, max = 500 } = {}) {
  const zeilen = daten?.zeilen ?? [];
  if (zeilen.length === 0) {
    if (!daten?.hinweis && daten?.ausgeblendet > 0) return `<p class="leise kp-leer">Keine Kollision in den geprüften Paaren (${esc((daten.geprueftePaare ?? []).map(paarText).join(', '))}) – ${daten.ausgeblendet} Kollision(en) nicht geprüfter Paare sind ausgeblendet («auch nicht geprüfte Paare» zeigt sie).</p>`;
    return `<p class="leise kp-leer">${esc(daten?.hinweis || 'Keine Kollision passt zum Filter.')}</p>`;
  }
  const sichtbar = zeilen.slice(0, max);
  const alleGewaehlt = sichtbar.length > 0 && sichtbar.every((z) => auswahl.has(z.kollision));
  const mitPaar = zeilen.some((z) => 'paar' in z);   // ältere Kerne liefern kein Paar
  const kopf = `<tr><th><input type="checkbox" class="kp-alle" title="Alle sichtbaren wählen"${alleGewaehlt ? ' checked' : ''}></th><th>Kollision</th><th>Art</th><th>Typ</th>${mitPaar ? '<th>Paar</th>' : ''}<th>Status</th><th class="zahl">Mass mm</th><th>Geschoss</th><th>Beteiligte</th><th>Kommentar</th></tr>`;
  const geprueft = daten?.geprueftePaare ?? null;
  const paarZelle = (z) => (geprueft && z.paar && !geprueft.includes(z.paar)
    ? `<td class="kp-paar-aus" title="${esc(paarText(z.paar))}: im letzten Prüflauf nicht geprüft – der Status wird nicht mehr nachgeführt">${esc(paarText(z.paar))} (nicht geprüft)</td>`
    : `<td title="${esc(z.paar ?? '')}">${esc(paarText(z.paar))}</td>`);
  const reihen = sichtbar.map((z) => `<tr class="kp-zeile${aktiv === z.kollision ? ' aktiv' : ''}" data-kz="${esc(z.kollision)}">`
    + `<td><input type="checkbox" class="kp-wahl" aria-label="${esc(z.kollision)} wählen"${auswahl.has(z.kollision) ? ' checked' : ''}></td>`
    + `<td>${esc(z.kollision)}</td><td class="kp-art-${esc(z.art)}">${esc(z.art)}</td><td>${esc(z.typ)}</td>${mitPaar ? paarZelle(z) : ''}<td class="kp-st-${esc(z.status)}">${esc(statusText(z.status))}</td>`
    + `<td class="zahl">${esc(zahlAnzeige(z.mass_mm, 1))}</td><td>${esc(z.geschoss ?? '–')}</td><td title="${esc(z.beteiligte ?? '')}">${esc(kurz(z.beteiligte, 80))}</td><td title="${esc(z.kommentar ?? '')}">${esc(kurz(z.kommentar, 40))}</td></tr>`).join('');
  const mehr = zeilen.length > max ? `<tr><td colspan="${9 + (mitPaar ? 1 : 0)}" class="leise">… und ${zeilen.length - max} weitere – Filter einengen.</td></tr>` : '';
  return `<table class="tab kp-tab"><thead>${kopf}</thead><tbody>${reihen}${mehr}</tbody></table>`;
}

/** Einzelheiten der aktiven Kollision: Beteiligte (Elemente des Modells sind anklickbar), Lage, Prüflauf, Kommentar. */
export function kollisionDetailHtml(z) {
  if (!z) return '<span class="leise">Eine Zeile wählen: sie wird im Grundriss gezeigt.</span>';
  const bet = (z.beteiligteListe ?? []).map((b) => (b.kennzeichen
    ? `<button type="button" class="chip kp-beteiligt" data-ref="${esc(b.kennzeichen)}" title="Im Grundriss zeigen">${esc(b.text)}</button>`
    : `<span class="chip kp-ifc" title="${esc(b.kennung ?? '')}">${esc(b.text)}</span>`)).join(' ');
  const lage = Array.isArray(z.lage_mm) ? `Lage x ${zahlAnzeige(z.lage_mm[0], 1)} · y ${zahlAnzeige(z.lage_mm[1], 1)} · z ${zahlAnzeige(z.lage_mm[2], 1)} mm` : '';
  const k = text(z.kommentar);
  return `<b>${esc(z.kollision)}</b> <span class="leise">${esc(z.art)} ${esc(z.typ)}${z.bauteil ? ' · Bauteil ' + esc(z.bauteil) : ''}</span> ${bet}`
    + `<div class="leise">${esc(lage)}${z.pruefung ? ` · ${esc(z.pruefung)}` : ''}</div>${k ? `<div class="kp-kommentar">${esc(k)}</div>` : ''}`;
}

/**
 * Welche Zeilen gelten für eine Aktion: die angehakten (die es in der Liste noch gibt); ohne Häkchen die aktive Zeile.
 * auswahl: Set der angehakten Schlüssel, zeilen: aktuelle Zeilen, aktiv: Schlüssel der aktiven Zeile, schluessel: Feldname der Zeile.
 */
export function ausgewaehlte(auswahl, zeilen, aktiv, schluessel = 'kollision') {
  const da = new Set(zeilen.map((z) => z[schluessel]));
  const haken = [...auswahl].filter((k) => da.has(k));
  if (haken.length > 0) return haken;
  return aktiv && da.has(aktiv) ? [aktiv] : [];
}

/** Parameter von `kollision_status`. f = { kollisionen[], status, kommentar, kommentarEntfernen }. */
export function kollisionStatusParameter(f) {
  const p = { kollisionen: [...f.kollisionen], status: f.status };
  const k = text(f.kommentar);
  if (k) p.kommentar = k;
  else if (f.kommentarEntfernen) p.kommentar = '';
  return p;
}
export function kollisionStatusPruefen(f) {
  if (!f.kollisionen || f.kollisionen.length === 0) return 'Zuerst Kollisionen wählen (Häkchen oder Zeile anklicken).';
  if (!KOLLISION_STATUS.includes(f.status)) return 'Einen Status wählen.';
  return true;
}

// ---------------------------------------------------------------------------------------------- Aussparungen: Liste, Status, Läufe

/** Filter der Aussparungsliste nach Status (im Browser; die Abfrage filtert nur nach Geschoss). status = Array der gewählten (leer = keine), ohne Angabe alle. */
export function aussparungFilter(zeilen, f = {}) {
  if (!Array.isArray(f.status)) return zeilen ?? [];
  return (zeilen ?? []).filter((z) => f.status.includes(z.status));
}

export function aussparungKopf(daten, anzeige) {
  if (!daten) return '';
  const je = Object.entries(daten.jeStatus ?? {}).map(([k, v]) => `${statusText(k)} ${v}`).join(', ');
  const teile = [`${daten.anzahl ?? (daten.zeilen ?? []).length} Aussparungen`];
  if (anzeige !== undefined && anzeige !== (daten.zeilen ?? []).length) teile[0] = `${anzeige} von ${daten.anzahl} Aussparungen`;
  if (je) teile.push(je);
  const hand = (daten.zeilen ?? []).filter((z) => z.formVonHand === true).length;
  if (hand) teile.push(`${hand} Form von Hand`);
  const unbekannt = (daten.zeilen ?? []).filter((z) => z.materialUnbekannt === true).length;
  if (unbekannt) teile.push(`${unbekannt} mit unbekanntem Material`);
  return teile.join(' · ');
}

// ---------------------------------------------------------------------------------------------- Form der Aussparung (aussparung_form, Entscheid Christian 19.10.2026)

export const FORMEN = ['rund', 'rechteckig'];
export const FORM_TEXT = { rund: 'rund (Kernbohrung)', rechteckig: 'rechteckig' };
export const FORM_KNOPF = { rund: 'Als Kernbohrung', rechteckig: 'Als Rechteck' };
export const FORM_VON_HAND_TEXT = 'von Hand';
export const FORM_VON_HAND_TITEL = 'Form von Hand gewählt (Kernbohrung): aussparungen_vorschlagen führt nur das Mass nach, die Form bleibt rund';

/** Zeigt die Liste Material-Spalten? Nur wenn der Kern die Felder liefert (Teil B3c). */
export const hatMaterial = (zeilen) => (zeilen ?? []).some((z) => 'material' in z || 'materialUnbekannt' in z);

/** Schalter «rechteckig | rund» einer Zeile: aktiv nur bei `formWaehlbar`, sonst mit `formSperre` als Tooltip; die aktuelle Form ist gewählt (aria-pressed) und nicht anklickbar. */
export function formSchalterHtml(z) {
  const name = FORM_TEXT[z.form] ?? z.form ?? '';
  if (z.formWaehlbar === undefined) return esc(name);   // ältere Kerne kennen den Schalter nicht
  const sperre = z.formWaehlbar ? '' : (text(z.formSperre) || 'Form nicht wählbar');
  const knopf = (f) => {
    const jetzt = z.form === f;
    const aus = !z.formWaehlbar || jetzt;
    const titel = !z.formWaehlbar ? sperre : jetzt ? `ist schon ${f}` : f === 'rund' ? 'Als Kernbohrung (rund) – nur von Hand, mit Vorschau' : 'Als Rechteck (Regel) – mit Vorschau';
    return `<button type="button" class="kp-formwahl${jetzt ? ' aktiv' : ''}" data-form="${f}" data-aus="${esc(z.aussparung)}" aria-pressed="${jetzt}" title="${esc(titel)}"${aus ? ' disabled' : ''}>${f}</button>`;
  };
  return `<span class="kp-formschalter${z.formWaehlbar ? '' : ' gesperrt'}" title="${esc(sperre || 'Form wählen: rechteckig (Regel) oder rund (Kernbohrung, nur von Hand)')}">${FORMEN.slice().reverse().map(knopf).join('')}</span>`
    + `${z.formVonHand === true ? ` <span class="kp-vonhand" title="${esc(FORM_VON_HAND_TITEL)}">${FORM_VON_HAND_TEXT}</span>` : ''}`;
}

/**
 * Welche der gewählten Aussparungen lassen sich in die Form bringen? { senden[], uebersprungen[{ aussparung, grund }] }.
 * Nicht wählbar ist, was der Kern in der Liste so meldet (`formWaehlbar` false, Grund `formSperre`); alles andere geht an den Kern, der den Rest prüft.
 */
export function formZiele(zeilen, gewaehlt) {
  const nach = new Map((zeilen ?? []).map((z) => [z.aussparung, z]));
  const senden = [], uebersprungen = [];
  for (const kz of gewaehlt ?? []) {
    const z = nach.get(kz);
    if (!z) continue;
    if (z.formWaehlbar === false) uebersprungen.push({ aussparung: kz, nummer: z.nummer ?? null, grund: text(z.formSperre) || 'Form nicht wählbar' });
    else senden.push(kz);
  }
  return { senden, uebersprungen };
}

/** Parameter von `aussparung_form`. */
export const formParameter = ({ aussparungen, form }) => ({ aussparungen: [...aussparungen], form });
export function formPruefen({ aussparungen, form }) {
  if (!aussparungen || aussparungen.length === 0) return 'Zuerst Aussparungen wählen (Häkchen oder Zeile anklicken).';
  if (!FORMEN.includes(form)) return 'Die Form ist rund oder rechteckig.';
  return true;
}

const uebersprungenHtml = (u) => ((u ?? []).length
  ? `<div class="kp-uebersprungen"><b>Nicht wählbar, nicht gesendet:</b><ul class="hinweisliste">${u.map((x) => `<li class="kp-warn">${esc(x.aussparung)}${x.nummer ? ` (${esc(x.nummer)})` : ''}: ${esc(fuerMenschen(x.grund))}</li>`).join('')}</ul></div>` : '');

/** Rückmeldung von `aussparung_form` in der Palette: Ablehnung (immer Fehler und Vorschlag), Vorschau des Trockenlaufs mit «Ausführen» / «Abbrechen», oder das Ergebnis. */
export function formMeldungHtml(r, { trocken = false, form = '', ziele = null } = {}) {
  if (!r.angenommen) {
    return `<b>${esc(fuerMenschen(r.fehler || r.meldung || 'Abgelehnt.'))}</b>${r.vorschlag ? `<div>Vorschlag: ${esc(fuerMenschen(r.vorschlag))}</div>` : '<div>Der Kern nennt keinen Vorschlag.</div>'}${uebersprungenHtml(ziele?.uebersprungen)}`;
  }
  const h = (r.hinweise ?? []).filter(Boolean);
  const n = (r.aenderungen ?? []).length;
  const kopf = trocken
    ? `Vorschau (Trockenlauf – nichts wurde geändert): ${FORM_KNOPF[form] ?? form}, ${n ? `${n} Änderung(en)` : 'keine Änderung'}`
    : (h[0] ?? 'Form gesetzt.');
  const liste = hinweiseListeHtml(trocken ? h : h.slice(1), { max: 12 });
  // die Knöpfe stehen neben der Überschrift: die Meldung der Palette ist niedrig und scrollt
  const knoepfe = trocken ? ` <span class="kp-formknoepfe"><button type="button" class="kp-knopf kp-primaer" data-kp="form-ja"${n ? '' : ' disabled'}>Ausführen</button><button type="button" class="kp-knopf" data-kp="form-nein">Abbrechen</button></span>` : '';
  return `<b>${esc(fuerMenschen(kopf))}</b>${knoepfe}${liste}${uebersprungenHtml(ziele?.uebersprungen)}`;
}

/** Meldung, wenn keine der gewählten Aussparungen wählbar ist (nichts wird gesendet): die Gründe des Kerns (`formSperre`). */
export const formKeineHtml = (ziele, form) => `<b>${esc(FORM_KNOPF[form] ?? form)}: keine der gewählten Aussparungen ist wählbar – es wurde nichts gesendet.</b>${uebersprungenHtml(ziele?.uebersprungen)}`;

const koteText = (z, geschoss) => {
  const uk = geschoss ? z.koteUkGeschoss : z.koteUk, ok = geschoss ? z.koteOkGeschoss : z.koteOk, ac = geschoss ? z.koteAchseGeschoss : z.koteAchse;
  if (z.form === 'rund') return ac === null || ac === undefined ? '–' : `Achse ${zahlAnzeige(ac, 1)}`;
  return uk === null || uk === undefined ? '–' : `UK ${zahlAnzeige(uk, 1)} · OK ${zahlAnzeige(ok, 1)}`;
};

export function aussparungTabelleHtml(zeilen, { auswahl = new Set(), aktiv = null, max = 500, leerHinweis = 'Keine Aussparung.' } = {}) {
  if (!zeilen || zeilen.length === 0) return `<p class="leise kp-leer">${esc(leerHinweis)}</p>`;
  const sichtbar = zeilen.slice(0, max);
  const alle = sichtbar.every((z) => auswahl.has(z.aussparung));
  const mat = hatMaterial(zeilen);
  const spalten = 14 + (mat ? 1 : 0);
  const kopf = `<tr><th><input type="checkbox" class="kp-alle" title="Alle sichtbaren wählen"${alle ? ' checked' : ''}></th><th>Nummer</th><th title="Kurzzeichen nach SIA 400 B.10.2 (Feld kurzzeichen des Kerns)">Kurzzeichen</th><th>Aussparung</th><th>Geschoss</th><th>Bauteil</th>${mat ? '<th>Material</th>' : ''}<th>Form</th><th>Mass</th><th class="zahl">Tiefe mm</th><th>Kote ±0.00</th><th>Kote über OK Geschoss</th><th>Art</th><th>Status</th><th>Leitungen</th></tr>`;
  const materialZelle = (z) => (z.materialUnbekannt === true
    ? '<td class="kp-material"><span class="kp-warn kp-matunbekannt" title="Material unbekannt (Hinweis des Kerns)">Material unbekannt</span></td>'
    : `<td class="kp-material">${esc(z.material ?? '–')}</td>`);
  const reihen = sichtbar.map((z) => `<tr class="kp-zeile${aktiv === z.aussparung ? ' aktiv' : ''}" data-kz="${esc(z.aussparung)}">`
    + `<td><input type="checkbox" class="kp-wahl" aria-label="${esc(z.aussparung)} wählen"${auswahl.has(z.aussparung) ? ' checked' : ''}></td>`
    + `<td>${esc(z.nummer ?? '–')}</td><td class="kp-kurzzeichen">${esc(z.kurzzeichen ?? '')}</td><td>${esc(z.aussparung)}</td><td>${esc(z.geschoss ?? '–')}</td><td title="${esc(z.bauteilArt ?? '')}">${esc(z.bauteil ?? '–')}</td>${mat ? materialZelle(z) : ''}`
    + `<td class="kp-formzelle">${formSchalterHtml(z)}</td><td class="kp-masse">${esc(z.masse ?? '')}</td><td class="zahl">${esc(zahlAnzeige(z.tiefe, 1))}</td><td>${esc(koteText(z, false))}</td><td>${esc(koteText(z, true))}</td>`
    + `<td>${esc(z.art ?? '')}</td><td class="kp-sa-${esc(z.status)}">${esc(statusText(z.status))}</td><td title="${esc((z.leitungen ?? []).join(', '))}">${esc(kurz((z.leitungen ?? []).join(', '), 40))}</td></tr>`).join('');
  const mehr = zeilen.length > max ? `<tr><td colspan="${spalten}" class="leise">… und ${zeilen.length - max} weitere – Filter einengen.</td></tr>` : '';
  return `<table class="tab kp-tab"><thead>${kopf}</thead><tbody>${reihen}${mehr}</tbody></table>`;
}

export function aussparungDetailHtml(z) {
  if (!z) return '<span class="leise">Eine Zeile wählen: sie wird im Grundriss gezeigt.</span>';
  const lei = (z.leitungen ?? []).map((l) => `<button type="button" class="chip kp-beteiligt" data-ref="${esc(l)}" title="Im Grundriss zeigen">${esc(l)}</button>`).join(' ');
  const bauteil = z.bauteil && !String(z.bauteil).startsWith('?') && !z.unterlage ? `<button type="button" class="chip kp-beteiligt" data-ref="${esc(z.bauteil)}" title="Im Grundriss zeigen">${esc(z.bauteil)}</button>` : esc(z.bauteil ?? '–');
  const form = z.formVonHand === true ? ` · <b>Form ${FORM_VON_HAND_TEXT}</b>` : '';
  const sperre = z.formWaehlbar === false ? `<div class="kp-formsperre leise">Form nicht wählbar: ${esc(fuerMenschen(text(z.formSperre) || 'ohne Angabe des Kerns'))}</div>` : '';
  const material = 'material' in z || 'materialUnbekannt' in z
    ? (z.materialUnbekannt === true ? ' · <span class="kp-warn">Material unbekannt</span>' : z.material ? ` · Material ${esc(z.material)}` : '') : '';
  return `<b>${esc(z.nummer ?? z.aussparung)}</b> <span class="leise">${esc(z.aussparung)} · ${esc(z.art ?? '')} · ${esc(z.masse ?? '')}</span>${form}${material} Bauteil ${bauteil}${z.unterlage ? ` <span class="leise">(${esc(z.unterlage)})</span>` : ''} Leitungen ${lei || '–'}${sperre}`;
}

/** Parameter von `aussparung_status`. */
export const aussparungStatusParameter = ({ aussparungen, status }) => ({ aussparungen: [...aussparungen], status });
export function aussparungStatusPruefen({ aussparungen, status }) {
  if (!aussparungen || aussparungen.length === 0) return 'Zuerst Aussparungen wählen (Häkchen oder Zeile anklicken).';
  if (!AUSSPARUNG_STATUS.includes(status)) return 'Einen Status wählen.';
  return true;
}

/** Liste aus einem Eingabefeld: getrennt durch Komma, Semikolon oder Leerzeichen. */
export const listeAusText = (t) => text(t).split(/[,;\s]+/).filter(Boolean);

/** Parameter von `aussparungen_vorschlagen`. f = { geschoss, leitungen (Text), bauteile (Text) }. Der Vorschlag ist immer rechteckig; eine Kernbohrung wählt nur ein Mensch (aussparung_form). */
export function vorschlagenParameter(f) {
  const p = {};
  if (text(f.geschoss)) p.geschoss = text(f.geschoss);
  const b = listeAusText(f.bauteile), l = listeAusText(f.leitungen);
  if (b.length) p.bauteile = b;
  if (l.length) p.leitungen = l;
  return p;
}

/** Parameter von `aussparungen_nummerieren`. */
export function nummerierenParameter(f) {
  const p = {};
  if (text(f.geschoss)) p.geschoss = text(f.geschoss);
  if (f.alle === true) p.alle = true;
  return p;
}

/** Parameter von `aussparungen_abgleichen`. */
export const abgleichParameter = (unterlage) => ({ unterlage: text(unterlage) });
export const abgleichPruefen = (unterlage) => (text(unterlage) ? true : 'Die IFC-Unterlage der neuen Architekten-Fassung wählen.');

/** IFC-Unterlagen aus den Elementen der Klasse Unterlage. */
export const ifcUnterlagen = (unterlagen) => (unterlagen ?? []).filter((u) => String(u.werte?.art ?? '').toLowerCase() === 'ifc');

// ---------------------------------------------------------------------------------------------- Aussparungsplan

export const planAnsichtKennzeichen = (geschoss) => `AN-AUS-${geschoss}`;
export const PLAN_MASSSTAEBE = [20, 50, 100, 200];
export const PLAN_FORMATE = ['A4', 'A3', 'A2', 'A1', 'A0'];

/** Parameter von `element_anlegen` für den Grundriss mit Darstellung «aussparungen». */
export function planAnsichtParameter({ geschoss, massstab, gewerke = [], detailgrad = 'mittel' }) {
  const eig = { art: 'grundriss', darstellung: 'aussparungen', detailgrad };
  if (massstab) eig.massstab = Number(massstab);
  if (gewerke.length) eig.gewerke = [...gewerke];
  return { klasse: 'Ansicht', kennzeichen: planAnsichtKennzeichen(geschoss), name: `Aussparungsplan ${geschoss}`, geschoss, eigenschaften: eig };
}

/** Parameter von `planblatt_anlegen`. */
export function planblattParameter({ nummer, titel, format, hochformat }) {
  const p = { nummer: text(nummer), titel: text(titel) };
  if (text(format)) p.format = text(format);
  if (hochformat) p.hochformat = true;
  return p;
}

/**
 * Prüft die Zeilen des Aussparungsplans. zeilen: [{ geschoss, gewaehlt, blatt (bool), nummer, titel }].
 * Plannummern denkt sich die Oberfläche nicht aus: mit Planblatt muss jede Zeile eine eigene Nummer haben.
 */
export function planPruefen(zeilen, blattNummernVorhanden = []) {
  const gew = zeilen.filter((z) => z.gewaehlt);
  if (gew.length === 0) return 'Mindestens ein Geschoss wählen.';
  const nummern = [];
  for (const z of gew.filter((x) => x.blatt)) {
    if (!text(z.nummer)) return `${z.geschoss}: die Plannummer fehlt (sie vergibst du, KICAD denkt sich keine aus).`;
    if (!text(z.titel)) return `${z.geschoss}: der Titel des Planblatts fehlt.`;
    const n = text(z.nummer).toLowerCase();
    if (nummern.includes(n)) return `${z.geschoss}: die Plannummer ${text(z.nummer)} steht doppelt.`;
    if (blattNummernVorhanden.map((x) => String(x).toLowerCase()).includes(n)) return `${z.geschoss}: die Plannummer ${text(z.nummer)} gibt es schon.`;
    nummern.push(n);
  }
  return true;
}

/** Geschosse, die Aussparungen haben (aus den Zeilen der Aussparungsliste), nach Kote geordnet wie die Geschosse. */
export function geschosseMitAussparungen(zeilen) {
  return [...new Set((zeilen ?? []).map((z) => z.geschoss).filter(Boolean))];
}

// ---------------------------------------------------------------------------------------------- Koordinaten (BCF und IFC für den Architekten)

/**
 * Wahl der Koordinaten für BCF-Export und IFC für Architekt (Entscheid Christian 19.10.2026: beide wählbar). Der Wert '' heisst «Vorgabe des Kerns» und sendet nichts:
 * ohne Angabe gilt `ifc`, wenn das Projekt einen Projektbezug hat, sonst `projekt`. Eine ausdrückliche Wahl geht immer mit, auch `projekt`.
 */
export const KOORDINATEN_WAHL = [
  { wert: '', text: 'Vorgabe (IFC mit Projektbezug, sonst Projekt)' },
  { wert: 'ifc', text: 'IFC (Weltkoordinaten über den Projektbezug, Kamera im Modell des Partners)' },
  { wert: 'projekt', text: 'Projekt (Koordinaten von KICAD, Gebäude im Ursprung)' },
];
export const koordinatenOptionenHtml = (gewaehlt = '') => KOORDINATEN_WAHL.map((k) => `<option value="${k.wert}"${k.wert === gewaehlt ? ' selected' : ''}>${esc(k.text)}</option>`).join('');
/** Parameterwert der Koordinaten: null bei «Vorgabe» (nichts senden), sonst die Wahl. */
export const koordinatenWert = (wahl) => (['ifc', 'projekt'].includes(text(wahl).toLowerCase()) ? text(wahl).toLowerCase() : null);

// ---------------------------------------------------------------------------------------------- IFC für den Architekten

/** Was der Kern von `aussparungen_ifc` kennt (aus `faehigkeiten`): { vorhanden, parameter: Map name → typ }. */
export function ifcFaehigkeit(faehigkeiten) {
  const a = (faehigkeiten?.abfragen ?? []).find((x) => x.name === 'aussparungen_ifc');
  return { vorhanden: !!a, parameter: new Map((a?.parameter ?? []).map((p) => [p.name, p.typ])) };
}
export const IFC_FEHLT_HINWEIS = 'Die Abfrage aussparungen_ifc ist im Kern noch nicht vorhanden (Teil B4b).';

/**
 * Parameter von `aussparungen_ifc` (Abfrage des Kerns, Teil B4b): datei, fassung; geschoss, status und koordinaten nur, wenn der Kern sie kennt und etwas gewählt ist.
 * Ohne status gibt der Kern freigegebene und versandte aus, ohne koordinaten ifc (mit Projektbezug) bzw. projekt – beides ist seine Vorgabe,
 * die Oberfläche setzt keine eigene; eine ausdrückliche Wahl (auch «projekt») geht immer mit. f = { datei, fassung, geschoss, status[], koordinaten }.
 */
export function ifcParameter(f, faehig) {
  const p = { datei: text(f.datei).replace(/^"|"$/g, ''), fassung: f.fassung };
  const hat = (n) => !faehig || faehig.parameter.size === 0 || faehig.parameter.has(n);
  if (text(f.geschoss) && hat('geschoss')) p.geschoss = text(f.geschoss);
  if ((f.status ?? []).length && hat('status')) p.status = faehig?.parameter?.get('status') === 'text' ? f.status.join(',') : [...f.status];
  if (koordinatenWert(f.koordinaten) && hat('koordinaten')) p.koordinaten = koordinatenWert(f.koordinaten);
  return p;
}

/** Exportbericht von `aussparungen_ifc`: Datei, Fassung, Zahlen, Koordinaten, Rundreise (die Datei wird zur Kontrolle wieder gelesen), Warnungen und Hinweise. */
export function ifcExportHtml(r) {
  const je = (o) => Object.entries(o ?? {}).map(([k, v]) => `${statusText(k)} ${v}`).join(', ');
  const rr = r.rundreise ?? {};
  const zeilen = [
    ['Datei', r.datei], ['IFC-Fassung', r.fassung], ['Objekte', r.entitaet ? `${r.entitaet} (${r.kennzeichnung ?? ''})` : null],
    ['Aussparungen', r.anzahl != null ? `${r.anzahl}${je(r.jeArt) ? ' · ' + je(r.jeArt) : ''}` : null], ['Je Geschoss', je(r.jeGeschoss)], ['Je Status', je(r.jeStatus)],
    ['Grösse', r.groesseBytes != null ? `${zahlAnzeige(r.groesseBytes / 1024, 1)} kB` : null], ['Koordinaten', r.koordinaten?.text ?? r.koordinaten?.art],
    ['Rundreise (Datei wieder gelesen)', rr.ergebnis ? `${rr.ergebnis}${rr.aussparungenGeprueft != null ? ` · ${rr.aussparungenGeprueft} Aussparungen geprüft` : ''}` : null],
  ].filter(([, v]) => !leer(v));
  const abw = (rr.abweichungen ?? []).length ? `<div class="fehlerkasten" role="alert"><b>Abweichungen der Rundreise</b>${hinweiseListeHtml(rr.abweichungen)}</div>` : '';
  const warn = (r.warnungen ?? []).length ? `<div class="hkap-kasten"><b>Warnungen</b>${hinweiseListeHtml(r.warnungen)}</div>` : '';
  const h = (r.hinweise ?? []).filter(Boolean);
  return `<h4 class="kp-ergebnis">Geschrieben</h4><table class="tab huelle-details"><tbody>${zeilen.map(([k, v]) => `<tr><td>${esc(k)}</td><td>${esc(v)}</td></tr>`).join('')}</tbody></table>${abw}${warn}`
    + (h.length ? `<p class="leise">Hinweise des Kerns:</p>${hinweiseListeHtml(h)}` : '');
}

// ---------------------------------------------------------------------------------------------- BCF

/** Absoluter Pfad (Laufwerk, Netzpfad oder Unix) mit einer der Endungen? true oder ein Text. */
export function dateiPfadPruefen(pfad, endungen = ['.bcf', '.bcfzip'], was = 'Datei') {
  const p = text(pfad).replace(/^"|"$/g, '');
  if (p === '') return `${was}: den vollständigen Pfad angeben.`;
  if (!/^([A-Za-z]:[\\/]|\\\\|\/)/.test(p)) return `${was}: den vollständigen Pfad angeben (z. B. C:\\Temp\\Kollisionen.bcf) – ein Name allein würde im Ordner des Servers landen.`;
  const ende = p.slice(p.lastIndexOf('.')).toLowerCase();
  if (!endungen.includes(ende)) return `${was}: die Endung muss ${endungen.join(' oder ')} sein.`;
  return true;
}

/** Vorschlag für den Dateinamen des Exports: «Kollisionen_<Projekt>_JJJJ-MM-TT.bcf». */
export function bcfDateiname(projekt, datum = new Date()) {
  const roh = text(projekt).replace(/\(.*?\)/g, '').replace(/[^0-9A-Za-zÄÖÜäöüß]+/g, '_').replace(/^_+|_+$/g, '');
  const d = `${datum.getFullYear()}-${String(datum.getMonth() + 1).padStart(2, '0')}-${String(datum.getDate()).padStart(2, '0')}`;
  return `Kollisionen_${roh || 'Projekt'}_${d}.bcf`;
}

/** Parameter der Abfrage `bcf_export`. f = { datei, status[], art, autor, koordinaten, kollisionen[] }. */
export function bcfExportParameter(f) {
  const p = { datei: text(f.datei).replace(/^"|"$/g, '') };
  if ((f.kollisionen ?? []).length) p.kollisionen = [...f.kollisionen];
  if ((f.status ?? []).length && f.status.length < KOLLISION_STATUS.length) p.status = KOLLISION_STATUS.filter((s) => f.status.includes(s));
  if (text(f.art)) p.art = text(f.art);
  if (text(f.autor)) p.autor = text(f.autor);
  // «Vorgabe» sendet nichts (der Kern wählt: ifc mit Projektbezug, sonst projekt); eine ausdrückliche Wahl geht immer mit, auch «projekt»
  if (koordinatenWert(f.koordinaten)) p.koordinaten = koordinatenWert(f.koordinaten);
  return p;
}

/** Zeile «n Kollisionen werden exportiert» aus der Abfrage `kollisionen` mit demselben Filter. */
export function bcfExportVorschau(daten, auswahlAnzahl = 0) {
  if (auswahlAnzahl > 0) return `${auswahlAnzahl} gewählte Kollision(en) werden exportiert.`;
  const n = (daten?.zeilen ?? []).length;
  if (n === 0) return 'Keine Kollision passt zum Filter – nichts zu exportieren.';
  const je = daten?.zusammenfassung?.jeArt ?? {};
  return `${n} Kollision(en) werden exportiert (hart ${je.hart ?? 0}, weich ${je.weich ?? 0}).`;
}

/** Ergebnis von `bcf_export`. */
export function bcfExportHtml(r) {
  const zeilen = [['Datei', r.datei], ['BCF-Version', r.bcfVersion], ['Topics', `${r.topics} (hart ${r.hart}, weich ${r.weich})`],
    ['Grösse', r.groesseBytes != null ? `${zahlAnzeige(r.groesseBytes / 1024, 1)} kB` : null], ['Koordinaten', r.koordinaten], ['Prüfung gegen die BCF-Schemas', r.schemapruefung]]
    .filter(([, v]) => !leer(v));
  const h = (r.hinweise ?? []).filter(Boolean);
  return `<h4 class="kp-ergebnis">Geschrieben</h4><table class="tab huelle-details"><tbody>${zeilen.map(([k, v]) => `<tr><td>${esc(k)}</td><td>${esc(v)}</td></tr>`).join('')}</tbody></table>`
    + (h.length ? `<p class="leise">Hinweise des Kerns:</p>${hinweiseListeHtml(h)}` : '');
}

/** Vorschau des Imports aus `bcf_lesen`: Kopf, Topics mit Zuordnung und Status nach dem Import, unbekannte Topics, Hinweise. */
export function bcfLesenHtml(r) {
  if (!r) return '';
  const topics = r.topics ?? [];
  const kopf = [`BCF ${r.bcfVersion ?? '?'}`, `${r.anzahlTopics ?? topics.length} Topic(s)`, `${r.zugeordnet ?? 0} zugeordnet`, `${r.unbekannt ?? 0} unbekannt`, `${bcfImportAenderungen(r)} mit Änderung`, r.schemapruefung ? `Schemaprüfung: ${r.schemapruefung}` : null].filter(Boolean).join(' · ');
  const fremdProjekt = r.projekt && r.projekt.diesesProjekt === false
    ? `<div class="hkap-kasten">Die Datei nennt ein anderes Projekt (${esc(r.projekt.name ?? r.projekt.id ?? '?')}) – nur Topics mit passender Guid werden zugeordnet.</div>` : '';
  const zeilen = topics.map((t) => {
    const neu = (t.kommentare ?? []).filter((c) => c.neu === true).length;
    const nach = t.kollision ? esc(t.statusNachImport ?? '') : '';
    return `<tr class="${t.kollision ? '' : 'kp-unbekannt'}"><td>${esc(t.titel ?? 'ohne Titel')}</td><td>${t.kollision ? esc(t.kollision) : '<span class="leise">unbekannt</span>'}</td>`
      + `<td>${esc(t.status ?? '–')}</td><td>${t.kollision ? `${esc(statusText(t.statusKicad))} → <b>${nach}</b>` : '–'}</td><td class="zahl">${t.kollision ? neu : '–'}</td><td>${esc(t.zugewiesen ?? '')}</td></tr>`;
  }).join('');
  const tabelle = topics.length
    ? `<div class="huelle-scroll"><table class="tab"><thead><tr><th>Titel</th><th>Kollision</th><th>Status BCF</th><th>Status KICAD → nach Import</th><th class="zahl">Neue Kommentare</th><th>Zugewiesen</th></tr></thead><tbody>${zeilen}</tbody></table></div>`
    : '<p class="leise">Die Datei enthält keine Topics.</p>';
  const h = (r.hinweise ?? []).filter(Boolean);
  return `<p>${esc(kopf)}</p>${fremdProjekt}${tabelle}${h.length ? `<p class="leise">Hinweise zur Datei:</p>${hinweiseListeHtml(h)}` : ''}`;
}

/** Zahl der Topics, die ein Import ändern würde (Status anders oder neue Kommentare). */
export function bcfImportAenderungen(r) {
  return (r?.topics ?? []).filter((t) => t.kollision && ((t.statusNachImport && t.statusNachImport !== t.statusKicad && !String(t.statusNachImport).startsWith('unverändert')) || (t.kommentare ?? []).some((c) => c.neu === true))).length;
}

// ---------------------------------------------------------------------------------------------- Typenliste «Typname → Material» (Nachtrag C1c, Kern B3d)
// Die Abfrage `typmaterial` liefert: typen (Einträge mit Wirkung und Zahlen), unbekannt (Typen mit unbekanntem Material), ausTypname (Material nur aus dem Typnamen geraten, zum Bestätigen),
// kategorien (Auswahl samt Wirkung), ohneQuelle, doppelt, hinweise, zusammenfassung. Der Befehl `typmaterial_setzen { typen: [{ typname, kategorie, quelle? }], quelle? }` legt an, ändert
// (kategorie) und entfernt (kategorie null). Jede Festlegung braucht eine Quelle – die Oberfläche sendet nie eine eigene und füllt das Feld nicht vor.

export const TYPEN_REITER = [
  { id: 'unbekannt', titel: 'Unbekannte Typen' },
  { id: 'bestaetigen', titel: 'Material aus Typname' },
  { id: 'liste', titel: 'Typenliste' },
];
export const TYPEN_QUELLE_PLATZHALTER = 'Wer hat es festgelegt, wann (Besprechung, Mail, Plan …)';
export const TYPEN_QUELLENSATZ = 'Jede Festlegung braucht eine Quelle. KICAD setzt keine Kategorie aus dem Gedächtnis.';
export const TYPEN_HERKUNFT_TEXT = { typenliste: 'nach Typenliste', material: 'nach Materialzuordnung', typname: 'aus dem Typnamen (zu bestätigen)', entitaet: 'nach IFC-Entität', keine: 'unbekannt' };

const zahl0 = (x) => (Number.isFinite(Number(x)) ? Number(x) : 0);
const ordinal = (a, b) => (a < b ? -1 : a > b ? 1 : 0);

/** Parameter der Abfrage `typmaterial`: nur der Schalter «nur betroffene» (sonst nichts). */
export const typenAbfrageParameter = (form) => (form?.betroffen === true ? { betroffen: true } : {});

/** Leeres Formular der Typenliste. unbekannt: Schlüssel → Kategorie; bestaetigen: Schlüssel → { an, kategorie }; liste: Element → { kategorie, quelle, entfernen }. */
export const typenFormular = () => ({ reiter: 'unbekannt', betroffen: false, quelle: '', unbekannt: {}, bestaetigen: {}, liste: {} });

/** Reiter, mit dem der Dialog beginnt: der erste mit Inhalt (unbekannte Typen zuerst – dort liegt die Arbeit). */
export function typenStartReiter(daten) {
  if ((daten?.unbekannt ?? []).length > 0) return 'unbekannt';
  if ((daten?.ausTypname ?? []).length > 0) return 'bestaetigen';
  return 'liste';
}

/** Kategorien aus der Antwort des Kerns (`kategorien`): { kategorie, gruppe, aussparung, wirkung }. Die Oberfläche kennt keine eigene Liste. */
export const typenKategorien = (daten) => (daten?.kategorien ?? []).filter((k) => text(k.kategorie))
  .map((k) => ({ kategorie: text(k.kategorie), gruppe: text(k.gruppe) || text(k.kategorie), aussparung: k.aussparung === true, wirkung: text(k.wirkung) }));
export const kategorieWirkung = (daten, kat) => typenKategorien(daten).find((k) => k.kategorie === text(kat))?.wirkung ?? '';
export const kategorieName = (daten, kat) => typenKategorien(daten).find((k) => k.kategorie === text(kat))?.gruppe ?? text(kat);

export function kategorieOptionenHtml(daten, gewaehlt = '', leer = '(nicht festlegen)') {
  return `<option value="">${esc(leer)}</option>` + typenKategorien(daten).map((k) =>
    `<option value="${esc(k.kategorie)}" title="${esc(k.wirkung)}"${k.kategorie === text(gewaehlt) ? ' selected' : ''}>${esc(k.gruppe)}${k.aussparung ? ' (Aussparung)' : ''}</option>`).join('');
}

/** Unbekannte Typen nach Durchgängen absteigend (dann Aussparungen, Bauteile, Name) – dort lohnt die Festlegung am meisten. */
export const unbekanntSortiert = (liste) => [...(liste ?? [])].sort((a, b) => zahl0(b.durchgaenge) - zahl0(a.durchgaenge) || zahl0(b.aussparungen) - zahl0(a.aussparungen)
  || zahl0(b.bauteile) - zahl0(a.bauteile) || ordinal(text(a.schluessel), text(b.schluessel)));

/** Zeile «Material aus Typname»: Vorbelegung ist die erkannte Kategorie; angehakt wird erst, wenn der Mensch es bestätigt. */
export const bestaetigenZeile = (form, r) => form?.bestaetigen?.[r.schluessel] ?? { an: false, kategorie: text(r.kategorie) };
/** Zeile der Typenliste: Vorbelegung sind die Werte des Eintrags. */
export const listeZeile = (form, t) => form?.liste?.[t.element] ?? { kategorie: text(t.kategorie), quelle: text(t.quelle), entfernen: false };

const kategorieFehler = (daten, kat, name) => (typenKategorien(daten).some((k) => k.kategorie === kat) ? null : `«${name}»: Kategorie «${kat}» gibt es nicht.`);
const quellenFehler = (wem) => `Quelle fehlt – ${TYPEN_QUELLENSATZ.charAt(0).toLowerCase()}${TYPEN_QUELLENSATZ.slice(1)} Eine Quelle gilt für ${wem}.`;

/** Auswertung des Reiters «Unbekannte Typen»: { typen[{ typname, kategorie }], quelle, fehler[], warnungen[] }. Gesendet wird, wo eine Kategorie gewählt ist; die Quelle ist Pflicht. */
export function typenUnbekanntAuswertung(form, daten) {
  const typen = [], fehler = [];
  for (const r of daten?.unbekannt ?? []) {
    const k = text(form?.unbekannt?.[r.schluessel]);
    if (!k) continue;
    const f = kategorieFehler(daten, k, r.schluessel);
    if (f) { fehler.push(f); continue; }
    typen.push({ typname: r.schluessel, kategorie: k });
  }
  const quelle = text(form?.quelle);
  if (typen.length > 0 && quelle === '') fehler.push(quellenFehler('alle gewählten Typen'));
  return { typen, quelle, fehler, warnungen: [] };
}

/** Auswertung des Reiters «Material aus Typname»: bestätigt wird, was angehakt ist, mit der gezeigten Kategorie. */
export function typenBestaetigenAuswertung(form, daten) {
  const typen = [], fehler = [];
  for (const r of daten?.ausTypname ?? []) {
    const z = bestaetigenZeile(form, r);
    if (!z.an) continue;
    const k = text(z.kategorie);
    if (!k) { fehler.push(`«${r.schluessel}»: der Kern erkennt keine eindeutige Kategorie – eine wählen.`); continue; }
    const f = kategorieFehler(daten, k, r.schluessel);
    if (f) { fehler.push(f); continue; }
    typen.push({ typname: r.schluessel, kategorie: k });
  }
  const quelle = text(form?.quelle);
  if (typen.length > 0 && quelle === '') fehler.push(quellenFehler('alle bestätigten Typen'));
  return { typen, quelle, fehler, warnungen: [] };
}

/**
 * Auswertung des Reiters «Typenliste»: geändert (Kategorie, Quelle) oder entfernt wird nur, was sich gegenüber dem Eintrag unterscheidet. Jede Zeile trägt ihre eigene Quelle.
 * Ändert sich die Kategorie bei gleicher Quelle, fragt eine Warnung nach (wie bei den Projektvorgaben).
 */
export function typenListeAuswertung(form, daten) {
  const typen = [], fehler = [], warnungen = [];
  for (const t of daten?.typen ?? []) {
    const z = form?.liste?.[t.element];
    if (!z) continue;
    if (z.entfernen === true) { typen.push({ typname: t.typname, kategorie: null }); continue; }
    const kat = text(z.kategorie) || text(t.kategorie), q = text(z.quelle);
    if (kat === text(t.kategorie) && q === text(t.quelle)) continue;
    const f = kategorieFehler(daten, kat, t.typname);
    if (f) { fehler.push(f); continue; }
    if (q === '') { fehler.push(`${t.element} «${t.typname}»: Quelle fehlt.`); continue; }
    typen.push({ typname: t.typname, kategorie: kat, quelle: q });
    if (kat !== text(t.kategorie) && q === text(t.quelle)) warnungen.push(`${t.element} «${t.typname}»: Kategorie geändert, Quelle gleich geblieben – gilt «${q}» auch für ${kategorieName(daten, kat)}?`);
  }
  return { typen, quelle: '', fehler, warnungen };
}

/** Auswertung des Reiters, der gerade offen ist. */
export function typenAuswertung(form, daten) {
  return form?.reiter === 'bestaetigen' ? typenBestaetigenAuswertung(form, daten) : form?.reiter === 'liste' ? typenListeAuswertung(form, daten) : typenUnbekanntAuswertung(form, daten);
}

/** Parameter von `typmaterial_setzen`; null, wenn nichts zu senden ist oder die Eingabe Fehler hat. Die gemeinsame Quelle geht nur mit, wenn es eine gibt (Reiter «Typenliste»: je Zeile eine eigene). */
export function typenParameter(a) {
  if (!a || a.fehler.length > 0 || a.typen.length === 0) return null;
  const p = { typen: a.typen.map((t) => ({ ...t })) };
  if (text(a.quelle)) p.quelle = text(a.quelle);
  return p;
}

/** Prüfung vor dem Festlegen: true oder ein Text. */
export function typenPruefen(a) {
  if (a.fehler.length > 0) return a.fehler.join(' ');
  if (a.typen.length === 0) return 'Zuerst etwas wählen (Kategorie bei einem Typ, Häkchen oder Änderung in der Liste).';
  return true;
}

/** Aufschrift des Festlegen-Knopfes je Reiter, mit der Zahl der Typen, die gesendet würden. */
export function typenKnopfText(form, daten) {
  const a = typenAuswertung(form, daten);
  const n = a.typen.length;
  if (form?.reiter === 'bestaetigen') return n ? `Bestätigen (${n})` : 'Bestätigen';
  if (form?.reiter === 'liste') return n ? `Änderungen speichern (${n})` : 'Änderungen speichern';
  return n ? `Festlegen (${n})` : 'Festlegen';
}

// ---- Anzeige

const typenUntertitel = (r) => [text(r.entitaeten), text(r.material) ? `Material im IFC: ${text(r.material)}` : ''].filter(Boolean).join(' · ');
const zahlenZellen = (r) => `<td class="zahl">${zahl0(r.bauteile)}</td><td class="zahl">${zahl0(r.aussparungen)}</td><td class="zahl${zahl0(r.durchgaenge) > 0 ? ' t-viel' : ''}">${zahl0(r.durchgaenge)}</td>`;
const ZAHLEN_KOPF = '<th class="zahl" title="Bauteile mit diesem Typ">Bauteile</th><th class="zahl" title="gültige Aussparungen an diesen Bauteilen">Aussparungen</th><th class="zahl" title="Kollisionen typ durchdringung (nicht behoben) des letzten Prüflaufs">Durchgänge</th>';
const typnamenTitel = (r) => (r.typnamen ?? []).join('\n');

/** Kennzahlen über dem Dialog: Einträge, Bauteile je Herkunft des Materials, unbekannte Typen, Einträge ohne Quelle bzw. doppelte. */
export function typenKopfHtml(daten) {
  if (!daten) return '';
  const z = daten.zusammenfassung ?? {};
  const je = Object.entries(z.jeHerkunft ?? {}).map(([k, v]) => `${TYPEN_HERKUNFT_TEXT[k] ?? k} ${v}`).join(', ');
  const teile = [`<b>${zahl0(z.eintraege)}</b> ${zahl0(z.eintraege) === 1 ? 'Eintrag' : 'Einträge'} in der Typenliste`, `${zahl0(z.bauteile)} Bauteile (${esc(je || 'keine')})`];
  if (zahl0(z.unbekannteTypen) > 0) teile.push(`<b>${zahl0(z.unbekannteTypen)}</b> unbekannte Typen`);
  const ohne = (daten.ohneQuelle ?? []).length, dop = (daten.doppelt ?? []).length;
  if (ohne > 0) teile.push(`<span class="kp-warn"><b>${ohne}</b> Eintrag/Einträge ohne Quelle</span>`);
  if (dop > 0) teile.push(`<span class="kp-warn"><b>${dop}</b> Typ(en) doppelt</span>`);
  return `<p class="t-zahlen">${teile.join(' · ')}</p>`;
}

/** Hinweise des Kerns, getrennt: was geprüft werden muss (gut sichtbar) und was nur erklärt. */
export function typenHinweiseTeilen(daten) {
  const alle = (daten?.hinweise ?? []).filter(Boolean);
  const leise = alle.filter((t) => /^Durchgänge = |^Die Typenliste ist leer/.test(t));
  return { warnungen: alle.filter((t) => !leise.includes(t)), leise };
}

/** Kasten «Zu prüfen» mit den Hinweisen des Kerns (Eintrag ohne Quelle, doppelt, passt auf kein Bauteil – Schreibweise prüfen, fehlende Netzdateien …) und die Erklärung darunter. */
export function typenWarnungenHtml(daten) {
  if (!daten) return '';
  const { warnungen, leise } = typenHinweiseTeilen(daten);
  const kasten = warnungen.length
    ? `<div class="hkap-kasten kp-typen-warn" role="alert"><b>Zu prüfen (${warnungen.length})</b><ul class="hinweisliste kp-hinweise">${warnungen.map((t) => `<li class="kp-warn">${esc(fuerMenschen(t))}</li>`).join('')}</ul></div>` : '';
  return kasten + (leise.length ? `<p class="leise t-leise">${leise.map((t) => esc(fuerMenschen(t))).join(' ')}</p>` : '');
}

/** Reiterleiste mit Zahlen und dem Schalter «nur betroffene». Bei gesetztem Schalter steht «n von N» (N = alle unbekannten Typen des Projekts, Angabe des Kerns). */
export function typenReiterHtml(daten, form) {
  const z = daten?.zusammenfassung ?? {};
  const n = { unbekannt: (daten?.unbekannt ?? []).length, bestaetigen: (daten?.ausTypname ?? []).length, liste: (daten?.typen ?? []).length };
  const zahlText = (id) => (id === 'unbekannt' && form?.betroffen && zahl0(z.unbekannteTypen) > n.unbekannt ? `${n.unbekannt} von ${zahl0(z.unbekannteTypen)}` : String(n[id]));
  const knoepfe = TYPEN_REITER.map((r) => `<button type="button" class="kp-reiter t-reiter-knopf${form?.reiter === r.id ? ' aktiv' : ''}" data-reiter="${r.id}" role="tab" aria-selected="${form?.reiter === r.id}">${esc(r.titel)} (${esc(zahlText(r.id))})</button>`).join('');
  return `<div class="t-reiterleiste" role="tablist">${knoepfe}<label class="haken t-betroffen-feld" title="Nur Typen mit Aussparungen oder Durchgängen in den Listen «Unbekannte Typen» und «Material aus Typname»"><input type="checkbox" class="t-betroffen"${form?.betroffen ? ' checked' : ''}> nur betroffene</label></div>`;
}

/** Tabelle der unbekannten Typen: nach Durchgängen absteigend, je Zeile die Auswahl der Kategorie (aus `kategorien` des Kerns) und ihre Wirkung. */
export function typenUnbekanntHtml(daten, form) {
  const zeilen = unbekanntSortiert(daten?.unbekannt);
  if (zeilen.length === 0) return `<p class="leise kp-leer">${daten?.filter?.betroffen ? 'Kein unbekannter Typ mit Aussparungen oder Durchgängen.' : 'Keine unbekannten Typen: Das Material aller Bauteile mit Typname ist bekannt.'}</p>`;
  const reihen = zeilen.map((r) => {
    const k = text(form?.unbekannt?.[r.schluessel]);
    return `<tr class="t-zeile${k ? ' gewaehlt' : ''}" data-s="${esc(r.schluessel)}"><td class="t-name" title="${esc(typnamenTitel(r))}"><b>${esc(r.schluessel)}</b><div class="leise t-sub">${esc(typenUntertitel(r))}</div></td>${zahlenZellen(r)}`
      + `<td><select class="t-kat" aria-label="Kategorie ${esc(r.schluessel)}">${kategorieOptionenHtml(daten, k)}</select></td><td class="t-wirkung leise">${esc(kategorieWirkung(daten, k))}</td></tr>`;
  }).join('');
  return `<div class="t-scroll"><table class="tab t-tab t-tab-unbekannt"><thead><tr><th>Typname</th>${ZAHLEN_KOPF}<th>Kategorie</th><th>Wirkung</th></tr></thead><tbody>${reihen}</tbody></table></div>`;
}

/** Tabelle «Material aus Typname»: der Kern hat die Kategorie aus dem Typnamen geraten (Wortliste); bestätigt wird mit Häkchen und Quelle, die Kategorie lässt sich ändern. */
export function typenBestaetigenHtml(daten, form) {
  const zeilen = unbekanntSortiert(daten?.ausTypname);
  if (zeilen.length === 0) return `<p class="leise kp-leer">${daten?.filter?.betroffen ? 'Kein Typ mit Aussparungen oder Durchgängen, dessen Material aus dem Typnamen stammt.' : 'Kein Typ hat sein Material nur aus dem Typnamen.'}</p>`;
  const alle = zeilen.every((r) => bestaetigenZeile(form, r).an);
  const reihen = zeilen.map((r) => {
    const z = bestaetigenZeile(form, r);
    const erkannt = text(r.gruppe) ? `erkannt: ${r.gruppe}` : '';
    const annahme = text(r.annahme) ? `<div class="kp-warn t-sub" title="${esc(r.annahme)}">${esc(kurz(r.annahme, 150))}</div>` : '';
    return `<tr class="t-zeile${z.an ? ' gewaehlt' : ''}" data-s="${esc(r.schluessel)}"><td class="t-name" title="${esc(typnamenTitel(r))}"><b>${esc(r.schluessel)}</b><div class="leise t-sub">${esc([text(r.entitaeten), erkannt].filter(Boolean).join(' · '))}</div>${annahme}</td>${zahlenZellen(r)}`
      + `<td><select class="t-kat" aria-label="Kategorie ${esc(r.schluessel)}">${kategorieOptionenHtml(daten, z.kategorie, '(wählen)')}</select></td><td class="t-wirkung leise">${esc(kategorieWirkung(daten, z.kategorie))}</td>`
      + `<td><input type="checkbox" class="t-an" aria-label="${esc(r.schluessel)} bestätigen"${z.an ? ' checked' : ''}></td></tr>`;
  }).join('');
  return `<div class="t-scroll"><table class="tab t-tab t-tab-bestaetigen"><thead><tr><th>Typname</th>${ZAHLEN_KOPF}<th>Kategorie</th><th>Wirkung</th><th><label title="Alle bestätigen"><input type="checkbox" class="t-an-alle"${alle ? ' checked' : ''}> bestätigen</label></th></tr></thead><tbody>${reihen}</tbody></table></div>`;
}

/** Tabelle der Einträge: Kennzeichen, Typname, Kategorie (änderbar), Wirkung, Quelle (änderbar), Zahlen, entfernen. Ein Eintrag, der auf kein Bauteil passt, trägt den Vermerk «Schreibweise prüfen». */
export function typenListeHtml(daten, form) {
  const zeilen = daten?.typen ?? [];
  if (zeilen.length === 0) return '<p class="leise kp-leer">Die Typenliste ist leer. Unter «Unbekannte Typen» lassen sich Typen mit Kategorie und Quelle festlegen.</p>';
  const reihen = zeilen.map((t) => {
    const z = listeZeile(form, t);
    const leer = zahl0(t.bauteile) === 0;
    return `<tr class="t-zeile${z.entfernen ? ' t-weg' : ''}${leer ? ' t-ohne' : ''}" data-e="${esc(t.element)}"><td>${esc(t.element)}</td>`
      + `<td class="t-name" title="${esc(typnamenTitel(t))}"><b>${esc(t.typname)}</b>${leer ? '<div class="kp-warn t-sub">passt auf kein Bauteil – Schreibweise prüfen</div>' : `<div class="leise t-sub">${esc(text(t.entitaeten))}</div>`}</td>`
      + `<td><select class="t-kat" aria-label="Kategorie ${esc(t.element)}">${kategorieOptionenHtml(daten, z.kategorie, '(wählen)')}</select></td><td class="t-wirkung leise">${esc(kategorieWirkung(daten, z.kategorie))}</td>`
      + `<td class="t-quellzelle"><input type="text" class="t-zq" value="${esc(z.quelle)}" aria-label="Quelle ${esc(t.element)}" data-kein-enter="1"><div class="leise t-sub">${esc(text(t.gesetzt))}</div></td>${zahlenZellen(t)}`
      + `<td><label class="haken"><input type="checkbox" class="t-entf"${z.entfernen ? ' checked' : ''}> entfernen</label></td></tr>`;
  }).join('');
  return `<div class="t-scroll"><table class="tab t-tab t-tab-liste"><thead><tr><th>Eintrag</th><th>Typname</th><th>Kategorie</th><th>Wirkung</th><th>Quelle *</th>${ZAHLEN_KOPF}<th></th></tr></thead><tbody>${reihen}</tbody></table></div>`;
}

/** Inhalt des offenen Reiters: Erklärung, Tabelle und – bei den beiden ersten – das Feld «Quelle» (eine für alle). Der Knopf trägt die Zahl der Typen, die gesendet würden. */
export function typenInhaltHtml(daten, form) {
  const r = form?.reiter;
  const knopf = `<button type="button" class="kp-knopf kp-primaer t-los">${esc(typenKnopfText(form, daten))}</button>`;
  if (r === 'liste') {
    return `<p class="leise t-erklaerung">Kategorie und Quelle lassen sich ändern, ein Eintrag lässt sich entfernen (dann gilt wieder Materialzuordnung bzw. Typname). Jede Zeile trägt ihre eigene Quelle.</p>${typenListeHtml(daten, form)}`
      + ((daten?.typen ?? []).length > 0 ? `<div class="t-aktion">${knopf}</div>` : '');
  }
  const bestaetigen = r === 'bestaetigen';
  const erklaerung = bestaetigen
    ? 'Der Kern hat das Material dieser Typen aus dem Namen erkannt (Wortliste, Annahme der Prüfliste). Bestätigen legt einen Eintrag mit der gezeigten Kategorie an – sie lässt sich vorher ändern.'
    : 'Diese Typen haben kein erkennbares Material (im IFC nur Farben, «<Unnamed>» o. Ä.). Sortiert nach Durchgängen: Typen mit vielen Durchgängen zuerst. Wähle die Kategorie; ohne Wahl bleibt der Typ unbekannt.';
  const tabelle = bestaetigen ? typenBestaetigenHtml(daten, form) : typenUnbekanntHtml(daten, form);
  const hat = bestaetigen ? (daten?.ausTypname ?? []).length : (daten?.unbekannt ?? []).length;
  return `<p class="leise t-erklaerung">${esc(erklaerung)}</p>${tabelle}`
    + (hat > 0 ? `<div class="t-aktion"><label class="fz t-quelle-feld"><span>Quelle *</span><input type="text" class="t-quelle" value="${esc(form?.quelle)}" placeholder="${esc(TYPEN_QUELLE_PLATZHALTER)}" data-kein-enter="1"></label>${knopf}</div>` : '');
}

/** Vorschau des Trockenlaufs von `typmaterial_setzen`: Warnungen der Oberfläche, dann die Hinweise des Kerns (je Typ betroffene Bauteile, Aussparungen, Durchgänge) oder seine Ablehnung. */
export function typenVorschauHtml(r, a) {
  const warn = (a?.warnungen ?? []).length ? `<ul class="hinweisliste warnung">${a.warnungen.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>` : '';
  if (!r.angenommen) return `${warn}<div class="fehlerkasten" role="alert"><b>${esc(fuerMenschen(r.fehler || r.meldung || 'Abgelehnt.'))}</b>${r.vorschlag ? `<div>Vorschlag: ${esc(fuerMenschen(r.vorschlag))}</div>` : ''}</div>`;
  return `${warn}<p class="leise">Vorschau des Kerns (Trockenlauf – nichts wurde geändert):</p>${hinweiseListeHtml(r.hinweise) || '<p class="leise">Keine Änderung.</p>'}`;
}

/** Hinweis nach einer echten Änderung: die Wirkung kommt erst mit neuem Vorschlag und neuem Prüflauf – die Knöpfe öffnen die vorhandenen Dialoge (sie lösen nichts selbst aus). */
export function typenNachherHtml(typen) {
  const n = (typen ?? []).length;
  const entf = (typen ?? []).filter((t) => t.kategorie === null).length;
  const fest = n - entf;
  const zahlTypen = (k) => `${k} Typ${k === 1 ? '' : 'en'}`;
  const was = n === 0 ? 'Typenliste geändert' : fest === 0 ? `${zahlTypen(entf)} aus der Typenliste entfernt` : `${zahlTypen(fest)} festgelegt${entf ? `, ${entf} entfernt` : ''}`;
  return `<div class="hkap-kasten kp-nachher"><b>${esc(was)}.</b> Die Wirkung kommt erst mit einem neuen Vorschlag und einem neuen Prüflauf: `
    + `<button type="button" class="kp-knopf t-nach" data-nach="vorschlagen">Aussparungen neu vorschlagen …</button> <button type="button" class="kp-knopf t-nach" data-nach="pruefen">Kollisionen prüfen …</button></div>`;
}
