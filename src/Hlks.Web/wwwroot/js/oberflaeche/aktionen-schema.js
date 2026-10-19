// Aktionen der Schema-Ansicht (Modul 3, Teil C2): Schema anlegen (Register Ansicht › Erstellen), Werkzeuge Verbinden, Trennen, Element
// einfügen und Neu anordnen (Register Ansicht › Gruppe «Schema», dieselben Aktionen wie die Leiste in der Schema-Ansicht), «Im Grundriss
// zeigen» und «Im Grundriss platzieren» (Schema → Grundriss), «Im Schema zeigen» (Grundriss und 3D → Schema; Kontextmenü und
// Kontextregister «Ändern | Klasse»). Alles geht über Befehle und Abfragen des Kerns; hier stehen nur Dialoge, Aufrufe und das Öffnen
// und Zoomen der Ansichten. Reine Logik: ansichten/schema-logik.js, Abläufe mit Dialogen: schema-bearbeiten.js.

import { zustand, element, derKlasse, auswaehlen } from '../zustand.js';
import { abfrage } from '../api.js';
import * as fenster from '../fenster.js';
import { registrieren } from './register.js';
import { zeigen, meldung, kontextmenue } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { esc, knoten as dom, gewerkeListe, freiesKennzeichen, freierName, aufElement, anzeigeName, vergleichen } from './werkzeug.js';
import { WERKZEUGE } from '../werkzeuge/optionen.js';
import { werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import * as bearbeiten from './schema-bearbeiten.js';
import * as L from '../ansichten/schema-logik.js';

/** Die aktive Schema-Ansicht (Instanz) oder null. */
export function aktivesSchema() {
  const o = fenster.aktive();
  return o?.instanz?.istSchema ? o.instanz : null;
}
const ziel = (p) => p?.instanz ?? aktivesSchema();
const schemaAnsichten = () => derKlasse('Ansicht').filter((a) => a.werte?.art === 'schema' && a.werte?.vorlage !== true);
const netzElement = (el) => !!el && !!el.rolle;
const KEIN_SCHEMA = 'Zuerst eine Schema-Ansicht öffnen (Projektbrowser › Schemas) oder mit «Schema» anlegen.';

// ---------------------------------------------------------------------------------------------- Schema anlegen

function schemaFormular() {
  const gewerke = gewerkeListe();
  const systeme = derKlasse('System').sort((a, b) => vergleichen(a.kennzeichen, b.kennzeichen));
  const w = dom(`<div class="ansichtform sc-anlegen">
    <label class="fz"><span>Name</span><input type="text" class="f-name"></label>
    <label class="fz"><span>Kennzeichen</span><input type="text" class="f-kz"></label>
    <fieldset class="f-gewerke"><legend>Gewerke</legend><div class="spalten">${gewerke.map(([k, n]) => `<label class="haken"><input type="checkbox" value="${esc(k)}"> ${esc(n)}</label>`).join('')}</div>
      <p class="leise">Ist nichts angekreuzt, zeigt das Schema alle Gewerke.</p></fieldset>
    <fieldset class="f-systeme"><legend>Systeme</legend>${systeme.length === 0 ? '<p class="leise">Das Modell hat noch kein System.</p>'
      : `<div class="spalten">${systeme.map((s) => `<label class="haken"><input type="checkbox" value="${esc(s.kennzeichen)}"> ${esc(s.kennzeichen)}${s.name ? ' – ' + esc(s.name) : ''}</label>`).join('')}</div>`}
      <p class="leise">Ist nichts angekreuzt, zeigt das Schema alle Systeme. Angelegt wird ein Strangschema: eine Spalte je Strang, eine Zeile je Geschoss.</p></fieldset>
  </div>`);
  const q = (s) => w.querySelector(s);
  let kzGeaendert = false, nameGeaendert = false;
  const gewaehlt = () => [...w.querySelectorAll('.f-systeme input:checked')].map((c) => c.value);
  const vorschlag = () => {
    const v = L.schemaVorschlag(gewaehlt());
    if (!kzGeaendert) q('.f-kz').value = freiesKennzeichen(v.kennzeichen);
    if (!nameGeaendert) q('.f-name').value = freierName(v.name, derKlasse('Ansicht').map((a) => a.name || a.kennzeichen));
  };
  q('.f-kz').oninput = () => { kzGeaendert = true; };
  q('.f-name').oninput = () => { nameGeaendert = true; };
  w.querySelectorAll('.f-systeme input').forEach((c) => c.addEventListener('change', vorschlag));
  vorschlag();
  return w;
}

async function schemaAnlegen() {
  const w = schemaFormular();
  const ok = await zeigen({
    titel: 'Schema anlegen', inhalt: w, breite: 520,
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Anlegen', wert: true, primaer: true,
        pruefen: () => !w.querySelector('.f-kz').value.trim() ? 'Das Kennzeichen darf nicht leer sein.' : !w.querySelector('.f-name').value.trim() ? 'Der Name darf nicht leer sein.' : true }],
  });
  if (ok !== true) return;
  const param = L.schemaAnlegenParameter({
    kennzeichen: w.querySelector('.f-kz').value.trim(), name: w.querySelector('.f-name').value.trim(),
    gewerke: [...w.querySelectorAll('.f-gewerke input:checked')].map((c) => c.value),
    systeme: [...w.querySelectorAll('.f-systeme input:checked')].map((c) => c.value),
  });
  const r = await sende('element_anlegen', param);
  if (!ergebnisMelden(r, `Schema «${param.name}» angelegt.`)) return;
  if (await aufElement(param.kennzeichen)) await fenster.ansichtOeffnen(param.kennzeichen);
  else meldung('Die neue Ansicht ist noch nicht eingetroffen.', 'warnung', { vorschlag: 'Im Projektbrowser nachsehen.' });
}

