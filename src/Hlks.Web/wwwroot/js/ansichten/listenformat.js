// Aufbereitung der Ableitungen des Kerns (Raumliste, Strangliste, Mengen, seit Modul 1c Raumgrenzen und Hüllbauteile, seit Modul 3 Materialliste,
// seit Modul 3b Kollisionsliste und Aussparungsliste) für die Listenansicht:
// Spalten mit deutschen Titeln in sinnvoller Reihenfolge, Werte mit Dezimalkomma, Sortieren. Reine Funktionen (Node-testbar).
// Der Kern liefert die Zahlen; hier wird nichts gerechnet, nur dargestellt.

/** Spaltenvorgaben je Ableitung: Reihenfolge, Titel, Art (zahl | text). Unbekannte Felder werden hinten angefügt. */
const VORGABEN = {
  raumliste: {
    titel: 'Raumliste',
    zeilen: 'zeilen',
    schluessel: 'raum',   // Kennzeichen des Elements für den Klick
    spalten: [
      ['raum', 'Raum'], ['name', 'Name'], ['geschoss', 'Geschoss'], ['nutzung', 'Nutzung'],
      ['flaeche_m2', 'Fläche m²', 'zahl'], ['hoehe_m', 'Höhe m', 'zahl'], ['volumen_m3', 'Volumen m³', 'zahl'],
      ['anzahlEndgeraete', 'Anzahl Endgeräte', 'zahl'], ['endgeraete', 'Endgeräte'], ['systeme', 'Systeme'],
    ],
  },
  strangliste: {
    titel: 'Strangliste',
    zeilen: 'zeilen',
    schluessel: 'strang',
    spalten: [
      ['strang', 'Strang'], ['name', 'Name'], ['art', 'Art'], ['system', 'System'], ['systemtyp', 'Systemtyp'],
      ['geschosse', 'Geschosse'], ['groesse', 'Grösse'], ['leitungslaenge_m', 'Leitungslänge m', 'zahl'], ['laengen', 'Längen je Serie und Grösse'], ['bauteile', 'Bauteile', 'zahl'],
      ['anzahlDirekt', 'Endgeräte direkt', 'zahl'], ['anzahlGesamt', 'Endgeräte gesamt', 'zahl'],
      ['endgeraeteDirekt', 'Endgeräte'], ['datenpunkte', 'Datenpunkte'],
    ],
    verbergen: ['anzahlDirektJeKlasse'],
  },
  // Modul 1c: eine Zeile je Raumgrenze (nicht summenJeRaum); Klick wählt den Raum. Summen je Angrenzung stehen als Fusszeile (aufbereiten).
  raumgrenzen: {
    titel: 'Raumgrenzen',
    zeilen: 'zeilen',
    schluessel: 'raum',
    spalten: [
      ['raum', 'Raum'], ['art', 'Art'], ['nachbar', 'Nachbar'], ['angrenzung', 'Angrenzung'], ['bauteil', 'Bauteil'], ['aufbau', 'Aufbau'],
      ['flaeche_m2', 'Fläche m²', 'zahl'], ['azimut', 'Azimut °', 'zahl'], ['neigung', 'Neigung °', 'zahl'],
      ['u', 'U W/(m²·K)', 'zahl'], ['ua_WK', 'U·A W/K', 'zahl'],
    ],
    verbergen: ['laenge_m', 'hoehe_m', 'offen', 'vorgabe', 'gerechnet', 'uQuelle'],
  },
  // Modul 1c: Klick wählt das Bauteil.
  huellbauteile: {
    titel: 'Hüllbauteile',
    zeilen: 'zeilen',
    schluessel: 'bauteil',
    spalten: [
      ['bauteil', 'Bauteil'], ['klasse', 'Klasse'], ['geschoss', 'Geschoss'], ['aufbau', 'Aufbau'], ['u', 'U W/(m²·K)', 'zahl'],
      ['flaecheInnen_m2', 'Fläche innen m²', 'zahl'], ['flaecheAussen_m2', 'Fläche aussen m²', 'zahl'], ['azimut', 'Orientierung (Azimut °)', 'zahl'],
      ['neigung', 'Neigung °', 'zahl'], ['angrenzung', 'Angrenzung'], ['raeume', 'Räume'], ['hinweis', 'Hinweis'],
    ],
    verbergen: ['uQuelle', 'angrenzungen'],
  },
  // Modul 3 (B4): wie «mengen» zwei Tabellen ohne Klick-Schlüssel – Leitungen je Serie und Grösse (Länge in m), Formteile je Familie und Typ
  // (Stück; Bögen je Bogenwinkel, T-Stücke mit Winkelparameter je Abzweigwinkel).
  materialliste: {
    titel: 'Materialliste',
    tabellen: [
      {
        titel: 'Leitungen', zeilen: 'leitungen',
        spalten: [['serie', 'Serie'], ['groesse', 'Grösse'], ['stueck', 'Stück', 'zahl'], ['laenge_m', 'Länge m', 'zahl']],
      },
      {
        titel: 'Formteile', zeilen: 'formteile',
        spalten: [['familie', 'Familie'], ['typ', 'Typ'], ['klasse', 'Klasse'], ['winkel', 'Winkel °', 'zahl'], ['stueck', 'Stück', 'zahl']],
      },
    ],
  },
  // Modul 3b (B4a): Kollisionsliste (Abfrage kollisionen); Klick wählt die Kollision (Kennzeichen). Spalten, die in keiner Zeile einen Wert haben,
  // entfallen (leereSpaltenAus); die Beteiligten stehen als Text in «beteiligte», die Einzelheiten (beteiligteListe) und die Kennung bleiben verborgen.
  kollisionen: {
    titel: 'Kollisionsliste',
    zeilen: 'zeilen',
    schluessel: 'kollision',
    leereSpaltenAus: true,
    spalten: [
      ['kollision', 'Kollision'], ['art', 'Art'], ['typ', 'Typ'], ['status', 'Status'], ['mass_mm', 'Mass mm', 'zahl'], ['geschoss', 'Geschoss'],
      ['beteiligte', 'Beteiligte'], ['bauteil', 'Gequertes Bauteil'], ['lage_mm', 'Lage x, y, z mm'], ['kommentar', 'Kommentar'], ['pruefung', 'Prüflauf'],
    ],
    verbergen: ['kennung', 'beteiligteListe'],
  },
  // Modul 3b (B4a): Aussparungsliste (Abfrage aussparungsliste); Klick wählt die Aussparung. Rund: Achse statt UK/OK; Koten in mm über ±0.00
  // bzw. über OK Geschoss (…Geschoss). Leere Spalten entfallen; b, h, d stehen bereits im Text «masse».
  aussparungsliste: {
    titel: 'Aussparungsliste',
    zeilen: 'zeilen',
    schluessel: 'aussparung',
    leereSpaltenAus: true,
    spalten: [
      ['nummer', 'Nummer'], ['kurzzeichen', 'Kurzzeichen'], ['aussparung', 'Aussparung'], ['geschoss', 'Geschoss'], ['status', 'Status'], ['art', 'Art'], ['form', 'Form'],
      ['masse', 'Mass mm'], ['tiefe', 'Tiefe mm', 'zahl'],
      ['koteUk', 'UK ü. ±0.00 mm', 'zahl'], ['koteOk', 'OK ü. ±0.00 mm', 'zahl'], ['koteAchse', 'Achse ü. ±0.00 mm', 'zahl'],
      ['koteUkGeschoss', 'UK ü. OK Geschoss mm', 'zahl'], ['koteOkGeschoss', 'OK ü. OK Geschoss mm', 'zahl'], ['koteAchseGeschoss', 'Achse ü. OK Geschoss mm', 'zahl'],
      ['bauteil', 'Bauteil'], ['bauteilArt', 'Bauteilart'], ['unterlage', 'Unterlage'], ['leitungen', 'Leitungen'], ['mitte', 'Mitte x, y, z mm'],
    ],
    verbergen: ['d', 'b', 'h'],
  },
  mengen: {
    titel: 'Mengen',
    tabellen: [
      {
        titel: 'Leitungen', zeilen: 'leitungen',
        spalten: [['system', 'System'], ['klasse', 'Klasse'], ['material', 'Material'], ['groesse', 'Grösse'], ['stueck', 'Stück', 'zahl'], ['laenge_m', 'Länge m', 'zahl']],
      },
      {
        titel: 'Bauteile', zeilen: 'bauteile',
        spalten: [['klasse', 'Klasse'], ['gewerk', 'Gewerk'], ['stueck', 'Stück', 'zahl'], ['bkp', 'BKP'], ['bkpHinweis', 'Hinweis']],
      },
    ],
  },
};

