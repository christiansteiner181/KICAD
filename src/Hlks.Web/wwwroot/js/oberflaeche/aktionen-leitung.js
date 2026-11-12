// Aktionen für Leitungen (Modul 3, Teil C1): Gruppe «Leitungen» mit Rohr (PI, Register Heizung und Sanitär) und Kanal (DT, Register Lüftung), «Aufteilen mit Produktlinie …»
// (Kontextregister und Palette, nur für Leitungen ohne Serie) und die Listenansicht «Materialliste» (Register Ansicht › Erstellen,
// Kontextmenü auf «Listen» im Projektbrowser). Keine Fachlogik: alles geht über Befehle und Abfragen des Kerns
// (leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse, leitung_serie, materialliste, element_anlegen).

import { registrieren } from './register.js';
import { zustand, element, derKlasse } from '../zustand.js';
import { ansichtOeffnen } from '../fenster.js';
import { meldung, zeigen } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { aufElement, freiesKennzeichen, knoten, esc } from './werkzeug.js';
import { auswahlElemente } from '../werkzeuge/auswahl.js';
import { planAnsicht, werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import { leitungWerkzeug } from '../werkzeuge/leitung.js';
import { daemmDicke } from '../werkzeuge/optionen.js';
import { parallelWerkzeug } from '../werkzeuge/parallel.js';
import { fallleitungWerkzeug } from '../werkzeuge/fallleitung.js';
import { steigleitungWerkzeug } from '../werkzeuge/steigleitung.js';
import { aufteilenDialog } from '../werkzeuge/leitung-bearbeiten.js';
import { istAltbestand } from '../werkzeuge/leitung-logik.js';

/** Nur bei Leitungen im Kontextregister zeigen (menueband.js fuer). */
const istLeitungEl = (el) => ['Rohr', 'Kanal'].includes(el?.klasse);

const projektOffen = () => !!zustand.status?.offen;
const imGrundriss = () => projektOffen() && planAnsicht()?.art === 'grundriss';
const nurGrundriss = () => (!projektOffen() ? 'Zuerst ein Projekt öffnen.' : planAnsicht() ? 'Geht nur in einem Grundriss.' : 'Zuerst einen Grundriss öffnen und aktivieren.');

// ---------------------------------------------------------------------------------------------- Rohr und Kanal zeichnen

// Register je Gewerk (04.11.2026): Rohr unter Heizung und Sanitär, Kanal unter Lüftung
for (const [id, titel, symbol, kuerzel, kategorie, wid, register] of [
  ['gt.rohr', 'Rohr', 'rohr', 'PI', 'Rohr', 'rohr', ['heizung', 'sanitaer']],
  ['gt.kanal', 'Kanal', 'kanal', 'DT', 'Kanal', 'kanal', ['lueftung']],
]) {
  registrieren({
    id, titel, symbol, kuerzel, register, gruppe: 'Leitungen',
    aktiv: imGrundriss, hinweis: nurGrundriss,
    gedrueckt: () => werkzeugAktiv() === wid,
    ausfuehren() { werkzeugStarten(leitungWerkzeug(kategorie)); },
  });
}

// Sanitär: Abwasser- und Versorgungsleitung (Christian 09.11.2026: «bei sanitär braucht es zudem eine funktion abwasserleitung und Versorgungsleitung»)
// – Rohr mit Systemauswahl nur Abwasser bzw. nur Versorgung (Druck) und eigenem Gedächtnis für System und Produktlinie
for (const [id, titel, variante] of [['sa.abwasser', 'Abwasserleitung', 'abwasser'], ['sa.versorgung', 'Versorgungsleitung', 'versorgung']]) {
  registrieren({
    id, titel, symbol: variante, kuerzel: null, register: 'sanitaer', gruppe: 'Leitungen',
    aktiv: imGrundriss, hinweis: nurGrundriss,
    gedrueckt: () => werkzeugAktiv() === variante,
    ausfuehren() { werkzeugStarten(leitungWerkzeug('Rohr', variante)); },
  });
}

// Fluss- und Gefällsrichtung umkehren (Christian 09.11.2026: «bei abläufen muss die fluss und gefällsrichtung angezeigt und invertiert werden können»):
// Kontextregister einer gewählten Leitung; Befehl fliessrichtung_umkehren (ganzer Zug, Gefälle in die neue Richtung)
const leitungZumUmkehren = () => { const el = element(zustand.auswahl); return el && ['Rohr', 'Kanal'].includes(el.klasse) ? el : null; };
registrieren({
  id: 'kontext.fliess-umkehren', fuer: istLeitungEl, titel: 'Fliessrichtung umkehren', symbol: 'netzAuf', kuerzel: null, register: 'kontext', gruppe: 'Netz',
  aktiv: () => !!leitungZumUmkehren(), hinweis: 'Zuerst eine Leitung auswählen.',
  async ausfuehren() { const el = leitungZumUmkehren(); if (el) ergebnisMelden(await sende('fliessrichtung_umkehren', { leitung: el.kennzeichen })); },
});

// ---------------------------------------------------------------------------------------------- Dämmung und Neigung (Kontextregister)
// Christian 11.11.2026: «ist eine leitung angewählt soll der tab "ändern" aktiv sein», «in ändern werden auch dinge wie isolation angezeigt. richte dich
// dabei an der revit basis» – wie Revit «Ändern | Rohre»: Dämmung hinzufügen, Dämmung bearbeiten, Dämmung entfernen, Neigung. Gilt für alle gewählten
// Leitungen (Mehrfachauswahl in einem Paket); die Formteile dazwischen bekommen die Dämmung ihrer Leitungen (DaemmGrafik).
const gewaehlteLeitungen = () => auswahlElemente().filter((e) => ['Rohr', 'Kanal'].includes(e.klasse));
const dicke = (e) => Number(e.werte?.daemmdicke) || 0;
const gedaemmt = () => gewaehlteLeitungen().some((e) => dicke(e) > 0);

async function daemmungSetzen(leitungen, eig) {
  const kz = leitungen.map((e) => e.kennzeichen);
  const r = kz.length === 1 ? await sende('element_aendern', { element: kz[0], eigenschaften: eig })
    : await sende('elemente_aendern', { elemente: kz, eigenschaften: eig });
  ergebnisMelden(r, eig.daemmdicke ? `Dämmung ${eig.daemmdicke} mm an ${kz.length} Leitung(en).` : `Dämmung an ${kz.length} Leitung(en) entfernt.`);
}

/** Dialog «Dämmung hinzufügen» bzw. «Dämmung bearbeiten» (wie Revit): Art bzw. Produkt und Dicke. Vorgabe: Werte der ersten gedämmten Leitung,
 *  sonst die Dämmdicke ihres Systems. */
async function daemmungDialog(bearbeiten) {
  const l = gewaehlteLeitungen();
  if (!l.length) return;
  const muster = l.find((e) => dicke(e) > 0) ?? l[0];
  const sys = element(muster.system);
  const vorDicke = dicke(muster) || Number(sys?.werte?.daemmdicke) || '';
  // Dämmarten aus dem Katalog (Christian 12.11.2026: «Dämmingsarten sollten auswählbar sein», «Ja, Katalog»); «andere …» für eine freie Angabe
  const arten = zustand.katalog?.daemmarten ?? [];
  const bisher = muster.werte?.daemmung ?? '';
  const imKatalog = arten.some((a) => a.name === bisher);
  const w = knoten(`<div>
    <label class="fz"><span>Dämmart</span><select class="d-wahl">
      ${arten.map((a) => `<option value="${esc(a.name)}"${a.name === bisher || (!bisher && a === arten[0]) ? ' selected' : ''}>${esc(a.name)}</option>`).join('')}
      <option value=""${bisher && !imKatalog ? ' selected' : ''}>andere …</option></select></label>
    <label class="fz d-frei-zeile"${bisher && !imKatalog ? '' : ' style="display:none"'}><span>Art bzw. Produkt</span><input type="text" class="d-art" value="${esc(imKatalog ? '' : bisher)}" placeholder="z. B. Produktname des Herstellers"></label>
    <label class="fz"><span>Dämmdicke [mm]</span><input type="text" class="d-dicke" inputmode="decimal" value="${esc(String(vorDicke))}"></label>
    <p class="leise d-regel"></p>
    <p class="leise">${l.length} Leitung(en) gewählt. Die Dicke nach Energiegesetz (MuKEn 2014) gibt die λ-Klasse der Dämmart vor, sonst die Dämmregel des Systems (Koordination › Anlagen und Systeme).</p></div>`);
  const wahl = w.querySelector('.d-wahl');
  // Dicke vorschlagen: Regel der Dämmart (λ-Klasse), sonst die des Systems, mit der DN der ersten Leitung
  const dn = Number(muster.werte?.dn) || Number(/DN\s*(\d+)/i.exec(String(muster.werte?.familientyp ?? ''))?.[1]) || 0;
  // beim Hinzufügen gleich die Dicke vorschlagen, beim Bearbeiten erst, wenn die Art gewechselt wird (die bisherige Dicke bleibt sonst)
  const vorschlag = (gewechselt = false) => {
    w.querySelector('.d-frei-zeile').style.display = wahl.value !== '' ? 'none' : '';   // .fz setzt display, «hidden» griffe nicht
    const art = arten.find((a) => a.name === wahl.value);
    const regeln = zustand.katalog?.daemmregeln ?? [];
    const regel = regeln.find((r) => r.name === art?.regel) ?? regeln.find((r) => r.name === sys?.werte?.daemmregel);
    const d = regel ? daemmDicke(regel, dn) : null;
    w.querySelector('.d-regel').textContent = art && !art.regel ? `λ-Klasse der Dämmart offen (Herstellerangabe fehlt)${regel ? ' – es gilt die Regel des Systems.' : '.'}`
      : regel ? `Regel: ${regel.name}${d ? `, DN ${dn} → ${d} mm` : ''}.` : '';
    if (d && (!bearbeiten || gewechselt)) w.querySelector('.d-dicke').value = String(d);
  };
  wahl.addEventListener('change', () => vorschlag(true));
  vorschlag();
  const ok = await zeigen({
    titel: bearbeiten ? 'Dämmung bearbeiten' : 'Dämmung hinzufügen', inhalt: w, breite: 440,
    knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Übernehmen', wert: true, primaer: true,
      pruefen: () => { const n = Number(w.querySelector('.d-dicke').value.replace(',', '.')); return Number.isFinite(n) && Math.round(n) >= 1 && n <= 500 ? true : 'Dämmdicke in ganzen mm eingeben (1 bis 500).'; } }],   // gespeichert wird gerundet: 0.4 ergäbe 0 = «entfernt»
  });
  if (ok !== true) return;
  const art = wahl.value || w.querySelector('.d-art').value.trim();
  await daemmungSetzen(l, { daemmdicke: Math.round(Number(w.querySelector('.d-dicke').value.replace(',', '.'))), daemmung: art || null });
}

