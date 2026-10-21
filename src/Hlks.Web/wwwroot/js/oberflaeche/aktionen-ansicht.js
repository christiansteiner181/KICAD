// Aktionen des Registers «Ansicht»: Ansichten anlegen (Grundriss, 3D, Liste), duplizieren, Sichtbarkeit/Grafiken (VV),
// Ansichtsvorlagen speichern und anwenden, Fensteranordnung (WT, WC, eigenes Fenster), Zoom (ZF, ZA), Design hell/dunkel.
// Eine Ansicht ist ein Element der Klasse Ansicht; alles läuft über element_anlegen / element_aendern.

import { zustand, element, derKlasse, abonnieren } from '../zustand.js';
import * as fenster from '../fenster.js';
import { registrieren } from './register.js';
import { zeigen, meldung, eingabe, kontextmenue } from './dialoge.js';
import { benutzerWert, benutzerSetzen } from './benutzer.js';
import { RAND_RAUM, RAND_RAUM_SCHLUESSEL } from '../ansichten/schnittbereich-logik.js';
import { sende, ergebnisMelden } from './befehle.js';
import { design, designUmschalten } from './design.js';
import { einlageplanDialog } from './einlageplaene-dialog.js';
import { esc, knoten, vergleichen, gewerkeListe, aufElement, freiesKennzeichen, freierName, anzeigeName } from './werkzeug.js';
import { systemGewerk } from './projektbrowser-gliederung.js';
import { anlageMitGewerk, anlagenVorne } from './sanitaer-anlage.js';
import { MODI, MODUS_NAMEN, ARTEN as IFC_ARTEN, ifcModus } from '../ansichten/ifc-darstellung.js';

const MASSSTAEBE = [10, 20, 50, 100, 200, 500];
const DETAILGRADE = ['grob', 'mittel', 'fein'];
// Modul 3 (C3): Materialliste (Kennzeichen AN-LI-MATERIALLISTE wie die Aktion «Materialliste» in aktionen-leitung.js)
// N3 (Punkt 3): Kollisionsliste (Abfrage kollisionen) und Aussparungsliste (Abfrage aussparungsliste), Modul 3b – die Listenansicht zeichnet sie seit B4a
const LISTEN = {
  raumliste: ['Raumliste', 'RAUM'], strangliste: ['Strangliste', 'STRANG'], mengen: ['Mengen', 'MENGEN'], materialliste: ['Materialliste', 'MATERIALLISTE'],
  kollisionen: ['Kollisionsliste', 'KOLLISIONEN'], aussparungsliste: ['Aussparungsliste', 'AUSSPARUNGEN'],
  // Modul 4 Teil A-O: Sanitär-Zusammenstellung (W3) je Anlage (Abfrage sanitaer_zusammenstellung); der Dialog fragt zusätzlich die Anlage
  sanitaer_zusammenstellung: ['Sanitär-Zusammenstellung (W3)', 'SANITAER'],
};
const anlagen = () => derKlasse('Anlage').sort((a, b) => vergleichen(a.kennzeichen, b.kennzeichen));
/** Anlagen mit einem System des Gewerks zuerst, die übrigen danach – die Sanitär-Zusammenstellung schlägt so eine
 *  Sanitäranlage vor und nicht die alphabetisch erste (A-KAE, seit Kälte eine eigene Anlage im Gewerk Heizung ist). */
const anlagenFuer = (gewerk) => {
  const ctx = { systemtypen: zustand.katalog?.systemtypen ?? [], medien: zustand.katalog?.medien ?? [] };
  const systeme = derKlasse('System');
  return anlagenVorne(anlagen(), (a) => anlageMitGewerk(a, systeme, (s) => systemGewerk(ctx, s), element, gewerk));
};
/** Einstellungen, die eine Ansichtsvorlage überträgt (Art und Geschoss bleiben bei der Ansicht). */
const VORLAGE_EINSTELLUNGEN = ['massstab', 'schnitthoehe', 'detailgrad', 'gewerke', 'klassenAusblenden', 'ifcDarstellung'];
const NUR_MIT_VORLAGE_LEER = ['gewerke', 'klassenAusblenden', 'ifcDarstellung'];
const GLIEDERUNG = new Set(['Gebaeude', 'Geschoss', 'Anlage', 'System', 'Strang', 'Zone']);
const NIE_GRAFISCH = new Set(['ansicht', 'funktion']);

const ansichtVon = (p) => {
  const a = element(p?.ansicht ?? zustand.aktiveAnsicht);
  return a && a.klasse === 'Ansicht' ? a : null;
};
const hatAnsicht = (p) => !!ansichtVon(p);
const KEINE_ANSICHT = 'Zuerst eine Ansicht öffnen oder im Projektbrowser wählen.';
const ansichten = () => derKlasse('Ansicht');
const vorlagen = () => ansichten().filter((a) => a.werte?.vorlage === true).sort((a, b) => vergleichen(anzeigeName(a), anzeigeName(b)));
const geschosse = () => derKlasse('Geschoss').sort((a, b) => (a.werte?.kote ?? 0) - (b.werte?.kote ?? 0));

/** Kopie der Werte einer Ansicht ohne leere Einträge. */
const werteKopie = (a) => JSON.parse(JSON.stringify(a.werte ?? {}));

// ---------------------------------------------------------------------------------------------- Ansicht anlegen

