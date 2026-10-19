// Projektbrowser (links unten): Baum mit Auswahlfeld der Gliederung und Suchfeld. G1 (entwurf/projektbrowser-gliederung.md): die Gliederung ist umschaltbar – Gebäude (wie bisher),
// Sanitär, Heizung, Lüftung, Koordination, Energie und Umwelt; jeder Benutzer stellt seine primäre Gliederung ein (gilt überall, auf allen seinen Rechnern: /api/benutzer/einstellungen).
// Die Gliederungen und ihre Äste stehen in EINER Tabelle (projektbrowser-gliederung.js), die Knoten der Äste in projektbrowser-aeste.js (rein, in Node getestet); hier steht nur
// das Zeichnen und Bedienen. Fachinhalt steht oben, alles andere eingeklappt unter «Weitere»; die Gewerke sind in ihrer Pastellfarbe hinterlegt (Katalog gewerke.json).
//
// Äste von «Gebäude»: Ansichten (nach Art gruppiert), Ansichtsvorlagen, Pläne (nach Gewerk), Gebäude (Gebäude → Geschoss → Räume, dazu «Nicht platzierte Räume» für HKAP-Räume ohne Geometrie, Modul 1d),
// Hülle (Materialien, Aufbauten, Wärmebrücken; Modul 1c), Systeme (Anlage → System → Strang), Unterlagen (IFC, CAD, PDF, Bild), Familien (Katalog nach Schicht → Klasse).
// Doppelklick auf eine Ansicht oder ein Planblatt öffnet sie, Klick auf ein Element wählt es aus, Katalogklassen zeigen ihre Beschreibung in der
// Eigenschaftenpalette. Familien (Modul 2, C1): die Familien des Projekts je Kategorie mit ihren Typen (Entwürfe grau mit Marke «Entwurf»), dazu der Knoten
// «Katalog» mit den Katalogklassen; Doppelklick auf eine Familie öffnet den Editor, auf einen Typ platziert; Kontextmenü Bearbeiten, Platzieren, Neue Familie,
// Familie laden. Modul 2, C3: dazu die Bibliotheksfamilien nach der Familienauswahl des Projekts (grau, Marke «Bibliothek»; Doppelklick lädt und platziert) und Marken an Projektfamilien
// («neuere Version», «parallel geändert», «noch nicht gespeichert», Knopf ↻ «Aktualisieren …»). Kontextmenü auf Ansichten und Elementen löst Aktionen aus. Live: Änderungen erscheinen sofort.
// Koordination (G1): Kollisionen, Aussparungen und Typenliste kommen über dieselben Abfragen wie die Palette Koordination (koordination-daten.js); Doppelklick auf eine Kollision oder
// Aussparung zeigt sie im Grundriss (wie ein Klick in der Palette).

import { zustand, abonnieren, derKlasse, auswaehlen, merken, element } from '../zustand.js';
import * as fenster from '../fenster.js';
import { aktionen, ausfuehren, istAktiv, hinweisText, familie, familieSetzen, beiFamilie } from './aktionen.js';
import { kontextmenue, meldung } from './dialoge.js';
import { symbol } from './symbole.js';
import { esc, entprellen } from './werkzeug.js';
import { familienListe, beiFamilien, familienLaden } from '../familien/daten.js';
import { bibliothekFamilien, beiBibliothek, bibliothekLaden } from '../familien/bibliothek-daten.js';   // Modul 2, C3: Bibliotheksfamilien im Explorer
import { markeKlasse } from '../familien/bibliothek-text.js';
import { plansaetzeDaten, beiPlansaetzen, plansaetzeEinrichten } from './serienpdf-daten.js';   // Serien-PDF: Pläne nach Gewerk gruppiert
import { blattGewerk } from './serienpdf-text.js';
import { koordinationSicht, koordinationAktiv, beiKoordinationsdaten } from './koordination-daten.js';
import { imGrundrissZeigen, in3dZeigen } from './koordination-panel.js';
import { benutzerLaden, benutzerWert, benutzerSetzen } from './benutzer.js';
import {
  GLIEDERUNGEN, GLIEDERUNG_VORGABE, EINSTELLUNG_GLIEDERUNG, gliederung, gliederungGueltig, offenSchluessel, offenVorgabe, gewerkFarben, filtern,
} from './projektbrowser-gliederung.js';
import { baumBauen } from './projektbrowser-aeste.js';