registrieren({
  id: 'kontext.daemmung-hinzufuegen', fuer: istLeitungEl, titel: 'Dämmung hinzufügen', symbol: 'daemmung', kuerzel: null, register: 'kontext', gruppe: 'Dämmung',
  aktiv: () => gewaehlteLeitungen().length > 0, hinweis: 'Zuerst eine Leitung auswählen.', ausfuehren: () => daemmungDialog(false),
});
registrieren({
  id: 'kontext.daemmung-bearbeiten', fuer: istLeitungEl, titel: 'Dämmung bearbeiten', symbol: 'daemmungBearbeiten', kuerzel: null, register: 'kontext', gruppe: 'Dämmung',
  aktiv: gedaemmt, hinweis: 'Die gewählten Leitungen haben keine Dämmung.', ausfuehren: () => daemmungDialog(true),
});
registrieren({
  id: 'kontext.daemmung-entfernen', fuer: istLeitungEl, titel: 'Dämmung entfernen', symbol: 'daemmungEntfernen', kuerzel: null, register: 'kontext', gruppe: 'Dämmung',
  aktiv: gedaemmt, hinweis: 'Die gewählten Leitungen haben keine Dämmung.',
  ausfuehren: () => daemmungSetzen(gewaehlteLeitungen().filter((e) => dicke(e) > 0), { daemmdicke: null, daemmung: null }),
});

