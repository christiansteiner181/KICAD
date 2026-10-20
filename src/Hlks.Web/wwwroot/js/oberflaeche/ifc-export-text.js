// IFC-Export (Modul 7 Teil B2, Dialog «IFC exportieren»): reine Hilfen für den Dialog – Anfrage an den Kern aus der Auswahl des Menschen,
// Prüfung der Auswahl, Darstellung des Exportberichts als HTML (alles Dynamische maskiert), Links zur Datei, Texte. Keine Fachlogik: Umfang,
// Entitäten, Zähler, Schemaprüfung und IDS-Ergebnis rechnet der Kern (POST /api/ifc/export); hier wird nur bedient und dargestellt.
// Läuft ohne Browser (Node-Test: ifc-export.test.mjs).

/** Maskiert Text für HTML. */
export const esc = (t) => String(t ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

/** Fassungen, die der Kern kennt (IFC 4.3 nicht, entwurf/modul-7.md Teil B2); IFC4 ist die Vorgabe. */
export const FASSUNGEN = [
  { id: 'IFC4', titel: 'IFC4', hinweis: 'Vorgabe: Stränge als IfcDistributionCircuit, Landeskoordinaten als IfcMapConversion' },
  { id: 'IFC2X3', titel: 'IFC2X3', hinweis: 'für ältere Programme: ohne Stränge und ohne Kartenbezug' },
];

/** Ausgangsauswahl: IFC4, alle Gewerke und Geschosse, ohne Räume, keine weiteren IDS-Dateien. */
export function auswahlStart() {
  return { fassung: 'IFC4', gewerkeAlle: true, gewerke: [], geschosseAlle: true, geschosse: [], raeume: false, ids: '' };
}

/** Pfade der weiteren IDS-Dateien aus dem Textfeld: eine Datei je Zeile, ohne Anführungszeichen und Leerraum, ohne Doppelte (Reihenfolge bleibt). */
export function idsPfade(text) {
  const gesehen = new Set();
  const aus = [];
  for (const z of String(text ?? '').split(/\r?\n/)) {
    const p = z.trim().replace(/^"+|"+$/g, '').trim();
    if (!p || gesehen.has(p.toLowerCase())) continue;
    gesehen.add(p.toLowerCase());
    aus.push(p);
  }
  return aus;
}

/** Prüft die Auswahl: true oder ein Satz, was fehlt. «alle» ist erlaubt; sonst muss mindestens ein Häkchen stehen. */
export function auswahlPruefen(a) {
  if (!FASSUNGEN.some((f) => f.id === a?.fassung)) return 'Die Fassung ist unbekannt (IFC4 oder IFC2X3).';
  if (!a.gewerkeAlle && (a.gewerke ?? []).length === 0) return 'Mindestens ein Gewerk wählen oder «alle Gewerke» ankreuzen.';
  if (!a.geschosseAlle && (a.geschosse ?? []).length === 0) return 'Mindestens ein Geschoss wählen oder «alle Geschosse» ankreuzen.';
  return true;
}

/** Körper für POST /api/ifc/export: { fassung, raeume, umfang?, ids? }. Das Ziel bestimmt der Kern (<Projekt>\ausgabe\ifc). */
export function anfrageAus(a) {
  const par = { fassung: a.fassung, raeume: !!a.raeume };
  const umfang = {};
  if (!a.gewerkeAlle && (a.gewerke ?? []).length > 0) umfang.gewerke = [...a.gewerke];
  if (!a.geschosseAlle && (a.geschosse ?? []).length > 0) umfang.geschosse = [...a.geschosse];
  if (Object.keys(umfang).length > 0) par.umfang = umfang;
  const ids = idsPfade(a.ids);
  if (ids.length > 0) par.ids = ids;
  return par;
}

/** Eine Zeile über den Umfang: «IFC4 · alle Gewerke · EG, 1.OG · ohne Räume · 1 weitere IDS-Datei». */
export function umfangText(a) {
  const t = [a.fassung];
  t.push(a.gewerkeAlle ? 'alle Gewerke' : `${(a.gewerke ?? []).length} ${(a.gewerke ?? []).length === 1 ? 'Gewerk' : 'Gewerke'}`);
  t.push(a.geschosseAlle ? 'alle Geschosse' : (a.geschosse ?? []).join(', '));
  t.push(a.raeume ? 'mit Räumen' : 'ohne Räume');
  const n = idsPfade(a.ids).length;
  if (n > 0) t.push(`${n} weitere IDS-${n === 1 ? 'Datei' : 'Dateien'}`);
  return t.join(' · ');
}

// ---------------------------------------------------------------------------------------------- Pfade und Links

/** Dateiname eines Windows- oder Unix-Pfads. */
export const dateiName = (pfad) => String(pfad ?? '').split(/[\\/]/).filter(Boolean).pop() ?? '';

/** Ordner einer Datei (Pfad ohne den letzten Teil); leer, wenn der Pfad keinen Ordner hat. */
export function ordnerVon(pfad) {
  const s = String(pfad ?? '');
  const i = Math.max(s.lastIndexOf('\\'), s.lastIndexOf('/'));
  return i > 0 ? s.slice(0, i) : '';
}

/** Adresse zum Herunterladen einer Datei aus ausgabe\ifc (GET /api/ifc/export/datei?pfad=<Dateiname>). */
export const downloadUrl = (pfad) => `/api/ifc/export/datei?pfad=${encodeURIComponent(dateiName(pfad))}`;

/** Zielhinweis im Dialog: «<Projektordner>\ausgabe\ifc». Ohne bekannten Projektordner der allgemeine Hinweis. */
export function zielText(ort) {
  const o = String(ort ?? '').replace(/[\\/]+$/, '');
  return o ? `${o}\\ausgabe\\ifc` : '<Projektordner>\\ausgabe\\ifc';
}

export function groesseText(bytes) {
  const b = bytes === null || bytes === '' ? NaN : Number(bytes);
  if (!Number.isFinite(b) || b < 0) return '–';
  if (b < 1024) return `${b} Byte`;
  if (b < 1024 * 1024) return `${(b / 1024).toFixed(1).replace('.', ',')} kB`;
  return `${(b / 1024 / 1024).toFixed(1).replace('.', ',')} MB`;
}

export function dauerText(sekunden) {
  const s = sekunden === null || sekunden === '' ? NaN : Number(sekunden);
  if (!Number.isFinite(s)) return '–';
  return s < 10 ? `${s.toFixed(2).replace('.', ',')} s` : `${s.toFixed(1).replace('.', ',')} s`;
}

/** Fehlermeldung der Abfrage mit Vorschlag in einem Satz. */
export function fehlerText(e) {
  const t = e?.message ?? String(e);
  return e?.vorschlag ? `${t} – ${e.vorschlag}` : t;
}

/** Fehler des Servers in Klartext: 409 = kein Projekt offen, 400 = Eingabe (Vorschlag steht dabei), sonst die Meldung selbst. */
export function fehlerAnzeige(e) {
  const meldung = e?.message ?? String(e);
  if (e?.status === 409) return { titel: 'Kein Projekt offen', text: meldung, vorschlag: e.vorschlag || 'Zuerst ein Projekt öffnen oder anlegen (Register Datei).' };
  if (e?.status === 400) return { titel: 'Der Export ist so nicht möglich', text: meldung, vorschlag: e.vorschlag || '' };
  return { titel: 'Nicht exportiert', text: meldung, vorschlag: e?.vorschlag || '' };
}

/**
 * Mögliche Kennzeichen des Modellelements zu einem Eintrag im IDS-Ergebnis. Der Kern nennt bei Ports «Element.Anschluss» und bei der Dämmung
 * «Dämmung Element»; angeklickt werden soll das Element selbst. Reihenfolge: zuerst der Text selbst (Kennzeichen dürfen Punkte enthalten),
 * dann ohne Vorsatz «Dämmung », dann ohne die hinteren Punktteile.
 */
export function kennzeichenKandidaten(text) {
  const t = String(text ?? '').trim();
  if (!t || t.startsWith('#')) return [];
  const aus = [t];
  const ohneDaemmung = t.replace(/^Dämmung\s+/, '');
  if (ohneDaemmung !== t) aus.push(ohneDaemmung);
  let rest = ohneDaemmung;
  for (let i = rest.lastIndexOf('.'); i > 0; i = rest.lastIndexOf('.')) {
    rest = rest.slice(0, i);
    if (!aus.includes(rest)) aus.push(rest);
  }
  return aus;
}

// ---------------------------------------------------------------------------------------------- Bericht als HTML

const ZAEHLER = [['elemente', 'Elemente'], ['raeume', 'Räume'], ['ports', 'Anschlüsse'], ['verbindungen', 'Verbindungen'], ['systeme', 'Systeme'],
  ['straenge', 'Stränge'], ['anlagen', 'Anlagen'], ['regelkreise', 'Regelkreise'], ['typobjekte', 'Typobjekte'], ['daemmungen', 'Dämmungen'],
  ['geschosse', 'Geschosse'], ['uebersprungen', 'Übersprungen']];

const gut = (t) => /^(bestanden|fehlerfrei)/.test(String(t ?? ''));
/** CSS-Klasse zum Ergebnis: ok (bestanden, fehlerfrei), schlecht (nicht bestanden, Fehler, abgewichen), leise (nicht anwendbar). */
export const ergebnisKlasse = (t) => (gut(t) ? 'ok' : /^nicht anwendbar/.test(String(t ?? '')) ? 'leise' : 'schlecht');

const marke = (titel, wert) => `<span class="ie-marke ${ergebnisKlasse(wert)}" data-art="${esc(titel)}"><span class="ie-marke-t">${esc(titel)}</span> <b>${esc(wert ?? '–')}</b></span>`;

/** Datei, Grösse, Links, Ergebnisse (Kontrolle, Schemaprüfung, IDS) und Dauer. */
export function kopfHtml(b) {
  const name = dateiName(b?.datei);
  const e = b?.ergebnis ?? {};
  const bericht = b?.bericht ?? {};
  const links = [`<a class="ie-download" href="${esc(downloadUrl(b?.datei))}" download="${esc(name)}">IFC-Datei herunterladen</a>`];
  if (bericht.md) links.push(`<a class="ie-bericht-md" href="${esc(downloadUrl(bericht.md))}" download="${esc(dateiName(bericht.md))}">Bericht (Markdown)</a>`);
  if (bericht.json) links.push(`<a class="ie-bericht-json" href="${esc(downloadUrl(bericht.json))}" download="${esc(dateiName(bericht.json))}">Bericht (JSON)</a>`);
  const d = b?.dauer ?? {};
  const teile = [['Aufbau und Schreiben', d.aufbauUndSchreiben], ['Schemaprüfung', d.schemapruefung], ['IDS', d.ids]].filter(([, v]) => Number.isFinite(Number(v)) && v !== null);
  return `<div class="ie-datei"><div class="ie-datei-name" title="${esc(b?.datei)}"><b class="ie-name">${esc(name)}</b>`
    + ` <span class="ie-groesse leise">${esc(groesseText(b?.groesseBytes))} · ${esc(b?.fassung ?? '')} · ${esc(b?.entitaetenDatei ?? '–')} Entitäten</span></div>`
    + `<div class="ie-links">${links.join(' · ')}</div>`
    + `<div class="ie-marken">${marke('Kontrolle beim Zurücklesen', e.kontrolle)}${marke('Schemaprüfung', e.schemapruefung)}${marke('IDS', e.ids)}</div>`
    + `<div class="ie-dauer leise">Dauer <b class="ie-dauer-zahl">${esc(dauerText(b?.dauerSekunden))}</b>${teile.length ? ` (${teile.map(([t, v]) => `${esc(t)} ${esc(dauerText(v))}`).join(', ')})` : ''}</div></div>`;
}

/** Zähler: Übersicht, Tabelle je Klasse (mit Entität, Typobjekt, PredefinedType) und je Entität. */
export function zaehlerHtml(b) {
  const a = b?.anzahl ?? {};
  const uebersicht = ZAEHLER.filter(([k]) => a[k] !== undefined).map(([k, t]) => `<span class="ie-zaehl" data-zaehler="${esc(k)}">${esc(t)} <b>${esc(a[k])}</b></span>`).join('');
  const zeilen = (b?.jeKlasse ?? []).map((k) => `<tr><td>${esc(k.klasse)}</td><td>${esc(k.entitaet)}</td><td>${esc(k.typobjekt ?? '–')}</td><td>${esc(k.predefinedType ?? '–')}</td><td class="zahl">${esc(k.anzahl)}</td></tr>`).join('');
  const jeEntitaet = Object.entries(b?.jeEntitaet ?? {}).map(([k, v]) => `<tr><td>${esc(k)}</td><td class="zahl">${esc(v)}</td></tr>`).join('');
  return `<div class="ie-abschnitt ie-zaehler"><h4>Zähler</h4><div class="ie-zaehlzeile">${uebersicht}</div>`
    + `<table class="tab ie-klassen"><thead><tr><th>Klasse</th><th>Entität</th><th>Typobjekt</th><th>PredefinedType</th><th class="zahl">Anzahl</th></tr></thead><tbody>${zeilen || '<tr><td colspan="5" class="leise">keine</td></tr>'}</tbody></table>`
    + `<details class="ie-entitaeten"><summary>Je IFC-Entität (${Object.keys(b?.jeEntitaet ?? {}).length})</summary><table class="tab"><thead><tr><th>Entität</th><th class="zahl">Anzahl</th></tr></thead><tbody>${jeEntitaet}</tbody></table></details></div>`;
}

/** Kennzeichen als Knopf: ein Klick wählt das Element im Modell (der Dialog hängt die Behandlung an «.ie-kz»). */
export const kennzeichenKnopf = (kz, titel = '') => `<button type="button" class="ie-kz" data-kz="${esc(kz)}"${titel ? ` title="${esc(titel)}"` : ''}>${esc(kz)}</button>`;

/** Übersprungene Elemente mit Grund, je Grund gruppiert. */
export function uebersprungenHtml(b) {
  const liste = b?.uebersprungen ?? [];
  const gesamt = b?.anzahl?.uebersprungen ?? liste.length;
  if (liste.length === 0 && !gesamt) return '<div class="ie-abschnitt ie-uebersprungen"><h4>Übersprungene Elemente</h4><p class="leise ie-keine">Keine.</p></div>';
  const gruppen = new Map();
  for (const u of liste) { if (!gruppen.has(u.grund)) gruppen.set(u.grund, []); gruppen.get(u.grund).push(u); }
  const summe = b?.uebersprungenJeGrund ?? {};
  const grundZeilen = [...gruppen].map(([grund, l]) => `<li class="ie-grund" data-grund="${esc(grund)}"><b>${esc(grund)}</b> (${esc(summe[grund] ?? l.length)}): `
    + `${l.slice(0, 30).map((u) => kennzeichenKnopf(u.element, u.klasse)).join(' ')}${l.length > 30 ? ` … und ${l.length - 30} weitere` : ''}</li>`).join('');
  const mehr = gesamt > liste.length ? `<p class="leise">Der Bericht nennt die ersten ${esc(liste.length)} von ${esc(gesamt)}; die Zahlen je Grund stehen oben.</p>` : '';
  return `<div class="ie-abschnitt ie-uebersprungen"><h4>Übersprungene Elemente (${esc(gesamt)})</h4><ul class="ie-gruende">${grundZeilen}</ul>${mehr}</div>`;
}

/** Schemaprüfung (Xbim) und Kontrolle beim Zurücklesen. */
export function schemaHtml(b) {
  const s = b?.schemapruefung ?? {};
  const k = b?.kontrolle ?? {};
  const bsp = (s.beispiele ?? []).map((x) => `<li>${esc(x)}</li>`).join('');
  const abw = (k.abweichungen ?? []).map((x) => `<li>${esc(x)}</li>`).join('');
  return `<div class="ie-abschnitt ie-schema"><h4>Schemaprüfung</h4>`
    + `<p><span class="ie-ergebnis ${ergebnisKlasse(s.ergebnis)}">${esc(s.ergebnis ?? '–')}</span> ${esc(s.entitaetenGeprueft ?? '–')} Entitäten geprüft, ${esc(s.meldungen ?? '–')} Meldungen`
    + ` <span class="leise">(${esc(s.werkzeug ?? '')})</span></p>${bsp ? `<ul class="ie-liste">${bsp}</ul>` : ''}`
    + `<p>Kontrolle beim Zurücklesen: <span class="ie-ergebnis ${ergebnisKlasse(k.ergebnis)}">${esc(k.ergebnis ?? '–')}</span> `
    + `<span class="leise">(${esc(k.produkte ?? '–')} Elemente, ${esc(k.ports ?? '–')} Anschlüsse, ${esc(k.gruppen ?? '–')} Gruppen; ${esc(k.fassungGelesen ?? '')}, ${esc(k.einheitGelesen ?? '')})</span></p>`
    + `${abw ? `<ul class="ie-liste">${abw}</ul>` : ''}</div>`;
}

/** IDS-Ergebnis je Datei und Regel; nicht bestandene Regeln sind offen, die Kennzeichen der betroffenen Elemente sind Knöpfe. */
export function idsHtml(b) {
  const blocks = (b?.ids ?? []).map((i) => {
    const regeln = (i.regeln ?? []).map((r) => {
      const befunde = r.befunde ?? [];
      const nicht = r.ergebnis === 'nicht bestanden';
      const zahl = r.nichtBestandenElemente ?? befunde.length;
      const kopf = `<span class="ie-regel-name">${esc(r.name)}</span> <span class="ie-ergebnis ${ergebnisKlasse(r.ergebnis)}">${esc(r.ergebnis)}</span>`
        + `${r.anwendbar !== undefined && r.anwendbar !== null ? ` <span class="leise">${esc(r.anwendbar)} anwendbar${nicht ? `, ${esc(zahl)} nicht bestanden` : ''}</span>` : ''}`
        + `${r.grund ? ` <span class="leise">(${esc(r.grund)})</span>` : ''}${r.ifcVersion ? ` <span class="ie-fassung leise">${esc(r.ifcVersion)}</span>` : ''}`;
      if (befunde.length === 0) return `<li class="ie-regel" data-ergebnis="${esc(r.ergebnis)}">${kopf}</li>`;
      const chips = befunde.slice(0, 60).map((f) => kennzeichenKnopf(f.kennzeichen, `${f.entitaet ?? ''}${(f.gruende ?? []).length ? ': ' + f.gruende.join('; ') : ''}`)).join(' ');
      const gruende = [...new Set(befunde.flatMap((f) => f.gruende ?? []))].slice(0, 5);
      const rest = Math.max(zahl, befunde.length) - Math.min(befunde.length, 60);
      return `<li class="ie-regel" data-ergebnis="${esc(r.ergebnis)}"><details${nicht ? ' open' : ''}><summary>${kopf}</summary>`
        + `${gruende.length ? `<ul class="ie-liste ie-gruende-regel">${gruende.map((g) => `<li>${esc(g)}</li>`).join('')}</ul>` : ''}`
        + `<div class="ie-betroffene">${chips}${rest > 0 ? ` <span class="leise">… und ${esc(rest)} weitere</span>` : ''}</div></details></li>`;
    }).join('');
    const xsd = (i.xsdMeldungen ?? []).map((x) => `<li>${esc(x)}</li>`).join('');
    return `<div class="ie-ids" data-basis="${i.basis ? '1' : '0'}"><div class="ie-ids-kopf"><b class="ie-ids-titel">${esc(i.titel ?? dateiName(i.datei))}</b>${i.basis ? ' <span class="ie-basis">IDS-Basis H+K</span>' : ''}`
      + ` <span class="ie-ergebnis ${ergebnisKlasse(i.ergebnis)}">${esc(i.ergebnis ?? '–')}</span> <span class="leise" title="${esc(i.datei)}">${esc(dateiName(i.datei))}</span></div>`
      + `${xsd ? `<ul class="ie-liste">${xsd}</ul>` : ''}<ul class="ie-regeln">${regeln || '<li class="leise">keine Regeln</li>'}</ul></div>`;
  });
  return `<div class="ie-abschnitt ie-idsblock"><h4>IDS-Prüfung</h4>${blocks.join('') || '<p class="leise">Keine IDS geprüft.</p>'}</div>`;
}

/** Hinweise und Warnungen des Exports (eingeklappt, wenn es viele sind). */
export function hinweiseHtml(b) {
  const w = b?.warnungen ?? [];
  const h = b?.hinweise ?? [];
  const liste = (l) => `<ul class="ie-liste">${l.map((x) => `<li>${esc(x)}</li>`).join('')}</ul>`;
  return `${w.length ? `<div class="ie-abschnitt ie-warnungen"><h4>Warnungen (${w.length})</h4>${liste(w)}</div>` : ''}`
    + `${h.length ? `<details class="ie-abschnitt ie-hinweise"><summary><b>Hinweise (${h.length})</b></summary>${liste(h)}</details>` : ''}`;
}

/** Der ganze Bericht. Jeder Wert aus dem Bericht ist maskiert; Kennzeichen stehen nur als Text und in data-kz. */
export function berichtHtml(b) {
  return kopfHtml(b) + zaehlerHtml(b) + uebersprungenHtml(b) + schemaHtml(b) + idsHtml(b) + hinweiseHtml(b);
}

/** Eine Zeile als Zusammenfassung der Meldung nach dem Export. */
export function zusammenfassung(b) {
  const e = b?.ergebnis ?? {};
  const n = b?.anzahl?.elemente ?? 0;
  const nicht = e.idsRegelnNichtBestanden ?? 0;
  return `${dateiName(b?.datei)}: ${n} ${n === 1 ? 'Element' : 'Elemente'}, Schemaprüfung ${e.schemapruefung ?? '–'}, IDS ${e.ids ?? '–'}${nicht ? ` (${nicht} ${nicht === 1 ? 'Regel' : 'Regeln'} nicht bestanden)` : ''}.`;
}