let wurzel, baum, suchfeld, knopf, liste;
let suche = '';
let aktivId = GLIEDERUNG_VORGABE;      // Kennung der gewählten Gliederung (Vorgabe «Gebäude», auch für Tests ohne Einstellung)
let vomBenutzerGewaehlt = false;       // der Benutzer hat in dieser Sitzung selbst gewählt: eine späte Antwort des Servers überstimmt ihn nicht
const offenJe = new Map();             // Kennung → Menge der aufgeklappten Äste (je Gliederung gemerkt, Browser-Speicher)
const gesehen = new Set();             // Gruppen, die einmal anfangs offen waren (danach entscheidet der Benutzer)

const offenFuer = (id) => {
  if (!offenJe.has(id)) offenJe.set(id, new Set(merken.lesen(offenSchluessel(id), offenVorgabe(gliederung(id)))));
  return offenJe.get(id);
};
const offen = () => offenFuer(aktivId);

export function einrichten(element_) {
  wurzel = element_;
  wurzel.classList.add('pb');
  wurzel.innerHTML = `<div class="pb-kopf"><div class="pb-gliederung"><button type="button" class="pb-gl-knopf" aria-haspopup="listbox" aria-expanded="false" title="Gliederung des Projektbrowsers"></button></div>`
    + `<div class="pb-suche"><input type="search" placeholder="Suchen …" aria-label="Projektbrowser durchsuchen"></div></div><div class="pb-baum" role="tree"></div>`;
  suchfeld = wurzel.querySelector('input');
  baum = wurzel.querySelector('.pb-baum');
  knopf = wurzel.querySelector('.pb-gl-knopf');
  suchfeld.addEventListener('input', () => { suche = suchfeld.value.trim().toLowerCase(); zeichnen(); });
  suchfeld.addEventListener('keydown', (e) => { if (e.key === 'Escape') { suchfeld.value = ''; suche = ''; zeichnen(); e.stopPropagation(); } });

  baum.addEventListener('click', klick);
  baum.addEventListener('dblclick', doppelklick);
  baum.addEventListener('contextmenu', kontext);
  gliederungVerdrahten();

  const spaeter = entprellen(zeichnen, 80);
  abonnieren('modell', spaeter);
  abonnieren('ansicht', () => markieren());
  abonnieren('auswahl', () => markieren(true));
  beiFamilie(() => markieren());
  beiFamilien(spaeter);
  beiBibliothek(spaeter);
  beiPlansaetzen(spaeter);
  beiKoordinationsdaten(spaeter);
  plansaetzeEinrichten();
  familienLaden();
  bibliothekLaden();

  // Benutzereinstellung: die primäre Gliederung des Benutzers – beim Start und nach jedem Laden eines Projekts vom Server gelesen
  const uebernehmen = async () => {
    await benutzerLaden();
    if (vomBenutzerGewaehlt) return;
    const id = benutzerWert(EINSTELLUNG_GLIEDERUNG, GLIEDERUNG_VORGABE);
    const neu = gliederungGueltig(id) ? id : GLIEDERUNG_VORGABE;
    if (neu !== aktivId) setzen(neu);
  };
  uebernehmen();
  abonnieren('modell', (d) => { if (d?.alles && zustand.status?.offen) uebernehmen(); });
  setzen(aktivId);
}

// ---------------------------------------------------------------------------------------------- Gliederung wählen

/** Macht eine Gliederung zur aktiven (Zeichnen, Mitführen der Koordination, Knopf), ohne sie zu speichern. */
function setzen(id) {
  aktivId = gliederung(id).id;
  koordinationAktiv(aktivId === 'koordination');
  knopfZeichnen();
  zeichnen();
}

/** Vom Benutzer gewählt: aktiv setzen und als primäre Gliederung beim Server merken (gilt in allen Projekten und auf allen seinen Rechnern). */
async function waehlen(id) {
  if (!gliederungGueltig(id)) return;
  vomBenutzerGewaehlt = true;
  setzen(id);
  try { await benutzerSetzen(EINSTELLUNG_GLIEDERUNG, id); }
  catch (e) { meldung(`Die Gliederung konnte nicht gespeichert werden: ${e.message ?? e}`, 'warnung', { vorschlag: e.vorschlag }); }
}