function ansichtFormular(art) {
  const aktiv = ansichtVon();
  const gs = geschosse();
  const gewerkeVorgabe = Array.isArray(aktiv?.werte?.gewerke) && aktiv.werte.gewerke.length ? aktiv.werte.gewerke : ['sanitaer', 'architektur'];
  const geschossVorgabe = aktiv?.geschoss && gs.some((g) => g.kennzeichen === aktiv.geschoss) ? aktiv.geschoss : gs[0]?.kennzeichen;
  const vl = vorlagen().filter((v) => v.werte?.art === art);
  const w = knoten(`<div class="ansichtform">
    <label class="fz"><span>Name</span><input type="text" class="f-name"></label>
    <label class="fz"><span>Kennzeichen</span><input type="text" class="f-kz"></label>
    ${art === 'grundriss' ? `<label class="fz"><span>Geschoss</span><select class="f-geschoss">${gs.map((g) => `<option value="${esc(g.kennzeichen)}" ${g.kennzeichen === geschossVorgabe ? 'selected' : ''}>${esc(g.kennzeichen)}${g.name ? ' – ' + esc(g.name) : ''}</option>`).join('')}</select></label>` : ''}
    ${art === 'liste' ? `<label class="fz"><span>Liste</span><select class="f-liste">${Object.entries(LISTEN).map(([k, [n]]) => `<option value="${k}">${esc(n)}</option>`).join('')}</select></label>` : ''}
    ${art === 'liste' ? `<label class="fz f-anlage-zeile" hidden><span>Anlage</span><select class="f-anlage">${anlagenFuer('sanitaer').map((a) => `<option value="${esc(a.kennzeichen)}">${esc(a.kennzeichen)}${a.name ? ' – ' + esc(a.name) : ''}</option>`).join('')}</select></label>` : ''}
    ${art !== 'liste' ? `<label class="fz"><span>Detailgrad</span><select class="f-detail">${DETAILGRADE.map((d) => `<option ${d === 'mittel' ? 'selected' : ''}>${d}</option>`).join('')}</select></label>` : ''}
    ${art === 'grundriss' ? `<label class="fz"><span>Massstab 1:</span><select class="f-massstab">${MASSSTAEBE.map((m) => `<option ${m === 50 ? 'selected' : ''}>${m}</option>`).join('')}</select></label>` : ''}
    ${art !== 'liste' ? `<fieldset class="f-gewerke"><legend>Gewerke</legend><div class="spalten">${gewerkeListe().map(([k, n]) => `<label class="haken"><input type="checkbox" value="${esc(k)}" ${gewerkeVorgabe.includes(k) ? 'checked' : ''}> ${esc(n)}</label>`).join('')}</div></fieldset>` : ''}
    ${vl.length ? `<label class="fz"><span>Vorlage</span><select class="f-vorlage"><option value="">(keine)</option>${vl.map((v) => `<option value="${esc(v.kennzeichen)}">${esc(anzeigeName(v))}</option>`).join('')}</select></label>` : ''}
  </div>`);
  const q = (s) => w.querySelector(s);
  let kzGeaendert = false, nameGeaendert = false;
  const vorschlag = () => {
    let kzBasis, nameBasis;
    if (art === 'grundriss') { const g = q('.f-geschoss')?.value ?? ''; kzBasis = `AN-GR-${g}`; nameBasis = `Grundriss ${g}`; }
    else if (art === '3d') { kzBasis = 'AN-3D'; nameBasis = '3D-Ansicht'; }
    else {
      const liste = q('.f-liste')?.value ?? 'raumliste';
      const l = LISTEN[liste];
      kzBasis = `AN-LI-${l[1]}`; nameBasis = l[0];
      const mitAnlage = liste === 'sanitaer_zusammenstellung';
      if (q('.f-anlage-zeile')) q('.f-anlage-zeile').hidden = !mitAnlage;
      if (mitAnlage && q('.f-anlage')?.value) { kzBasis += `-${q('.f-anlage').value}`; nameBasis += ` – ${q('.f-anlage').value}`; }
    }
    if (!kzGeaendert) q('.f-kz').value = freiesKennzeichen(kzBasis);
    if (!nameGeaendert) q('.f-name').value = freierName(nameBasis, ansichten().map((a) => a.name || a.kennzeichen));
  };
  q('.f-kz').oninput = () => { kzGeaendert = true; };
  q('.f-name').oninput = () => { nameGeaendert = true; };
  q('.f-geschoss')?.addEventListener('change', vorschlag);
  q('.f-liste')?.addEventListener('change', vorschlag);
  q('.f-anlage')?.addEventListener('change', vorschlag);
  q('.f-vorlage')?.addEventListener('change', (e) => {
    const v = element(e.target.value);
    if (!v) return;
    const t = v.werte ?? {};
    if (t.massstab && q('.f-massstab')) q('.f-massstab').value = String(t.massstab);
    if (t.detailgrad && q('.f-detail')) q('.f-detail').value = t.detailgrad;
    if (Array.isArray(t.gewerke)) w.querySelectorAll('.f-gewerke input').forEach((c) => { c.checked = t.gewerke.includes(c.value); });
  });
  vorschlag();
  return w;
}

