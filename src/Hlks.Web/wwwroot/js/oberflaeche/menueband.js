// Menüband wie in Revit: Register (Datei, Architektur, Hülle (Modul 1c), Gebäudetechnik, Einfügen, Beschriften, Ändern, Koordination, Ansicht, XR (26.10.2026), Verwalten) mit Knöpfen in
// beschrifteten Gruppen, Kontextregister «Ändern | <Klasse>» bei Auswahl, Optionsleiste darunter, Schnellzugriff im Kopf.
// Kontextregister «Familie» (Modul 2, C1): erscheint, solange ein Familieneditor die aktive Ansicht ist, und wird dann gewählt.
// Das Menüband löst nur Aktionen aus (aktionen.js); Zustand und Hinweise für gesperrte Knöpfe kommen aus den Aktionen.
// Die Optionsleiste zeigt bei laufendem Zeichenwerkzeug (Modus «werkzeug») dessen Felder (werkzeuge/optionsleiste.js) und bricht bei Platzmangel um.
//
// Das Band passt sich der Fensterbreite an, statt zu rollen (Teil R, entwurf/menueband-revit.md): Jede Gruppe hat die Stufen
// G gross · M mittel · K klein (nur Symbole) · Z zusammengeklappt (Flyout); die Wahl trifft menueband-stufen.js aus den gemessenen
// Breiten (Zwischenspeicher je Register, Design, Schrift). Reicht auch alles auf Z nicht, steht am Ende «» weitere» (Flyout).
// Das Band lässt sich minimieren (voll · nur Gruppentitel · nur Register, Doppelklick auf ein Register oder ⌃); minimiert öffnet ein Klick
// auf ein Register das Band als Überlagerung. Zustand gemerkt.

import { zustand, element, abonnieren, merken } from '../zustand.js';
import { aktionen, ausfuehren, istAktiv, hinweisText, modus, modusSetzen, beiModus, optionen } from './aktionen.js';
import { symbol } from './symbole.js';
import { esc, entprellen, elementText } from './werkzeug.js';
import { HILFE_ZEILE } from './hilfe.js';
import { symbolWz } from '../werkzeuge/symbole.js';
import { werkzeugOptionen } from '../werkzeuge/optionsleiste.js';
import { familieAktiv } from '../familien/aktiv.js';
import { STUFEN, gruppenPlan, stufenWaehlen, minWeiter, minUmschalten, MIN_ZUSTAENDE } from './menueband-stufen.js';

const REGISTER = [['datei', 'Datei'], ['architektur', 'Architektur'], ['huelle', 'Hülle'], ['gebaeudetechnik', 'Gebäudetechnik'], ['einfuegen', 'Einfügen'], ['beschriften', 'Beschriften'], ['aendern', 'Ändern'], ['koordination', 'Koordination'], ['ansicht', 'Ansicht'], ['xr', 'XR'], ['verwalten', 'Verwalten']];
// Reihenfolge der Gruppen je Register (Einfügereihenfolge der Aktionen reicht nicht, weil Werkzeuge nachträglich dazukommen)
const GRUPPENFOLGE = {
  datei: ['Projekt', 'Ausgabe'],   // «Zuletzt geöffnet» hängt gruppenListe() immer hinten an; Ausgabe seit Modul 7 Teil B2 (IFC exportieren)
  architektur: ['Bauteile', 'Decke, Boden, Dach', 'Raum', 'Raster'],
  huelle: ['Aufbauten', 'Materialien', 'Fenster', 'Übergang', 'Prüfen', 'Listen'],
  einfuegen: ['Verknüpfen / Übernehmen', 'Importieren', 'Unterlagen'],
  beschriften: ['Bemassung', 'Text', 'Detail', 'Symbol'],
  aendern: ['Auswahl', 'Verändern', 'Bearbeiten', 'Verlauf', 'Paletten'],
  ansicht: ['Erstellen', 'Ansicht', 'Ansichtsvorlage', 'Grafik', 'Fenster', 'Zoom', 'Darstellung', '3D-Schnitt'],   // 3D-Schnitt seit N13
  koordination: ['Vorgaben', 'Typenliste', 'Kollisionen', 'Aussparungen', 'Koordinationsplan', 'BCF'],   // Modul 3b, C1; Typenliste seit C1c; Koordinationsplan seit Modul 7 Teil B3
  xr: ['Brille', 'Zeigen', 'Koordination', 'Labor'],   // Register XR (26.10.2026): alles zu VR/AR und der Quest App, aktionen-quest.js, aktionen-vr-auftrag.js, problemmarker-panel.js
  verwalten: ['Projekt', 'Zusammenarbeit', 'Modell', 'HKAP', 'Hilfe'],
  familie: ['Parameter', 'Typen', 'Körper', 'Anschlüsse', 'Sinnbild', 'Ansicht', 'Prüfen', 'Unterlagen', 'Bibliothek'],
};
const SCHNELLZUGRIFF = ['start.zeigen', 'aendern.rueckgaengig', 'aendern.wiederholen', 'verwalten.synchronisieren'];