/** CSS-Eigenschaften der Hinterlegung eines Gewerks (Pastellton und Koordinationsfarbe aus dem Katalog); leer, wenn das Gewerk keine Farbe hat. */
function gewerkStil(id) {
  const f = id ? gewerkFarben({ gewerke: zustand.katalog?.gewerke }, id) : null;
  return f ? `--gw-pastell:${f.pastell};--gw-kord:${f.koordination}` : '';
}

let knopfSig = '';
function knopfZeichnen() {
  const g = gliederung(aktivId);
  const stil = g.gewerk ? gewerkStil(g.gewerk) : '';
  if (knopfSig === g.id + '|' + stil) return;   // unverändert: nicht neu schreiben (der Knopf behält den Fokus)
  knopfSig = g.id + '|' + stil;
  knopf.dataset.gliederung = g.id;
  knopf.innerHTML = `<span class="pb-sym">${symbol(g.sym, 16)}</span><span class="pb-gl-name">${esc(g.titel)}</span><span class="pb-pfeil">▾</span>`;
  knopf.classList.toggle('pb-gewerk', !!stil);
  knopf.style.cssText = stil;
}

function listeOeffnen() {
  listeSchliessen();
  const r = knopf.getBoundingClientRect();
  liste = document.createElement('div');
  liste.className = 'pb-gl-liste';
  liste.setAttribute('role', 'listbox');
  liste.style.cssText = `left:${Math.round(r.left)}px;top:${Math.round(r.bottom)}px;min-width:${Math.round(r.width)}px`;
  liste.innerHTML = GLIEDERUNGEN.map((g) => {
    const stil = g.gewerk ? gewerkStil(g.gewerk) : '';
    return `<div class="pb-gl-eintrag${stil ? ' pb-gewerk' : ''}${g.id === aktivId ? ' aktiv' : ''}" role="option" aria-selected="${g.id === aktivId}" data-id="${esc(g.id)}"${stil ? ` style="${esc(stil)}"` : ''}>`
      + `<span class="pb-sym">${symbol(g.sym, 16)}</span><span class="pb-gl-name">${esc(g.titel)}</span></div>`;
  }).join('');
  document.body.appendChild(liste);
  knopf.setAttribute('aria-expanded', 'true');
  liste.addEventListener('click', (e) => { const z = e.target.closest('.pb-gl-eintrag'); if (z) { const id = z.dataset.id; listeSchliessen(); knopf.focus(); waehlen(id); } });
  setTimeout(() => document.addEventListener('pointerdown', ausserhalb, true), 0);
}

function listeSchliessen() {
  document.removeEventListener('pointerdown', ausserhalb, true);
  liste?.remove();
  liste = null;
  knopf?.setAttribute('aria-expanded', 'false');
}
function ausserhalb(e) { if (!liste?.contains(e.target) && !knopf.contains(e.target)) listeSchliessen(); }

function gliederungVerdrahten() {
  knopf.addEventListener('click', () => { if (liste) listeSchliessen(); else listeOeffnen(); });
  knopf.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && liste) { listeSchliessen(); e.stopPropagation(); return; }
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    e.preventDefault();
    const i = GLIEDERUNGEN.findIndex((g) => g.id === aktivId);
    const n = GLIEDERUNGEN[(i + (e.key === 'ArrowDown' ? 1 : -1) + GLIEDERUNGEN.length) % GLIEDERUNGEN.length];
    waehlen(n.id);
  });
}

// ---------------------------------------------------------------------------------------------- Baum aufbauen

/** Alles, was die Äste brauchen, aus dem Zustand der Oberfläche (die Äste selbst kennen keinen Zustand und sind in Node getestet). */
function datenBauen() {
  const kat = zustand.katalog;
  return {
    klassen: kat?.klassen ?? [], systemtypen: kat?.systemtypen ?? [], medien: kat?.medien ?? [], gewerke: kat?.gewerke ?? [], katalogKlassen: kat?.klassen ?? [],
    derKlasse, element: (b) => element(b), plansaetze: plansaetzeDaten(), familien: familienListe(), bibliothek: bibliothekFamilien(),
    koordination: koordinationSicht(), idVon: (kz) => element(kz)?.id ?? null,
  };
}

