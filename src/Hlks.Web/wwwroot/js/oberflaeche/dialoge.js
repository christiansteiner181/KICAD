// Dialoge, Meldungen und Kontextmenüs der Oberfläche.
//   zeigen({ titel, inhalt, knoepfe, breite, schliessbar, klasse }) → Promise (Wert des gedrückten Knopfes, null bei Esc)
//   bestaetigen(text, opt) → Promise<bool> · eingabe(titel, feld, vorgabe, opt) → Promise<string|null>
//   meldung(text, art, opt) – kurze Einblendung unten rechts (info | ok | warnung | fehler)
//   kontextmenue(x, y, eintraege) – Menü an der Mausposition
//   projektStart() – zeigt den Startbildschirm (startbildschirm.js); projektOeffnenDialog() (Ordner-Browser), projektAnlegenDialog(beispiel)
//   warumDialog(element, eigenschaft) – Herkunft eines Werts (Abfrage warum)
// Keine Fachlogik: alles Lesen und Ändern geht über api.js.

import { zustand, melden as zustandMelden, auswaehlen, merken } from '../zustand.js';
import { holen, senden, abfrage } from '../api.js';
import { esc, knoten, fehlerText } from './werkzeug.js';
import { fensterAnbinden, kopfKnoepfeHtml } from './dialoge-fenster.js';
import { dialogArt } from './dialog-merken.js';

const schicht = () => document.getElementById('dialogschicht');
const stapel = [];   // offene Dialoge, der letzte ist oben

/** Nur der oberste Dialog ist bedienbar (er kann ja verschoben sein, sodass die darunter sichtbar werden). */
function stapelAktualisieren() {
  stapel.forEach((e, i) => { e.ctrl.fenster.inert = i < stapel.length - 1; e.ctrl.fenster.classList.toggle('gesperrt', i < stapel.length - 1); });
}

// ------------------------------------------------------------------------------------------------ Dialog

/** Titel des obersten offenen Dialogs (für die Hilfe mit F1, hilfe-oberflaeche.js) oder null, wenn keiner offen ist. */
export function obersterDialogTitel() { const o = stapel.at(-1); return o ? (o.ctrl.fenster.querySelector('.dlg-titel')?.textContent || o.titel || null) : null; }

/**
 * Zeigt einen Dialog. inhalt: HTML-Text, Knoten oder Funktion(ctrl) → Text/Knoten.
 * knoepfe: [{ text, wert, primaer, gefahr, schliesst=true, pruefen(), klick(ctrl) }]; pruefen liefert true/undefined (weiter),
 * false (bleiben) oder einen Fehlertext (bleiben, Text anzeigen). klick darf async sein und Fehler werfen.
 * Der Dialog ist an der Titelleiste verschiebbar, am Rand in der Grösse änderbar und einklappbar (dialoge-fenster.js, W1); Lage und Grösse werden je Dialogart gemerkt.
 * art: Schlüssel der Dialogart für das Merken (Vorgabe: der Titel ohne Namen in «…» und ohne Ziffern).
 */