let band, optionsleiste, schnell, reiterLeiste, titelZeile, feld, messung;
let aktivesRegister = 'aendern';
let zuvorRegister = 'aendern';
let minZustand = 'voll';       // 'voll' | 'titel' | 'register' (gemerkt)
let minLetzte = 'register';    // Ziel des Doppelklicks (gemerkt)
let ueberlagerung = false;     // minimiert: Band als Überlagerung offen

export function einrichten(bandElement, optionsleisteElement, schnellzugriffElement) {
  band = bandElement; optionsleiste = optionsleisteElement; schnell = schnellzugriffElement;
  aktivesRegister = merken.lesen('menuebandRegister', 'aendern');
  if (!REGISTER.some(([id]) => id === aktivesRegister)) aktivesRegister = 'aendern';
  zuvorRegister = aktivesRegister;
  minZustand = merken.lesen('menuebandMinimiert', 'voll');
  if (!MIN_ZUSTAENDE.includes(minZustand)) minZustand = 'voll';
  minLetzte = merken.lesen('menuebandMinLetzte', 'register');

  band.innerHTML = '<div class="mb-reiter" role="tablist"></div><div class="mb-titelzeile"></div><div class="mb-feld"></div><div class="mb-messung" aria-hidden="true"></div>';
  reiterLeiste = band.querySelector('.mb-reiter');
  titelZeile = band.querySelector('.mb-titelzeile');
  feld = band.querySelector('.mb-feld');
  messung = band.querySelector('.mb-messung');

  band.addEventListener('click', (e) => {
    const tab = e.target.closest('.mb-tab');
    if (tab) { tabKlick(tab.dataset.register); return; }
    if (e.target.closest('.mb-min')) { minSetzen(minWeiter(minZustand)); return; }
    const z = e.target.closest('.mb-zu, .mb-titel-knopf');
    if (z) { flyoutUmschalten('gruppe', z.dataset.gruppe, z, e.detail === 0); return; }
    const w = e.target.closest('.mb-weitere');
    if (w) { flyoutUmschalten('weitere', '', w, e.detail === 0); return; }
    const k = e.target.closest('.mb-knopf');
    if (k) knopfAusloesen(k);
  });
  band.addEventListener('dblclick', (e) => {
    if (!e.target.closest('.mb-tab')) return;
    const r = minUmschalten(minZustand, minLetzte);
    minLetzte = r.letzte; merken.schreiben('menuebandMinLetzte', minLetzte);
    minSetzen(r.zustand);
  });
  band.addEventListener('pointerenter', zustandAnwenden);
  // Flyout und Überlagerung schliessen: Klick ausserhalb, Esc
  document.addEventListener('pointerdown', (e) => {
    const imFlyout = flyout && (flyout.el.contains(e.target) || flyout.anker.contains(e.target));
    if (flyout && !imFlyout) flyoutSchliessen();
    if (ueberlagerung && !band.contains(e.target)) ueberlagerungSchliessen();
  }, true);
  window.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    if (flyout) { const anker = flyout.anker; flyoutSchliessen(); anker.focus?.(); e.stopPropagation(); e.preventDefault(); }
    else if (ueberlagerung) { ueberlagerungSchliessen(); e.stopPropagation(); e.preventDefault(); }
  }, true);

  schnellzugriffZeichnen();
  schnell.addEventListener('click', (e) => { const k = e.target.closest('.mb-knopf'); if (k) knopfAusloesen(k); });
  schnell.addEventListener('pointerenter', zustandAnwenden);

  reiterZeichnen();
  feldZeichnen();
  minAnwenden();
  optionenZeichnen();

  // Breite des Bands beobachten; neu gezeichnet wird nur, wenn sich dabei eine Stufe ändert
  if (typeof ResizeObserver === 'function') {
    let breite = band.clientWidth;
    new ResizeObserver(() => { if (band.clientWidth !== breite) { breite = band.clientWidth; flyoutSchliessen(); feldZeichnen(false); } }).observe(band);
  }
  // Design oder Schriftgrösse geändert → Messung gilt nicht mehr (der Zwischenspeicher kennt Design und Schrift)
  if (typeof MutationObserver === 'function') new MutationObserver(() => feldZeichnen(false)).observe(document.documentElement, { attributes: true, attributeFilter: ['data-design', 'style'] });

  const spaeter = entprellen(() => { zustandAnwenden(); optionenZeichnen(); }, 40);
  abonnieren('auswahl', (id) => {
    const el = id ? element(id) : null;
    if (el && aktivesRegister === 'aendern') registerWaehlen('kontext', false);
    else if (!el && aktivesRegister === 'kontext') registerWaehlen(zuvorRegister === 'kontext' ? 'aendern' : zuvorRegister, false);
    else { reiterZeichnen(); if (aktivesRegister === 'kontext') feldZeichnen(); }
    if (!el && modus.id === 'netz') modusSetzen('aendern');
    spaeter();
  });
  abonnieren('modell', (d) => {
    if (d.alles && aktivesRegister === 'kontext' && !element(zustand.auswahl)) registerWaehlen('aendern', false);
    spaeter();
  });
  abonnieren('ansicht', () => {
    // Familieneditor aktiv → Kontextregister «Familie» zeigen und wählen; beim Verlassen zurück zum vorigen Register
    const fa = familieAktiv();
    if (fa && aktivesRegister !== 'familie') registerWaehlen('familie', false);
    else if (!fa && aktivesRegister === 'familie') registerWaehlen(zuvorRegister === 'familie' || zuvorRegister === 'kontext' ? 'aendern' : zuvorRegister, false);
    else reiterZeichnen();
    spaeter();
  });
  let zuletztSig = '';
  abonnieren('status', () => {
    const sig = JSON.stringify(zustand.status?.zuletzt ?? []);
    if (sig !== zuletztSig) { zuletztSig = sig; if (aktivesRegister === 'datei') feldZeichnen(); }
    spaeter();
  });
  beiModus(() => { optionenZeichnen(); zustandAnwenden(); });
}