/** Lesbarer Titel für unbekannte Feldnamen: «anzahl_x» → «Anzahl x». */
export function titelVon(schluessel) {
  const t = String(schluessel).replace(/_m2$/, ' m²').replace(/_m3$/, ' m³').replace(/_m$/, ' m')
    .replace(/([a-zäöü])([A-Z])/g, '$1 $2').replace(/_/g, ' ').trim();
  return t.charAt(0).toUpperCase() + t.slice(1);
}

const istZeilenliste = (v) => Array.isArray(v) && v.length > 0 && v.every((x) => x && typeof x === 'object' && !Array.isArray(x));

function spaltenAus(zeilen, vorgabe, verbergen = []) {
  const vorhanden = new Set();
  for (const z of zeilen) for (const k of Object.keys(z)) vorhanden.add(k);
  const spalten = [];
  for (const [k, titel, art] of vorgabe ?? []) {
    if (vorhanden.has(k)) { spalten.push({ schluessel: k, titel, art: art ?? 'text' }); vorhanden.delete(k); }
  }
  for (const k of vorhanden) {
    if (verbergen.includes(k)) continue;
    const zahlen = zeilen.every((z) => z[k] === null || z[k] === undefined || typeof z[k] === 'number');
    spalten.push({ schluessel: k, titel: titelVon(k), art: zahlen ? 'zahl' : 'text' });
  }
  return spalten;
}

