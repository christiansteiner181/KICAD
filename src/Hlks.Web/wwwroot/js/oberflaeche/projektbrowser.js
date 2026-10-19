// Projektbrowser (links unten): Baum mit Auswahlfeld der Gliederung und Suchfeld. G1 (entwurf/projektbrowser-gliederung.md): die Gliederung ist umschaltbar – Gebäude (wie bisher),
// Sanitär, Heizung, Lüftung, Koordination, Energie und Umwelt; jeder Benutzer stellt seine primäre Gliederung ein (gilt überall, auf allen seinen Rechnern: /api/benutzer/einstellungen).
// G2: das Auswahlfeld ist eine Liste mit Häkchen – mehrere Fachgliederungen stehen als Blöcke untereinander (Kopf in der Gewerkfarbe), «Gebäude» schliesst die anderen aus, mindestens eine bleibt gewählt;
// die Einstellung ist eine Liste (ein alter Einzelwert gilt als Liste mit einem Eintrag). Regeln und Blockbildung: projektbrowser-gliederung.js bzw. baumBauenListe in projektbrowser-aeste.js.
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
  GLIEDERUNGEN, GLIEDERUNG_VORGABE, GLIEDERUNG_MEHRERE, EINSTELLUNG_GLIEDERUNG, gliederung, gliederungenNormieren, gliederungUmschalten, gliederungAnzeige, gleicheListe,
  offenKennung, offenSchluessel, offenVorgabe, gewerkFarben, filtern,
} from './projektbrowser-gliederung.js';
import { baumBauenListe } from './projektbrowser-aeste.js';
import { anmelden } from './paletten.js';

let wurzel, baum, suchfeld, knopf, liste;
let suche = '';
let aktivIds = [GLIEDERUNG_VORGABE];   // Kennungen der gewählten Gliederungen in Listenreihenfolge (G2: mehrere zugleich; Vorgabe «Gebäude», auch für Tests ohne Einstellung)
let vomBenutzerGewaehlt = false;       // der Benutzer hat in dieser Sitzung selbst gewählt: eine späte Antwort des Servers überstimmt ihn nicht
const offenJe = new Map();             // Kennung → Menge der aufgeklappten Äste (je Gliederung gemerkt, bei mehreren Gliederungen unter «mehrere»; Browser-Speicher)
const gesehen = new Set();             // Gruppen, die einmal anfangs offen waren (danach entscheidet der Benutzer)
let gesehenMehrere = null;             // dasselbe für mehrere Gliederungen zugleich, aber gespeichert: ein neuer Block (z. B. Lüftung dazu) ist anfangs offen, ein zugeklappter bleibt zu

const offenFuer = (kennung) => {
  if (!offenJe.has(kennung)) offenJe.set(kennung, new Set(merken.lesen(offenSchluessel(kennung), kennung === GLIEDERUNG_MEHRERE ? [] : offenVorgabe(gliederung(kennung)))));
  return offenJe.get(kennung);
};
const offen = () => offenFuer(offenKennung(aktivIds));

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

  // Benutzereinstellung: die primären Gliederungen des Benutzers (G2: eine Liste; ein alter Einzelwert gilt als Liste mit einem Eintrag) – beim Start und nach jedem Laden eines Projekts vom Server gelesen
  const uebernehmen = async () => {
    await benutzerLaden();
    if (vomBenutzerGewaehlt) return;
    const neu = gliederungenNormieren(benutzerWert(EINSTELLUNG_GLIEDERUNG, GLIEDERUNG_VORGABE));
    if (!gleicheListe(neu, aktivIds)) setzen(neu);
  };
  uebernehmen();
  abonnieren('modell', (d) => { if (d?.alles && zustand.status?.offen) uebernehmen(); });
  setzen(aktivIds);
}

// ---------------------------------------------------------------------------------------------- Gliederung wählen

/** Macht die Gliederungen zu den aktiven (Zeichnen, Mitführen der Koordination, Knopf, Häkchen der Liste), ohne sie zu speichern. */
function setzen(ids) {
  aktivIds = gliederungenNormieren(ids);
  koordinationAktiv(aktivIds.includes('koordination'));
  knopfZeichnen();
  listeAktualisieren();
  zeichnen();
}