// ---------------------------------------------------------------------------------------------- Register

function kontextVorhanden() { return !!element(zustand.auswahl); }

let reiterSignatur = '';

function reiterZeichnen() {
  const el = element(zustand.auswahl);
  // Gleiche Reiter: nur die Auswahl umschalten und die Knöpfe stehen lassen – ein Doppelklick auf ein Register (Band minimieren) braucht denselben Knopf
  const sig = [familieAktiv() ? 'familie' : '', el ? `kontext:${el.klasse}` : ''].join('|');
  if (sig === reiterSignatur && reiterLeiste.querySelector('.mb-tab')) {
    for (const b of reiterLeiste.querySelectorAll('.mb-tab')) { const a = b.dataset.register === aktivesRegister; b.classList.toggle('aktiv', a); b.setAttribute('aria-selected', String(a)); }
    return;
  }
  reiterSignatur = sig;
  let h = REGISTER.map(([id, name]) => `<button type="button" role="tab" class="mb-tab${id === 'datei' ? ' datei' : ''}${id === aktivesRegister ? ' aktiv' : ''}" data-register="${id}" aria-selected="${id === aktivesRegister}">${esc(name)}</button>`).join('');
  if (familieAktiv()) h += `<button type="button" role="tab" class="mb-tab kontext${aktivesRegister === 'familie' ? ' aktiv' : ''}" data-register="familie">Familie</button>`;
  if (el) h += `<button type="button" role="tab" class="mb-tab kontext${aktivesRegister === 'kontext' ? ' aktiv' : ''}" data-register="kontext">Ändern | ${esc(el.klasse)}</button>`;
  h += `<button type="button" class="mb-min" aria-label="Menüband minimieren"><svg class="sym" viewBox="0 0 12 12" width="12" height="12" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><path d="M2.5 7.5L6 4l3.5 3.5"/></svg></button>`;
  reiterLeiste.innerHTML = h;
  minAnwenden();
}