async function ansichtAnlegen(art) {
  if (art === 'grundriss' && geschosse().length === 0) { meldung('Im Modell gibt es noch kein Geschoss.', 'warnung', { vorschlag: 'Zuerst ein Geschoss anlegen.' }); return; }
  const w = ansichtFormular(art);
  const titel = { grundriss: 'Grundriss anlegen', '3d': '3D-Ansicht anlegen', liste: 'Liste anlegen' }[art];
  const ok = await zeigen({
    titel, inhalt: w, breite: 520,
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Anlegen', wert: true, primaer: true,
        pruefen: () => !w.querySelector('.f-kz').value.trim() ? 'Das Kennzeichen darf nicht leer sein.' : !w.querySelector('.f-name').value.trim() ? 'Der Name darf nicht leer sein.' : true }],
  });
  if (ok !== true) return;
  const q = (s) => w.querySelector(s);
  const kz = q('.f-kz').value.trim();
  const eig = { art };
  if (art === 'liste') {
    eig.liste = q('.f-liste').value;
    if (eig.liste === 'sanitaer_zusammenstellung' && q('.f-anlage')?.value) eig.anlage = q('.f-anlage').value;
  } else {
    eig.detailgrad = q('.f-detail').value;
    const g = [...w.querySelectorAll('.f-gewerke input:checked')].map((c) => c.value);
    if (g.length) eig.gewerke = g;
  }
  if (art === 'grundriss') eig.massstab = Number(q('.f-massstab').value);
  const v = element(q('.f-vorlage')?.value);
  if (v) {
    eig.ansichtsvorlage = v.kennzeichen;
    if (v.werte?.schnitthoehe !== undefined && art === 'grundriss') eig.schnitthoehe = v.werte.schnitthoehe;
    if (Array.isArray(v.werte?.klassenAusblenden)) eig.klassenAusblenden = v.werte.klassenAusblenden;
  }
  const param = { klasse: 'Ansicht', kennzeichen: kz, name: q('.f-name').value.trim(), eigenschaften: eig };
  if (art === 'grundriss') param.geschoss = q('.f-geschoss').value;
  const r = await sende('element_anlegen', param);
  if (!ergebnisMelden(r, `Ansicht «${param.name}» angelegt.`)) return;
  if (await aufElement(kz)) await fenster.ansichtOeffnen(kz);
  else meldung('Die neue Ansicht ist noch nicht eingetroffen.', 'warnung', { vorschlag: 'Im Projektbrowser nachsehen.' });
}

registrieren({ id: 'ansicht.grundriss', titel: 'Grundriss', symbol: 'grundriss', kuerzel: null, register: 'ansicht', gruppe: 'Erstellen', ausfuehren: () => ansichtAnlegen('grundriss') });
// N3 (Punkt 1): Einlageplan und Grundleitungsplan legen Ansicht und Planblatt über den Kernbefehl einlageplaene_anlegen an (Dialog: einlageplaene-dialog.js);
// als kleine Knöpfe nur mit Symbol (nurSymbol, Name im Tooltip) in einer Spalte neben «Grundriss»: die Gruppe «Erstellen» bleibt so schmal genug für das Menüband.
const planAktiv = () => !!zustand.status?.offen && geschosse().length > 0;
const planHinweis = () => (zustand.status?.offen ? 'Im Modell gibt es noch kein Geschoss.' : 'Zuerst ein Projekt öffnen.');
registrieren({ id: 'ansicht.einlageplan', titel: 'Einlageplan', symbol: 'einlageplan', kuerzel: null, register: 'ansicht', gruppe: 'Erstellen', groesse: 'klein', nurSymbol: true, aktiv: planAktiv, hinweis: planHinweis, ausfuehren: () => einlageplanDialog('einlage') });
registrieren({ id: 'ansicht.grundleitungsplan', titel: 'Grundleitungsplan', symbol: 'grundleitungsplan', kuerzel: null, register: 'ansicht', gruppe: 'Erstellen', groesse: 'klein', nurSymbol: true, aktiv: planAktiv, hinweis: planHinweis, ausfuehren: () => einlageplanDialog('erdreich') });
registrieren({ id: 'ansicht.3d', titel: '3D-Ansicht', symbol: 'ansicht3d', kuerzel: '3D', register: 'ansicht', gruppe: 'Erstellen', ausfuehren: () => ansichtAnlegen('3d') });
registrieren({ id: 'ansicht.liste', titel: 'Liste', symbol: 'liste', kuerzel: null, register: 'ansicht', gruppe: 'Erstellen', ausfuehren: () => ansichtAnlegen('liste') });

// ---------------------------------------------------------------------------------------------- Duplizieren und Vorlagen

registrieren({
  id: 'ansicht.duplizieren', titel: 'Ansicht duplizieren', symbol: 'duplizieren', kuerzel: null, register: 'ansicht', gruppe: 'Ansicht',
  aktiv: hatAnsicht, hinweis: KEINE_ANSICHT,
  async ausfuehren(p) {
    const a = ansichtVon(p);
    const kz = freiesKennzeichen(a.kennzeichen);
    const name = freierName(`${anzeigeName(a)} – Kopie`, ansichten().map((x) => x.name || x.kennzeichen));
    const param = { klasse: 'Ansicht', kennzeichen: kz, name, eigenschaften: werteKopie(a) };
    if (a.geschoss) param.geschoss = a.geschoss;
    const r = await sende('element_anlegen', param);
    if (!ergebnisMelden(r, `Ansicht «${name}» angelegt.`)) return;
    if (a.werte?.vorlage !== true && await aufElement(kz)) await fenster.ansichtOeffnen(kz);
  },
});

