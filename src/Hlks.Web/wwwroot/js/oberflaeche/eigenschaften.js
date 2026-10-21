// Eigenschaftenpalette (links oben): zeigt das gewählte Element in Gruppen wie in Revit – Identität, Zuordnung,
// Eigenschaften laut Katalog, Abgeleitet, Herkunft. Ändern mit Enter/Fokuswechsel geht als element_aendern an den Kern;
// abgelehnte Änderungen erscheinen mit Vorschlag am Feld. Das «?» neben einem Wert öffnet «Warum» (Herkunft).
// Familien (Modul 2, C1): bei einem platzierten Element zusätzlich die Gruppen «Familie» (Familie mit Sprung in den Editor, Typ, Exemplarparameter)
// und «Anschlüsse» (Lage in Weltkoordinaten); Kennwerte mit Herkunft «familie» tragen die Marke «Familie». Beim Element Familie selbst (Editor aktiv
// oder im Projektbrowser gewählt) zeigt die Palette die Angaben der Familie (Name, Hersteller, Produkt, Beschreibung). Bausteine: familien/familien-palette.js.
// Ist nichts gewählt, zeigt die Palette die Eigenschaften der aktiven Ansicht. Wählt man im Projektbrowser eine Familie,
// zeigt sie deren Beschreibung (nur lesen). Die Palette ändert den Zustand nie selbst – sie wartet auf das Ereignis.

import { zustand, abonnieren, element, derKlasse } from '../zustand.js';
import { abfrage } from '../api.js';
import { familie, beiFamilie, familieSetzen, ausfuehren } from './aktionen.js';
import { klasseHtml, klassenDefinition } from './katalogansicht.js';
import { sende } from './befehle.js';
import { warumDialog } from './dialoge.js';
import { symbol, symbolFuerKlasse } from './symbole.js';
import { esc, entprellen, vergleichen, gewerkeListe, zahlText } from './werkzeug.js';
import { ohneQuelleMarke, paletteHinweise, paletteHinweiseHtml } from './huelle-text.js';
import { elementGruppen, familienKopfHtml, familienMarke, exemplarNeu, kopfOp } from '../familien/familien-palette.js';
import { familieHolen } from '../familien/daten.js';
import { leitungGruppe, altbestandGruppe, formteilGruppe, leitungsArt, leitungEigenschaften } from '../werkzeuge/leitung-palette.js';
import { leitungAenderung } from '../werkzeuge/leitung-bearbeiten.js';
import { serieLaden, serienListe, befundeVon } from '../werkzeuge/leitung-daten.js';
import { istFrei, istAbwasser, freiEingabe } from '../werkzeuge/leitung-frei.js';
import { kategorieVon } from '../werkzeuge/leitung-logik.js';
import { anmelden } from './paletten.js';
import { artZeilen, leitungZeilen, kHinweisZeile, spezialFeld, spezialZeileHtml } from './sanitaer-palette.js';
import { SANITAER_ABGELEITET, spezialWert, SPEZIAL_FEHLT } from './sanitaer-text.js';
import { revisionFeld, revisionenWert, zeilenAusTabelle } from './planblatt-hk.js';   // Modul 7 Teil A: Revisionsliste des Planblatts
import { ausrichtungZeilen, planwinkelZeilen } from './ausrichtung-palette.js';   // N9: Ausrichtung der Grundrisse (Ansicht) und Planwinkel (Gebäude)

const lc = (t) => String(t ?? '').toLowerCase();
const GRUPPEN_ZU = 'palette.gruppenZu';
let wurzel, inhalt;
let nr = 0;                 // laufende Nummer des letzten Zeichnens (veraltete Antworten verwerfen)
let gerade = null;          // { id, kz } des dargestellten Elements
let schmutzig = false;      // Eingabe läuft, noch nicht übernommen
let reservierungsSig = '';
let hinweise = null;        // { id, texte } Hinweise des Kerns zum letzten angenommenen Befehl (Kasten oben, bis zur nächsten Eingabe oder Auswahl)
const zu = new Set((() => { try { return JSON.parse(localStorage.getItem('hlks.' + GRUPPEN_ZU) ?? '[]'); } catch { return []; } })());