function registerWaehlen(id, merkenJa = true) {
  if (id === 'kontext' && !kontextVorhanden()) id = 'aendern';
  if (id === 'familie' && !familieAktiv()) id = zuvorRegister === 'familie' || zuvorRegister === 'kontext' ? 'aendern' : zuvorRegister;
  if (aktivesRegister !== 'kontext' && aktivesRegister !== 'familie' && (id === 'kontext' || id === 'familie')) zuvorRegister = aktivesRegister;
  aktivesRegister = id;
  if (merkenJa && id !== 'kontext' && id !== 'familie') { merken.schreiben('menuebandRegister', id); zuvorRegister = id; }
  reiterZeichnen();
  feldZeichnen();
}

/** Klick auf ein Register: wählt es; minimiert öffnet (bzw. schliesst bei erneutem Klick) die Überlagerung. */
function tabKlick(id) {
  if (minZustand === 'voll') { registerWaehlen(id); return; }
  if (ueberlagerung && id === aktivesRegister) { ueberlagerungSchliessen(); return; }
  flyoutSchliessen();
  registerWaehlen(id);
  ueberlagerung = true;
  minAnwenden();
}

// ---------------------------------------------------------------------------------------------- Minimieren

function minSetzen(z) {
  if (!MIN_ZUSTAENDE.includes(z)) z = 'voll';
  flyoutSchliessen();
  ueberlagerung = false;
  minZustand = z;
  if (z !== 'voll') { minLetzte = z; merken.schreiben('menuebandMinLetzte', z); }
  merken.schreiben('menuebandMinimiert', z);
  minAnwenden();
  feldZeichnen(false);
  // Die Ansichten darunter bekommen mehr bzw. weniger Platz (sie beobachten ihre Grösse; das Ereignis deckt den Rest ab)
  if (typeof requestAnimationFrame === 'function') requestAnimationFrame(() => window.dispatchEvent(new Event('resize')));
}

function ueberlagerungSchliessen() {
  if (!ueberlagerung) return;
  flyoutSchliessen();
  ueberlagerung = false;
  minAnwenden();
}

const MIN_TEXT = { voll: 'Menüband minimieren (nur Gruppentitel)', titel: 'Menüband weiter minimieren (nur Register)', register: 'Menüband wieder ganz zeigen' };

function minAnwenden() {
  if (!band) return;
  band.classList.toggle('min-titel', minZustand === 'titel');
  band.classList.toggle('min-register', minZustand === 'register');
  band.classList.toggle('ueberlagerung', ueberlagerung && minZustand !== 'voll');
  document.body.dataset.menueband = minZustand;
  const m = reiterLeiste?.querySelector('.mb-min');
  if (m) {
    m.title = MIN_TEXT[minZustand];
    m.setAttribute('aria-label', MIN_TEXT[minZustand]);
    m.setAttribute('aria-expanded', String(minZustand === 'voll'));
  }
}

// ---------------------------------------------------------------------------------------------- Knöpfe und Gruppen

const ohneWeich = (t) => String(t ?? '').replace(/[\u200b\u00ad]/g, '');

/** Knopf einer Aktion (oder eines Eintrags der Liste «Zuletzt») in Stil gross | mittel | klein. */
function knopfHtml(a, stil) {
  if (a.nurSymbol && stil === 'mittel') stil = 'klein';   // nurSymbol: auch in den Stufen G und M nur das 16-px-Symbol (Text im Tooltip), spart Breite im Band (N3)
  const px = stil === 'gross' ? 32 : 16;
  const sym = symbolWz(a.symbol, px) ?? symbol(a.symbol, px);
  const ordner = a.ordner ? ` data-ordner="${esc(a.ordner)}" title="${esc(a.tooltip ?? a.ordner)}"` : '';
  const text = stil === 'klein' ? '' : `<span class="mb-text">${esc(a.titel)}</span>`;
  return `<button type="button" class="mb-knopf ${stil}${a.breit ? ' breit' : ''}${a.immerGross ? ' immer' : ''}" data-id="${esc(a.id)}"${ordner} aria-label="${esc(ohneWeich(a.beschriftung ?? a.titel))}"><span class="mb-sym">${sym}</span>${text}</button>`;
}