registrieren({
  id: 'ansicht.vorlage-speichern', titel: 'Als Vorlage speichern', symbol: 'vorlageSpeichern', kuerzel: null, register: 'ansicht', gruppe: 'Ansichtsvorlage',
  aktiv: (p) => { const a = ansichtVon(p); return !!a && a.werte?.vorlage !== true; },
  hinweis: (p) => (ansichtVon(p)?.werte?.vorlage === true ? 'Diese Ansicht ist schon eine Vorlage.' : KEINE_ANSICHT),
  async ausfuehren(p) {
    const a = ansichtVon(p);
    const name = await eingabe('Als Vorlage speichern', 'Name der Vorlage', `Vorlage ${anzeigeName(a)}`);
    if (!name) return;
    const kz = freiesKennzeichen('VL-' + a.kennzeichen.replace(/^AN-/i, ''));
    const eig = werteKopie(a);
    delete eig.ansichtsvorlage;
    eig.vorlage = true;
    const param = { klasse: 'Ansicht', kennzeichen: kz, name, eigenschaften: eig };
    if (a.geschoss) param.geschoss = a.geschoss;
    ergebnisMelden(await sende('element_anlegen', param), `Vorlage «${name}» gespeichert.`);
  },
});

/** Überträgt die Einstellungen einer Vorlage auf eine Ansicht (Art und Geschoss bleiben). */
async function vorlageUebertragen(v, a) {
  const eig = {};
  for (const k of VORLAGE_EINSTELLUNGEN) {
    if (v.werte?.[k] !== undefined) eig[k] = v.werte[k];
    else if (a.werte?.[k] !== undefined && NUR_MIT_VORLAGE_LEER.includes(k)) eig[k] = null;   // Vorlage ohne Filter hebt Filter auf
  }
  eig.ansichtsvorlage = v.kennzeichen;
  const r = await sende('element_aendern', { element: a.kennzeichen, eigenschaften: eig });
  ergebnisMelden(r, `Vorlage «${anzeigeName(v)}» auf «${anzeigeName(a)}» angewendet.`);
}

registrieren({
  id: 'ansicht.vorlage-anwenden', titel: 'Vorlage anwenden', symbol: 'vorlageAnwenden', kuerzel: null, register: 'ansicht', gruppe: 'Ansichtsvorlage',
  aktiv: (p) => { const a = ansichtVon(p); return !!a && a.werte?.vorlage !== true; },
  hinweis: (p) => (ansichtVon(p)?.werte?.vorlage === true ? 'Eine Vorlage kann nicht selbst eine Vorlage erhalten.' : KEINE_ANSICHT),
  async ausfuehren(p) {
    const a = ansichtVon(p);
    const liste = vorlagen();
    if (liste.length === 0) { meldung('Es gibt noch keine Ansichtsvorlage.', 'info', { vorschlag: 'Eine Ansicht wählen und «Als Vorlage speichern».' }); return; }
    let v = p?.vorlage ? element(p.vorlage) : null;
    if (!v) {
      const w = knoten(`<div><p class="dlg-text">Die Einstellungen der Vorlage (Massstab, Detailgrad, Gewerke, ausgeblendete Klassen) werden auf «${esc(anzeigeName(a))}» übertragen. Art und Geschoss bleiben.</p>
        <label class="fz"><span>Vorlage</span><select class="f-v">${liste.map((x) => `<option value="${esc(x.kennzeichen)}" ${x.kennzeichen === a.werte?.ansichtsvorlage ? 'selected' : ''}>${esc(anzeigeName(x))} (${esc(x.werte?.art ?? '?')})</option>`).join('')}</select></label></div>`);
      const ok = await zeigen({ titel: 'Vorlage anwenden', inhalt: w, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Anwenden', wert: true, primaer: true }] });
      if (ok !== true) return;
      v = element(w.querySelector('.f-v').value);
    }
    if (v) await vorlageUebertragen(v, a);
  },
});

// ---------------------------------------------------------------------------------------------- Sichtbarkeit / Grafiken

function klassenFuerAusblenden() {
  const klassen = (zustand.katalog?.klassen ?? []).filter((k) => !NIE_GRAFISCH.has(k.schicht) && !GLIEDERUNG.has(k.name) && !k.abstrakt);
  const basen = [...new Set(klassen.map((k) => k.basis).filter(Boolean))].sort(vergleichen);
  return { basen, klassen: klassen.map((k) => k.name).sort(vergleichen) };
}

async function sichtbarkeit(p) {
  const a = ansichtVon(p);
  const aktuellG = new Set(Array.isArray(a.werte?.gewerke) ? a.werte.gewerke : []);
  const aus = new Set(Array.isArray(a.werte?.klassenAusblenden) ? a.werte.klassenAusblenden : []);
  const { basen, klassen } = klassenFuerAusblenden();
  const haken = (wert, an, klasse) => `<label class="haken"><input type="checkbox" class="${klasse}" value="${esc(wert)}" ${an ? 'checked' : ''}> ${esc(wert)}</label>`;
  // N12: IFC-Unterlagen als Nachbau, Original oder beide zeigen (nur wo es eine IFC-Unterlage gibt und die Ansicht Modellgrafik zeigt)
  const ifcDa = derKlasse('Unterlage').some((u) => u.werte?.art === 'ifc') && IFC_ARTEN.includes(a.werte?.art);
  const ifcModusAlt = ifcModus(a);
  const ifcFeld = ifcDa ? `<fieldset class="v-ifc"><legend>IFC-Unterlagen zeigen</legend>
      ${MODI.map((m) => `<label class="haken"><input type="radio" name="v-ifc" class="v-ifc-modus" value="${m}" ${m === ifcModusAlt ? 'checked' : ''}> ${esc(MODUS_NAMEN[m])}</label>`).join('')}
      <p class="leise">Farbe und Transparenz von Original und Nachbau: Einfügen → Unterlagen verwalten → «Farben …».</p></fieldset>` : '';
  const w = knoten(`<div class="vv">
    <p class="dlg-text">Ansicht: <b>${esc(anzeigeName(a))}</b> (${esc(a.kennzeichen)})</p>
    ${ifcFeld}
    <fieldset><legend>Gewerke anzeigen</legend><div class="spalten">${gewerkeListe().map(([k, n]) => `<label class="haken"><input type="checkbox" class="v-gewerk" value="${esc(k)}" ${aktuellG.has(k) ? 'checked' : ''}> ${esc(n)}</label>`).join('')}</div>
      <p class="leise">Ist nichts angekreuzt, zeigt die Ansicht alle Gewerke.</p></fieldset>
    <fieldset><legend>Klassen ausblenden</legend>
      ${basen.length ? `<div class="v-unter">Basisklassen</div><div class="spalten">${basen.map((b) => haken(b, aus.has(b), 'v-klasse')).join('')}</div>` : ''}
      <div class="v-unter">Klassen</div><div class="spalten gross">${klassen.map((k) => haken(k, aus.has(k), 'v-klasse')).join('')}</div></fieldset></div>`);
  const ok = await zeigen({ titel: 'Sichtbarkeit/Grafiken', inhalt: w, breite: 640, klasse: 'dlg-hoch', knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'OK', wert: true, primaer: true }] });
  if (ok !== true) return;
  const g = [...w.querySelectorAll('.v-gewerk:checked')].map((c) => c.value);
  const k = [...w.querySelectorAll('.v-klasse:checked')].map((c) => c.value);
  const eig = { gewerke: g.length ? g : null, klassenAusblenden: k.length ? k : null };
  const ifcNeu = w.querySelector('.v-ifc-modus:checked')?.value;
  if (ifcNeu && ifcNeu !== ifcModusAlt) eig.ifcDarstellung = ifcNeu === 'nachbau' ? null : ifcNeu;
  const r = await sende('element_aendern', { element: a.kennzeichen, eigenschaften: eig });
  ergebnisMelden(r, 'Sichtbarkeit angepasst.');
}