export function einrichten(w) {
  wurzel = w;
  wurzel.classList.add('ep');
  wurzel.innerHTML = '<div class="ep-inhalt"></div>';
  inhalt = wurzel.querySelector('.ep-inhalt');

  inhalt.addEventListener('change', beiAenderung);
  inhalt.addEventListener('input', () => { schmutzig = true; });
  inhalt.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && e.target.matches('input, select')) { e.stopPropagation(); schmutzig = false; zeichnen(); }
  });
  inhalt.addEventListener('click', (e) => {
    const kopf = e.target.closest('.ep-gruppenkopf');
    if (kopf) { gruppeUmschalten(kopf.parentElement); return; }
    if (e.target.closest('.ep-hinweise-zu')) { hinweise = null; inhalt.querySelector('.ep-hinweise')?.remove(); return; }
    const fk = e.target.closest('.ep-fam-knopf');
    if (fk) { ausfuehren('familie.bearbeiten', { familie: fk.dataset.fam }); return; }
    const lt = e.target.closest('.ep-lt-knopf');
    if (lt && gerade) { ausfuehren('leitung.aufteilen', { leitung: gerade.kz }); return; }
    const sp = e.target.closest('.ep-spez-neu, .ep-spez-weg');
    if (sp) { spezialKlick(sp); return; }
    const rv = e.target.closest('.ep-rev-neu, .ep-rev-weg');
    if (rv) { revisionKlick(rv); return; }
    const pw = e.target.closest('.ep-pw-knopf');   // N9: Planwinkel des Gebäudes neu bestimmen
    if (pw) { ausfuehren('ansicht.planwinkel', { gebaeude: pw.dataset.gebaeude }); return; }
    const q = e.target.closest('.ep-warum');
    if (q && gerade) warumDialog(gerade.kz, q.dataset.warum);
  });

  const planen = entprellen(zeichnen, 50);
  abonnieren('auswahl', (id) => { if (id) familieSetzen(null); schmutzig = false; hinweise = null; planen(); });
  abonnieren('ansicht', () => { if (!zustand.auswahl) planen(); });
  abonnieren('modell', (d) => {
    if (d.alles) { planen(); return; }
    if (gerade && ((d.ids ?? []).includes(gerade.id) || (d.geloescht ?? []).includes(gerade.id))) planen();
  });
  abonnieren('status', () => {
    const sig = JSON.stringify(zustand.status?.reservierungen ?? []);
    if (sig !== reservierungsSig) { reservierungsSig = sig; planen(); }
  });
  beiFamilie(() => planen());
  zeichnen();
}

function gruppeUmschalten(sektion) {
  const name = sektion.dataset.g;
  if (zu.has(name)) zu.delete(name); else zu.add(name);
  sektion.classList.toggle('zu', zu.has(name));
  try { localStorage.setItem('hlks.' + GRUPPEN_ZU, JSON.stringify([...zu])); } catch { /* egal */ }
}

// ---------------------------------------------------------------------------------------------- Zeichnen

async function zeichnen() {
  if (!inhalt) return;
  if (schmutzig && inhalt.contains(document.activeElement)) return;   // Eingabe läuft: später, nach der Übernahme
  const mein = ++nr;
  reservierungsSig = JSON.stringify(zustand.status?.reservierungen ?? []);

  if (familie.name && !zustand.auswahl) { await familieZeigen(mein); return; }

  const istAnsicht = !zustand.auswahl;
  const ziel = istAnsicht ? element(zustand.aktiveAnsicht) : element(zustand.auswahl);
  if (!ziel) {
    gerade = null;
    inhalt.innerHTML = `<div class="ep-leer">${istAnsicht ? 'Nichts ausgewählt.<br>Eine Ansicht öffnen oder ein Element anklicken.' : 'Element nicht gefunden.'}</div>`;
    return;
  }
  let lesen, def = null, fam = null, aus = null;
  try {
    const famBezug = ziel.klasse === 'Familie' ? ziel.kennzeichen : ziel.bezuege?.familie ?? null;
    // N9: Drehung der Ansicht bzw. Planwinkel des Gebäudes (Abfrage planwinkel); fehlt sie, bleibt die Palette wie bisher
    const ausParameter = ziel.klasse === 'Ansicht' ? { ansicht: ziel.kennzeichen } : ziel.klasse === 'Gebaeude' ? { gebaeude: ziel.kennzeichen } : null;
    [lesen, def, fam, aus] = await Promise.all([
      ziel.klasse === 'Familie' ? Promise.resolve({}) : abfrage('element_lesen', { element: ziel.kennzeichen }),
      ziel.klasse === 'Familie' ? Promise.resolve(null) : klassenDefinition(ziel.klasse).catch((e) => { console.error(e); return null; }),
      famBezug ? famDaten(famBezug, ziel) : Promise.resolve(null),
      ausParameter ? abfrage('planwinkel', ausParameter).catch((e) => { console.warn('planwinkel', e?.message ?? e); return null; }) : Promise.resolve(null),
    ]);
  } catch (e) {
    console.error(e);
    if (mein === nr) inhalt.innerHTML = `<div class="ep-leer fehlertext">${esc(e.message)}${e.vorschlag ? `<br><span class="leise">${esc(e.vorschlag)}</span>` : ''}</div>`;
    return;
  }
  if (mein !== nr) return;
  let san = null;
  if (def?.eigenschaften?.some((d) => d.name === 'sanitaerArt')) san = await sanitaerKatalog();   // Modul 4: Vorgabe der Klasse für den Eintrag «Vorgabe der Klasse (…)»
  if (mein !== nr) return;
  if (fam && ziel.klasse !== 'Familie') fam = { ...fam, exemplar: lesen?.eigenschaften?.parameter?.wert ?? null };
  gerade = { id: ziel.id, kz: ziel.kennzeichen, fam };
  const y = inhalt.scrollTop;
  inhalt.innerHTML = ziel.klasse === 'Familie' ? familienKopfHtml({ f: fam?.f }, HELFER) : palette(ziel, lesen, def, istAnsicht, fam, san, aus);
  inhalt.scrollTop = y;
}