function baumBauenAktiv() {
  const k = baumBauen(gliederung(aktivId), datenBauen());
  const lauf = (n) => { if (n.anfangsOffen && !gesehen.has(aktivId + '|' + n.key)) { gesehen.add(aktivId + '|' + n.key); offen().add(n.key); } (n.kinder ?? []).forEach(lauf); };
  k.forEach(lauf);
  return k;
}

// ---------------------------------------------------------------------------------------------- Zeichnen

/** Marken einer Zeile (Modul 2, C3: mehrere, z. B. «Entwurf» und «neuere Version»; sonst die einzelne Marke). */
function markenHtml(k) {
  const l = Array.isArray(k.marken) ? k.marken : k.marke ? [k.marke] : [];
  return l.map((m) => `<span class="pb-marke ${markeKlasse(m)}">${esc(m)}</span>`).join('');
}

/** Knöpfe einer Zeile (Liste knoepfe, sonst der einzelne knopf); Parameter als JSON im Attribut. */
function knoepfeHtml(k) {
  const l = Array.isArray(k.knoepfe) ? k.knoepfe : k.knopf ? [k.knopf] : [];
  return l.map((b) => `<button type="button" class="pb-knopf" data-aktion="${esc(b.aktion)}"${b.param?.familie ? ` data-familie="${esc(b.param.familie)}"` : ''}${b.param ? ` data-param="${esc(JSON.stringify(b.param))}"` : ''} title="${esc(b.titel ?? '')}" aria-label="${esc(b.text)}">${esc(b.kurz ?? b.text)}</button>`).join('');
}

/** Zeile und (falls offen) ihre Kinder. gw: das Gewerk des Asts darüber, der gefärbt ist – darunter wird nur ein schmaler Streifen gezogen, einzelne Elemente werden nicht gefärbt. */
function zeilenHtml(k, tiefe, offeneIds, aus, gw = null) {
  const hatKinder = Array.isArray(k.kinder);
  const istOffen = hatKinder && (suche !== '' || offen().has(k.key));
  const zahl = hatKinder && k.zahl !== undefined ? ` <span class="pb-zahl">(${k.zahl})</span>` : '';
  const titel = k.tipp ? k.tipp : k.kz ? `${k.kz}${k.klasse ? ' · ' + k.klasse : ''}` : '';
  const zusatz = markenHtml(k) + knoepfeHtml(k);   // Marken und Knöpfe in einem Rahmen: reicht der Platz nicht, rutschen sie in eine zweite Zeile (Name bleibt lesbar)
  const eigen = !gw && k.gewerk && gewerkStil(k.gewerk) ? k.gewerk : null;   // dieser Ast stellt ein Gewerk dar und steht nicht unter einem gefärbten
  const stil = eigen ? gewerkStil(eigen) : gw ? gewerkStil(gw) : '';
  const gwKlasse = eigen ? ' pb-gewerk' : gw && stil ? ' pb-streifen' : '';
  aus.push(`<div class="pb-zeile${hatKinder ? ' ast' : ''}${zusatz ? ' mit-zusatz' : ''}${offeneIds.has(k.id) ? ' ansicht-offen' : ''}${k.css ? ' ' + k.css : ''}${gwKlasse}" role="treeitem" style="--t:${tiefe}${stil ? ';' + esc(stil) : ''}" data-key="${esc(k.key)}" data-art="${esc(k.art)}"${eigen ? ` data-gewerk="${esc(eigen)}"` : ''}${k.quelle ? ` data-quelle="${esc(k.quelle)}"` : ''}${k.kontext ? ` data-kontext="${esc(k.kontext)}"` : ''}${k.id ? ` data-id="${esc(k.id)}"` : ''}${k.kz ? ` data-kz="${esc(k.kz)}"` : ''}${k.klasse ? ` data-klasse="${esc(k.klasse)}"` : ''}${k.typ ? ` data-typ="${esc(k.typ)}"` : ''}${k.bibId ? ` data-bib-id="${esc(k.bibId)}"` : ''}${k.bibDatei ? ` data-bib-datei="${esc(k.bibDatei)}"` : ''}${k.vorlage ? ' data-vorlage="1"' : ''}${k.ansichtKz ? ` data-ansicht="${esc(k.ansichtKz)}"` : ''}${titel ? ` title="${esc(titel)}"` : ''}>`
    + `<span class="pb-pfeil">${hatKinder ? (istOffen ? '▾' : '▸') : ''}</span><span class="pb-sym">${symbol(k.sym ?? 'familie', 16)}</span><span class="pb-label">${esc(k.label)}${zahl}</span>${k.sub ? `<span class="pb-sub">${esc(k.sub)}</span>` : ''}${zusatz ? `<span class="pb-zusatz">${zusatz}</span>` : ''}</div>`);
  if (istOffen) {
    if (k.kinder.length === 0) aus.push(`<div class="pb-zeile leer" style="--t:${tiefe + 1}"><span class="pb-pfeil"></span><span class="pb-label">(leer)</span></div>`);
    for (const x of k.kinder) zeilenHtml(x, tiefe + 1, offeneIds, aus, eigen ?? gw);
  }
}