registrieren({
  id: 'ansicht.vv', titel: 'Sichtbarkeit/\u200bGrafiken', symbol: 'sichtbarkeit', kuerzel: 'VV', kuerzelAlt: ['VG'], register: 'ansicht', gruppe: 'Grafik',
  aktiv: hatAnsicht, hinweis: KEINE_ANSICHT, ausfuehren: sichtbarkeit,
});

// ---------------------------------------------------------------------------------------------- Fenster und Zoom

registrieren({
  id: 'ansicht.nebeneinander', titel: 'Fenster nebeneinander', symbol: 'nebeneinander', kuerzel: 'WT', register: 'ansicht', gruppe: 'Fenster', groesse: 'klein',
  gedrueckt: () => fenster.fenstermodus() === 'nebeneinander',
  aktiv: () => fenster.alle().length > 1, hinweis: 'Mindestens zwei Ansichten öffnen.',
  ausfuehren: () => fenster.nebeneinander(),
});
registrieren({
  id: 'ansicht.reiter', titel: 'Fenster als Reiter', symbol: 'reiter', kuerzel: 'WC', register: 'ansicht', gruppe: 'Fenster', groesse: 'klein',
  gedrueckt: () => fenster.fenstermodus() === 'reiter',
  ausfuehren: () => fenster.alsReiter(),
});
registrieren({
  id: 'ansicht.eigenes-fenster', titel: 'In eigenem Fenster öffnen', symbol: 'eigenesFenster', kuerzel: null, register: 'ansicht', gruppe: 'Fenster', groesse: 'klein',
  aktiv: () => !!fenster.aktive() && !fenster.imZweitfenster(), hinweis: (p) => (fenster.imZweitfenster() ? 'Diese Ansicht ist schon in einem eigenen Fenster.' : 'Zuerst eine Ansicht öffnen.'),
  ausfuehren: () => { const o = fenster.aktive(); if (o) fenster.eigenesFensterOeffnen(o.id); },
});
registrieren({
  id: 'ansicht.zoom-alles', titel: 'Zoom alles', symbol: 'zoomAlle', kuerzel: 'ZF', kuerzelAlt: ['ZE'], register: 'ansicht', gruppe: 'Zoom', groesse: 'klein',
  aktiv: () => !!fenster.aktive(), hinweis: 'Zuerst eine Ansicht öffnen.',
  ausfuehren: () => fenster.zoomAlles(),
});
registrieren({
  id: 'ansicht.zoom-alle', titel: 'Zoom alle Ansichten', symbol: 'zoomAlleAnsichten', kuerzel: 'ZA', register: 'ansicht', gruppe: 'Zoom', groesse: 'klein',
  aktiv: () => fenster.alle().length > 0, hinweis: 'Zuerst eine Ansicht öffnen.',
  ausfuehren: () => fenster.zoomAlleAnsichten(),
});