/** Daten der Familie zu einem Element: Familie selbst (Abfrage familie) und bei einem platzierten Element die Anschlusslagen. */
async function famDaten(bezug, ziel) {
  const f = await familieHolen(bezug);
  if (!f) return null;
  if (ziel.klasse === 'Familie') return { f };
  let lagen = null;
  try { lagen = await abfrage('anschlusslagen', { element: ziel.kennzeichen }); } catch (e) { console.warn('Anschlusslagen', e?.message ?? e); }
  // Leitungen (Modul 3, C1): Serie mit ihren Grössen und die Summen des Leitungszugs
  let serie = null, zug = null, befunde = [];
  if (leitungsArt(ziel) === 'leitung') {
    serie = await serieLaden(bezug, f);     // eine freie Serie bleibt leicht (nie die langsame Abfrage leitungsserie)
    try { zug = await abfrage('leitungszug', { leitung: ziel.kennzeichen }); } catch { zug = null; }
    // freie Grösse (P29) und Abwasser-Serie (P30): Prüfbefunde des Kerns zu dieser Leitung
    if (istFrei(serie) || istAbwasser(serie)) befunde = await befundeVon(ziel.kennzeichen);
  }
  return { f, lagen, serie, zug, befunde };
}

async function familieZeigen(mein) {
  gerade = null;
  const name = familie.name;
  inhalt.innerHTML = `<div class="ep-kopf"><div class="ep-typ"><span class="ep-sym">${symbol('familie', 16)}</span><b>${esc(name)}</b></div><div class="ep-hinweis">Familie (Katalog) – nur lesen</div></div><div class="ep-familie leise">Wird geladen …</div>`;
  const html = await klasseHtml(name);
  if (mein !== nr) return;
  inhalt.querySelector('.ep-familie').classList.remove('leise');
  inhalt.querySelector('.ep-familie').innerHTML = html;
}

function reservierungVon(el) {
  return (zustand.status?.reservierungen ?? []).find((r) => lc(r.element) === lc(el.kennzeichen)) ?? null;
}

// ---------------------------------------------------------------------------------------------- Bausteine

function zeile(label, feld, { titel = '', warum = null, pflicht = false, klasse = '', marke = '' } = {}) {
  return `<div class="ep-zeile ${klasse}${marke ? ' ohne-quelle' : ''}"><label class="ep-label" title="${esc(titel)}">${esc(label)}${pflicht ? ' *' : ''}</label>`
    + `<div class="ep-feld">${feld}${warum ? `<button type="button" class="ep-warum" data-warum="${esc(warum)}" title="Warum? Herkunft dieses Werts">?</button>` : ''}</div>${marke ? `<div class="ep-marke">${marke}</div>` : ''}</div>`;
}

function gruppe(name, zeilen) {
  if (!zeilen.length) return '';
  return `<section class="ep-gruppe${zu.has(name) ? ' zu' : ''}" data-g="${esc(name)}"><h4 class="ep-gruppenkopf"><span class="ep-pfeil"></span>${esc(name)}</h4><div class="ep-gruppeninhalt">${zeilen.join('')}</div></section>`;
}

const nurLesen = (text) => `<span class="ep-text">${esc(text ?? '')}</span>`;
const textFeld = (ziel, wert, extra = '') => `<input type="text" data-ziel="${esc(ziel)}" data-typ="text" value="${esc(wert ?? '')}" ${extra}>`;