/** Gruppen eines Registers: [{ name, items: [Aktion …], symbol }] in der Reihenfolge des Registers, bei «Datei» mit «Zuletzt geöffnet». */
function gruppenListe(registerId) {
  const m = new Map();
  for (const a of aktionen.values()) {
    if (a.register !== registerId) continue;
    if (!m.has(a.gruppe)) m.set(a.gruppe, []);
    m.get(a.gruppe).push(a);
  }
  const folge = GRUPPENFOLGE[registerId];
  let eintraege = [...m];
  if (folge) {
    const rang = (n) => { const i = folge.indexOf(n); return i < 0 ? folge.length : i; };
    eintraege = eintraege.sort((x, y) => rang(x[0]) - rang(y[0]));
  }
  const gruppen = eintraege.map(([name, items]) => ({ name, items, symbol: gruppenSymbol(items) }));
  if (registerId === 'datei') gruppen.push(zuletztGruppe());
  return gruppen;
}

/** Symbol der zusammengeklappten Gruppe: das der ersten «immerGross»-Aktion, sonst der ersten grossen, sonst der ersten. */
function gruppenSymbol(items) {
  const a = items.find((x) => x.immerGross) ?? items.find((x) => x.groesse !== 'klein') ?? items[0];
  return a?.symbol ?? 'familie';
}

function gruppeHtml(g, stufe) {
  if (stufe === 'Z') return zuGruppeHtml(g);
  if (g.leer) return `<div class="mb-gruppe" data-gruppe="${esc(g.name)}"><div class="mb-knoepfe"><span class="mb-leer">${esc(g.leer)}</span></div><div class="mb-gruppenname">${esc(g.name)}</div></div>`;
  let inhalt = '';
  for (const s of gruppenPlan(g.items, stufe)) {
    if (s.art === 'gross') inhalt += knopfHtml(s.item, 'gross');
    else inhalt += `<div class="mb-spalte${s.stil === 'mittel' && s.items.some((x) => x.breit) ? ' breit' : ''}">${s.items.map((x) => knopfHtml(x, s.stil)).join('')}</div>`;
  }
  return `<div class="mb-gruppe" data-gruppe="${esc(g.name)}"><div class="mb-knoepfe">${inhalt}</div><div class="mb-gruppenname">${esc(g.name)}</div></div>`;
}

/** Stufe Z: ein Knopf mit Symbol, Name und ▾; ein Klick öffnet die Gruppe als Flyout. */
function zuGruppeHtml(g) {
  const sym = symbolWz(g.symbol, 32) ?? symbol(g.symbol, 32);
  return `<div class="mb-gruppe zu" data-gruppe="${esc(g.name)}"><button type="button" class="mb-zu" data-gruppe="${esc(g.name)}" aria-haspopup="true" aria-expanded="false" title="${esc(g.name)}"><span class="mb-sym">${sym}</span><span class="mb-text">${esc(g.name)}</span><span class="mb-pfeil" aria-hidden="true">▾</span></button></div>`;
}

const WEITERE_HTML = '<div class="mb-gruppe weitere-zelle"><button type="button" class="mb-weitere" aria-haspopup="true" aria-expanded="false" title="Weitere Gruppen">» weitere</button></div>';

function zuletztGruppe() {
  const z = (zustand.status?.zuletzt ?? []).slice(0, 3);
  // Gleiche Namen unterscheidet der Projektordner (drittletzter Teil des Pfads: …\<Projekt>\c_Pläne und Modelle\e_KICAD)
  const projektordner = (o) => String(o).split(/[\\/]/).filter(Boolean).slice(-3, -2)[0] ?? '';
  const items = z.map((x) => {
    const zusatz = projektordner(x.ordner) ? ` · ${projektordner(x.ordner)}` : '';
    return { id: 'datei.zuletzt-oeffnen', titel: `${x.name}${zusatz}`, symbol: 'zuletzt', groesse: 'klein', breit: true, ordner: x.ordner, tooltip: `${x.name}${zusatz}\n${x.ordner}` };
  });
  return { name: 'Zuletzt geöffnet', items, symbol: 'zuletzt', leer: items.length === 0 ? 'noch keine' : null };
}

// ---------------------------------------------------------------------------------------------- Messung und Stufenwahl

const breitenSpeicher = new Map();   // Schlüssel (Register, Design, Schrift, Gruppeninhalt, Stufe) → Breite in px

function messKontext() {
  const cs = getComputedStyle(band);
  return `${aktivesRegister}|${document.documentElement.dataset.design ?? 'hell'}|${cs.fontFamily}|${cs.fontSize}`;
}