// Neigung (Revit «Neigung»): Gefälle des ganzen Leitungszugs der gewählten Leitung (leitung_gefaelle, zug)
registrieren({
  id: 'kontext.neigung', fuer: istLeitungEl, titel: 'Neigung', symbol: 'neigung', kuerzel: null, register: 'kontext', gruppe: 'Leitung',
  aktiv: () => gewaehlteLeitungen().length === 1, hinweis: 'Genau eine Leitung auswählen.',
  async ausfuehren() {
    const el = gewaehlteLeitungen()[0];
    const w = knoten(`<div><label class="fz"><span>Gefälle [%]</span><input type="text" class="n-g" inputmode="decimal" value="${esc(String(el.werte?.gefaelle ?? ''))}" placeholder="z. B. 2"></label>
      <p class="leise">Neigt den ganzen Leitungszug in Fliessrichtung; 0 = waagrecht. Bögen wandern mit.</p></div>`);
    const ok = await zeigen({ titel: `Neigung – ${el.kennzeichen}`, inhalt: w, breite: 400, knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Übernehmen', wert: true, primaer: true,
      pruefen: () => (Number.isFinite(Number(w.querySelector('.n-g').value.replace(',', '.'))) ? true : 'Gefälle in % eingeben.') }] });
    if (ok !== true) return;
    ergebnisMelden(await sende('leitung_gefaelle', { leitung: el.kennzeichen, gefaelle: Number(w.querySelector('.n-g').value.replace(',', '.')), zug: true }));
  },
});