// Darstellung: Design hell/dunkel (gemerkt im Browser, Vorgabe dunkel)
registrieren({
  id: 'ansicht.design', titel: 'Design hell/​dunkel', symbol: 'design', kuerzel: null, register: 'ansicht', gruppe: 'Darstellung',
  gedrueckt: () => design() === 'dunkel',
  ausfuehren: () => { designUmschalten(); },
});

// Darstellung «Hülle» (Modul 1c, C1b): schaltet die Ansichtseigenschaft darstellung der aktiven Ansicht (Grundriss, 3D) zwischen
// huelle und standard. Der Kern zeichnet die Hülle (HuelleGrafik, Raumgrenzen); hier wird nur der Befehl gesendet.
const huelleMoeglich = (p) => { const a = ansichtVon(p); return !!a && ['grundriss', '3d'].includes(a.werte?.art) && a.werte?.vorlage !== true; };
registrieren({
  id: 'ansicht.huelle', titel: 'Hülle', symbol: 'huelleAnsicht', kuerzel: null, register: 'ansicht', gruppe: 'Darstellung',
  gedrueckt: () => ansichtVon()?.werte?.darstellung === 'huelle',
  aktiv: huelleMoeglich,
  hinweis: (p) => (ansichtVon(p) ? 'Die Darstellung Hülle gibt es für Grundriss und 3D.' : KEINE_ANSICHT),
  async ausfuehren(p) {
    const a = ansichtVon(p);
    const an = a.werte?.darstellung !== 'huelle';
    ergebnisMelden(await sende('element_aendern', { element: a.kennzeichen, eigenschaften: { darstellung: an ? 'huelle' : null } }),
      an ? `Darstellung Hülle eingeschaltet (${anzeigeName(a)}).` : `Darstellung Hülle ausgeschaltet (${anzeigeName(a)}).`);
  },
});

// N9: Ausrichtung der Grundrisse – «Nach Himmelsrichtung» schaltet die Eigenschaft ausrichtung der aktiven Grundriss-Ansicht zwischen
// himmelsrichtung (geografisch Nord oben) und der Vorgabe geometrie (ohne Angabe: längste Gebäudekante waagrecht); «Planwinkel neu bestimmen»
// sendet planwinkel_bestimmen für das Gebäude der Ansicht (über ihr Geschoss), sonst für alle Gebäude. Gedreht wird im Kern und in grundriss.js.
const grundrissVon = (p) => { const a = ansichtVon(p); return a && (a.werte?.art ?? 'grundriss') === 'grundriss' ? a : null; };
registrieren({
  id: 'ansicht.ausrichtung', titel: 'Nach Himmels­richtung', symbol: 'ausrichtungNord', kuerzel: null, register: 'ansicht', gruppe: 'Darstellung',
  gedrueckt: () => grundrissVon()?.werte?.ausrichtung === 'himmelsrichtung',
  aktiv: (p) => !!grundrissVon(p),
  hinweis: (p) => (ansichtVon(p) ? 'Die Ausrichtung gibt es für Grundrisse; Schnitt und Ansicht folgen ihrer Linie, 3D, Schema und Liste werden nicht gedreht.' : KEINE_ANSICHT),
  async ausfuehren(p) {
    const a = grundrissVon(p);
    const an = a.werte?.ausrichtung !== 'himmelsrichtung';
    ergebnisMelden(await sende('element_aendern', { element: a.kennzeichen, eigenschaften: { ausrichtung: an ? 'himmelsrichtung' : null } }),
      an ? `${anzeigeName(a)}: nach Himmelsrichtung ausgerichtet (geografisch Nord oben).` : `${anzeigeName(a)}: nach Geometrie ausgerichtet (längste Gebäudekante waagrecht).`);
  },
});
registrieren({
  id: 'ansicht.planwinkel', titel: 'Planwinkel neu bestimmen', symbol: 'planwinkel', kuerzel: null, register: 'ansicht', gruppe: 'Darstellung', groesse: 'klein',
  aktiv: () => derKlasse('Gebaeude').length > 0,
  hinweis: 'Im Modell gibt es kein Gebäude – der Planwinkel steht am Gebäude.',
  async ausfuehren(p) {
    const a = ansichtVon(p);
    const geb = p?.gebaeude ?? element(element(a?.geschoss)?.teilVon)?.kennzeichen ?? null;
    const r = await sende('planwinkel_bestimmen', geb ? { gebaeude: geb } : {});
    ergebnisMelden(r, (r.hinweise ?? []).filter(Boolean).join(' ') || 'Planwinkel bestimmt.');
  },
});