/** Spalten mit Dezimalstellen: Felder mit Endung _m2, _m, _m3 zeigen 2 Nachkommastellen. */
export const stellenVon = (schluessel) => (/_(m2|m3|m)$/.test(schluessel) ? 2 : schluessel === 'u' ? 3 : null);

/** Anzeige von Kernwerten ohne Umlaut (Katalogwerte, Modul 3b: Status bestaetigt, ausgefuehrt). Alles andere bleibt, wie es ist. */
const ANZEIGE_WERT = { bestaetigt: 'bestätigt', ausgefuehrt: 'ausgeführt' };
export const anzeigeWert = (wert) => (typeof wert === 'string' && Object.hasOwn(ANZEIGE_WERT, wert) ? ANZEIGE_WERT[wert] : wert);

/** Wert für die Anzeige: Zahlen mit Komma, Listen mit «, », Objekte als «schluessel: wert». */
export function formatWert(schluessel, wert) {
  if (wert === null || wert === undefined) return '';
  if (typeof wert === 'string') return anzeigeWert(wert);
  if (typeof wert === 'number') {
    if (!Number.isFinite(wert)) return '';
    const st = stellenVon(schluessel);
    if (st !== null) return wert.toFixed(st).replace('.', ',');
    return Number.isInteger(wert) ? String(wert) : String(Math.round(wert * 100) / 100).replace('.', ',');
  }
  if (typeof wert === 'boolean') return wert ? 'ja' : 'nein';
  // Längen je Serie und Grösse (Strangliste «laengen»): «DN 20 (Serie): 4,45 m»
  if (Array.isArray(wert) && wert.length > 0 && wert.every((x) => x && typeof x === 'object' && 'groesse' in x && 'laenge_m' in x)) {
    return wert.map((x) => `${x.groesse}${x.serie ? ` (${x.serie})` : ''}: ${formatWert('laenge_m', x.laenge_m)} m`).join('; ');
  }
  if (Array.isArray(wert)) return wert.map((x) => formatWert(schluessel, x)).join(', ');
  if (typeof wert === 'object') return Object.entries(wert).map(([k, v]) => `${k}: ${formatWert(k, v)}`).join(', ');
  return String(wert);
}