function zeichnen() {
  if (!baum) return;
  const offeneIds = new Set(fenster.alle().map((o) => o.id));
  knopfZeichnen();
  const aus = [];
  const tiefe = Number(zustand.katalog?.gewerkeDunkelDeckkraft);
  if (tiefe > 0 && tiefe < 1) wurzel.style.setProperty('--gw-dunkel', `${Math.round(tiefe * 1000) / 10}%`);
  for (const wurzelKnoten of baumBauenAktiv()) {
    const k = filtern(wurzelKnoten, suche);
    if (k) zeilenHtml(k, 0, offeneIds, aus);
  }
  if (aus.length === 0) aus.push(`<div class="pb-zeile leer" style="--t:0"><span class="pb-label">${suche ? 'Nichts gefunden.' : 'Kein Projekt offen.'}</span></div>`);
  baum.innerHTML = aus.join('');
  markieren();
}

/** Hebt Auswahl und aktive Ansicht hervor, ohne den Baum neu aufzubauen. */
function markieren(scrollen = false) {
  if (!baum) return;
  const offeneIds = new Set(fenster.alle().map((o) => o.id));
  baum.querySelectorAll('.pb-zeile').forEach((z) => {
    const id = z.dataset.id;
    z.classList.toggle('gewaehlt', !!id && id === zustand.auswahl && z.dataset.art !== 'familie');
    const fensterArt = z.dataset.art === 'ansicht' || z.dataset.art === 'planblatt';   // Ansichten und Planblätter öffnen sich als Fenster
    z.classList.toggle('ansicht-aktiv', fensterArt && z.dataset.kz === zustand.aktiveAnsicht);
    z.classList.toggle('ansicht-offen', fensterArt && offeneIds.has(id));
    z.classList.toggle('familie-gewaehlt', z.dataset.art === 'familie' && z.dataset.klasse === familie.name);
  });
  if (scrollen) baum.querySelector('.pb-zeile.gewaehlt')?.scrollIntoView({ block: 'nearest' });
}

// ---------------------------------------------------------------------------------------------- Bedienung

function zeileVon(e) { return e.target.closest('.pb-zeile'); }

function umschalten(key) {
  const o = offen();
  if (o.has(key)) o.delete(key); else o.add(key);
  merken.schreiben(offenSchluessel(aktivId), [...o]);   // je Gliederung gemerkt
  zeichnen();
}

function klick(e) {
  const kn = e.target.closest('.pb-knopf');   // Knopf in der Zeile (Modul 2, C2: Familie aus Unterlagen, Entwurf prüfen)
  if (kn) { e.stopPropagation(); ausfuehren(kn.dataset.aktion, kn.dataset.param ? JSON.parse(kn.dataset.param) : kn.dataset.familie ? { familie: kn.dataset.familie } : undefined); return; }
  const z = zeileVon(e);
  if (!z || z.classList.contains('leer')) return;
  const art = z.dataset.art;
  const istAst = art === 'ast' || art === 'element-ast' || art === 'planblatt' || art === 'fam';
  // Pfeil oder reiner Ast klappt auf; Elemente mit Kindern (Gebäude, Geschoss …) wählen aus und klappen beim Pfeil
  // (Der zweite Klick eines Doppelklicks auf einen Ast mit eigener Aktion klappt nicht wieder zu: der Baum würde zwischen den Klicks neu gezeichnet und der Doppelklick ginge verloren.)
  if (art === 'ast' && e.detail > 1 && z.dataset.kontext && !e.target.closest('.pb-pfeil')) return;
  if (art === 'ast' || (istAst && e.target.closest('.pb-pfeil'))) { umschalten(z.dataset.key); return; }
  if (art === 'hinweis') return;
  if (art === 'familie') { auswaehlen(null); familieSetzen(z.dataset.klasse); return; }
  if (art === 'fam-typ') { familieSetzen(null); auswaehlen(element(z.dataset.kz)?.id ?? null); return; }
  if (z.dataset.id) { familieSetzen(null); auswaehlen(z.dataset.id); }
}