function gruppenSignatur(g) {
  return `${g.name}\u0001${g.symbol}\u0001${g.items.map((i) => [i.id, i.titel, i.symbol, i.groesse, i.immerGross ? 1 : 0, i.nurSymbol ? 1 : 0, i.ordner ?? ''].join('|')).join('\u0002')}`;
}

/** Breiten aller Gruppen in allen Stufen; Fehlendes wird in einem Rutsch unsichtbar gemessen (eine Layoutberechnung). */
function breitenHolen(gruppen) {
  const kontext = messKontext();
  const schluessel = (g, s) => `${kontext}|${g.signatur}|${s}`;
  const fehlend = [];
  for (const g of gruppen) {
    g.signatur = gruppenSignatur(g);
    for (const s of STUFEN) if (!breitenSpeicher.has(schluessel(g, s))) fehlend.push([g, s]);
  }
  const weitereSchluessel = `${kontext}|weitere`;
  const weitereFehlt = !breitenSpeicher.has(weitereSchluessel);
  if (fehlend.length > 0 || weitereFehlt) {
    messung.innerHTML = fehlend.map(([g, s]) => gruppeHtml(g, s)).join('') + (weitereFehlt ? WEITERE_HTML : '');
    const kinder = messung.children;
    fehlend.forEach(([g, s], i) => breitenSpeicher.set(schluessel(g, s), Math.ceil(kinder[i].getBoundingClientRect().width)));
    if (weitereFehlt) breitenSpeicher.set(weitereSchluessel, Math.ceil(kinder[fehlend.length].getBoundingClientRect().width));
    messung.innerHTML = '';
  }
  return {
    gruppen: gruppen.map((g) => ({ breiten: Object.fromEntries(STUFEN.map((s) => [s, breitenSpeicher.get(schluessel(g, s))])) })),
    weitereBreite: breitenSpeicher.get(weitereSchluessel),
  };
}

let aktuelleGruppen = [];
let aktuelleWahl = { stufen: [], weitere: 0 };
let feldSignatur = '';

/** Zeichnet das Feld des aktiven Registers. Mit erzwingen=false nur, wenn sich dabei Inhalt oder eine Stufe ändert (kein Flackern). */
function feldZeichnen(erzwingen = true) {
  if (!feld) return;
  const gruppen = gruppenListe(aktivesRegister);
  const { gruppen: breiten, weitereBreite } = breitenHolen(gruppen);
  const wahl = stufenWaehlen({ gruppen: breiten, verfuegbar: band.clientWidth, weitereBreite });
  const sig = `${aktivesRegister}|${gruppen.map((g) => g.signatur).join('\u0003')}|${wahl.stufen.join('')}`;
  aktuelleGruppen = gruppen;
  aktuelleWahl = wahl;
  if (!erzwingen && sig === feldSignatur) return;
  feldSignatur = sig;
  flyoutSchliessen();
  feld.innerHTML = gruppen.map((g, i) => (wahl.stufen[i] === 'W' ? '' : gruppeHtml(g, wahl.stufen[i]))).join('') + (wahl.weitere > 0 ? WEITERE_HTML : '');
  titelZeichnen();
  zustandAnwenden();
}

/** Zeile der Gruppentitel (Zustand «nur Gruppentitel»): jeder Titel öffnet seine Gruppe als Flyout. */
function titelZeichnen() {
  titelZeile.innerHTML = aktuelleGruppen.map((g) => `<button type="button" class="mb-titel-knopf" data-gruppe="${esc(g.name)}" aria-haspopup="true" aria-expanded="false">${esc(g.name)} <span aria-hidden="true">▾</span></button>`).join('');
}

/**
 * Hilfe (F1): Titel von Register und Knopf, wie angezeigt, für einen Knopf des Menübands (auch im Flyout, im Kontextregister und im Schnellzugriff).
 * Ergebnis { register, knopf } oder null (kein Knopf einer Aktion, z. B. ein Eintrag der Liste «Zuletzt geöffnet»).
 */
export function knopfHilfe(k) {
  if (!k || k.dataset.ordner) return null;
  const a = aktionen.get(k.dataset.id);
  if (!a) return null;
  // Im Band gehört der Knopf zum gewählten Register (dessen Reiter trägt den angezeigten Namen, auch «Ändern | Klasse»); im Schnellzugriff zu dem der Aktion
  const reiter = k.closest('#menueband') ? reiterLeiste?.querySelector('.mb-tab.aktiv') : null;
  const register = reiter ? reiter.textContent : (REGISTER.find(([id]) => id === a.register)?.[1] ?? '');
  return { register: ohneWeich(register).trim(), knopf: ohneWeich(a.titel).trim() };
}