// ---------------------------------------------------------------------------------------------- Fallleitung (06.11.2026)

registrieren({
  id: 'sa.fallleitung', titel: 'Fallleitung', symbol: 'fallleitung', kuerzel: null, register: 'sanitaer', gruppe: 'Leitungen',
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'fallleitung',
  ausfuehren() { werkzeugStarten(fallleitungWerkzeug()); },
});

registrieren({
  id: 'gt.steigleitung', titel: 'Steigleitung', symbol: 'steigleitung', kuerzel: null, register: ['sanitaer', 'heizung'], gruppe: 'Leitungen',
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'steigleitung',
  ausfuehren() { werkzeugStarten(steigleitungWerkzeug()); },
});

registrieren({
  id: 'lu.steigleitung', titel: 'Steigleitung', symbol: 'steigleitungLueftung', kuerzel: null, register: 'lueftung', gruppe: 'Leitungen',   // AUL, FOL, ZUL, ABL (06.11.2026)
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'steigleitungLueftung',
  ausfuehren() { werkzeugStarten(steigleitungWerkzeug('steigleitungLueftung')); },
});

// ---------------------------------------------------------------------------------------------- Parallele Leitungen (Bearbeitungswerkzeuge B2)

registrieren({
  id: 'gt.parallel', titel: 'Parallele Leitungen', symbol: 'trasse', kuerzel: null, register: ['heizung', 'lueftung', 'sanitaer'], gruppe: 'Leitungen',
  aktiv: imGrundriss, hinweis: nurGrundriss,
  gedrueckt: () => werkzeugAktiv() === 'parallel',
  ausfuehren() { werkzeugStarten(parallelWerkzeug()); },
});

// ---------------------------------------------------------------------------------------------- Aufteilen mit Produktlinie