/** Vom Benutzer ein Häkchen gesetzt oder entfernt: «Gebäude» ersetzt alles, eine Fachgliederung hebt «Gebäude» auf, mindestens eine bleibt. Die Wahl wird als Liste beim Server
 *  gemerkt (gilt in allen Projekten und auf allen seinen Rechnern). */
async function waehlen(id) {
  const ids = gliederungUmschalten(aktivIds, id);
  if (gleicheListe(ids, aktivIds)) return;
  vomBenutzerGewaehlt = true;
  setzen(ids);
  try { await benutzerSetzen(EINSTELLUNG_GLIEDERUNG, [...ids]); }
  catch (e) { meldung(`Die Gliederung konnte nicht gespeichert werden: ${e.message ?? e}`, 'warnung', { vorschlag: e.vorschlag }); }
}

/** CSS-Eigenschaften der Hinterlegung eines Gewerks (Pastellton und Koordinationsfarbe aus dem Katalog); leer, wenn das Gewerk keine Farbe hat. */
function gewerkStil(id) {
  const f = id ? gewerkFarben({ gewerke: zustand.katalog?.gewerke }, id) : null;
  return f ? `--gw-pastell:${f.pastell};--gw-kord:${f.koordination}` : '';
}

/** Streifen links am Auswahlfeld bei mehreren Gliederungen: ein Abschnitt je Gewerk in dessen Koordinationsfarbe (aus dem Katalog), von oben nach unten in Listenreihenfolge. */
function streifenStil(defs) {
  const farben = defs.map((g) => (g.gewerk ? gewerkFarben({ gewerke: zustand.katalog?.gewerke }, g.gewerk)?.koordination : null)).filter(Boolean);
  if (farben.length === 0) return '';
  const n = farben.length;
  return `--gw-streifen:linear-gradient(to bottom,${farben.map((f, i) => `${f} ${Math.round((100 * i) / n)}% ${Math.round((100 * (i + 1)) / n)}%`).join(',')})`;
}

let knopfSig = '';
function knopfZeichnen() {
  const defs = aktivIds.map((id) => gliederung(id));
  const einzeln = defs.length === 1;
  const stil = einzeln && defs[0].gewerk ? gewerkStil(defs[0].gewerk) : einzeln ? '' : streifenStil(defs);
  const sig = aktivIds.join('+') + '|' + stil;
  if (knopfSig === sig) return;   // unverändert: nicht neu schreiben (der Knopf behält den Fokus)
  knopfSig = sig;
  knopf.dataset.gliederung = aktivIds.join('+');   // eine Gliederung: ihre Kennung; mehrere: «sanitaer+heizung»
  knopf.title = einzeln ? 'Gliederung des Projektbrowsers' : `Gliederungen des Projektbrowsers: ${defs.map((g) => g.titel).join(', ')}`;
  knopf.innerHTML = `<span class="pb-sym">${symbol(defs[0].sym, 16)}</span><span class="pb-gl-name">${esc(gliederungAnzeige(aktivIds))}</span><span class="pb-pfeil">▾</span>`;
  knopf.classList.toggle('pb-gewerk', einzeln && !!stil);
  knopf.classList.toggle('pb-mehrere', !einzeln);
  knopf.style.cssText = stil;
}

function listeOeffnen(fokus = false) {
  listeSchliessen();
  const r = knopf.getBoundingClientRect();
  liste = document.createElement('div');
  liste.className = 'pb-gl-liste';
  liste.setAttribute('role', 'listbox');
  liste.setAttribute('aria-multiselectable', 'true');
  liste.setAttribute('aria-label', 'Gliederungen des Projektbrowsers');
  liste.style.cssText = `left:${Math.round(r.left)}px;top:${Math.round(r.bottom)}px;min-width:${Math.round(r.width)}px`;
  liste.innerHTML = GLIEDERUNGEN.map((g) => {
    const stil = g.gewerk ? gewerkStil(g.gewerk) : '';
    return `<div class="pb-gl-eintrag${stil ? ' pb-gewerk' : ''}" role="option" tabindex="-1" data-id="${esc(g.id)}"${stil ? ` style="${esc(stil)}"` : ''}>`
      + `<span class="pb-gl-haken" aria-hidden="true"></span><span class="pb-sym">${symbol(g.sym, 16)}</span><span class="pb-gl-name">${esc(g.titel)}</span></div>`;
  }).join('');
  document.body.appendChild(liste);
  knopf.setAttribute('aria-expanded', 'true');
  listeAktualisieren();
  // Die Liste bleibt offen: ein Klick setzt oder entfernt ein Häkchen (so lassen sich mehrere Gliederungen nacheinander wählen); zu geht sie mit Escape oder einem Klick daneben.
  liste.addEventListener('click', (e) => { const z = e.target.closest('.pb-gl-eintrag'); if (z) { z.focus(); waehlen(z.dataset.id); } });
  liste.addEventListener('keydown', listeTaste);
  setTimeout(() => document.addEventListener('pointerdown', ausserhalb, true), 0);
  if (fokus) (liste.querySelector('.pb-gl-eintrag.aktiv') ?? liste.querySelector('.pb-gl-eintrag')).focus();
}