/** Sortiert Zeilen nach einer Spalte; Zahlen numerisch, Text natürlich (U.2 vor U.10); leere Werte ans Ende. */
export function sortiere(zeilen, schluessel, absteigend = false) {
  const leer = (v) => v === null || v === undefined || v === '' || (Array.isArray(v) && v.length === 0);
  const sortText = (v) => (Array.isArray(v) ? v.join(', ') : String(v));
  const vgl = new Intl.Collator('de', { numeric: true, sensitivity: 'base' });
  const kopie = zeilen.map((z, i) => [z, i]);
  kopie.sort(([a, ia], [b, ib]) => {
    const x = a[schluessel], y = b[schluessel];
    if (leer(x) && leer(y)) return ia - ib;
    if (leer(x)) return 1;     // leer immer ans Ende, unabhängig von der Richtung
    if (leer(y)) return -1;
    const r = (typeof x === 'number' && typeof y === 'number') ? x - y : vgl.compare(sortText(x), sortText(y));
    return (absteigend ? -r : r) || ia - ib;
  });
  return kopie.map(([z]) => z);
}

/**
 * Fusszeilen (Summen je Angrenzung) aus `summen.jeAngrenzung` (Raumgrenzen) bzw. `summen` (Hüllbauteile) des Kerns. Nur Darstellung – die Zahlen kommen vom Kern.
 * Jede Zeile hat in der ersten Spalte die Beschriftung («Summe aussen»), dazu die Summenfelder der Spalten. → [{ <spalte>: wert }]
 */
export function fusszeilen(name, d, spalten) {
  const erste = spalten?.[0]?.schluessel;
  if (!erste) return [];
  const zeilen = [];
  const m2 = (v) => formatWert('x_m2', v);
  if (name === 'raumgrenzen') {
    const j = d?.summen?.jeAngrenzung;
    if (!j || typeof j !== 'object') return [];
    for (const [angrenzung, s] of Object.entries(j)) {
      const ohneU = typeof s?.ohneU_m2 === 'number' && s.ohneU_m2 > 0 ? ` (ohne U: ${m2(s.ohneU_m2)} m²)` : '';
      zeilen.push({ [erste]: `Summe ${angrenzung}${ohneU}`, flaeche_m2: s?.flaeche_m2 ?? null, ua_WK: s?.ua_WK ?? null });
    }
  } else if (name === 'huellbauteile') {
    const j = d?.summen;
    if (!j || typeof j !== 'object') return [];
    for (const [angrenzung, s] of Object.entries(j)) {
      zeilen.push({ [erste]: `Summe ${angrenzung} (${s?.bauteile ?? 0} ${s?.bauteile === 1 ? 'Bauteil' : 'Bauteile'})`, flaecheInnen_m2: s?.flaecheInnen_m2 ?? null, flaecheAussen_m2: s?.flaecheAussen_m2 ?? null });
    }
  }
  return zeilen;
}