registrieren({ id: 'ansicht.schema', titel: 'Schema', symbol: 'schema', kuerzel: null, register: 'ansicht', gruppe: 'Erstellen', ausfuehren: schemaAnlegen });

// ---------------------------------------------------------------------------------------------- Werkzeuge der Schema-Ansicht

const imSchema = (p) => !!ziel(p);

registrieren({
  id: 'schema.auswahl', titel: 'Ändern', symbol: 'auswaehlen', kuerzel: null,
  aktiv: imSchema, hinweis: KEIN_SCHEMA,
  ausfuehren(p) { ziel(p)?.setzeModus('auswahl'); },
});

registrieren({
  id: 'schema.verbinden', titel: 'Verbinden', symbol: 'schemaVerbinden', kuerzel: null, register: 'ansicht', gruppe: 'Schema',
  gedrueckt: () => aktivesSchema()?.modus === 'verbinden',
  aktiv: imSchema, hinweis: KEIN_SCHEMA,
  ausfuehren(p) {
    const s = ziel(p);
    if (!s) return;
    if (p?.von) s.verbindenVon(p.von); else s.setzeModus(s.modus === 'verbinden' ? 'auswahl' : 'verbinden');
  },
});

registrieren({
  id: 'schema.trennen', titel: 'Trennen', symbol: 'schemaTrennen', kuerzel: null, register: 'ansicht', gruppe: 'Schema',
  gedrueckt: () => aktivesSchema()?.modus === 'trennen',
  aktiv: imSchema, hinweis: KEIN_SCHEMA,
  ausfuehren(p) { const s = ziel(p); s?.setzeModus(s.modus === 'trennen' ? 'auswahl' : 'trennen'); },
});

registrieren({
  id: 'schema.einfuegen', titel: 'Element einfügen', symbol: 'schemaEinfuegen', kuerzel: null, register: 'ansicht', gruppe: 'Schema',
  gedrueckt: () => aktivesSchema()?.modus === 'einfuegen',
  aktiv: imSchema, hinweis: KEIN_SCHEMA,
  async ausfuehren(p) {
    const s = ziel(p);
    if (!s) return;
    if (s.modus === 'einfuegen') { s.setzeModus('auswahl'); return; }
    const wahl = await bearbeiten.einfuegenDialog();
    if (wahl) s.setzeModus('einfuegen', { wahl });
  },
});