// N13: 3D-Schnittbereich (wie Revit) – Schalter, an Auswahl anpassen, auf Geschoss, zurücksetzen. Gerechnet und gezeichnet wird in der 3D-Ansicht
// (ansichten/schnittbereich3d.js), gespeichert als Eigenschaft schnittbereich der Ansicht über den Kern. Die Aktionen arbeiten an der aktiven 3D-Ansicht.
const ansicht3dVon = (p) => { const a = ansichtVon(p); return a && a.werte?.art === '3d' ? a : null; };
const KEINE_3D = 'Zuerst eine 3D-Ansicht öffnen (der 3D-Schnittbereich gehört zur 3D-Ansicht).';
/** Öffnet die 3D-Ansicht (oder holt sie nach vorn) und gibt ihre Instanz zurück; null, wenn sie nicht zu öffnen ist (z. B. eigenes Fenster). */
async function instanz3d(a) {
  const o = await fenster.ansichtOeffnen(a.kennzeichen);
  return o?.instanz?.schnittbereich ? o.instanz : null;
}
const schnittMeldung = (r, ok) => (r.ok ? meldung(ok, 'ok') : meldung(r.fehler || 'Der Schnittbereich konnte nicht gesetzt werden.', 'warnung'));
registrieren({
  id: 'ansicht.schnittbereich', titel: '3D-Schnitt­bereich', symbol: 'schnittbereich', kuerzel: null, register: 'ansicht', gruppe: '3D-Schnitt',
  gedrueckt: () => ansicht3dVon()?.werte?.schnittbereich?.aktiv === true,
  aktiv: (p) => !!ansicht3dVon(p),
  hinweis: KEINE_3D,
  async ausfuehren(p) {
    const a = ansicht3dVon(p);
    const i = await instanz3d(a);
    if (!i) return;
    const an = p?.an ?? a.werte?.schnittbereich?.aktiv !== true;
    schnittMeldung(await i.schnittbereich.schalten(an), an ? '3D-Schnittbereich eingeschaltet: sichtbar ist nur, was im Quader liegt. Die Griffe an den Flächen verschieben sie.' : '3D-Schnittbereich ausgeschaltet.');
  },
});
registrieren({
  id: 'ansicht.schnittbereichAuswahl', titel: 'An Auswahl anpassen', symbol: 'schnittbereichAuswahl', kuerzel: null, register: 'ansicht', gruppe: '3D-Schnitt', groesse: 'klein',
  aktiv: (p) => !!ansicht3dVon(p) && !!zustand.auswahl,
  hinweis: (p) => (ansicht3dVon(p) ? 'Zuerst ein Element wählen, an das sich der Schnittbereich anpassen soll.' : KEINE_3D),
  async ausfuehren(p) {
    const i = await instanz3d(ansicht3dVon(p));
    if (i) schnittMeldung(await i.schnittbereich.anAuswahl(), 'Schnittbereich an die Auswahl angepasst (Rand 300 mm).');
  },
});
const geschosseSortiert = () => derKlasse('Geschoss').filter((g) => Number.isFinite(g.werte?.kote) && Number.isFinite(g.werte?.hoehe)).sort((a, b) => a.werte.kote - b.werte.kote);
/** Geschoss der Auswahl (das Geschoss selbst oder das des gewählten Elements); sonst null. */
const geschossDerAuswahl = () => { const e = element(zustand.auswahl); return e?.klasse === 'Geschoss' ? e : element(e?.geschoss); };
function geschossWaehlen(x, y) {
  return new Promise((fertig) => kontextmenue(x ?? window.innerWidth / 2, y ?? 140, geschosseSortiert().map((g) => ({
    titel: `${g.name || g.kennzeichen} (OK ${(g.werte.kote / 1000).toFixed(2)} m, Höhe ${(g.werte.hoehe / 1000).toFixed(2)} m)`, aktion: () => fertig(g),
  }))));
}
registrieren({
  id: 'ansicht.schnittbereichGeschoss', titel: 'Auf Geschoss', symbol: 'schnittbereichGeschoss', kuerzel: null, register: 'ansicht', gruppe: '3D-Schnitt', groesse: 'klein',
  aktiv: (p) => !!ansicht3dVon(p) && geschosseSortiert().length > 0,
  hinweis: (p) => (ansicht3dVon(p) ? 'Im Modell gibt es kein Geschoss mit Kote und Höhe.' : KEINE_3D),
  async ausfuehren(p) {
    const a = ansicht3dVon(p);
    const g = element(p?.geschoss) ?? geschossDerAuswahl() ?? await geschossWaehlen(p?.x, p?.y);
    if (!g?.werte) return;
    const i = await instanz3d(a);
    if (i) schnittMeldung(await i.schnittbereich.aufGeschoss(g.werte.kote, g.werte.hoehe), `Schnittbereich auf ${g.name || g.kennzeichen}: von OK ${(g.werte.kote / 1000).toFixed(2)} m bis OK Geschoss darüber.`);
  },
});
registrieren({
  id: 'ansicht.schnittbereichZurueck', titel: 'Zurücksetzen', symbol: 'schnittbereichZurueck', kuerzel: null, register: 'ansicht', gruppe: '3D-Schnitt', groesse: 'klein',
  aktiv: (p) => !!ansicht3dVon(p),
  hinweis: KEINE_3D,
  async ausfuehren(p) {
    const i = await instanz3d(ansicht3dVon(p));
    if (i) schnittMeldung(await i.schnittbereich.zuruecksetzen(), 'Schnittbereich auf das ganze Modell zurückgesetzt.');
  },
});