/** «Status: neu 3 · bestätigt 1» aus einem Zählobjekt {wert: anzahl}; leer, wenn nichts zu zählen ist. */
function jeText(titel, zaehler) {
  if (!zaehler || typeof zaehler !== 'object') return '';
  const teile = Object.entries(zaehler).map(([k, v]) => `${anzeigeWert(k)} ${v}`);
  return teile.length ? `${titel}: ${teile.join(' · ')}` : '';
}

/** Gesetzte Filter der Abfrage als «status neu, geschoss EG»; null und leer entfallen. */
function filterText(filter) {
  if (!filter || typeof filter !== 'object') return '';
  return Object.entries(filter).filter(([, v]) => v).map(([k, v]) => `${k} ${v}`).join(', ');
}

/**
 * Bereitet das Ergebnis einer Ableitung auf.
 * Ergebnis: { titel, projekt, stand: { pruefsumme, pakete }, erzeugt, zusammenfassung: [Text…],
 *             tabellen: [{ titel, schluessel (Kennzeichenfeld oder null), spalten[], zeilen[], fuss? (Summenzeilen, nur Raumgrenzen und Hüllbauteile) }] }
 */
export function aufbereiten(name, daten) {
  const d = (daten && typeof daten === 'object') ? daten : {};
  const vorgabe = VORGABEN[name];
  const kopf = {
    titel: d.ableitung ?? vorgabe?.titel ?? titelVon(name),
    projekt: d.projekt ?? '',
    stand: { pruefsumme: d.modellStand?.pruefsumme ?? '', pakete: d.modellStand?.pakete ?? null },
    erzeugt: d.erzeugt ?? '',
    zusammenfassung: [],
    tabellen: [],
  };
  const z = kopf.zusammenfassung;
  const m2 = (v) => formatWert('x_m2', v);

  if (name === 'raumliste') {
    if (typeof d.anzahl === 'number') z.push(`${d.anzahl} ${d.anzahl === 1 ? 'Raum' : 'Räume'}${typeof d.flaecheTotal_m2 === 'number' ? ` · Gesamtfläche ${m2(d.flaecheTotal_m2)} m²` : ''}`);
    if (Array.isArray(d.summenJeGeschoss)) z.push(d.summenJeGeschoss.map((s) => `${s.geschoss}: ${s.raeume} ${s.raeume === 1 ? 'Raum' : 'Räume'}, ${m2(s.flaeche_m2)} m²`).join('  |  '));
  } else if (name === 'strangliste') {
    if (typeof d.anzahl === 'number') z.push(`${d.anzahl} ${d.anzahl === 1 ? 'Strang' : 'Stränge'}`);
  } else if (name === 'mengen') {
    if (typeof d.leitungslaengeTotal_m === 'number') z.push(`Leitungslänge total ${m2(d.leitungslaengeTotal_m)} m`);
  } else if (name === 'materialliste') {
    if (typeof d.leitungslaengeTotal_m === 'number') z.push(`Leitungslänge total ${m2(d.leitungslaengeTotal_m)} m`);
    if (typeof d.formteileTotal === 'number') z.push(`${d.formteileTotal} ${d.formteileTotal === 1 ? 'Formteil' : 'Formteile'}`);
    const f = d.filter && typeof d.filter === 'object' ? Object.entries(d.filter).filter(([, v]) => v).map(([k, v]) => `${k} ${v}`) : [];
    if (f.length) z.push(`Filter: ${f.join(', ')}`);
    if (d.hinweis) z.push(String(d.hinweis));   // Hinweis des Kerns (z. B. Leitungen ohne Serie)
  } else if (name === 'raumgrenzen') {
    if (typeof d.anzahl === 'number') z.push(`${d.anzahl} ${d.anzahl === 1 ? 'Raumgrenze' : 'Raumgrenzen'}${typeof d.anzahlRaeume === 'number' ? ` in ${d.anzahlRaeume} ${d.anzahlRaeume === 1 ? 'Raum' : 'Räumen'}` : ''}`);
    for (const h of Array.isArray(d.hinweise) ? d.hinweise : []) if (h) z.push(String(h));   // Hinweise des Kerns über der Tabelle
  } else if (name === 'huellbauteile') {
    if (typeof d.anzahl === 'number') z.push(`${d.anzahl} ${d.anzahl === 1 ? 'Hüllbauteil' : 'Hüllbauteile'}`);
    for (const h of Array.isArray(d.hinweise) ? d.hinweise : []) if (h) z.push(String(h));
  } else if (name === 'kollisionen') {
    // Modul 3b (B4a): Zahlen je Status, Art und Typ stammen aus zusammenfassung des Kerns
    if (typeof d.anzahl === 'number') z.push(`${d.anzahl} ${d.anzahl === 1 ? 'Kollision' : 'Kollisionen'}`);
    for (const [titel, feld] of [['Status', 'jeStatus'], ['Art', 'jeArt'], ['Typ', 'jeTyp']]) {
      const t = jeText(titel, d.zusammenfassung?.[feld]);
      if (t) z.push(t);
    }
    if (d.kollisionslauf) z.push(`Letzter Prüflauf: ${d.kollisionslauf}`);
    const f = filterText(d.filter);
    if (f) z.push(`Filter: ${f}`);
    if (d.hinweis) z.push(String(d.hinweis));
  } else if (name === 'aussparungsliste') {
    if (typeof d.anzahl === 'number') z.push(`${d.anzahl} ${d.anzahl === 1 ? 'Aussparung' : 'Aussparungen'}${d.geschoss ? ` im Geschoss ${d.geschoss}` : ''}`);
    for (const [titel, feld] of [['Status', 'jeStatus'], ['Art', 'jeArt']]) {
      const t = jeText(titel, d[feld]);
      if (t) z.push(t);
    }
    if (d.einheit) z.push(`Einheiten: ${d.einheit}`);
  } else if (typeof d.anzahl === 'number') z.push(`${d.anzahl} Einträge`);

  if (vorgabe?.tabellen) {
    for (const t of vorgabe.tabellen) {
      const zeilen = Array.isArray(d[t.zeilen]) ? d[t.zeilen] : [];
      kopf.tabellen.push({ titel: t.titel, schluessel: null, spalten: spaltenAus(zeilen, t.spalten), zeilen });
    }
  } else {
    // Zeilenliste: laut Vorgabe «zeilen», sonst das erste Feld mit einer Liste von Objekten
    let feld = vorgabe?.zeilen;
    if (!feld || !Array.isArray(d[feld])) feld = Object.keys(d).find((k) => istZeilenliste(d[k]) && k !== 'summenJeGeschoss') ?? null;
    const zeilen = feld && Array.isArray(d[feld]) ? d[feld] : [];
    const verbergen = [...(vorgabe?.verbergen ?? [])];
    const sk = vorgabe?.schluessel ?? null;
    // «nummer» nur ausblenden, wenn es in jeder Zeile gleich dem Kennzeichen ist
    const spalten = spaltenAus(zeilen, vorgabe?.spalten, verbergen);
    const leer = (v) => v === null || v === undefined || v === '' || (Array.isArray(v) && v.length === 0);
    const gefiltert = spalten.filter((s) => !(s.schluessel === 'nummer' && sk && zeilen.every((r) => r.nummer === r[sk]))
      && !(vorgabe?.leereSpaltenAus && s.schluessel !== sk && zeilen.every((r) => leer(r[s.schluessel]))));   // Modul 3b: Spalten ohne jeden Wert entfallen
    const tabelle = { titel: '', schluessel: sk && zeilen.some((r) => r[sk] !== undefined) ? sk : null, spalten: gefiltert, zeilen };
    const fuss = fusszeilen(name, d, gefiltert);
    if (fuss.length) tabelle.fuss = fuss;
    kopf.tabellen.push(tabelle);
  }
  return kopf;
}