/** Für Tests und Bilder: aktuelle Stufen der Gruppen und Zustand des Bands. */
export function bandInfo() {
  return {
    register: aktivesRegister, min: minZustand, ueberlagerung, flyout: flyout ? { art: flyout.art, name: flyout.name } : null,
    weitere: aktuelleWahl.weitere,
    gruppen: aktuelleGruppen.map((g, i) => ({ name: g.name, stufe: aktuelleWahl.stufen[i] })),
  };
}

// ---------------------------------------------------------------------------------------------- Flyout

let flyout = null;   // { el, anker, art, name }

function flyoutUmschalten(art, name, anker, perTastatur) {
  if (flyout && flyout.anker === anker) { flyoutSchliessen(); return; }
  flyoutOeffnen(art, name, anker, perTastatur);
}

function flyoutOeffnen(art, name, anker, perTastatur = false) {
  flyoutSchliessen();
  const gruppen = art === 'weitere' ? aktuelleGruppen.filter((_, i) => aktuelleWahl.stufen[i] === 'W') : aktuelleGruppen.filter((g) => g.name === name);
  if (gruppen.length === 0) return;
  const el = document.createElement('div');
  el.className = `mb-flyout${art === 'weitere' ? ' weitere' : ''}`;
  el.setAttribute('role', 'group');
  el.setAttribute('aria-label', art === 'weitere' ? 'Weitere Gruppen' : name);
  el.dataset.art = art;
  if (name) el.dataset.gruppe = name;
  el.innerHTML = gruppen.map((g) => gruppeHtml(g, 'G')).join('');
  band.appendChild(el);
  const b = band.getBoundingClientRect(), a = anker.getBoundingClientRect();
  const untenFeld = anker.closest('.mb-feld') ? feld.getBoundingClientRect().bottom : a.bottom;
  el.style.maxWidth = `${Math.max(120, b.width)}px`;
  el.style.top = `${Math.round(untenFeld - b.top)}px`;
  el.style.left = `${Math.max(0, Math.min(Math.round(a.left - b.left), Math.round(b.width - el.offsetWidth)))}px`;
  anker.setAttribute('aria-expanded', 'true');
  anker.classList.add('offen');
  flyout = { el, anker, art, name };
  zustandAnwenden();
  if (perTastatur) el.querySelector('.mb-knopf:not(.gesperrt)')?.focus();
}

function flyoutSchliessen() {
  if (!flyout) return;
  flyout.el.remove();
  flyout.anker.setAttribute?.('aria-expanded', 'false');
  flyout.anker.classList?.remove('offen');
  flyout = null;
}

async function knopfAusloesen(k) {
  const id = k.dataset.id;
  const param = k.dataset.ordner ? { ordner: k.dataset.ordner } : undefined;
  const a = aktionen.get(id);
  // Ausgeführt wird, was verfügbar ist: dann schliessen Flyout und Überlagerung. Gesperrtes zeigt nur seinen Hinweis.
  if (a && (k.dataset.ordner || istAktiv(a))) { flyoutSchliessen(); ueberlagerungSchliessen(); }
  await ausfuehren(id, param);
  zustandAnwenden();
}

/** Setzt gesperrt, gedrückt und Tooltip aller Knöpfe nach dem aktuellen Zustand (auch im Flyout und bei Knöpfen ohne Text). */
function zustandAnwenden() {
  for (const k of document.querySelectorAll('#menueband .mb-knopf, #schnellzugriff .mb-knopf')) {
    const a = aktionen.get(k.dataset.id);
    if (!a) continue;
    const aktiv = k.dataset.ordner ? true : istAktiv(a);
    const kz = [a.kuerzel, ...a.kuerzelAlt].filter(Boolean).join(' / ');
    let tip = k.dataset.ordner ? k.title : ohneWeich(a.titel) + (kz ? ` (${kz})` : '');
    if (!aktiv) { const h = hinweisText(a); if (h) tip += '\n' + h; }
    if (!k.dataset.ordner) tip += '\n' + HILFE_ZEILE;   // letzte Zeile: F1 öffnet die Anleitung zu diesem Befehl (hilfe-oberflaeche.js)
    k.title = tip;
    k.classList.toggle('gesperrt', !aktiv);
    k.setAttribute('aria-disabled', String(!aktiv));
    let gedr = false;
    try { gedr = !!a.gedrueckt?.(); } catch { /* egal */ }
    k.classList.toggle('gedrueckt', gedr);
    if (a.gedrueckt) k.setAttribute('aria-pressed', String(gedr));
  }
}