function doppelklick(e) {
  const z = zeileVon(e);
  if (!z) return;
  if (z.dataset.art === 'ansicht') ausfuehren('ansicht.oeffnen', { ansicht: z.dataset.kz });
  else if (z.dataset.art === 'planblatt') ausfuehren('plan.oeffnen', { planblatt: z.dataset.kz });   // Doppelklick öffnet das Blatt
  else if (z.dataset.art === 'ansichtsfenster' && z.dataset.ansicht) ausfuehren('ansicht.oeffnen', { ansicht: z.dataset.ansicht });
  else if (z.dataset.art === 'huelle-material') ausfuehren('huelle.material', { material: z.dataset.kz });
  else if (z.dataset.art === 'huelle-aufbau') ausfuehren('huelle.aufbau', { aufbau: z.dataset.kz });
  else if (z.dataset.art === 'element-ast') umschalten(z.dataset.key);
  else if (z.dataset.art === 'fam') ausfuehren('familie.bearbeiten', { familie: z.dataset.kz });                                     // Modul 2: Editor
  else if (z.dataset.art === 'fam-typ') ausfuehren('familie.platzieren-aus-browser', { familie: z.dataset.kz, typ: z.dataset.typ });   // Modul 2: Platzieren
  else if (z.dataset.art === 'bib-fam') ausfuehren('familie.bibliothek-platzieren', bibParam(z));                                          // Modul 2, C3: laden, dann platzieren
  else if (z.dataset.art === 'kollision' || z.dataset.art === 'aussparung') imGrundrissZeigen(z.dataset.kz);                              // G1: wie ein Klick in der Palette Koordination
  else if (z.dataset.art === 'ast' && z.dataset.kontext === 'kollisionen') ausfuehren('koordination.kollisionsliste');
  else if (z.dataset.art === 'ast' && z.dataset.kontext === 'aussparungen') ausfuehren('koordination.aus-liste');
  else if (z.dataset.art === 'ast' && z.dataset.kontext === 'typenliste') ausfuehren('koordination.typenliste');
}

/** Parameter einer Bibliotheksfamilie im Baum: id (Bibliothek) bzw. datei (mitgeliefert). */
const bibParam = (z) => (z.dataset.bibId ? { id: z.dataset.bibId } : { datei: z.dataset.bibDatei });

function eintrag(titel, id, param) {
  const a = aktionen.get(id);
  return { titel, aktiv: a ? istAktiv(a, param) : false, hinweis: a ? hinweisText(a, param) : '', aktion: () => ausfuehren(id, param) };
}

