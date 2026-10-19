// Aktionen des Registers «Ansicht»: Ansichten anlegen (Grundriss, 3D, Liste), duplizieren, Sichtbarkeit/Grafiken (VV),
// Ansichtsvorlagen speichern und anwenden, Fensteranordnung (WT, WC, eigenes Fenster), Zoom (ZF, ZA), Design hell/dunkel.
// Eine Ansicht ist ein Element der Klasse Ansicht; alles läuft über element_anlegen / element_aendern.

import { zustand, element, derKlasse } from '../zustand.js';
import * as fenster from '../fenster.js';
import { registrieren } from './register.js';
import { zeigen, meldung, eingabe } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { design, designUmschalten } from './design.js';
import { esc, knoten, vergleichen, gewerkeListe, aufElement, freiesKennzeichen, freierName, anzeigeName } from './werkzeug.js';

const MASSSTAEBE = [10, 20, 50, 100, 200, 500];
const DETAILGRADE = ['grob', 'mittel', 'fein'];
// Modul 3 (C3): Materialliste (Kennzeichen AN-LI-MATERIALLISTE wie die Aktion «Materialliste» in aktionen-leitung.js)
const LISTEN = { raumliste: ['Raumliste', 'RAUM'], strangliste: ['Strangliste', 'STRANG'], mengen: ['Mengen', 'MENGEN'], materialliste: ['Materialliste', 'MATERIALLISTE'] };
/** Einstellungen, die eine Ansichtsvorlage überträgt (Art und Geschoss bleiben bei der Ansicht). */
const VORLAGE_EINSTELLUNGEN = ['massstab', 'schnitthoehe', 'detailgrad', 'gewerke', 'klassenAusblenden'];
const NUR_MIT_VORLAGE_LEER = ['gewerke', 'klassenAusblenden'];
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
    else { const l = LISTEN[q('.f-liste')?.value ?? 'raumliste']; kzBasis = `AN-LI-${l[1]}`; nameBasis = l[0]; }
    if (!kzGeaendert) q('.f-kz').value = freiesKennzeichen(kzBasis);
    if (!nameGeaendert) q('.f-name').value = freierName(nameBasis, ansichten().map((a) => a.name || a.kennzeichen));
  };
  q('.f-kz').oninput = () => { kzGeaendert = true; };
  q('.f-name').oninput = () => { nameGeaendert = true; };
  q('.f-geschoss')?.addEventListener('change', vorschlag);
  q('.f-liste')?.addEventListener('change', vorschlag);
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
  if (art === 'liste') eig.liste = q('.f-liste').value;
  else {
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
  const w = knoten(`<div class="vv">
    <p class="dlg-text">Ansicht: <b>${esc(anzeigeName(a))}</b> (${esc(a.kennzeichen)})</p>
    <fieldset><legend>Gewerke anzeigen</legend><div class="spalten">${gewerkeListe().map(([k, n]) => `<label class="haken"><input type="checkbox" class="v-gewerk" value="${esc(k)}" ${aktuellG.has(k) ? 'checked' : ''}> ${esc(n)}</label>`).join('')}</div>
      <p class="leise">Ist nichts angekreuzt, zeigt die Ansicht alle Gewerke.</p></fieldset>
    <fieldset><legend>Klassen ausblenden</legend>
      ${basen.length ? `<div class="v-unter">Basisklassen</div><div class="spalten">${basen.map((b) => haken(b, aus.has(b), 'v-klasse')).join('')}</div>` : ''}
      <div class="v-unter">Klassen</div><div class="spalten gross">${klassen.map((k) => haken(k, aus.has(k), 'v-klasse')).join('')}</div></fieldset></div>`);
  const ok = await zeigen({ titel: 'Sichtbarkeit/Grafiken', inhalt: w, breite: 640, klasse: 'dlg-hoch', knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'OK', wert: true, primaer: true }] });
  if (ok !== true) return;
  const g = [...w.querySelectorAll('.v-gewerk:checked')].map((c) => c.value);
  const k = [...w.querySelectorAll('.v-klasse:checked')].map((c) => c.value);
  const r = await sende('element_aendern', { element: a.kennzeichen, eigenschaften: { gewerke: g.length ? g : null, klassenAusblenden: k.length ? k : null } });
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

// Aktionen für den Projektbrowser (Kontextmenü auf Ansichten)
registrieren({
  id: 'ansicht.oeffnen', titel: 'Öffnen', symbol: 'oeffnen', kuerzel: null,
  aktiv: hatAnsicht, hinweis: KEINE_ANSICHT,
  ausfuehren: async (p) => { await fenster.ansichtOeffnen(ansichtVon(p).kennzeichen); },
});
