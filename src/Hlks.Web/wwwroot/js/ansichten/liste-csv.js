// CSV-Ausgabe einer Listenansicht (Nachbearbeitung N3, Punkt 3): macht aus der aufbereiteten Liste (listenformat.aufbereiten) den Text einer CSV-Datei.
// Rein (ohne DOM, Node-testbar). Format: UTF-8 mit BOM (Excel erkennt Umlaute), Semikolon als Trenner, Dezimalkomma, Zeilenende CRLF, erste Zeile = Kopfzeile
// mit den Spaltentiteln. Es steht drin, was die Liste zeigt – dieselben Spalten, derselbe Text je Zelle (formatWert), dieselbe Reihenfolge wie beim Sortieren
// im Bild; die Zahlen kommen vom Kern, hier wird nichts gerechnet. Listen mit mehreren Tabellen (Mengen, Materialliste) stehen untereinander, je mit einer
// Titelzeile und durch eine Leerzeile getrennt. Fusszeilen (Summen der Raumgrenzen und Hüllbauteile, Totalzeile der Sanitär-Zusammenstellung) folgen unter ihrer Tabelle. Spalten mit nurCsv (Formeln der
// Sanitär-Zusammenstellung mit Excel-Zelle) stehen nur in der Datei; Zellen mit eigener Formatierung (Spalte.format) erscheinen genau so wie in der Ansicht.

import { zelle, sortiere } from './listenformat.js';

export const BOM = '﻿';
export const TRENNER = ';';
export const ZEILENENDE = '\r\n';
export const MIME = 'text/csv;charset=utf-8';

/** Ein Feld für die CSV-Zeile: in Anführungszeichen, wenn es Trenner, Anführungszeichen oder Zeilenumbruch enthält (Anführungszeichen verdoppelt). */
export function csvFeld(wert) {
  const t = wert === null || wert === undefined ? '' : String(wert);
  return /[;"\r\n]/.test(t) ? `"${t.replace(/"/g, '""')}"` : t;
}

const zeile = (felder) => felder.map(csvFeld).join(TRENNER);

/**
 * CSV-Text einer aufbereiteten Liste.
 *   daten       Ergebnis von aufbereiten(name, antwort): { titel, tabellen: [{ titel, spalten[], zeilen[], fuss? }] }
 *   sortierung  Map Tabellenindex → { schluessel, absteigend } (die Sortierung der Ansicht, optional)
 * Ergebnis: Text mit führendem BOM.
 */
export function csvAus(daten, sortierung = new Map()) {
  const zeilen = [];
  const tabellen = Array.isArray(daten?.tabellen) ? daten.tabellen : [];
  const mehrere = tabellen.length > 1;
  tabellen.forEach((t, index) => {
    if (index > 0) zeilen.push('');
    if (mehrere && t.titel) zeilen.push(zeile([t.titel]));
    const spalten = t.spalten ?? [];
    if (spalten.length > 0) zeilen.push(zeile(spalten.map((s) => s.titel)));   // eine leere Liste hat noch keine Spalten (sie entstehen aus den Daten)
    const sort = sortierung instanceof Map ? sortierung.get(index) : undefined;
    const sortiert = sort ? sortiere(t.zeilen ?? [], sort.schluessel, sort.absteigend) : (t.zeilen ?? []);
    for (const z of sortiert) zeilen.push(zeile(spalten.map((s) => zelle(s, z[s.schluessel]))));
    for (const f of Array.isArray(t.fuss) ? t.fuss : []) zeilen.push(zeile(spalten.map((s) => zelle(s, f[s.schluessel]))));
  });
  return BOM + (zeilen.length > 0 ? zeilen.join(ZEILENENDE) + ZEILENENDE : '');
}

/** Anzahl der Datenzeilen (ohne Kopf- und Fusszeilen) über alle Tabellen. */
export function csvZeilenzahl(daten) {
  return (Array.isArray(daten?.tabellen) ? daten.tabellen : []).reduce((n, t) => n + (t.zeilen?.length ?? 0), 0);
}

/** Datum als JJJJ-MM-TT (lokale Zeit). */
export function datumText(d = new Date()) {
  const z = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${z(d.getMonth() + 1)}-${z(d.getDate())}`;
}

/** Dateiname ohne Zeichen, die Windows nicht erlaubt: «Kollisionsliste_Bernstrasse_2026-10-19.csv». Titel und Projekt aus der Liste, Datum von aussen. */
export function csvDateiname(daten, datum = datumText()) {
  const sauber = (t) => String(t ?? '').replace(/[\\/:*?"<>|\u0000-\u001f]/g, '').replace(/\s+/g, '_').replace(/^[._]+|[._]+$/g, '');
  const teile = [sauber(daten?.titel) || 'Liste', sauber(daten?.projekt), datum].filter(Boolean);
  return teile.join('_') + '.csv';
}

/** Stellt sicher, dass ein vom Benutzer geänderter Name auf «.csv» endet und keine unzulässigen Zeichen hat. */
export function csvDateinameSauber(name, ersatz = 'Liste.csv') {
  const t = String(name ?? '').replace(/[\\/:*?"<>|\u0000-\u001f]/g, '').trim();
  if (!t || /^\.+$/.test(t)) return ersatz;
  return /\.csv$/i.test(t) ? t : t + '.csv';
}