function auswahlFeld(ziel, optionen, aktuell, typ = 'auswahl') {
  const liste = optionen.some(([w]) => w === aktuell) || aktuell === null || aktuell === undefined || aktuell === '' ? optionen : [...optionen, [aktuell, aktuell]];
  return `<select data-ziel="${esc(ziel)}" data-typ="${typ}"><option value="">–</option>${liste.map(([w, t]) => `<option value="${esc(w)}"${w === aktuell ? ' selected' : ''}>${esc(t)}</option>`).join('')}</select>`;
}

/** Eingabefeld passend zur Katalogdefinition einer Eigenschaft. */
function eigenschaftsFeld(d, wert) {
  const ziel = 'eig:' + d.name;
  switch (d.typ) {
    case 'zahl':
      return `<input type="text" inputmode="decimal" data-ziel="${esc(ziel)}" data-typ="zahl" value="${esc(zahlText(wert))}">${d.einheit ? `<span class="ep-einheit">${esc(d.einheit)}</span>` : ''}`;
    case 'auswahl':
      return auswahlFeld(ziel, (d.auswahl ?? []).map((a) => [a, a]), wert ?? null);
    case 'bool':
      return `<input type="checkbox" data-ziel="${esc(ziel)}" data-typ="bool" ${(d.name === 'belastung' ? wert !== false : wert === true) ? 'checked' : ''}>`;   // Modul 4: Ansicht.belastung ist ohne Angabe ein (Häkchen, solange nicht false)
    case 'liste': {
      const aktuell = Array.isArray(wert) ? wert : [];
      const moegliche = d.auswahl ? d.auswahl.map((a) => [a, a]) : d.name === 'gewerke' ? gewerkeListe() : null;
      if (moegliche) {
        const alle = [...moegliche, ...aktuell.filter((a) => !moegliche.some(([w]) => w === a)).map((a) => [a, a])];
        return `<div class="ep-haken" data-ziel="${esc(ziel)}" data-typ="haken">${alle.map(([w, t]) => `<label><input type="checkbox" value="${esc(w)}" ${aktuell.includes(w) ? 'checked' : ''}> ${esc(t)}</label>`).join('')}</div>`;
      }
      return `<input type="text" data-ziel="${esc(ziel)}" data-typ="liste" value="${esc(aktuell.join(', '))}" placeholder="durch Komma getrennt">`;
    }
    case 'objekt':
      return `<code class="ep-objekt" title="Wird in dieser Palette nicht bearbeitet">${esc(wert === undefined ? '' : JSON.stringify(wert))}</code>`;
    default:
      if (d.name === 'anlage' && d.typ === 'text') return auswahlFeld(ziel, derKlasse('Anlage').sort((a, b) => vergleichen(a.kennzeichen, b.kennzeichen)).map(kennOption), wert ?? null);   // Ansicht.anlage (Liste Sanitär-Zusammenstellung)
      return textFeld(ziel, wert === undefined || wert === null ? '' : (typeof wert === 'string' ? wert : JSON.stringify(wert)));
  }
}

/** Bausteine für familien-palette.js (gleiches Aussehen wie die übrigen Gruppen). */
const HELFER = { zeile: (label, feld, o) => zeile(label, feld, o), gruppe: (name, zeilen) => gruppe(name, zeilen), nurLesen: (t) => nurLesen(t), symbol: symbol('famFamilie', 16) };

const ABGELEITET_NAMEN = { laenge: 'Länge', flaeche: 'Fläche', volumen: 'Volumen', hoehe: 'Höhe' };
const ABGELEITET_EINHEIT = { m: 'm', m2: 'm²', m3: 'm³', mm: 'mm' };

function teilVonKandidaten(el, def) {
  const k = el.klasse;
  if (k === 'Geschoss') return derKlasse('Gebaeude');
  if (k === 'System') return derKlasse('Anlage');
  if (k === 'Strang') return derKlasse('System');
  if (k === 'Raum') return derKlasse('Zone');
  if (['Leitung', 'Formteil', 'Armatur'].includes(el.basis) || (def?.basis && ['Leitung', 'Formteil', 'Armatur'].includes(def.basis))) return derKlasse('Strang');
  return null;
}

const kennOption = (e) => [e.kennzeichen, e.name ? `${e.kennzeichen} – ${e.name}` : e.kennzeichen];

// ---------------------------------------------------------------------------------------------- Palette aufbauen