registrieren({
  id: 'schema.neu-anordnen', titel: 'Neu anordnen', symbol: 'schemaAnordnen', kuerzel: null, register: 'ansicht', gruppe: 'Schema',
  aktiv: imSchema, hinweis: KEIN_SCHEMA,
  async ausfuehren(p) { await ziel(p)?.neuAnordnen(); },
});

registrieren({
  id: 'schema.zoom-alles', titel: 'Zoom alles', symbol: 'zoomAlle', kuerzel: null,
  aktiv: imSchema, hinweis: KEIN_SCHEMA,
  ausfuehren(p) { ziel(p)?.zoomAlles(); },
});

registrieren({
  id: 'schema.lage-zuruecksetzen', titel: 'Automatische Lage', symbol: 'schema', kuerzel: null,
  aktiv: imSchema, hinweis: KEIN_SCHEMA,
  async ausfuehren(p) { const s = ziel(p); if (s && p?.element) await bearbeiten.lageZuruecksetzen(s.kennzeichen, p.element); },
});

registrieren({
  id: 'schema.trennen-verbindung', titel: 'Verbindung trennen', symbol: 'schemaTrennen', kuerzel: null,
  async ausfuehren(p) { if (p?.verbindung) await bearbeiten.trennenVerbindung(p.verbindung); },
});

// ---------------------------------------------------------------------------------------------- Schema ↔ Grundriss

/** Öffnet den Grundriss des Geschosses (oder holt ihn nach vorn). null, wenn es keinen gibt (Meldung ist gezeigt). */
async function grundrissOeffnen(el) {
  const offen = fenster.alle().map((o) => element(o.id)?.kennzeichen).filter(Boolean);
  const w = L.grundrissFuer(derKlasse('Ansicht'), el, offen);
  if (!w) {
    meldung(`Es gibt keinen Grundriss für das Geschoss ${el.geschoss ?? '–'}.`, 'warnung', { vorschlag: 'Register Ansicht › Grundriss anlegen, dann nochmals.' });
    return null;
  }
  const o = await fenster.ansichtOeffnen(w.ansicht.kennzeichen);
  if (!w.sichtbar) meldung(`Der Grundriss «${anzeigeName(w.ansicht)}» blendet ${el.kennzeichen} aus (Gewerke oder Klassen der Ansicht).`, 'warnung', { vorschlag: 'Register Ansicht › Sichtbarkeit/Grafiken.' });
  return { ...w, offen: o };
}

async function imGrundrissZeigen(p) {
  const el = element(p?.element ?? zustand.auswahl);
  if (!el) return;
  if (!el.geometrie) {
    meldung(`${el.kennzeichen} ist nicht platziert (steht nur im Schema).`, 'info', { vorschlag: 'Im Schema: Rechtsklick › «Im Grundriss platzieren …».' });
    return;
  }
  const g = await grundrissOeffnen(el);
  if (!g) return;
  const b = L.grundrissAusschnitt(el);
  if (b) g.offen?.instanz?.zoomAuf?.(b);
  auswaehlen(el.id);
}

registrieren({
  id: 'schema.im-grundriss-zeigen', titel: 'Im Grundriss zeigen', symbol: 'grundriss', kuerzel: null,
  aktiv: (p) => { const el = element(p?.element ?? zustand.auswahl); return !!el?.geometrie && !!el.geschoss; },
  hinweis: (p) => { const el = element(p?.element ?? zustand.auswahl); return el && !el.geometrie ? `${el.kennzeichen} ist nicht platziert (steht nur im Schema).` : 'Zuerst ein platziertes Element wählen.'; },
  ausfuehren: imGrundrissZeigen,
});

