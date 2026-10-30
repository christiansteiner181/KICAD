// Aktionen der Familien (Modul 2, C1): Kontextregister «Familie» im Menüband (Gruppen Parameter, Typen, Körper, Anschlüsse, Sinnbild, Ansicht,
// Prüfen – sichtbar, solange ein Familieneditor aktiv ist), Platzieren (Register Gebäudetechnik, Kürzel CM) und die Aktionen des Projektbrowsers
// (Bearbeiten, Platzieren, Neue Familie, Familie laden). Keine Fachlogik: alles geht über die Befehle familie_entwurf, familie_aendern,
// familie_laden, familie_platzieren und die Abfragen familien, familie, familie_ansicht des Kerns.

import { registrieren } from '../oberflaeche/register.js';
import { zustand, element, derKlasse, auswaehlen, merken } from '../zustand.js';
import { ansichtOeffnen } from '../fenster.js';
import { zeigen, meldung } from '../oberflaeche/dialoge.js';
import { sende } from '../oberflaeche/befehle.js';
import { knoten, aufElement, esc } from '../oberflaeche/werkzeug.js';
import { planAnsicht, werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import { aktiverEditor } from './aktiv.js';
import { familienListe, familienLaden } from './daten.js';
import { familienKategorien, familieEntwurfMinimal, neueFamiliePruefen, kernFehler, leitungsArt, leitungFormen } from './familien-text.js';
import { ladenDialog } from './bibliothek-dialoge.js';
import { typSetzen, platzierWerkzeug } from './platzieren.js';

const KEIN_EDITOR = 'Zuerst eine Familie im Projektbrowser doppelklicken (Familieneditor).';
const imEditor = () => !!aktiverEditor();
const projektOffen = () => !!zustand.status?.offen;

// ================================================================================================ Register «Familie»

function fam(def) {
  registrieren({
    register: 'familie', aktiv: imEditor, hinweis: KEIN_EDITOR,
    ausfuehren: () => aktiverEditor()?.aktion(def.aktion, def.param),
    ...def,
  });
}

fam({ id: 'familie.parameter-neu', titel: 'Parameter hinzufügen', symbol: 'famParameterNeu', gruppe: 'Parameter', aktion: 'parameter-neu' });
fam({ id: 'familie.parameter', titel: 'Parameter', symbol: 'famParameter', gruppe: 'Parameter', ausfuehren: () => aktiverEditor()?.tabWaehlen('parameter'), gedrueckt: () => aktiverEditor()?.tab === 'parameter' });

fam({ id: 'familie.typ-neu', titel: 'Typ hinzufügen', symbol: 'famTypNeu', gruppe: 'Typen', aktion: 'typ-neu' });
fam({ id: 'familie.typ-kopieren', titel: 'Typ kopieren', symbol: 'famTypKopie', gruppe: 'Typen', aktion: 'typ-kopieren' });
fam({ id: 'familie.typ-loeschen', titel: 'Typ löschen', symbol: 'famTypWeg', gruppe: 'Typen', aktion: 'typ-loeschen' });
fam({ id: 'familie.typen', titel: 'Typen', symbol: 'famTypen', gruppe: 'Typen', ausfuehren: () => aktiverEditor()?.tabWaehlen('typen'), gedrueckt: () => aktiverEditor()?.tab === 'typen' });

fam({ id: 'familie.koerper', titel: 'Körper', symbol: 'famKoerper', gruppe: 'Körper', ausfuehren: () => aktiverEditor()?.tabWaehlen('koerper'), gedrueckt: () => aktiverEditor()?.tab === 'koerper' });
for (const [art, titel, symbol] of [['quader', 'Quader', 'famQuader'], ['zylinder', 'Zylinder', 'famZylinder'], ['kegel', 'Kegel', 'famKegel'], ['kugel', 'Kugel', 'famKugel'],
  ['extrusion', 'Extrusion', 'famExtrusion'], ['rotation', 'Rotation', 'famRotation'], ['sweep', 'Sweep', 'famSweep'], ['uebergang', 'Übergang', 'famKegel'], ['netz', 'Netz', 'famKoerper']])
  fam({ id: `familie.koerper-${art}`, titel, symbol, gruppe: 'Körper', groesse: 'klein', aktion: 'koerper-neu', param: { art } });
fam({ id: 'familie.koerper-loeschen', titel: 'Körper löschen', symbol: 'famKoerperWeg', gruppe: 'Körper', groesse: 'klein', aktion: 'koerper-loeschen' });
fam({ id: 'familie.netz-uebernehmen', titel: 'Geometrie übernehmen', symbol: 'famKoerper', gruppe: 'Körper', groesse: 'klein',
  ausfuehren: () => netzUebernehmen(aktiverEditor()?.kennzeichen()) });

fam({ id: 'familie.anschluss-neu', titel: 'Anschluss hinzufügen', symbol: 'famAnschluss', gruppe: 'Anschlüsse', aktion: 'anschluss-neu' });
fam({ id: 'familie.anschluesse', titel: 'Anschlüsse', symbol: 'famAnschluesse', gruppe: 'Anschlüsse', ausfuehren: () => aktiverEditor()?.tabWaehlen('anschluesse'), gedrueckt: () => aktiverEditor()?.tab === 'anschluesse' });

fam({ id: 'familie.sinnbild', titel: 'Sinnbild bearbeiten', symbol: 'famSinnbild', gruppe: 'Sinnbild', aktion: 'sinnbild', gedrueckt: () => aktiverEditor()?.tab === 'sinnbild' });

const ansichtsZustand = () => aktiverEditor()?.ansichtszustand;
fam({ id: 'familie.ansicht-vier', titel: 'Vier Fenster', symbol: 'famVier', gruppe: 'Ansicht', ausfuehren: () => aktiverEditor()?.ansichtEinstellen({ modus: 'vier' }), gedrueckt: () => ansichtsZustand()?.modus === 'vier' });
for (const [a, titel, symbol] of [['vorne', 'Vorne', 'famVorne'], ['hinten', 'Hinten', 'famHinten'], ['links', 'Links', 'famLinks'], ['rechts', 'Rechts', 'famRechts'], ['oben', 'Oben', 'famOben'], ['unten', 'Unten', 'famUnten']])
  fam({ id: `familie.ansicht-${a}`, titel, symbol, gruppe: 'Ansicht', groesse: 'klein', ausfuehren: () => aktiverEditor()?.ansichtEinstellen({ modus: 'eins', ansicht: a }), gedrueckt: () => ansichtsZustand()?.modus === 'eins' && ansichtsZustand()?.einzeln === a });
fam({ id: 'familie.ansicht-3d', titel: '3D', symbol: 'ansicht3d', gruppe: 'Ansicht', ausfuehren: () => aktiverEditor()?.ansichtEinstellen({ modus: 'eins', ansicht: '3d' }), gedrueckt: () => ansichtsZustand()?.modus === 'eins' && ansichtsZustand()?.einzeln === '3d' });
for (const [g, symbol] of [['grob', 'famGrob'], ['mittel', 'famMittel'], ['fein', 'famFein']])
  fam({ id: `familie.detail-${g}`, titel: `Detail ${g}`, symbol, gruppe: 'Ansicht', groesse: 'klein', ausfuehren: () => aktiverEditor()?.ansichtEinstellen({ detailgrad: g }), gedrueckt: () => ansichtsZustand()?.detailgrad === g });

fam({ id: 'familie.pruefen', titel: 'Familie prüfen', symbol: 'famPruefen', gruppe: 'Prüfen', aktion: 'pruefen', gedrueckt: () => aktiverEditor()?.tab === 'pruefen' });

// ================================================================================================ Platzieren

const nurGrundriss = () => (planAnsicht() ? 'Geht nur in einem Grundriss.' : 'Zuerst einen Grundriss öffnen und aktivieren.');

/** Startet das Platzieren; p: { familie?, typ? } wählt die Typauswahl vor. Ohne offenen Grundriss wird der erste geöffnet. */
export async function platzieren(p = {}) {
  await familienLaden();
  if (!planAnsicht()) {
    const g = derKlasse('Ansicht').filter((a) => a.werte?.art === 'grundriss' && !a.werte?.vorlage).sort((a, b) => a.kennzeichen.localeCompare(b.kennzeichen))[0];
    if (!g) { meldung('Zum Platzieren wird ein Grundriss gebraucht.', 'warnung', { vorschlag: 'Im Register Ansicht einen Grundriss anlegen.' }); return undefined; }
    await ansichtOeffnen(g.kennzeichen);
  }
  if (familienListe().length === 0) { meldung('Das Projekt hat noch keine Familie.', 'warnung', { vorschlag: 'Im Projektbrowser «Neue Familie» wählen oder eine Familie laden.' }); return undefined; }
  const f = p.familie ? familienListe().find((x) => x.kennzeichen === p.familie || x.name === p.familie) : null;
  if (p.familie && !f) { meldung(`Familie «${p.familie}» gibt es nicht.`, 'warnung'); return undefined; }
  if (f) {
    const typ = p.typ ?? f.typen?.[0];
    if (!typ) { meldung(`Die Familie ${f.kennzeichen} hat noch keinen Typ.`, 'warnung'); return undefined; }
    if (f.gueltig === false) { meldung(`Die Familie ${f.kennzeichen} ist nicht gültig und lässt sich nicht platzieren.`, 'warnung', { vorschlag: 'Im Familieneditor die Fehler unter «Prüfen» beheben.' }); return undefined; }
    typSetzen(f.kennzeichen, typ);
  }
  return werkzeugStarten(platzierWerkzeug());
}

registrieren({
  id: 'familie.platzieren', titel: 'Familie platzieren', symbol: 'famPlatzieren', kuerzel: 'CM', register: 'gebaeudetechnik', gruppe: 'Apparate und Armaturen',
  aktiv: () => projektOffen() && !!planAnsicht() && planAnsicht()?.art === 'grundriss',
  hinweis: () => (projektOffen() ? nurGrundriss() : 'Zuerst ein Projekt öffnen.'),
  gedrueckt: () => werkzeugAktiv() === 'familie',
  ausfuehren: (p) => platzieren(p ?? {}),
});

// Aktionen für Kontextmenüs und Doppelklick im Projektbrowser: kein Eintrag im Menüband
registrieren({
  id: 'familie.platzieren-aus-browser', titel: 'Platzieren', symbol: 'famPlatzieren', aktiv: () => projektOffen(),
  ausfuehren: (p) => platzieren(p ?? {}),
});

registrieren({
  id: 'familie.bearbeiten', titel: 'Bearbeiten', symbol: 'famEditor', aktiv: () => projektOffen(),
  async ausfuehren(p) {
    const f = element(p?.familie);
    if (!f || f.klasse !== 'Familie') { meldung(`Familie «${p?.familie}» gibt es nicht.`, 'warnung'); return undefined; }
    return ansichtOeffnen(f.kennzeichen);
  },
});

// ================================================================================================ Neue Familie

registrieren({
  id: 'familie.neu', titel: 'Neue Familie', symbol: 'neu', aktiv: projektOffen, hinweis: 'Zuerst ein Projekt öffnen.',
  async ausfuehren() {
    const kategorien = familienKategorien(zustand.katalog?.klassen);
    await familienLaden();
    const w = knoten(`<div class="fe-dlg">
      <label class="fz"><span>Kategorie *</span><select class="nf-kategorie">${kategorien.map((k) => `<option>${esc(k)}</option>`).join('')}</select></label>
      <label class="fz nf-form-zeile" hidden><span>Querschnitt</span><select class="nf-form"></select></label>
      <label class="fz"><span>Name *</span><input type="text" class="nf-name" data-kein-enter="1" placeholder="z. B. Heizkörper Typ 22"></label>
      <p class="leise nf-text">Es entsteht ein Entwurf mit dem Typ «Standard», ohne Parameter, Körper und Anschlüsse. Alles Weitere baust du im Familieneditor.</p></div>`);
    const q = (s) => w.querySelector(s);
    const klassen = zustand.katalog?.klassen ?? [];
    const felder = () => ({ kategorie: q('.nf-kategorie').value, name: q('.nf-name').value, form: q('.nf-form').value });
    // Leitungsserie (Modul 3, C3): Rohr ist immer rund, Kanal rund oder rechteckig; es entstehen die Typparameter der Vorlage ohne Werte
    const kategorieGeaendert = () => {
      const formen = leitungFormen(leitungsArt(q('.nf-kategorie').value, klassen));
      q('.nf-form').innerHTML = formen.map(([v, t]) => `<option value="${esc(v)}">${esc(t)}</option>`).join('');
      q('.nf-form-zeile').hidden = formen.length < 2;
      q('.nf-text').textContent = formen.length
        ? 'Es entsteht eine Leitungsserie (Entwurf) mit dem Block «leitung», den Typparametern für Grösse und Aussendurchmesser und einem Typ «Standard» – alle ohne Werte. Die Werte (Grössen, Aussendurchmesser) trägst du im Familieneditor aus der Herstellerunterlage ein.'
        : 'Es entsteht ein Entwurf mit dem Typ «Standard», ohne Parameter, Körper und Anschlüsse. Alles Weitere baust du im Familieneditor.';
    };
    q('.nf-kategorie').addEventListener('change', kategorieGeaendert);
    kategorieGeaendert();
    await zeigen({
      titel: 'Neue Familie', inhalt: w, breite: 500, klasse: 'familie',
      knoepfe: [{ text: 'Abbrechen', wert: false },
        { text: 'Anlegen', primaer: true, schliesst: false,
          pruefen: () => neueFamiliePruefen(felder(), familienListe().map((f) => f.name)),
          async klick(c) {
            const f = felder();
            const r = await sende('familie_entwurf', { definition: familieEntwurfMinimal(f.kategorie, f.name, { klassen, form: f.form }) });
            if (!r.angenommen) { const k = kernFehler(r); c.fehler(`${k.text}${k.vorschlag ? ' – ' + k.vorschlag : ''}`); return; }
            const kz = r.aenderungen?.find((a) => a.klasse === 'Familie')?.kennzeichen;
            c.schliessen(true);
            await familienLaden();
            if (leitungsArt(f.kategorie, klassen)) merken.schreiben('familieEditorTab', 'leitung');   // eine neue Leitungsserie öffnet auf ihrer Seite
            if (kz && (await aufElement(kz))) await ansichtOeffnen(kz);
          } }],
    });
    return undefined;
  },
});

// ================================================================================================ Geometrie aus IFC bzw. 3D-Modell

// 30.10.2026 (Christian: «es muss auch möglich sein fehlende familien aus einem ifc geometrisch abzubilden»): Befehl familie_netz_uebernehmen.
// Mit familie kommt der Körper «netz» in diese Familie, sonst entsteht eine neue. Objekt: GlobalId bzw. übernommenes Element (vorbelegt aus der
// Auswahl) oder die Id eines Befunds form3d einer STL/OBJ/STEP-Unterlage.
const NETZ_ARTEN = ['ifc', 'stl', 'obj', 'step', 'iges'];
async function netzUebernehmen(familie) {
  const unterlagen = derKlasse('Unterlage').filter((u) => NETZ_ARTEN.includes(u.werte?.art));
  if (!unterlagen.length) { meldung('Keine IFC-, STL-, OBJ- oder STEP-Unterlage im Projekt.', 'warnung', { vorschlag: 'Zuerst über «Unterlagen» bzw. «IFC übernehmen» einfügen.' }); return undefined; }
  const gewaehlt = element(zustand.auswahl);
  const vorgabeUl = gewaehlt?.kennungen?.quelle ? element(gewaehlt.kennungen.quelle)?.kennzeichen : null;
  const kategorien = familie ? [] : familienKategorien(zustand.katalog?.klassen);
  const w = knoten(`<div class="fe-dlg">
    <label class="fz"><span>Unterlage *</span><select class="nu-ul">${unterlagen.map((u) => `<option value="${esc(u.kennzeichen)}"${u.kennzeichen === vorgabeUl ? ' selected' : ''}>${esc(u.kennzeichen)} · ${esc(u.name ?? '')} (${esc(u.werte?.art)})</option>`).join('')}</select></label>
    <label class="fz"><span>Objekt *</span><input type="text" class="nu-obj" data-kein-enter="1" value="${esc(gewaehlt?.kennungen?.ifc ? gewaehlt.kennzeichen : '')}" placeholder="IFC-GlobalId, Element-Kennzeichen oder Befund-Id (form3d)"></label>
    ${familie ? '' : `<label class="fz"><span>Kategorie *</span><select class="nu-kat">${kategorien.map((k) => `<option>${esc(k)}</option>`).join('')}</select></label>
    <label class="fz"><span>Name *</span><input type="text" class="nu-name" data-kein-enter="1" placeholder="z. B. Badewanne aus Architektur-IFC"></label>`}
    <label class="fz"><span>Drehung °</span><input type="text" class="nu-dreh" data-kein-enter="1" placeholder="leer: IFC automatisch"></label>
    <label class="fz"><span>Nullpunkt</span><select class="nu-null"><option value="">Vorgabe (IFC unten Mitte, Modell unverändert)</option><option value="unten_mitte">unten Mitte</option>
      <option value="unten_hinten_mitte">unten hinten Mitte (Wandobjekt)</option><option value="unveraendert">unverändert</option></select></label>
    <p class="leise">Das Netz kommt als Körper «netz» ${familie ? `in ${esc(familie)}` : 'in eine neue Familie (Entwurf)'}; Anschlüsse und Parameter ergänzt du danach.</p></div>`);
  const q = (s) => w.querySelector(s);
  await zeigen({
    titel: 'Geometrie übernehmen', inhalt: w, breite: 520, klasse: 'familie',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Übernehmen', primaer: true, schliesst: false,
        pruefen: () => (!q('.nu-obj').value.trim() ? 'Objekt angeben.' : !familie && !q('.nu-name').value.trim() ? 'Name angeben.' : true),
        async klick(c) {
          const p = { unterlage: q('.nu-ul').value, objekt: q('.nu-obj').value.trim() };
          if (familie) p.familie = familie; else { p.kategorie = q('.nu-kat').value; p.name = q('.nu-name').value.trim(); }
          const d = q('.nu-dreh').value.trim().replace(',', '.');
          if (d) p.drehung = Number(d);
          if (q('.nu-null').value) p.nullpunkt = q('.nu-null').value;
          const r = await sende('familie_netz_uebernehmen', p);
          if (!r.angenommen) { const k = kernFehler(r); c.fehler(`${k.text}${k.vorschlag ? ' – ' + k.vorschlag : ''}`); return; }
          c.schliessen(true);
          for (const h of r.hinweise ?? []) if (h.startsWith('Körper')) meldung(h, 'info');
          await familienLaden();
          const kz = familie ?? r.aenderungen?.find((a) => a.klasse === 'Familie')?.kennzeichen;
          if (!familie && kz && (await aufElement(kz))) await ansichtOeffnen(kz);
        } }],
  });
  return undefined;
}

registrieren({
  id: 'familie.aus-geometrie', titel: 'Familie aus IFC-Objekt / 3D-Modell', symbol: 'famKoerper', aktiv: projektOffen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => netzUebernehmen(null),
});

// ================================================================================================ Familie laden

// Modul 2, C3: die Liste kommt aus der Abfrage familien_bibliothek (Familienbibliothek mit Version, dazu die mitgelieferten), nicht mehr aus dem Vorschlag
// des Kerns zu einer unbekannten Datei; ein voller Pfad geht weiter (freies Feld). Dialog: bibliothek-dialoge.js.
registrieren({
  id: 'familie.laden', titel: 'Familie laden', symbol: 'oeffnen', aktiv: projektOffen, hinweis: 'Zuerst ein Projekt öffnen.',
  async ausfuehren() {
    const kz = await ladenDialog();
    if (kz) { await aufElement(kz); auswaehlen(element(kz)?.id ?? null); }
    return undefined;
  },
});