function schnellzugriffZeichnen() {
  schnell.innerHTML = SCHNELLZUGRIFF.map((id) => {
    const a = aktionen.get(id);
    return a ? `<button type="button" class="mb-knopf sz" data-id="${esc(a.id)}"><span class="mb-sym">${symbol(a.symbol, 16)}</span></button>` : '';
  }).join('');
}

// ---------------------------------------------------------------------------------------------- Optionsleiste

function medien() { return zustand.katalog?.medien ?? []; }

let letzteOptionen = '';
/** Schreibt die Optionsleiste nur neu, wenn sich der Inhalt geändert hat (Fokus in den Feldern bleibt sonst erhalten). */
function optionenSetzen(html, binden) {
  if (html === letzteOptionen) return;
  letzteOptionen = html;
  optionsleiste.innerHTML = html;
  binden?.();
}

function optionenZeichnen() {
  if (!optionsleiste) return;
  const el = element(zustand.auswahl);
  if (modus.id === 'loeschen') {
    optionenSetzen(`<span class="ol-titel">Löschen</span><span>${esc(modus.daten.kennzeichen ?? '')}</span><span class="ol-leise">Die Folgen werden zuerst als Vorschau gezeigt (Trockenlauf); gelöscht wird erst nach Bestätigung.</span>`);
    return;
  }
  if (modus.id === 'werkzeug') {
    // Während der Eingabe in einem Feld nicht neu schreiben (Fokus bliebe sonst nicht erhalten)
    if (document.activeElement?.matches?.('#optionsleiste input, #optionsleiste select')) return;
    const o = werkzeugOptionen(modus.daten.werkzeug);
    optionenSetzen(o.html, () => o.binden(optionsleiste));
    return;
  }
  if (modus.id === 'netz') {
    const von = el?.kennzeichen ?? modus.daten.von ?? '';
    optionenSetzen(`<span class="ol-titel">Netz folgen</span><span>${esc(von)}</span>
      <label>Richtung <select class="ol-richtung"><option value="stromab"${optionen.netzRichtung === 'stromab' ? ' selected' : ''}>stromab</option><option value="stromauf"${optionen.netzRichtung === 'stromauf' ? ' selected' : ''}>stromauf</option></select></label>
      <label>Medium <select class="ol-medium"><option value="">alle</option>${medien().map((m) => `<option value="${esc(m.kennung)}"${optionen.netzMedium === m.kennung ? ' selected' : ''}>${esc(m.kennung)} – ${esc(m.name)}</option>`).join('')}</select></label>
      <button type="button" class="ol-knopf">Folgen</button>
      ${modus.daten.anzahl !== undefined ? `<span class="ol-leise">${esc(modus.daten.anzahl)} Elemente gefunden</span>` : ''}
      <span class="ol-leise">Esc beendet.</span>`, () => {
      optionsleiste.querySelector('.ol-richtung').onchange = (e) => { optionen.netzRichtung = e.target.value; };
      optionsleiste.querySelector('.ol-medium').onchange = (e) => { optionen.netzMedium = e.target.value; };
      optionsleiste.querySelector('.ol-knopf').onclick = () => ausfuehren('kontext.netz-optionen');
    });
    return;
  }
  if (familieAktiv() && !el) {
    const f = element(zustand.aktiveAnsicht);
    optionenSetzen(`<span class="ol-titel">Familieneditor</span><span>${esc(f?.kennzeichen ?? '')} ${esc(f?.name ?? '')}</span><span class="ol-leise">Parameter, Typen, Körper, Anschlüsse und Sinnbild links bearbeiten, rechts die Familie allein. Strg+Z macht eine Änderung rückgängig.</span>`);
    return;
  }
  if (el) optionenSetzen(`<span class="ol-titel">Ändern</span><span>${esc(elementText(el))}</span><span class="ol-leise">Entf löscht, Esc hebt die Auswahl auf.</span>`);
  else optionenSetzen('<span class="ol-titel">Ändern</span><span class="ol-leise">Ein Element in der Ansicht oder im Projektbrowser anklicken. Kürzel: zwei Buchstaben tippen (Liste: KS).</span>');
}