/** Häkchen und Auszeichnung der geöffneten Liste an die gewählten Gliederungen anpassen (ohne die Liste neu zu bauen: Fokus und Maus bleiben). */
function listeAktualisieren() {
  if (!liste) return;
  for (const z of liste.querySelectorAll('.pb-gl-eintrag')) {
    const an = aktivIds.includes(z.dataset.id);
    z.classList.toggle('aktiv', an);
    z.setAttribute('aria-selected', String(an));
    z.setAttribute('aria-checked', String(an));
  }
}

function listeTaste(e) {
  const eintraege = [...liste.querySelectorAll('.pb-gl-eintrag')];
  const i = eintraege.indexOf(document.activeElement);
  if (e.key === 'Escape') { listeSchliessen(); knopf.focus(); e.stopPropagation(); return; }
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); eintraege[(i + (e.key === 'ArrowDown' ? 1 : -1) + eintraege.length) % eintraege.length].focus(); return; }
  if (e.key === 'Home' || e.key === 'End') { e.preventDefault(); eintraege[e.key === 'Home' ? 0 : eintraege.length - 1].focus(); return; }
  if ((e.key === 'Enter' || e.key === ' ') && i >= 0) { e.preventDefault(); waehlen(eintraege[i].dataset.id); return; }
  if (e.key === 'Tab') listeSchliessen();
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
    if (!liste) listeOeffnen(true);   // Pfeiltaste öffnet die Liste und springt zur gewählten Gliederung
    else (liste.querySelector('.pb-gl-eintrag.aktiv') ?? liste.querySelector('.pb-gl-eintrag')).focus();
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
  const k = baumBauenListe(aktivIds.map((id) => gliederung(id)), datenBauen());
  const kennung = offenKennung(aktivIds);
  if (kennung === GLIEDERUNG_MEHRERE) {
    // mehrere Gliederungen: Blöcke und ihre Äste sind anfangs offen; was der Benutzer zugeklappt hat, bleibt zu (beides gespeichert)
    gesehenMehrere ??= new Set(merken.lesen('browserGesehen.mehrere', []));
    let neu = false;
    const lauf = (n) => { if (n.anfangsOffen && !gesehenMehrere.has(n.key)) { gesehenMehrere.add(n.key); offen().add(n.key); neu = true; } (n.kinder ?? []).forEach(lauf); };
    k.forEach(lauf);
    if (neu) { merken.schreiben('browserGesehen.mehrere', [...gesehenMehrere]); merken.schreiben(offenSchluessel(kennung), [...offen()]); }
    return k;
  }
  const lauf = (n) => { if (n.anfangsOffen && !gesehen.has(kennung + '|' + n.key)) { gesehen.add(kennung + '|' + n.key); offen().add(n.key); } (n.kinder ?? []).forEach(lauf); };
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
  merken.schreiben(offenSchluessel(offenKennung(aktivIds)), [...o]);   // je Gliederung gemerkt
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

// Als Palette der Palettenverwaltung (W1): Vorgabe links unter den Eigenschaften; dort wird einrichten() mit dem Inhaltsknoten aufgerufen.
anmelden({ id: 'projektbrowser', titel: 'Projektbrowser', ort: 'links', reihenfolge: 20, elementId: 'palette-browser', inhaltId: 'projektbrowser', symbol: 'projektbrowser', bauen: einrichten });