function palette(el, l, def, istAnsicht, fam = null, san = null, aus = null) {
  const res = reservierungVon(el);
  let h = `<div class="ep-kopf"><div class="ep-typ"><span class="ep-sym">${symbol(symbolFuerKlasse(el.klasse, el.basis), 16)}</span>`
    + `<select disabled title="Typwechsel folgt später"><option>${esc(el.klasse)}</option></select></div>`
    + `${istAnsicht ? `<div class="ep-hinweis">Ansichtseigenschaften – ${esc(el.name || el.kennzeichen)}</div>` : ''}`
    + `${res ? `<div class="ep-reserviert ${res.eigene ? 'eigene' : 'fremde'}">${res.eigene ? 'Von dir reserviert' : `Reserviert von ${esc(res.akteur)}@${esc(res.geraet)}`} bis ${esc(res.bis)}${res.eigene ? '' : ' – Änderungen werden abgelehnt.'}</div>` : ''}</div>`;

  if (hinweise && hinweise.id === el.id) h += paletteHinweiseHtml(hinweise.texte);

  h += gruppe('Identität', [
    zeile('Kennzeichen', textFeld('kz', el.kennzeichen), { pflicht: true, titel: 'Eindeutiges Kennzeichen' }),
    zeile('Name', textFeld('name', el.name ?? ''), { titel: 'Bezeichnung (frei)' }),
    zeile('Klasse', nurLesen(el.klasse), { titel: def?.beschreibung ?? '' }),
  ]);

  // Familie (Modul 2, C1): Familie, Typ, Exemplarparameter und Anschlüsse eines platzierten Elements
  // Leitungen (Modul 3, C1): Serie/Grösse/Dämmung statt der allgemeinen Familiengruppe; Altbestand: Aufteilen; Formteile: Art, Typ, Winkel
  const ltArt = leitungsArt(el);
  if (ltArt === 'leitung' && fam?.f) h += leitungGruppe({ el, l, f: fam.f, serie: fam.serie ?? null, serien: serienListe(kategorieVon(el.klasse)), zug: fam.zug ?? null, befunde: fam.befunde ?? [] }, HELFER);
  else if (ltArt === 'altbestand') h += altbestandGruppe({ el }, HELFER);
  else if (ltArt === 'formteil') h += formteilGruppe({ el, l, f: fam?.f ?? null }, HELFER);
  if (fam?.f && ltArt !== 'leitung') h += elementGruppen({ f: fam.f, lagen: fam.lagen, typ: l.eigenschaften?.familientyp?.wert ?? null, exemplar: l.eigenschaften?.parameter?.wert ?? null, kz: el.kennzeichen }, HELFER);
  fam = fam?.f ? fam : null;
  const ltGruppe = ltArt === 'leitung' && !!fam;
  const ltEig = ltGruppe ? leitungEigenschaften(fam.serie) : [];

  // Zuordnung: Auswahllisten aus passenden Elementen
  const zu_ = [];
  const geschossNoetig = def?.brauchtGeschoss || el.geschoss || (el.klasse === 'Ansicht' && el.werte?.art === 'grundriss');
  if (geschossNoetig) {
    const gs = derKlasse('Geschoss').sort((a, b) => (a.werte?.kote ?? 0) - (b.werte?.kote ?? 0)).map(kennOption);
    zu_.push(zeile('Geschoss', auswahlFeld('zu:geschoss', gs, el.geschoss ?? null), { warum: 'geschoss' }));
  }
  if (def?.brauchtRaum || el.raum) {
    const rs = derKlasse('Raum').filter((r) => !el.geschoss || lc(r.geschoss) === lc(el.geschoss)).sort((a, b) => vergleichen(a.kennzeichen, b.kennzeichen)).map(kennOption);
    zu_.push(zeile('Raum', auswahlFeld('zu:raum', rs, el.raum ?? null), { warum: 'raum' }));
  }
  if (def?.brauchtSystem || el.system) {
    const ss = derKlasse('System').sort((a, b) => vergleichen(a.kennzeichen, b.kennzeichen)).map(kennOption);
    zu_.push(zeile('System', auswahlFeld('zu:system', ss, el.system ?? null), { warum: 'system' }));
  }
  const tv = teilVonKandidaten(el, def);
  if (tv || el.teilVon) {
    const opt = (tv ?? []).sort((a, b) => vergleichen(a.kennzeichen, b.kennzeichen)).map(kennOption);
    zu_.push(zeile('teilVon', auswahlFeld('zu:teilVon', opt, el.teilVon ?? null), { titel: 'Übergeordnetes Element' }));
  }
  h += gruppe('Zuordnung', zu_);

  // Eigenschaften laut Katalog
  const werte = l.eigenschaften ?? {};
  const ez = [];
  const bekannt = new Set();
  for (const d of def?.eigenschaften ?? []) {
    bekannt.add(d.name);
    if (fam && (d.name === 'familientyp' || d.name === 'parameter')) continue;   // stehen in der Gruppe «Familie»
    if (ltGruppe && ltEig.includes(d.name)) continue;                            // stehen in der Gruppe «Leitung»
    const w = werte[d.name]?.wert;
    // Modul 4 Teil A-O: Apparateart mit Namen und abgeleiteten Werten darunter, Tabelle der Spezialinstallationen, Hinweis zu K
    if (d.name === 'sanitaerArt' && san) { ez.push(...artZeilen({ def: d, wert: w ?? null, vorgabe: san.vorgabeJeKlasse?.[el.klasse] ?? null, l }, HELFER)); continue; }
    if (d.name === 'spezialinstallationen' && d.typ === 'liste') {
      ez.push(zeile(d.name, spezialFeld(w), { titel: [d.beschreibung, werte[d.name]?.herkunft].filter(Boolean).join('\n'), warum: werte[d.name] ? d.name : null, klasse: 'ep-breit' }));
      continue;
    }
    // Modul 7 Teil A: Revisionsliste des Planblatts als Tabelle (Index, Datum, Beschreibung); neue Revisionen setzt der Kern (revision_hinzufuegen)
    if (el.klasse === 'Planblatt' && d.name === 'revisionen') {
      ez.push(zeile(d.name, revisionFeld(w), { titel: [d.beschreibung, werte[d.name]?.herkunft].filter(Boolean).join('\n'), warum: werte[d.name] ? d.name : null, klasse: 'ep-breit' }));
      continue;
    }
    // Kennwerte ohne Quelle gelten in der Rechnung nicht: Marke «ohne Quelle» am Feld (Herkunft aus element_lesen)
    const z = zeile(d.name, eigenschaftsFeld(d, w) + familienMarke(werte[d.name]?.herkunft), { titel: [d.beschreibung, werte[d.name]?.herkunft].filter(Boolean).join('\n'), pflicht: d.pflicht, warum: werte[d.name] ? d.name : null,
      marke: werte[d.name] ? ohneQuelleMarke(el.klasse, d.name, werte[d.name].herkunft) : '' });
    ez.push(z);
    if (d.name === 'abflusskennzahl') ez.push(kHinweisZeile(HELFER));
    if (el.klasse === 'Ansicht' && d.name === 'ausrichtung') ez.push(...ausrichtungZeilen(aus, HELFER));                  // N9: Drehung und Quelle
    if (el.klasse === 'Gebaeude' && d.name === 'planwinkel') ez.push(...planwinkelZeilen(aus, el.kennzeichen, HELFER));   // N9: Knopf «Planwinkel neu bestimmen»
  }
  for (const [name, v] of Object.entries(werte)) {
    if (bekannt.has(name) || (fam && (name === 'familientyp' || name === 'parameter')) || (ltGruppe && ltEig.includes(name))) continue;
    ez.push(zeile(name, nurLesen(v?.wert === undefined ? '' : (typeof v.wert === 'object' ? JSON.stringify(v.wert) : `${v.wert}${v.einheit ? ' ' + v.einheit : ''}`)), { titel: v?.herkunft ?? '', warum: name }));
  }
  h += gruppe('Eigenschaften', ez);

  // Abgeleitet (nur lesen)
  const ab = Object.entries(l.abgeleitet ?? {}).filter(([k]) => !SANITAER_ABGELEITET.includes(k)).map(([k, v]) => {
    const [basis, einheit] = k.split('_');
    return zeile(ABGELEITET_NAMEN[basis] ?? basis, nurLesen(`${v}${einheit ? ' ' + (ABGELEITET_EINHEIT[einheit] ?? einheit) : ''}`), { warum: basis, titel: 'Berechnet aus der Geometrie' });
  });
  ab.push(...(el.basis === 'Apparat' ? [] : leitungZeilen(l, HELFER)));   // Modul 4: Belastungssummen einer Leitung (die Werte eines Apparats stehen unter der Apparateart)
  h += gruppe('Abgeleitet', ab);

  // Herkunft
  const verlauf = String(l.herkunft ?? '').split(';').map((s) => s.trim()).filter(Boolean);
  h += gruppe('Herkunft', [
    ...verlauf.map((v, i) => zeile(i === 0 ? 'Verlauf' : '', nurLesen(v), { klasse: 'klein' })),
    zeile('Id', `<span class="ep-text mono">${esc(el.id)}</span>`, { klasse: 'klein' }),
    zeile('Version', nurLesen(String(l.version ?? el.version ?? '')), { klasse: 'klein' }),
  ]);
  return h;
}