export function zeigen({ titel = '', inhalt = '', knoepfe = null, breite = null, schliessbar = true, klasse = '', beiOeffnen = null, art = null } = {}) {
  return new Promise((fertig) => {
    const vorherFokus = document.activeElement;
    const fenster = document.createElement('div');
    fenster.className = ('dlg ' + klasse).trim();
    fenster.setAttribute('role', 'dialog');
    fenster.setAttribute('aria-modal', 'true');
    if (breite) fenster.style.width = typeof breite === 'number' ? breite + 'px' : breite;
    fenster.innerHTML = `<div class="dlg-kopf"><span class="dlg-titel"></span>${kopfKnoepfeHtml(schliessbar)}</div>
      <div class="dlg-inhalt"></div><div class="dlg-fehler" hidden></div><div class="dlg-fuss"></div>`;
    let rahmen = null;
    fenster.querySelector('.dlg-titel').textContent = titel;
    const koerper = fenster.querySelector('.dlg-inhalt');
    const fuss = fenster.querySelector('.dlg-fuss');
    const fehlerFeld = fenster.querySelector('.dlg-fehler');
    let offen = true;

    const ctrl = {
      fenster, koerper, fuss,
      schliessen(wert = null) {
        if (!offen) return;
        offen = false;
        const i = stapel.indexOf(eintrag);
        if (i >= 0) stapel.splice(i, 1);
        rahmen?.abbauen();
        fenster.remove();
        stapelAktualisieren();
        try { vorherFokus?.focus?.(); } catch { /* egal */ }
        fertig(wert);
      },
      fehler(text) { fehlerFeld.hidden = !text; fehlerFeld.textContent = text ?? ''; },
      beschaeftigt(ja) { fenster.classList.toggle('beschaeftigt', !!ja); },
    };

    const liste = knoepfe ?? [{ text: 'OK', wert: true, primaer: true }];
    for (const b of liste) {
      const k = document.createElement('button');
      k.type = 'button';
      k.className = 'dlg-knopf' + (b.primaer ? ' primaer' : '') + (b.gefahr ? ' gefahr' : '');
      k.textContent = b.text;
      k.onclick = async () => {
        ctrl.fehler(null);
        if (b.pruefen) {
          let r;
          try { r = b.pruefen(); } catch (e) { console.error(e); r = String(e.message ?? e); }
          if (typeof r === 'string') { ctrl.fehler(r); return; }
          if (r === false) return;
        }
        if (b.klick) {
          ctrl.beschaeftigt(true);
          try { await b.klick(ctrl); }
          catch (e) { console.error(e); ctrl.fehler(fehlerText(e)); ctrl.beschaeftigt(false); return; }
          ctrl.beschaeftigt(false);
        }
        if (b.schliesst !== false) ctrl.schliessen(b.wert === undefined ? true : b.wert);
      };
      k.dataset.primaer = b.primaer ? '1' : '';
      fuss.appendChild(k);
    }
    if (liste.length === 0) fuss.remove();

    const eintrag = { ctrl, schliessbar, titel };
    fenster.querySelector('.dlg-x')?.addEventListener('click', () => ctrl.schliessen(null));

    let teil = typeof inhalt === 'function' ? inhalt(ctrl) : inhalt;
    if (teil instanceof Node) koerper.appendChild(teil); else koerper.innerHTML = String(teil ?? '');

    stapel.push(eintrag);
    schicht().appendChild(fenster);
    stapelAktualisieren();
    try { rahmen = fensterAnbinden(fenster, { art: dialogArt({ art, titel, klasse }) }); } catch (e) { console.error(e); }
    try { beiOeffnen?.(ctrl); } catch (e) { console.error(e); }
    const fokus = fenster.querySelector('[autofocus], input:not([type=checkbox]):not([type=radio]), select, textarea') ?? fenster.querySelector('.dlg-knopf.primaer') ?? fenster.querySelector('.dlg-knopf');
    fokus?.focus?.();
    if (fokus?.select && fokus.tagName === 'INPUT') fokus.select();
  });
}

document.addEventListener('keydown', (e) => {
  const oben = stapel.at(-1);
  if (!oben) return;
  if (e.key === 'Escape') {
    if (oben.schliessbar) { e.preventDefault(); e.stopPropagation(); oben.ctrl.schliessen(null); }
    return;
  }
  if (e.key === 'Enter' && !e.shiftKey && !e.ctrlKey) {
    const z = e.target;
    if (z instanceof HTMLTextAreaElement || z instanceof HTMLButtonElement || z instanceof HTMLSelectElement) return;
    if (z instanceof HTMLElement && z.dataset.keinEnter) return;
    const primaer = oben.ctrl.fuss?.querySelector('.dlg-knopf.primaer');
    if (primaer) { e.preventDefault(); primaer.click(); }
  }
}, true);

/** Ja/Nein-Abfrage. */
export function bestaetigen(text, { ja = 'OK', nein = 'Abbrechen', gefahr = false, titel = 'Bestätigen' } = {}) {
  return zeigen({
    titel,
    inhalt: `<p class="dlg-text">${esc(text).replace(/\n/g, '<br>')}</p>`,
    knoepfe: [{ text: nein, wert: false }, { text: ja, wert: true, primaer: true, gefahr }],
  }).then((w) => w === true);
}

/** Einzeiliges Eingabefeld. Gibt den Text zurück oder null bei Abbruch. */
export async function eingabe(titel, feld, vorgabe = '', { leerErlaubt = false, platzhalter = '' } = {}) {
  const inh = knoten(`<label class="fz"><span>${esc(feld)}</span><input type="text" class="gross" value="${esc(vorgabe)}" placeholder="${esc(platzhalter)}"></label>`);
  const ok = await zeigen({
    titel, inhalt: inh,
    knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'OK', wert: true, primaer: true, pruefen: () => (leerErlaubt || inh.querySelector('input').value.trim()) ? true : `${feld} darf nicht leer sein.` }],
  });
  return ok === true ? inh.querySelector('input').value.trim() : null;
}