// N14: «Raum im 3D zeigen» – Kontextmenü im Grundriss bei gewähltem Raum (grundriss.js) und Knopf im Register Ansicht. Der Befehl ansicht_raum_zeigen setzt den
// Schnittbereich der zuletzt benutzten 3D-Ansicht (sonst legt er eine an) auf den Raum plus Rand; danach öffnet die Oberfläche die Ansicht und stellt die Kamera
// schräg von vorne rechts so ein, dass der ganze Bereich im Bild ist. Rand: Benutzereinstellung ansicht3d.raumrand (mm), Vorgabe 1500.
let letzte3d = null;   // Kennzeichen der zuletzt benutzten 3D-Ansicht
abonnieren('ansicht', (kz) => { if (element(kz)?.werte?.art === '3d') letzte3d = element(kz).kennzeichen; });
const raumRand = () => { const n = Number(benutzerWert(RAND_RAUM_SCHLUESSEL, RAND_RAUM)); return Number.isFinite(n) && n >= 0 ? n : RAND_RAUM; };
/** Gewählte Räume (Kennzeichen): p.raeume, sonst das gewählte Element, wenn es ein Raum ist. */
function raeumeVon(p) {
  const liste = (Array.isArray(p?.raeume) ? p.raeume : [element(zustand.auswahl)?.kennzeichen]).map((k) => element(k)).filter((e) => e?.klasse === 'Raum');
  return liste.map((e) => e.kennzeichen);
}
/** Die 3D-Ansicht für «Raum im 3D zeigen»: die zuletzt benutzte, sonst eine offene, sonst irgendeine (nach Kennzeichen); null = der Befehl legt eine an. */
function ansicht3dFuerRaum() {
  const alle = ansichten().filter((a) => a.werte?.art === '3d').sort((a, b) => vergleichen(a.kennzeichen, b.kennzeichen));
  const offen = new Set(fenster.alle().map((o) => o.id));
  return alle.find((a) => a.kennzeichen === letzte3d) ?? alle.find((a) => offen.has(a.id)) ?? alle[0] ?? null;
}
registrieren({
  id: 'ansicht.raum3d', titel: 'Raum im 3D zeigen', symbol: 'raum3d', kuerzel: null, register: 'ansicht', gruppe: '3D-Schnitt',
  aktiv: (p) => raeumeVon(p).length > 0,
  hinweis: 'Zuerst einen Raum wählen (im Grundriss anklicken).',
  async ausfuehren(p) {
    const raeume = raeumeVon(p);
    const ziel = ansicht3dFuerRaum();
    const rand = raumRand();
    const r = await sende('ansicht_raum_zeigen', { raum: raeume, ...(ziel ? { ansicht: ziel.kennzeichen } : {}), rand });
    if (!ergebnisMelden(r, `${raeume.join(', ')} im 3D gezeigt (Rand ${(rand / 1000).toFixed(2).replace(/\.?0+$/, '')} m).`)) return;
    const kz = ziel?.kennzeichen ?? (r.aenderungen ?? []).find((x) => x.klasse === 'Ansicht')?.kennzeichen ?? null;
    if (!kz) return;
    // die Eigenschaft muss in der Ansicht angekommen sein (Ereignis des Servers), bevor die Kamera auf den Bereich zeigt
    for (let i = 0; i < 60 && element(kz)?.werte?.schnittbereich?.aktiv !== true; i++) await new Promise((fertig) => setTimeout(fertig, 50));
    const o = await fenster.ansichtOeffnen(kz);
    letzte3d = kz;
    o?.instanz?.zeigeBereich?.();
  },
});
registrieren({
  id: 'ansicht.raum3dRand', titel: 'Rand für «Raum im 3D zeigen» …', symbol: 'raum3d', kuerzel: null,
  async ausfuehren() {
    const eingegeben = await eingabe('Raum im 3D zeigen', 'Rand um den Raum in mm (alle Richtungen)', String(raumRand()));
    if (eingegeben === null || eingegeben === undefined) return;
    const n = Number(String(eingegeben).replace(',', '.'));
    if (!Number.isFinite(n) || n < 0 || n > 100000) { meldung('Der Rand muss eine Zahl zwischen 0 und 100000 mm sein.', 'warnung'); return; }
    try { await benutzerSetzen(RAND_RAUM_SCHLUESSEL, n); meldung(`Rand für «Raum im 3D zeigen»: ${n} mm (gilt für alle Projekte und Rechner des Benutzers).`, 'ok'); }
    catch (e) { meldung(e.message ?? String(e), 'warnung', { vorschlag: e.vorschlag }); }
  },
});

// N3 (Punkt 3): CSV jeder Listenansicht – Knopf «CSV …» in der Kopfzeile der Liste (ansichten/liste.js) und Eintrag im Kontextmenü der Liste im Projektbrowser.
// Die Aktion öffnet die Liste, wenn sie noch nicht offen ist, und zeigt dann deren Dialog (ansichten/liste-csv-dialog.js).
registrieren({
  id: 'liste.csv', titel: 'CSV …', symbol: 'listeCsv', kuerzel: null,
  aktiv: (p) => ansichtVon(p)?.werte?.art === 'liste', hinweis: 'Zuerst eine Listenansicht öffnen oder im Projektbrowser wählen.',
  async ausfuehren(p) {
    const a = ansichtVon(p);
    let o = fenster.alle().find((x) => x.id === a.id);
    if (!o) { await fenster.ansichtOeffnen(a.kennzeichen); o = fenster.alle().find((x) => x.id === a.id); }
    await o?.instanz?.csv?.();
  },
});

// Aktionen für den Projektbrowser (Kontextmenü auf Ansichten)
registrieren({
  id: 'ansicht.oeffnen', titel: 'Öffnen', symbol: 'oeffnen', kuerzel: null,
  aktiv: hatAnsicht, hinweis: KEINE_ANSICHT,
  ausfuehren: async (p) => { await fenster.ansichtOeffnen(ansichtVon(p).kennzeichen); },
});