// ---------------------------------------------------------------------------------------------- Ändern

/** Liest den Wert eines Feldes; wirft bei ungültiger Eingabe einen Fehler mit verständlichem Text. */
function wertLesen(feld) {
  const typ = feld.dataset.typ;
  switch (typ) {
    case 'zahl': {
      const t = feld.value.trim();
      if (t === '') return null;
      const n = Number(t.replace(',', '.'));
      if (!Number.isFinite(n)) throw new Error('Bitte eine Zahl eingeben.');
      return n;
    }
    case 'bool': return feld.checked;
    case 'spezial': {   // Modul 4 Teil A-O: Tabelle der Spezialinstallationen → Liste { bezeichnung, versorgung, entsorgung }; unvollständige Zeilen werden nicht gesendet
      const zeilen = [...feld.querySelectorAll('tbody tr')].map((tr) => ({ bezeichnung: tr.querySelector('.sp-b').value, versorgung: tr.querySelector('.sp-v').value, entsorgung: tr.querySelector('.sp-e').value }));
      const r = spezialWert(zeilen);
      feld.querySelector('.ep-spez-hinweis').hidden = r.offen === 0;
      if (r.offen > 0) throw Object.assign(new Error(SPEZIAL_FEHLT), { still: true });
      return r.wert;
    }
    case 'revisionen': return revisionenWert(zeilenAusTabelle(feld));   // Modul 7 Teil A: Tabelle der Revisionen → Liste { index, datum, beschreibung }
    case 'haken': { const l = [...feld.querySelectorAll('input:checked')].map((c) => c.value); return l.length ? l : null; }
    case 'liste': { const l = feld.value.split(',').map((s) => s.trim()).filter(Boolean); return l.length ? l : null; }
    default: { const t = feld.value.trim(); return t === '' ? null : t; }
  }
}