// ------------------------------------------------------------------------------------------------ Meldungen

/**
 * Kurze Einblendung unten rechts. opt.vorschlag: zweiter Satz mit Weg zur Lösung.
 * opt.ersetze: Schlüssel; eine noch sichtbare Meldung mit demselben Schlüssel wird ersetzt statt gestapelt (Gesten: «Reiter: …» bei schnellen Wischen).
 * opt.dauer: Anzeigedauer in ms (Vorgabe nach Art).
 */
export function meldung(text, art = 'info', opt = {}) {
  const arten = ['info', 'ok', 'warnung', 'fehler'];
  if (!arten.includes(art)) art = 'info';
  if (art === 'fehler') console.error(text, opt.vorschlag ?? '');
  const ort = document.getElementById('meldungen');
  if (!ort) { console.log(art, text); return; }
  if (opt.ersetze) for (const alt of [...ort.children]) if (alt.dataset.ersetze === opt.ersetze) alt.remove();
  const m = document.createElement('div');
  m.className = 'meldung ' + art;
  if (opt.ersetze) m.dataset.ersetze = opt.ersetze;
  m.innerHTML = `<div class="m-text"></div>${opt.vorschlag ? '<div class="m-vorschlag"></div>' : ''}`;
  m.querySelector('.m-text').textContent = text;
  if (opt.vorschlag) m.querySelector('.m-vorschlag').textContent = 'Vorschlag: ' + opt.vorschlag;
  m.onclick = () => m.remove();
  while (ort.children.length >= 4) ort.firstElementChild.remove();
  ort.appendChild(m);
  const dauer = opt.dauer > 0 ? opt.dauer : art === 'fehler' || opt.vorschlag ? 9000 : art === 'warnung' ? 6000 : 3800;
  setTimeout(() => m.remove(), dauer);
}

// ------------------------------------------------------------------------------------------------ Kontextmenü

let offenesMenue = null;
function menueSchliessen() { offenesMenue?.remove(); offenesMenue = null; }
document.addEventListener('pointerdown', (e) => { if (offenesMenue && !offenesMenue.contains(e.target)) menueSchliessen(); }, true);
document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && offenesMenue) { menueSchliessen(); e.stopPropagation(); } }, true);
window.addEventListener('blur', menueSchliessen);
window.addEventListener('resize', menueSchliessen);

/** eintraege: [{ titel, aktion(), aktiv = true, hinweis, trenner }] */
export function kontextmenue(x, y, eintraege) {
  menueSchliessen();
  const m = document.createElement('div');
  m.className = 'kmenu';
  for (const e of eintraege) {
    if (e.trenner) { const t = document.createElement('div'); t.className = 'kmenu-trenner'; m.appendChild(t); continue; }
    const b = document.createElement('button');
    b.type = 'button';
    b.textContent = e.titel;
    const aktiv = e.aktiv !== false;
    b.classList.toggle('gesperrt', !aktiv);
    if (!aktiv && e.hinweis) b.title = e.hinweis;
    b.onclick = () => { menueSchliessen(); if (aktiv) { try { e.aktion?.(); } catch (err) { console.error(err); meldung(String(err.message ?? err), 'fehler'); } } };
    m.appendChild(b);
  }
  document.body.appendChild(m);
  const r = m.getBoundingClientRect();
  m.style.left = Math.max(0, Math.min(x, window.innerWidth - r.width - 4)) + 'px';
  m.style.top = Math.max(0, Math.min(y, window.innerHeight - r.height - 4)) + 'px';
  offenesMenue = m;
}

// ------------------------------------------------------------------------------------------------ Projekt öffnen und anlegen

/** Nach dem Öffnen oder Anlegen: alte Ansichten schliessen, Status setzen, Katalog und Modell laden. */
export async function projektGeoeffnet(status) {
  import('./startbildschirm.js').then((s) => s.schliessen()).catch((e) => console.error(e));   // auch wenn das Projekt anderswo geöffnet wurde (anderes Fenster, KI)
  const fenster = await import('../fenster.js');
  for (const o of fenster.alle()) fenster.schliessen(o.id);
  auswaehlen(null);
  zustand.status = status;
  zustand.geladen = status?.ort ?? null;   // sofort, damit das Ereignis «projekt» nicht ein zweites Mal lädt
  zustandMelden('status', status);
  const { projektLaden } = await import('../app.js');
  await projektLaden();
  // Kälte gehört zur Heizung (Entscheid Christian 20.10.2026): alte Kennungen sind beim Lesen umgeschrieben – Hinweis des Kerns, kein Fehler
  if (status?.aliasHinweis) meldung(status.aliasHinweis, 'info');
}