WERKZEUGE['schema-platzieren'] ??= { titel: 'Im Grundriss platzieren', felder: [], hinweis: 'Punkt anklicken, an dem das Element im Grundriss stehen soll. Esc beendet.' };

async function imGrundrissPlatzieren(p) {
  const el = element(p?.element ?? zustand.auswahl);
  if (!el) return;
  if (el.geometrie) { meldung(`${el.kennzeichen} ist schon platziert.`, 'info'); return; }
  if (!el.geschoss) { meldung(`${el.kennzeichen} hat kein Geschoss.`, 'warnung', { vorschlag: 'Zuerst in der Eigenschaftenpalette das Geschoss setzen.' }); return; }
  let def = null;
  try { def = await abfrage('katalog', { klasse: el.klasse }); } catch { /* der Kern sagt es beim Platzieren */ }
  if (def && !L.istPunktklasse(def)) {
    meldung(`${el.kennzeichen} (${el.klasse}) wird im Grundriss mit dem Leitungswerkzeug gezeichnet.`, 'info', { vorschlag: 'Das Leitungswerkzeug folgt in Modul 3 (Teil C1); bis dahin leitung_zeichnen bzw. element_aendern mit geometrie.' });
    return;
  }
  const g = await grundrissOeffnen(el);
  if (!g) return;
  const kz = el.kennzeichen;
  werkzeugStarten({
    id: 'schema-platzieren', titel: 'Im Grundriss platzieren', planarten: ['grundriss'],
    neu: () => ({}),
    hinweis: () => `${kz}: Punkt anklicken`,
    async klick(z, fang, ctx) {
      const r = await ctx.befehl('element_aendern', L.platzierenParameter(kz, fang.punkt), `${kz} im Grundriss platziert.`);
      if (r?.angenommen) ctx.beenden();
    },
    ende() { /* nichts aufzuräumen */ },
  });
}

registrieren({
  id: 'schema.im-grundriss-platzieren', titel: 'Im Grundriss platzieren', symbol: 'grundriss', kuerzel: null,
  aktiv: (p) => { const el = element(p?.element ?? zustand.auswahl); return !!el && !el.geometrie && !!el.rolle; },
  hinweis: 'Ein nicht platziertes Element wählen.',
  ausfuehren: imGrundrissPlatzieren,
});

// ---------------------------------------------------------------------------------------------- Grundriss/3D → Schema

/** Welche Schema-Ansichten zeigen das Element? [{ ansicht (Kennzeichen), name, wo }] – offene zuerst. */
async function schemaTreffer(el) {
  const offen = fenster.alle().map((o) => element(o.id)?.kennzeichen);
  const liste = schemaAnsichten().sort((a, b) => (offen.includes(b.kennzeichen) ? 1 : 0) - (offen.includes(a.kennzeichen) ? 1 : 0) || vergleichen(a.kennzeichen, b.kennzeichen));
  const aus = await Promise.all(liste.map(async (a) => {
    try { const wo = L.zeigtElement(await abfrage('schema', { ansicht: a.kennzeichen }), el.id); return wo ? { ansicht: a.kennzeichen, name: anzeigeName(a), wo } : null; }
    catch { return null; }
  }));
  return { treffer: aus.filter(Boolean), anzahl: liste.length };
}

async function imSchemaZeigen(p) {
  const el = element(p?.element ?? zustand.auswahl);
  if (!el) return;
  let wahl = p?.ansicht ? { ansicht: p.ansicht } : null;
  if (!wahl) {
    const { treffer, anzahl } = await schemaTreffer(el);
    wahl = treffer[0];
    if (!wahl) {
      meldung(anzahl === 0 ? 'Es gibt keine Schema-Ansicht.' : `Keine Schema-Ansicht zeigt ${el.kennzeichen}.`, 'info',
        { vorschlag: anzahl === 0 ? 'Register Ansicht › Schema.' : 'Gewerke, Systeme und ausgeblendete Klassen der Schema-Ansicht prüfen.' });
      return;
    }
  }
  const o = await fenster.ansichtOeffnen(wahl.ansicht);
  o?.instanz?.zeigen?.(el.id);
}