function fehlerAnzeigen(feld, text, vorschlag) {
  const z = feld.closest('.ep-zeile');
  if (!z) return;
  z.classList.add('fehlerhaft');
  z.querySelector('.ep-fehler')?.remove();
  const f = document.createElement('div');
  f.className = 'ep-fehler';
  f.textContent = vorschlag ? `${text} – ${vorschlag}` : text;
  z.appendChild(f);
}

/** Änderung eines Familienfeldes: Kopfangaben der Familie (kopf_setzen) oder Exemplarparameter eines Elements (element_aendern). */
async function famAenderung(feld, ziel) {
  const f = gerade?.fam?.f;
  if (!f) return;
  schmutzig = false;
  if (ziel.startsWith('famkopf:')) {
    const k = kopfOp(ziel.slice('famkopf:'.length), feld.value);
    if (k.fehler) { fehlerAnzeigen(feld, k.fehler); return; }
    const r = await sende('familie_aendern', { familie: gerade.kz, ops: [k.op] });
    if (!r.angenommen) fehlerAnzeigen(feld, r.fehler || r.meldung || 'Die Änderung wurde abgelehnt.', r.vorschlag);
    return;
  }
  const name = ziel.slice('fam:param:'.length);
  const p = (f.definition?.parameter ?? []).find((x) => x.name === name);
  if (!p) return;
  const bisher = gerade.fam.exemplar ?? null;
  const n = exemplarNeu(bisher, p, feld.type === 'checkbox' ? feld.checked : feld.value);
  if (n.fehler) { fehlerAnzeigen(feld, n.fehler); return; }
  const r = await sende('element_aendern', { element: gerade.kz, eigenschaften: { parameter: n.objekt } });
  if (!r.angenommen) fehlerAnzeigen(feld, r.fehler || r.meldung || 'Die Änderung wurde abgelehnt.', r.vorschlag);
}

/** Modul 4 Teil A-O: Zeile der Tabelle Spezialinstallationen hinzufügen oder löschen (Löschen sendet sofort, Hinzufügen erst mit der ausgefüllten Zeile). */
function spezialKlick(knopf) {
  const tabelle = knopf.closest('.ep-spezial');
  if (!tabelle) return;
  if (knopf.classList.contains('ep-spez-neu')) {
    schmutzig = true;
    tabelle.querySelector('tbody').insertAdjacentHTML('beforeend', spezialZeileHtml());
    tabelle.querySelector('tbody tr:last-child .sp-b').focus();
    return;
  }
  knopf.closest('tr').remove();
  tabelle.dispatchEvent(new Event('change', { bubbles: true }));
}

/** Modul 7 Teil A: Revision hinzufügen (Dialog, dann Befehl revision_hinzufuegen) oder eine Zeile der Revisionstabelle löschen (sendet sofort). */
function revisionKlick(knopf) {
  if (knopf.classList.contains('ep-rev-neu')) { if (gerade) ausfuehren('plan.revision', { planblatt: gerade.kz }); return; }
  const tabelle = knopf.closest('.ep-revisionen');
  knopf.closest('tr').remove();
  tabelle.dispatchEvent(new Event('change', { bubbles: true }));
}