const ORDNER_PLATZHALTER = '…\\c_Pläne und Modelle\\e_KICAD';

export async function oeffnenAusfuehren(ordner) {
  merken.schreiben('projektOrdner', ordner);
  const status = await senden('/api/projekt/oeffnen', { ordner });
  await projektGeoeffnet(status);
}

export async function anlegenAusfuehren(ordner, name, beispiel) {
  merken.schreiben('projektOrdner', ordner);
  const status = await senden('/api/projekt/anlegen', { ordner, name, beispiel });
  await projektGeoeffnet(status);
}

/** Dialog «Projekt öffnen»: Ordner-Browser über den Server (Laufwerke, Projektablage, zuletzt benutzte Ordner); Pfad eintippen geht weiter. */
export async function projektOeffnenDialog(ordnerVorgabe = '') {
  const { ordnerWaehlen } = await import('./ordner-browser.js');
  return ordnerWaehlen({ titel: 'Projekt öffnen', modus: 'oeffnen', vorgabe: ordnerVorgabe, okText: 'Öffnen', ausfuehren: (pfad) => oeffnenAusfuehren(pfad) });
}

/** Dialog «Projekt anlegen» (leer oder Beispielprojekt); der Zielordner lässt sich im Ordner-Browser wählen oder eintippen. */
export async function projektAnlegenDialog(beispiel) {
  const inh = knoten(`<div>
    ${beispiel ? '' : '<label class="fz"><span>Projektname</span><input type="text" class="gross name" value="Neues Projekt"></label>'}
    <label class="fz"><span>Zielordner (e_KICAD)</span><span class="fz-zeile"><input type="text" class="gross ordner" placeholder="${esc(ORDNER_PLATZHALTER)}"><button type="button" class="dlg-knopf durchsuchen" style="min-width:0">Durchsuchen …</button></span></label>
    <p class="leise">${beispiel ? 'Legt das fiktive Beispielprojekt «MFH Musterweg» im angegebenen Ordner an.' : 'Legt ein leeres Projekt im angegebenen Ordner an.'}</p></div>`);
  const ordner = inh.querySelector('.ordner');
  const name = inh.querySelector('.name');
  inh.querySelector('.durchsuchen').onclick = async () => {
    const { ordnerWaehlen } = await import('./ordner-browser.js');
    const gewaehlt = await ordnerWaehlen({ titel: 'Zielordner wählen', modus: 'ziel', vorgabe: ordner.value.trim(), okText: 'Wählen' });
    if (gewaehlt) ordner.value = gewaehlt;
    ordner.focus();
  };
  return zeigen({
    titel: beispiel ? 'Beispielprojekt anlegen' : 'Projekt anlegen', inhalt: inh, breite: 600,
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Anlegen', wert: true, primaer: true,
        pruefen: () => ordner.value.trim() ? true : 'Bitte den Zielordner angeben.',
        klick: () => anlegenAusfuehren(ordner.value.trim(), name?.value.trim() || 'Beispielprojekt', beispiel) }],
  });
}

/** Ohne offenes Projekt (Start, nach Schliessen): der Startbildschirm. Der frühere kleine Dialog «Projekt wählen» ist ersetzt. */
export async function projektStart() {
  const s = await import('./startbildschirm.js');
  return s.zeigen();
}

// ------------------------------------------------------------------------------------------------ Warum (Herkunft eines Werts)

const WARUM_REIHENFOLGE = ['wert', 'einheit', 'begruendung', 'rechenweg', 'quelle', 'herkunft'];
const WARUM_NAMEN = { wert: 'Wert', einheit: 'Einheit', begruendung: 'Begründung', rechenweg: 'Rechenweg', quelle: 'Quelle', herkunft: 'Herkunft', verlauf: 'Verlauf' };

function warumWert(v) {
  if (v === null || v === undefined) return '–';
  if (Array.isArray(v)) return v.map(warumWert).join(', ');
  if (typeof v === 'object') return Object.entries(v).filter(([, x]) => x !== null && x !== undefined).map(([k, x]) => `${k}: ${warumWert(x)}`).join(' · ');
  return String(v);
}