registrieren({
  id: 'schema.im-schema-zeigen', titel: 'Im Schema zeigen', symbol: 'schema', kuerzel: null,
  aktiv: (p) => netzElement(element(p?.element ?? zustand.auswahl)) && schemaAnsichten().length > 0,
  hinweis: (p) => (netzElement(element(p?.element ?? zustand.auswahl)) ? 'Es gibt keine Schema-Ansicht (Register Ansicht › Schema).' : 'Im Schema stehen nur Netzelemente (Endgeräte, Geräte, Armaturen, Formteile, Leitungen).'),
  ausfuehren: imSchemaZeigen,
});

// Kontextregister «Ändern | <Klasse>»: dieselben Wege, ohne Menü
registrieren({
  id: 'kontext.im-schema-zeigen', titel: 'Im Schema zeigen', symbol: 'schema', kuerzel: null, register: 'kontext', gruppe: 'Schema',
  aktiv: (p) => netzElement(element(p?.element ?? zustand.auswahl)) && schemaAnsichten().length > 0,
  hinweis: (p) => (netzElement(element(p?.element ?? zustand.auswahl)) ? 'Es gibt keine Schema-Ansicht (Register Ansicht › Schema).' : 'Im Schema stehen nur Netzelemente.'),
  ausfuehren: imSchemaZeigen,
});
registrieren({
  id: 'kontext.im-grundriss-zeigen', titel: 'Im Grundriss zeigen', symbol: 'grundriss', kuerzel: null, register: 'kontext', gruppe: 'Schema',
  aktiv: (p) => { const el = element(p?.element ?? zustand.auswahl); return !!el?.geometrie && !!el.geschoss && el.klasse !== 'Ansicht'; },
  hinweis: (p) => { const el = element(p?.element ?? zustand.auswahl); return el && !el.geometrie ? `${el.kennzeichen} ist nicht platziert (steht nur im Schema).` : 'Zuerst ein platziertes Element wählen.'; },
  ausfuehren: imGrundrissZeigen,
});

/** Kontextmenü im Grundriss (Element unter dem Zeiger) und im 3D (gewähltes Element): «Im Schema zeigen». */
async function menueImPlan(x, y, el) {
  const { treffer, anzahl } = await schemaTreffer(el);
  const eintraege = L.grundrissMenue({ element: el, treffer, schemaAnzahl: anzahl }).map((m) => ({
    titel: m.titel, aktiv: m.aktiv !== false, hinweis: m.hinweis, aktion: () => imSchemaZeigen(m.param),
  }));
  kontextmenue(x, y, eintraege);
}

if (typeof document !== 'undefined') {
  // Im 3D verschiebt die rechte Taste die Ansicht: nur ein Klick (kaum Bewegung zwischen Drücken und Loslassen) öffnet das Menü
  let rechtsGedrueckt = null;
  document.addEventListener('pointerdown', (e) => { if (e.button === 2) rechtsGedrueckt = { x: e.clientX, y: e.clientY }; }, true);
  document.addEventListener('contextmenu', (e) => {
    const t = e.target;
    if (!(t instanceof Element) || werkzeugAktiv()) return;
    if (rechtsGedrueckt && Math.hypot(e.clientX - rechtsGedrueckt.x, e.clientY - rechtsGedrueckt.y) > 5) return;
    const grundriss = t.closest('.gr-svg:not(.sc-svg)');
    const dreiD = t.closest('.d3-wurzel');
    if (!grundriss && !dreiD) return;
    const id = grundriss ? t.closest('[data-id]')?.getAttribute('data-id') : zustand.auswahl;
    const el = id ? element(id) : null;
    if (!netzElement(el)) return;
    e.preventDefault();
    if (grundriss) auswaehlen(el.id);
    menueImPlan(e.clientX, e.clientY, el).catch((err) => console.error('Menü «Im Schema zeigen»', err));
  }, true);
}