// Katalog der Sanitär-Vorgaben (Abfrage sanitaer_katalog: Vorgabe je Klasse), einmal je Modellstand-Wechsel «alles»
let sanKatalog = null;
abonnieren('modell', (d) => { if (d.alles) sanKatalog = null; });
function sanitaerKatalog() {
  sanKatalog ??= abfrage('sanitaer_katalog', {}).catch((e) => { console.warn('sanitaer_katalog', e?.message ?? e); sanKatalog = null; return null; });
  return sanKatalog;
}

async function beiAenderung(e) {
  const feld = e.target.closest('[data-ziel]');
  if (!feld || !gerade) return;
  e.stopPropagation();
  const zeileEl = feld.closest('.ep-zeile');
  zeileEl?.classList.remove('fehlerhaft');
  zeileEl?.querySelector('.ep-fehler')?.remove();
  const ziel = feld.dataset.ziel;
  if (ziel.startsWith('fam')) { await famAenderung(feld, ziel); return; }
  if (['lt:serie', 'lt:groesse', 'lt:raster', 'lt:b', 'lt:h', 'lt:d'].includes(ziel)) {
    // Leitung (Modul 3, C1): Rückfrage «nur diese Leitung / ganzer Zug», dann leitung_serie bzw. leitung_groesse
    // Freie Grösse (Nachtrag): Zahlenfelder b, h bzw. d (beide Felder zusammen, Grenzen und Schritt prüft der Kern) und die Normgrösse als Vorschlag
    schmutzig = false;
    let wert = feld.value;
    if (ziel === 'lt:b' || ziel === 'lt:h' || ziel === 'lt:d') {
      const lesen = (n) => inhalt.querySelector(`[data-ziel="lt:${n}"]`)?.value;
      const e = freiEingabe(gerade.fam?.serie, { b: lesen('b'), h: lesen('h'), d: lesen('d') });
      if (e.fehler) { fehlerAnzeigen(feld, e.fehler); return; }
      wert = e.groesse;
    }
    const r = await leitungAenderung(ziel === 'lt:serie' ? 'serie' : 'groesse', gerade.kz, wert);
    if (r?.fehler) fehlerAnzeigen(feld, r.fehler, r.vorschlag);
    else if (!r?.ok) zeichnen();      // abgebrochen: die Auswahl zeigt wieder den Stand des Modells
    return;
  }
  let wert;
  try { wert = wertLesen(feld); }
  catch (err) { if (!err.still) fehlerAnzeigen(feld, err.message); return; }   // still: Zeile der Tabelle noch unvollständig, Hinweis steht schon an der Tabelle

  const param = { element: gerade.kz };
  if (ziel === 'kz') {
    if (wert === null) { fehlerAnzeigen(feld, 'Das Kennzeichen darf nicht leer sein.'); return; }
    param.kennzeichen = wert;
  } else if (ziel === 'name') param.name = wert;
  else if (ziel.startsWith('zu:')) param[ziel.slice(3)] = wert;
  else if (ziel.startsWith('eig:')) param.eigenschaften = { [ziel.slice(4)]: wert };
  else return;

  schmutzig = false;
  hinweise = null;
  inhalt.querySelector('.ep-hinweise')?.remove();
  const r = await sende('element_aendern', param);
  if (!r.angenommen) {
    fehlerAnzeigen(feld, r.fehler || r.meldung || 'Die Änderung wurde abgelehnt.', r.vorschlag);
    return;
  }
  // Hinweise des Kerns zu einem angenommenen Befehl (z. B. «Kennwerte ohne Quelle … gelten in der Rechnung als fehlend») zeigt die Palette in einem Kasten oben
  const texte = paletteHinweise(r.hinweise);
  if (texte.length > 0 && gerade) hinweise = { id: gerade.id, texte };
  // Bei «Keine Änderung» kommt kein Ereignis; Anzeige trotzdem auf den Stand bringen (z. B. «5,0» → «5»); mit Hinweisen sicherheitshalber ebenfalls
  if ((r.aenderungen ?? []).length === 0 || texte.length > 0) zeichnen();
}

// Als Palette der Palettenverwaltung (W1): Vorgabe links oben; dort wird einrichten() mit dem Inhaltsknoten aufgerufen. Andocken, Reiter, Schweben: paletten.js.
anmelden({ id: 'eigenschaften', titel: 'Eigenschaften', ort: 'links', reihenfolge: 10, elementId: 'palette-eigenschaften', inhaltId: 'eigenschaften', symbol: 'eigenschaften', bauen: einrichten });   // Kürzel PP: Aktion aendern.eigenschaften
