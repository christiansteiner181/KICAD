// Projektbrowser «Familien» (Modul 2, C1; C3): die Familien des Projekts je Kategorie mit ihren Typen, dazu die Bibliotheksfamilien nach der Familienauswahl
// des Projekts (grau, Marke «Bibliothek») und der Knoten «Katalog» mit den Katalogklassen (bisheriger Inhalt). Rein (Baumknoten aus den Antworten der
// Abfragen familien und familien_bibliothek), in Node getestet.
// Entwürfe stehen grau mit der Marke «Entwurf»; Familien mit Fehlern in der Definition tragen die Marke «Fehler». C3: Marken nach bibliothekStand
// («neuere Version», «parallel geändert», «noch nicht gespeichert») und der Knopf «Aktualisieren …».
//   Doppelklick auf eine Familie öffnet den Editor, Doppelklick auf einen Typ platziert; Doppelklick auf eine Bibliotheksfamilie lädt und platziert sie;
//   Kontextmenü: Bearbeiten, Platzieren, Neue Familie, Familie laden, Familien im Explorer …, Aktualisieren ….

import { standMarke, standKnoepfe, standTipp, bibliotheksKnoten } from './bibliothek-text.js';

const nachName = (a, b) => String(a.name ?? a.kennzeichen).localeCompare(String(b.name ?? b.kennzeichen), 'de', { numeric: true });

/** Knoten einer Familie: art «fam», Kinder = Typen (art «fam-typ»). idVon(kz) liefert die Element-Id (zum Auswählen). */
export function familienKnoten(f, idVon = () => null) {
  const entwurf = f.status !== 'freigegeben';
  const fehlerhaft = f.gueltig === false;
  const typen = (f.typen ?? []).map((t) => ({
    key: `ft:${f.kennzeichen}:${t}`, label: t, sub: '', sym: 'famTyp', art: 'fam-typ', kz: f.kennzeichen, typ: t,
    tipp: `Typ «${t}» der Familie ${f.kennzeichen}. Doppelklick platziert.`, css: entwurf ? 'entwurf' : '',
  }));
  // Modul 2, C3: Stand zur Familienbibliothek
  const stand = standMarke(f.bibliothekStand);
  const marken = [fehlerhaft ? 'Fehler' : entwurf ? 'Entwurf' : '', stand].filter(Boolean);
  // Modul 2, C2: Entwürfe lassen sich aus dem Browser prüfen (Prüfansicht im Editor)
  const pruefen = entwurf ? { aktion: 'familie.entwurf-pruefen-browser', text: 'Prüfen', titel: `Entwurf ${f.kennzeichen} prüfen: Abdeckung, Konflikte, Masse, Freigabe`, param: { familie: f.kennzeichen } } : undefined;
  const knoepfe = [pruefen, ...standKnoepfe(f)].filter(Boolean);
  const standTippText = standTipp(f.bibliothekStand);
  return {
    key: `fam:${f.kennzeichen}`, label: f.name ?? f.kennzeichen, sub: f.kennzeichen, sym: 'famFamilie', art: 'fam', kz: f.kennzeichen, id: idVon(f.kennzeichen) ?? undefined,
    kinder: typen, zahl: typen.length, klasse: f.kategorie,
    marke: marken[0] ?? '', marken,
    knopf: knoepfe[0], knoepfe,
    css: [entwurf ? 'entwurf' : '', fehlerhaft ? 'fehlerhaft' : '', stand ? 'bib-stand' : ''].filter(Boolean).join(' '),
    tipp: `${f.kennzeichen} · ${f.kategorie} · ${fehlerhaft ? `${f.definitionsfehler ?? ''} Fehler in der Definition` : entwurf ? 'Entwurf' : 'freigegeben'} · Version ${f.version}${f.verwendungen ? ` · ${f.verwendungen}× platziert` : ''}\nDoppelklick öffnet den Editor.${standTippText ? `\n${standTippText}` : ''}`,
  };
}

/**
 * Der ganze Knoten «Familien»: Kategorien (nach Namen) mit ihren Familien, am Ende der Knoten «Katalog» (katalogKnoten).
 * bibliothek (C3, optional): Familien aus familien_bibliothek – gezeigt werden die sichtbaren, die nicht im Projekt sind (der Kern entscheidet, was «sichtbar» ist).
 */
export function familienAst(liste, katalogKnoten, idVon = () => null, bibliothek = []) {
  const imProjekt = new Set((liste ?? []).map((f) => f.id).filter(Boolean));
  const fremde = (bibliothek ?? []).filter((f) => f.sichtbar === true && !f.imProjekt && !(f.id && imProjekt.has(f.id)));
  const nachKategorie = new Map();
  const dazu = (k, knoten) => { if (!nachKategorie.has(k)) nachKategorie.set(k, []); nachKategorie.get(k).push(knoten); };
  for (const f of [...(liste ?? [])].sort(nachName)) dazu(f.kategorie ?? '?', familienKnoten(f, idVon));
  for (const f of [...fremde].sort(nachName)) dazu(f.kategorie ?? '?', bibliotheksKnoten(f));
  const aeste = [...nachKategorie].sort((a, b) => a[0].localeCompare(b[0], 'de')).map(([k, ks]) => ({
    key: `fk:${k}`, label: k, kinder: ks, zahl: ks.length, sym: 'ordner', art: 'ast',
  }));
  const kinder = katalogKnoten ? [...aeste, katalogKnoten] : aeste;
  return {
    key: 'w:familien', label: 'Familien', kinder, zahl: (liste ?? []).length + fremde.length, sym: 'ordner', art: 'ast',
    knopf: { aktion: 'familie.aus-unterlagen', text: '＋ Unterlagen', titel: 'Familie aus Herstellerunterlagen erstellen (PDF, DWG/DXF, STEP, Tabellen …)' },   // Modul 2, C2
    knoepfe: [
      { aktion: 'familie.aus-unterlagen', text: '＋ Unterlagen', titel: 'Familie aus Herstellerunterlagen erstellen (PDF, DWG/DXF, STEP, Tabellen …)' },
      { aktion: 'familie.explorer', text: 'Explorer …', kurz: '⚙', titel: 'Familien im Explorer …: welche Familien der Bibliothek dieses Projekt zeigt' },   // Modul 2, C3
    ],
  };
}