function kontext(e) {
  const z = zeileVon(e);
  if (!z) return;
  const art = z.dataset.art;
  const kontextArt = art === 'ast' ? z.dataset.kontext : undefined;   // Äste mit eigenem Kontextmenü (plaene, familien, liste, kollisionen, aussparungen, typenliste)
  const famAst = kontextArt === 'familien';
  const bibFam = art === 'bib-fam';   // Modul 2, C3
  const listenAst = kontextArt === 'liste';   // Modul 3, C1: Materialliste anlegen
  const plaeneAstZeile = kontextArt === 'plaene';   // Serien-PDF
  const koAst = kontextArt === 'kollisionen' || kontextArt === 'aussparungen' || kontextArt === 'typenliste';   // G1
  const koZeile = art === 'kollision' || art === 'aussparung' || art === 'typ';   // G1
  if (!famAst && !bibFam && !listenAst && !plaeneAstZeile && !koAst && !koZeile && art !== 'fam' && art !== 'fam-typ' && art !== 'ansicht' && art !== 'element' && art !== 'element-ast' && art !== 'planblatt' && art !== 'ansichtsfenster' && art !== 'unterlage' && art !== 'raum-offen' && art !== 'huelle-material' && art !== 'huelle-aufbau') return;
  e.preventDefault();
  const k = z.dataset.kz;
  const eintraege = [];
  if (koAst) {
    if (kontextArt === 'kollisionen') eintraege.push(eintrag('Kollisionsliste', 'koordination.kollisionsliste'), eintrag('Kollisionen prüfen …', 'koordination.pruefen'), eintrag('BCF exportieren …', 'koordination.bcf-export'));
    else if (kontextArt === 'aussparungen') eintraege.push(eintrag('Aussparungsliste', 'koordination.aus-liste'), eintrag('Aussparungen vorschlagen …', 'koordination.aus-vorschlagen'), eintrag('Aussparungen nummerieren …', 'koordination.aus-nummerieren'));
    else eintraege.push(eintrag('Typenliste …', 'koordination.typenliste'));
  } else if (koZeile) {
    if (art === 'typ') {
      eintraege.push({ titel: 'Auswählen', aktion: () => { familieSetzen(null); auswaehlen(z.dataset.id); } }, { trenner: true }, eintrag('Typenliste …', 'koordination.typenliste'));
    } else {
      eintraege.push({ titel: 'Im Grundriss zeigen', aktion: () => imGrundrissZeigen(k) }, { titel: 'In 3D zeigen', aktion: () => in3dZeigen(k) },
        { titel: 'Auswählen', aktion: () => { familieSetzen(null); auswaehlen(z.dataset.id); } }, { trenner: true },
        art === 'kollision' ? eintrag('Kollisionsliste', 'koordination.kollisionsliste') : eintrag('Aussparungsliste', 'koordination.aus-liste'));
    }
  } else if (plaeneAstZeile) {
    eintraege.push(eintrag('Serien-PDF …', 'plan.serienpdf', k ? { plansatz: k } : undefined), { trenner: true }, eintrag('Planblatt anlegen …', 'plan.anlegen'), eintrag('Ansicht platzieren …', 'plan.platzieren'));
  } else if (listenAst) {
    eintraege.push(eintrag('Materialliste anlegen / öffnen', 'ansicht.materialliste'));
  } else if (famAst) {
    eintraege.push(eintrag('Familie aus Unterlagen …', 'familie.aus-unterlagen'), { trenner: true }, eintrag('Neue Familie …', 'familie.neu'), eintrag('Familie laden …', 'familie.laden'),
      { trenner: true }, eintrag('Familien im Explorer …', 'familie.explorer'));
  } else if (bibFam) {
    // Modul 2, C3: Bibliotheksfamilie, die noch nicht im Projekt ist – zuerst laden (familie_laden), dann wie gewohnt platzieren
    const bp = bibParam(z);
    eintraege.push(eintrag('Platzieren (lädt die Familie ins Projekt)', 'familie.bibliothek-platzieren', bp), eintrag('In das Projekt laden', 'familie.bibliothek-laden', bp), { trenner: true },
      eintrag('Familien im Explorer …', 'familie.explorer'), eintrag('Familie laden …', 'familie.laden'));
  } else if (art === 'fam' || art === 'fam-typ') {
    // Familien (Modul 2, C1): im Editor öffnen oder platzieren; neue Familie und Laden stehen überall
    const p_ = { familie: k, typ: z.dataset.typ };
    eintraege.push(eintrag('Bearbeiten', 'familie.bearbeiten', { familie: k }), eintrag('Entwurf prüfen', 'familie.entwurf-pruefen-browser', { familie: k }), eintrag('Platzieren', 'familie.platzieren-aus-browser', p_), { trenner: true },
      eintrag('Familie aus Unterlagen …', 'familie.aus-unterlagen'), eintrag('Aus Unterlagen ersetzen …', 'familie.ersetzen-aus-unterlagen', { familie: k }),
      eintrag('Neue Familie …', 'familie.neu'), eintrag('Familie laden …', 'familie.laden'), eintrag('Aktualisieren …', 'familie.aktualisieren', { familie: k }), eintrag('Familien im Explorer …', 'familie.explorer'));
    if (art === 'fam') eintraege.push({ trenner: true }, { titel: 'Auswählen', aktion: () => { familieSetzen(null); auswaehlen(z.dataset.id); } }, eintrag('Löschen', 'aendern.loeschen', { element: k }));
  } else if (art === 'raum-offen') {
    // Nicht platzierter Raum aus HKAP (Modul 1d): zeichnen lassen oder einem schon gezeichneten Raum zuordnen
    const p = { element: k };
    eintraege.push({ titel: 'Auswählen', aktion: () => { familieSetzen(null); auswaehlen(z.dataset.id); } },
      { trenner: true },
      eintrag('Platzieren …', 'hkap.raum-platzieren', p),
      eintrag('Einem gezeichneten Raum zuordnen …', 'hkap.raum-zuordnen', p));
  } else if (art === 'huelle-aufbau') {
    // Aufbau (Modul 1c): im Editor öffnen oder den gewählten Bauteilen zuweisen
    const p = { element: k, aufbau: k };
    eintraege.push(eintrag('Bearbeiten …', 'huelle.aufbau', p), eintrag('Zuweisen …', 'huelle.zuweisen', p), { trenner: true },
      { titel: 'Auswählen', aktion: () => { familieSetzen(null); auswaehlen(z.dataset.id); } },
      eintrag('Umbenennen', 'aendern.umbenennen', p), eintrag('Löschen', 'aendern.loeschen', p));
  } else if (art === 'huelle-material') {
    const p = { element: k, material: k };
    eintraege.push(eintrag('Bearbeiten …', 'huelle.material', p), { trenner: true },
      { titel: 'Auswählen', aktion: () => { familieSetzen(null); auswaehlen(z.dataset.id); } },
      eintrag('Umbenennen', 'aendern.umbenennen', p), eintrag('Löschen', 'aendern.loeschen', p));
  } else if (art === 'unterlage') {
    const u = element(z.dataset.id);
    const p = { element: k, unterlage: k };
    eintraege.push({ titel: 'Auswählen', aktion: () => { familieSetzen(null); auswaehlen(z.dataset.id); } },
      eintrag(u?.werte?.anzeigen === false ? 'Anzeigen' : 'Ausblenden', 'unterlage.anzeigen', p),
      eintrag('Deckkraft …', 'unterlage.deckkraft', p),
      eintrag('Lagebericht zeigen', 'unterlage.lagebericht', p));
    if (u?.werte?.art !== 'ifc') eintraege.push(eintrag('Kalibrieren', 'einfuegen.kalibrieren', p));
    eintraege.push({ trenner: true }, eintrag('Entfernen', 'aendern.loeschen', p));
  } else if (art === 'planblatt') {
    const p = { planblatt: k, element: k };
    const gw = blattGewerk(plansaetzeDaten(), k);
    eintraege.push(eintrag('Öffnen', 'plan.oeffnen', p), eintrag('PDF öffnen', 'plan.pdf', p), eintrag('Serien-PDF …', 'plan.serienpdf', gw ? { plansatz: 'auto:' + gw.gewerk } : undefined), { trenner: true },
      eintrag('Gewerk …', 'plan.gewerk', p), eintrag('Gewerk bestätigen', 'plan.gewerk-bestaetigen', p), { trenner: true },
      eintrag('Umbenennen', 'aendern.umbenennen', p), eintrag('Löschen', 'aendern.loeschen', p));
  } else if (art === 'ansicht') {
    const p = { ansicht: k, element: k };
    if (z.dataset.vorlage) {
      eintraege.push(
        eintrag('Auf aktive Ansicht anwenden', 'ansicht.vorlage-anwenden', { vorlage: k }),
        eintrag('Umbenennen', 'aendern.umbenennen', p), eintrag('Löschen', 'aendern.loeschen', p));
    } else {
      eintraege.push(
        eintrag('Öffnen', 'ansicht.oeffnen', p), eintrag('Umbenennen', 'aendern.umbenennen', p),
        eintrag('Duplizieren', 'ansicht.duplizieren', p), eintrag('Löschen', 'aendern.loeschen', p),
        { trenner: true },
        eintrag('Als Vorlage speichern', 'ansicht.vorlage-speichern', p), eintrag('Vorlage anwenden …', 'ansicht.vorlage-anwenden', p));
    }
  } else {
    const p = { element: k };
    eintraege.push({ titel: 'Auswählen', aktion: () => { familieSetzen(null); auswaehlen(z.dataset.id); } },
      eintrag('Umbenennen', 'aendern.umbenennen', p), eintrag('Löschen', 'aendern.loeschen', p));
  }
  kontextmenue(e.clientX, e.clientY, eintraege);
}