function warumHtml(r) {
  let h = '<table class="wtab">';
  const gezeigt = new Set(['element', 'eigenschaft', 'verlauf', 'beitraege']);
  for (const k of WARUM_REIHENFOLGE) if (k in r) { gezeigt.add(k); h += `<tr><th>${esc(WARUM_NAMEN[k])}</th><td>${esc(warumWert(r[k]))}</td></tr>`; }
  for (const [k, v] of Object.entries(r)) if (!gezeigt.has(k)) h += `<tr><th>${esc(k)}</th><td>${esc(warumWert(v))}</td></tr>`;
  h += '</table>';
  // Belastungssummen einer Leitung (Modul 4 Teil A-O): Beiträge der Apparate als Tabelle (Apparat, Art, Anschluss, Wert, Excel-Zeile)
  if (Array.isArray(r.beitraege) && r.beitraege.length) {
    h += `<h4>Beiträge der Apparate</h4><table class="tab"><thead><tr><th>Apparat</th><th>Apparateart</th><th>Anschluss</th><th>Spalte</th><th>Wert</th><th>Excel-Zeile</th></tr></thead><tbody>${
      r.beitraege.map((b) => `<tr><td>${esc(b.apparat)}</td><td>${esc(b.artName ?? b.art ?? '')}</td><td>${esc(b.anschluss ?? '')}</td><td>${esc(b.spalte ?? '')}</td><td>${esc(b.wert ?? '–')}</td><td>${esc(b.zeile ?? '')}</td></tr>`).join('')}</tbody></table>`;
  }
  const verlauf = Array.isArray(r.verlauf) ? r.verlauf : [];
  h += '<h4>Verlauf</h4>';
  h += verlauf.length === 0 ? '<p class="leise">Unverändert seit dem Anlegen.</p>' :
    `<table class="tab"><thead><tr><th>Zeit</th><th>Akteur</th><th>Befehl</th><th>Vorher</th><th>Nachher</th></tr></thead><tbody>${
      verlauf.map((v) => `<tr><td>${esc(v.zeit)}</td><td>${esc(v.akteur)}</td><td>${esc(v.befehl)}</td><td>${esc(v.vorher ?? '–')}</td><td>${esc(v.nachher ?? '–')}</td></tr>`).join('')}</tbody></table>`;
  return h;
}

/**
 * Dialog «Warum»: verfolgt einen Wert bis zur Quelle. Ohne eigenschaft darf eine aus der Liste gewählt werden.
 * moeglich: Liste der wählbaren Eigenschaften.
 */
export function warumDialog(kennzeichen, eigenschaft = null, moeglich = []) {
  const liste = moeglich.length > 0 ? moeglich : (eigenschaft ? [eigenschaft] : []);
  const w = knoten(`<div class="warum">
    <div class="warum-kopf"><b>${esc(kennzeichen)}</b>${liste.length > 1 || !eigenschaft ? `<label>Eigenschaft <select class="w-wahl">${liste.map((x) => `<option ${x === eigenschaft ? 'selected' : ''}>${esc(x)}</option>`).join('')}</select></label>` : `<span class="leise"> · ${esc(eigenschaft)}</span>`}</div>
    <div class="warum-inhalt"><p class="leise">Wird geladen …</p></div></div>`);
  const ziel = w.querySelector('.warum-inhalt');
  let nr = 0;
  const laden = async (name) => {
    const mein = ++nr;
    if (!name) { ziel.innerHTML = '<p class="leise">Keine Eigenschaft zur Auswahl.</p>'; return; }
    ziel.innerHTML = '<p class="leise">Wird geladen …</p>';
    try {
      const r = await abfrage('warum', { element: kennzeichen, eigenschaft: name });
      if (mein === nr) ziel.innerHTML = warumHtml(r);
    } catch (e) {
      console.error(e);
      if (mein === nr) ziel.innerHTML = `<p class="fehlertext">${esc(e.message)}</p>${e.vorschlag ? `<p class="leise">Vorschlag: ${esc(e.vorschlag)}</p>` : ''}`;
    }
  };
  const wahl = w.querySelector('.w-wahl');
  if (wahl) wahl.onchange = () => laden(wahl.value);
  laden(wahl ? wahl.value : eigenschaft);
  return zeigen({ titel: 'Warum – Herkunft des Werts', inhalt: w, breite: 640, knoepfe: [{ text: 'Schliessen', wert: true, primaer: true }] });
}