const gewaehlteLeitung = (p) => element(p?.leitung ?? p?.element ?? zustand.auswahl);

const aufteilenAktiv = (p) => istAltbestand(gewaehlteLeitung(p));
const aufteilenHinweis = (p) => {
  const el = gewaehlteLeitung(p);
  if (!el || !['Rohr', 'Kanal'].includes(el.klasse)) return 'Zuerst eine Leitung (Rohr oder Kanal) wählen.';
  return `${el.kennzeichen} hat schon eine Produktlinie.`;
};

registrieren({
  id: 'leitung.aufteilen', titel: 'Aufteilen mit Produktlinie …', symbol: 'rohr', kuerzel: null,
  aktiv: aufteilenAktiv, hinweis: aufteilenHinweis,
  ausfuehren: (p) => aufteilenDialog(gewaehlteLeitung(p).kennzeichen),
});
registrieren({
  id: 'kontext.leitung-aufteilen', fuer: istLeitungEl, titel: 'Aufteilen mit Produktlinie …', symbol: 'rohr', kuerzel: null, register: 'kontext', gruppe: 'Leitung',
  aktiv: aufteilenAktiv, hinweis: aufteilenHinweis,
  ausfuehren: (p) => aufteilenDialog(gewaehlteLeitung(p).kennzeichen),
});

// ---------------------------------------------------------------------------------------------- Materialliste

/** Öffnet die Listenansicht «Materialliste» (Ansicht art liste, liste materialliste); beim ersten Mal wird sie angelegt. */
export async function materiallisteOeffnen() {
  let a = derKlasse('Ansicht').find((x) => x.werte?.art === 'liste' && x.werte?.liste === 'materialliste' && x.werte?.vorlage !== true);
  if (!a) {
    const kz = freiesKennzeichen('AN-LI-MATERIALLISTE');
    const r = await sende('element_anlegen', { klasse: 'Ansicht', kennzeichen: kz, name: 'Materialliste', eigenschaften: { art: 'liste', liste: 'materialliste' } });
    if (!r.angenommen) { meldung(r.fehler || r.meldung || 'Die Liste konnte nicht angelegt werden.', 'warnung', { vorschlag: r.vorschlag }); return; }
    a = await aufElement(kz);
  }
  if (a) await ansichtOeffnen(a.kennzeichen);
  else meldung('Die neue Ansicht ist noch nicht eingetroffen.', 'warnung', { vorschlag: 'Im Projektbrowser unter «Listen» nachsehen.' });
}

registrieren({
  id: 'ansicht.materialliste', titel: 'Material​liste', symbol: 'liste', kuerzel: null, register: 'ansicht', gruppe: 'Erstellen',
  aktiv: projektOffen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: materiallisteOeffnen,
});

// ---------------------------------------------------------------------------------------------- Fliessrichtung aus der Quelle

// Christian 02.11.2026: Fliessrichtung aus dem Pumpeneinbau bzw. dem Lüftungsgerät, auf Befehl (fliessrichtung_aus_quelle, ein Paket).
const QUELLEN = ['Pumpe', 'Lueftungsgeraet'];
const gewaehlteQuelle = (p) => element(p?.element ?? zustand.auswahl);
registrieren({
  id: 'kontext.fliessrichtung', fuer: (el) => QUELLEN.includes(el?.klasse), titel: 'Fliessrichtung ab hier', symbol: 'netzAb', kuerzel: null, register: 'kontext', gruppe: 'Netz',
  aktiv: (p) => QUELLEN.includes(gewaehlteQuelle(p)?.klasse),
  hinweis: 'Zuerst eine Pumpe oder ein Lüftungsgerät wählen.',
  async ausfuehren(p) {
    const r = await sende('fliessrichtung_aus_quelle', { quelle: gewaehlteQuelle(p).kennzeichen });
    ergebnisMelden(r, (r.hinweise ?? []).join(' ') || null);
  },
});
