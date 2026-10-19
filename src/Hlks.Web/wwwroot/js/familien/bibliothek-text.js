// Familienbibliothek in der Oberfläche (Modul 2, C3; entwurf/modul-2-bibliothek.md): reine Text-, HTML- und Parameterfunktionen (in Node getestet,
// bibliothek.test.mjs) für den Explorer (Projektbrowser, Dialog «Familien im Explorer …»), «Aktualisieren …», «Familie laden» und die Statusleiste.
// Die Oberfläche entscheidet und rechnet nichts: Stand, Version, Sichtbarkeit, Doppel und die Vorschau einer Übernahme kommen vom Kern (Abfrage
// familien_bibliothek, Befehle familie_laden, familie_aktualisieren, familie_in_bibliothek, familienauswahl_setzen). Einzige Spiegelung der Kernregel:
// sichtbarNach() – nur für die Vorschau im Dialog, solange die Auswahl noch nicht gespeichert ist; der Kern (Trockenlauf von familienauswahl_setzen)
// nennt die massgebliche Zahl, und ein Browser-Test vergleicht die Spiegelung mit dem Feld «sichtbar» des Kerns.
//
// Stand einer Familie (Kern, stand.art): nicht_im_projekt · aktuell · neuere_version · im_projekt_geaendert · parallel_geaendert · nicht_in_bibliothek · gesperrt.

import { esc } from './familien-text.js';

// ================================================================================================ Namen

const GEWERK_NAMEN = { sanitaer: 'Sanitär', heizung: 'Heizung', lueftung: 'Lüftung', kaelte: 'Kälte', elektro: 'Elektro', msrl: 'MSRL', architektur: 'Architektur' };
export const gewerkName = (g) => GEWERK_NAMEN[g] ?? (g ? String(g)[0].toUpperCase() + String(g).slice(1) : '–');
export const STATUS_NAMEN = { entwurf: 'Entwurf', freigegeben: 'freigegeben' };
export const statusName = (s) => STATUS_NAMEN[s ?? 'entwurf'] ?? String(s);
const lc = (t) => String(t ?? '').trim().toLowerCase();
const nachText = (a, b) => String(a ?? '').localeCompare(String(b ?? ''), 'de', { numeric: true });

/** «19.10.2026 14:32» (Ortszeit) aus einer ISO-Zeit; leer, wenn unlesbar. */
export function zeitText(iso) {
  const d = new Date(iso ?? '');
  if (Number.isNaN(d.getTime())) return '';
  const z = (n) => String(n).padStart(2, '0');
  return `${z(d.getDate())}.${z(d.getMonth() + 1)}.${d.getFullYear()} ${z(d.getHours())}:${z(d.getMinutes())}`;
}

// ================================================================================================ Stand und Marken (Projektbrowser)

/** Marke einer Familie des Projekts nach bibliothekStand (Abfrage familien). «aktuell» und alles andere: keine Marke. */
const MARKEN = { neuere_version: 'neuere Version', parallel_geaendert: 'parallel geändert', im_projekt_geaendert: 'noch nicht gespeichert' };
export const standMarke = (stand) => MARKEN[stand?.art] ?? '';

/** Lässt sich die Familie aus der Bibliothek aktualisieren (Knopf «Aktualisieren …»)? */
export const aktualisierbar = (stand) => stand?.art === 'neuere_version' || stand?.art === 'parallel_geaendert';

/** Tooltip-Zeile zum Stand (Text des Kerns; bei Warteschlange und Offline mit Zusatz). */
export function standTipp(stand) {
  if (!stand?.text) return '';
  const zusatz = [stand.warteschlange ? 'wartet in der Warteschlange' : '', stand.offline ? 'Bibliothek nicht erreichbar (letzter bekannter Stand)' : ''].filter(Boolean);
  return `Bibliothek: ${stand.text}${zusatz.length ? ` (${zusatz.join(', ')})` : ''}`;
}

/** Knöpfe am Knoten einer Projektfamilie: «Aktualisieren …» bei neuerer Version bzw. parallel geänderter Familie. */
export function standKnoepfe(f) {
  if (!aktualisierbar(f?.bibliothekStand)) return [];
  const par = f.bibliothekStand.art === 'parallel_geaendert';
  return [{
    aktion: 'familie.aktualisieren', text: 'Aktualisieren …', kurz: '↻', param: { familie: f.kennzeichen },   // im schmalen Browser nur das Zeichen (Tooltip nennt die Aktion)
    titel: par ? `${f.kennzeichen} wurde parallel geändert – Bibliothek übernehmen oder die eigene Fassung als aktuell setzen` : `${f.kennzeichen}: neuere Version in der Bibliothek – Vorschau und Übernahme`,
  }];
}

/** Knoten einer Bibliotheksfamilie, die nicht im Projekt ist: grau, Marke «Bibliothek» (und «Entwurf»); Doppelklick lädt und platziert. */
export function bibliotheksKnoten(f) {
  const entwurf = f.status !== 'freigegeben';
  const mitgeliefert = f.quelle === 'mitgeliefert';
  const doppel = Array.isArray(f.doppel) && f.doppel.length > 0;
  const tipp = [`${f.name ?? f.datei ?? f.id} · ${f.kategorie ?? '?'} · ${entwurf ? 'Entwurf' : 'freigegeben'}${f.version != null ? ` · Version ${f.version}` : ''}`,
    mitgeliefert ? 'Mitgeliefert mit der Software (nur lesbar).' : `In der Familienbibliothek${f.projekt ? ` (zuletzt von Projekt ${f.projekt})` : ''}.`,
    f.hersteller ? `Hersteller: ${f.hersteller}${f.produkt ? `, ${f.produkt}` : ''}` : '',
    doppel ? 'Mögliches Doppel: gleicher Hersteller und gleiches Produkt unter anderer Kennung.' : '',
    'Doppelklick lädt die Familie ins Projekt und platziert sie.'].filter(Boolean).join('\n');
  return {
    key: `bib:${f.quelle ?? 'bibliothek'}:${f.id ?? f.datei}`, label: f.name ?? f.datei ?? f.id, sub: f.version != null ? `v${f.version}` : '', sym: 'famFamilie', art: 'bib-fam',
    bibId: f.quelle === 'mitgeliefert' ? undefined : f.id, bibDatei: f.quelle === 'mitgeliefert' ? f.datei : undefined, klasse: f.kategorie,
    marken: ['Bibliothek', ...(entwurf ? ['Entwurf'] : []), ...(doppel ? ['Doppel?'] : [])],
    marke: 'Bibliothek', css: ['bibliothek', entwurf ? 'entwurf' : ''].filter(Boolean).join(' '), tipp,
  };
}

// ================================================================================================ Auswahl «Familien im Explorer»

export const FILTER_FELDER = [['gewerke', 'Gewerk'], ['kategorien', 'Kategorie'], ['hersteller', 'Hersteller'], ['status', 'Status']];
const LISTEN = ['gewerke', 'kategorien', 'hersteller', 'status', 'ein', 'aus'];

/** Zustand des Dialogs aus Projektinformation.familienAuswahl (Antwort familien_bibliothek, Feld auswahl): null = nur die Familien des Projekts. */
export function auswahlLesen(auswahl) {
  const s = { an: auswahl !== null && auswahl !== undefined && typeof auswahl === 'object' };
  for (const l of LISTEN) s[l] = s.an && Array.isArray(auswahl[l]) ? auswahl[l].map(String) : [];
  return s;
}

/** Parameter für familienauswahl_setzen: null (nur Projekt) oder { gewerke, kategorien, hersteller, status, ein, aus } ohne leere Listen. */
export function auswahlParameter(s) {
  if (!s?.an) return { auswahl: null };
  const a = {};
  for (const l of LISTEN) if (Array.isArray(s[l]) && s[l].length > 0) a[l] = [...s[l]];
  return { auswahl: a };
}

const gleich = (a, b) => lc(a) === lc(b);
const enthalten = (liste, wert) => (liste ?? []).some((x) => gleich(x, wert));

/** Treffen die Filter (Gewerk, Kategorie, Hersteller, Status; leere Liste = keine Einschränkung) die Familie? Ohne ein/aus. */
export function filterTrifft(s, f) {
  const trifft = (liste, wert) => (liste ?? []).length === 0 || (wert !== null && wert !== undefined && enthalten(liste, wert));
  return trifft(s.gewerke, f.gewerk) && trifft(s.kategorien, f.kategorie) && trifft(s.hersteller, f.hersteller) && trifft(s.status, f.status ?? 'entwurf');
}

/** Sichtbar im Explorer nach der Auswahl – SPIEGELUNG der Kernregel FamilienBibliothek.Sichtbar (aus nie, ein immer, sonst die Filter); nur für die Vorschau im Dialog. */
export function sichtbarNach(s, f) {
  if (!s?.an) return false;
  if (f.id && enthalten(s.aus, f.id)) return false;
  if (f.id && enthalten(s.ein, f.id)) return true;
  return filterTrifft(s, f);
}

const ohne = (liste, wert) => (liste ?? []).filter((x) => !gleich(x, wert));

/** Häkchen einer einzelnen Familie: übersteuert die Filter über ein/aus; passt der Wunsch zum Filter, entfällt der Eintrag. */
export function einzelnSetzen(s, f, an) {
  if (!f?.id) return s;
  const nach = filterTrifft(s, f);
  const neu = { ...s, ein: ohne(s.ein, f.id), aus: ohne(s.aus, f.id) };
  if (an && !nach) neu.ein = [...neu.ein, f.id];
  if (!an && nach) neu.aus = [...neu.aus, f.id];
  return neu;
}

/** Ein Wert in der Filterliste an- bzw. ausschalten. */
export function filterUmschalten(s, feld, wert) {
  const vorhanden = enthalten(s[feld], wert);
  return { ...s, [feld]: vorhanden ? ohne(s[feld], wert) : [...(s[feld] ?? []), wert] };
}

/** Auswahlwerte eines Filters aus den Familien der Bibliothek: [{ wert, text, anzahl }], sortiert; bei Status immer beide. */
export function filterWerte(familien, feld) {
  if (feld === 'status') return ['entwurf', 'freigegeben'].map((w) => ({ wert: w, text: statusName(w), anzahl: (familien ?? []).filter((f) => (f.status ?? 'entwurf') === w).length }));
  const key = { gewerke: 'gewerk', kategorien: 'kategorie', hersteller: 'hersteller' }[feld];
  const m = new Map();
  for (const f of familien ?? []) {
    const w = f[key];
    if (w === null || w === undefined || String(w).trim() === '') continue;
    const k = lc(w);
    if (!m.has(k)) m.set(k, { wert: String(w).trim(), text: feld === 'gewerke' ? gewerkName(String(w).trim()) : String(w).trim(), anzahl: 0 });
    m.get(k).anzahl++;
  }
  return [...m.values()].sort((a, b) => nachText(a.text, b.text));
}

/** Zeilen der Liste im Dialog: alle Familien der Bibliothek (beide Quellen) nach Kategorie und Name, mit Häkchen nach der Auswahl; suche = nur Anzeige. */
export function explorerZeilen(familien, s, suche = '') {
  const q = lc(suche);
  return (familien ?? [])
    .filter((f) => !q || [f.name, f.id, f.kategorie, f.hersteller, f.produkt, f.datei, f.gewerk].some((t) => lc(t).includes(q)))
    .map((f) => ({
      key: `${f.quelle ?? 'bibliothek'}:${f.id ?? f.datei}`, id: f.id ?? null, name: f.name ?? f.datei ?? f.id ?? '?', kategorie: f.kategorie ?? '', gewerk: f.gewerk ?? '',
      hersteller: f.hersteller ?? '', produkt: f.produkt ?? '', status: f.status ?? 'entwurf', version: f.version ?? null, quelle: f.quelle ?? 'bibliothek',
      imProjekt: f.imProjekt ?? null, doppel: f.doppel ?? [], haken: !!f.imProjekt || sichtbarNach(s, f), fest: !!f.imProjekt || !f.id || !s?.an,
      uebersteuert: !!f.id && (enthalten(s?.ein, f.id) || enthalten(s?.aus, f.id)),
    }))
    .sort((a, b) => nachText(a.kategorie, b.kategorie) || nachText(a.name, b.name));
}

/** Zahlen für den Fuss des Dialogs (aus allen Familien, nicht nur den gesuchten). */
export function explorerZahlen(familien, s) {
  const alle = familien ?? [];
  const imProjekt = alle.filter((f) => f.imProjekt).length;
  const sichtbar = alle.filter((f) => !f.imProjekt && sichtbarNach(s, f)).length;
  return { gesamt: alle.length, imProjekt, sichtbar, ausserhalb: alle.length - imProjekt };
}

export function zahlenText(z) {
  return `${z.sichtbar} von ${z.ausserhalb} Bibliotheksfamilien sichtbar` + (z.imProjekt ? ` – dazu immer die ${z.imProjekt} Familie${z.imProjekt === 1 ? '' : 'n'} des Projekts` : '');
}

export const GILT_FUER_ALLE = 'Gilt für alle, die an diesem Projekt arbeiten.';

/** Doppel-Hinweise des Kerns (Feld doppel von familien_bibliothek) als Textzeilen. */
export function doppelTexte(doppel) {
  return (doppel ?? []).map((d) => `${d.hersteller}${d.produkt ? ` · ${d.produkt}` : ''}: ${(d.familien ?? []).map((f) => `«${f.name}»${f.quelle === 'mitgeliefert' ? ' (mitgeliefert)' : ''}`).join(' und ')}`);
}

/** Filterzeilen mit Chips (Gewerk, Kategorie, Hersteller, Status). Chip aus = keine Einschränkung durch diesen Wert. */
export function explorerFilterHtml(familien, s) {
  return FILTER_FELDER.map(([feld, titel]) => {
    const werte = filterWerte(familien, feld);
    const chips = werte.length === 0 ? '<span class="leise">(keine)</span>' : werte.map((w) =>
      `<button type="button" class="bib-chip${enthalten(s[feld], w.wert) ? ' an' : ''}" data-feld="${esc(feld)}" data-wert="${esc(w.wert)}" aria-pressed="${enthalten(s[feld], w.wert)}" ${s.an ? '' : 'disabled'} title="${esc(w.anzahl)} Familie${w.anzahl === 1 ? '' : 'n'}">${esc(w.text)}</button>`).join('');
    return `<div class="bib-filter" data-feld="${esc(feld)}"><span class="bib-filter-titel">${esc(titel)}</span><span class="bib-chips">${chips}</span></div>`;
  }).join('');
}

/** Liste mit Häkchen. Familien des Projekts sind fest angehakt («im Projekt»), ohne Auswahl ist alles grau. */
export function explorerListeHtml(zeilen, s) {
  if (!zeilen.length) return '<p class="leise bib-leer">Keine Familien in der Bibliothek.</p>';
  const kopf = '<thead><tr><th></th><th>Name</th><th>Kategorie</th><th>Hersteller</th><th>Status</th><th>Version</th><th></th></tr></thead>';
  const rows = zeilen.map((z) => {
    const hinweise = [z.imProjekt ? `im Projekt (${z.imProjekt}) – immer sichtbar` : '', z.quelle === 'mitgeliefert' ? 'mitgeliefert' : '', z.doppel.length ? 'mögliches Doppel' : '', z.uebersteuert ? 'einzeln gesetzt' : ''].filter(Boolean);
    return `<tr class="bib-zeile${z.haken ? ' an' : ''}${z.imProjekt ? ' im-projekt' : ''}${z.status !== 'freigegeben' ? ' entwurf' : ''}" data-key="${esc(z.key)}">`
      + `<td class="bib-haken"><input type="checkbox" data-id="${esc(z.id ?? '')}"${z.haken ? ' checked' : ''}${z.fest ? ' disabled' : ''} aria-label="${esc(z.name)} im Explorer zeigen"></td>`
      + `<td>${esc(z.name)}</td><td>${esc(z.kategorie)}</td><td>${esc(z.hersteller)}</td><td>${esc(statusName(z.status))}</td><td>${z.version ?? ''}</td>`
      + `<td class="bib-hinweis">${esc(hinweise.join(' · '))}</td></tr>`;
  }).join('');
  return `<table class="tab bib-tab">${kopf}<tbody>${rows}</tbody></table>`;
}

/** Fuss des Dialogs: Zahl der sichtbaren, Hinweis «gilt für alle», Antwort des Kerns auf den Trockenlauf, Doppel-Hinweise. */
export function explorerFussHtml({ zahlen, kernHinweis = '', kernFehler = '', doppel = [], an = true }) {
  const doppelHtml = doppel.length
    ? `<div class="bib-doppel"><b>Mögliche Doppel</b> (gleicher Hersteller und gleiches Produkt, andere Kennung – wird nicht zusammengeführt):<ul>${doppel.map((t) => `<li>${esc(t)}</li>`).join('')}</ul></div>` : '';
  return `<div class="bib-zahl">${esc(zahlenText(zahlen))}${an ? '' : ' <span class="leise">(Auswahl aus: der Explorer zeigt nur die Familien des Projekts.)</span>'} <span class="leise">${esc(GILT_FUER_ALLE)}</span></div>`
    + (kernFehler ? `<div class="bib-kernfehler">${esc(kernFehler)}</div>` : kernHinweis ? `<div class="bib-kern leise">Kern: ${esc(kernHinweis)}</div>` : '') + doppelHtml;
}

// ================================================================================================ Aktualisieren …

/** Wege im Dialog «Aktualisieren …» nach dem Stand: Bibliothek übernehmen (familie_aktualisieren) und/oder die eigene Fassung schreiben (familie_in_bibliothek). */
export function wegeFuer(stand) {
  const uebernehmen = { id: 'uebernehmen', befehl: 'familie_aktualisieren', text: 'Bibliothek übernehmen', titel: 'Aktualisieren' };
  switch (stand?.art) {
    case 'neuere_version': return [uebernehmen];
    case 'parallel_geaendert': return [uebernehmen, { id: 'eigene', befehl: 'familie_in_bibliothek', text: 'Meine Fassung wird aktuell', titel: 'Meine Fassung wird aktuell', alsAktuell: true, warnung: true }];
    case 'im_projekt_geaendert': return [{ ...uebernehmen, text: 'Bibliothek übernehmen (meine Fassung wird ersetzt)', ersetzt: true }, { id: 'schreiben', befehl: 'familie_in_bibliothek', text: 'Meine Fassung in die Bibliothek schreiben', titel: 'In die Bibliothek schreiben' }];
    case 'nicht_in_bibliothek': return [{ id: 'schreiben', befehl: 'familie_in_bibliothek', text: 'In die Bibliothek schreiben', titel: 'In die Bibliothek schreiben' }];
    default: return [];
  }
}

/** Erklärung zum Stand über der Wahl (Satz des Kerns plus, was die Wege bedeuten). */
export function standErklaerung(stand) {
  switch (stand?.art) {
    case 'neuere_version': return `In der Bibliothek liegt eine neuere Version${stand.aktuelleVersion != null ? ` (${stand.aktuelleVersion})` : ''}${stand.version != null ? `; das Projekt hat Version ${stand.version}` : ''}. Sie wird erst übernommen, wenn du es hier bestätigst.`;
    case 'parallel_geaendert': return 'Diese Familie wurde parallel geändert: in einem anderen Projekt und hier, von derselben Version aus. Beide Fassungen bleiben in der Bibliothek erhalten; welche gilt, entscheidest du.';
    case 'im_projekt_geaendert': return 'Die Fassung im Projekt ist noch nicht in der Bibliothek' + (stand.warteschlange ? ' (sie wartet in der Warteschlange, bis die Bibliothek erreichbar ist).' : '.');
    case 'aktuell': return 'Die Familie entspricht der aktuellen Version der Bibliothek.';
    case 'nicht_in_bibliothek': return 'Die Familie ist nicht in der Bibliothek.';
    case 'gesperrt': return stand.text ?? 'Die Familienbibliothek ist gesperrt.';
    default: return stand?.text ?? '';
  }
}

/** Wahl der Fassung für die Übernahme: Versionen, die neuer sind als die des Projekts (neueste zuerst; die aktuelle trägt den Wert '' = Vorgabe des Kerns), und offene
 *  Abzweige anderer Projekte. Hat die Familie im Projekt eine eigene Fassung oder ist sie ein Abzweig, stehen alle Versionen zur Wahl. */
export function versionsOptionen(stand, detail) {
  const aktuell = detail?.version ?? stand?.aktuelleVersion ?? null;
  const eigene = stand?.version ?? null;
  const aus = [];
  const vs = [...(detail?.versionen ?? [])].sort((a, b) => b.version - a.version);
  for (const v of vs) {
    if (eigene !== null && v.version <= eigene) continue;
    const wer = [v.projekt, v.akteurArt && v.akteurArt !== 'mensch' ? `${v.akteurArt}` : '', zeitText(v.zeit)].filter(Boolean).join(' · ');
    aus.push({ wert: v.version === aktuell ? '' : String(v.version), text: `Version ${v.version}${v.version === aktuell ? ' (aktuell)' : ''}${v.status === 'freigegeben' ? ' · freigegeben' : ''}${wer ? ` · ${wer}` : ''}`, art: 'version' });
  }
  for (const a of detail?.abzweige ?? []) {
    if (!a.offen || a.abzweig === stand?.abzweig) continue;
    aus.push({ wert: a.abzweig, text: `Parallele Fassung ${a.abzweig}${a.projekt ? ` · ${a.projekt}` : ''}${a.zeit ? ` · ${zeitText(a.zeit)}` : ''}`, art: 'abzweig' });
  }
  return aus;
}

/** Parameter von familie_aktualisieren (Trockenlauf und echt). */
export const aktualisierenParameter = (kz, version) => ({ familie: kz, ...(version ? { version: String(version) } : {}) });
/** Parameter von familie_in_bibliothek für «Meine Fassung wird aktuell» bzw. «In die Bibliothek schreiben». */
export const schreibenParameter = (kz, alsAktuell) => ({ familien: [kz], ...(alsAktuell ? { als_aktuell: true } : {}) });

const TEIL_TITEL = { parameter: 'Parameter', typen: 'Typ', koerper: 'Körper', anschluesse: 'Anschluss' };
const WEITERE = { sinnbild: 'Sinnbild', schemasymbol: 'Schemasymbol', leitung: 'Leitungsserie', masse: 'Massbezüge', konflikte: 'Konflikte', abdeckung: 'Abdeckung', unterlagen: 'Unterlagen' };

/** Zeilen der Änderungen aus der strukturierten Vorschau des Kerns (familien_bibliothek { familie }.familie.vorschau). */
export function aenderungsZeilen(vorschau) {
  if (!vorschau) return [];
  const z = [];
  for (const k of vorschau.kopf ?? []) z.push(`Kopf: ${k.feld} «${k.alt ?? '–'}» → «${k.neu ?? '–'}»`);
  for (const [feld, titel] of Object.entries(TEIL_TITEL)) {
    for (const x of vorschau[feld] ?? []) {
      if (x.art === 'neu') z.push(`${titel} «${x.name}»: neu`);
      else if (x.art === 'entfaellt') z.push(`${titel} «${x.name}»: entfällt`);
      else z.push(`${titel} «${x.name}»: ${(x.aenderungen ?? ['geändert']).join('; ')}`);
    }
  }
  for (const w of vorschau.weitere ?? []) z.push(`${WEITERE[w] ?? w} geändert`);
  return z;
}

/** HTML der Vorschau: Zusammenfassung (erste Zeile der Hinweise des Trockenlaufs), Änderungen, betroffene Elemente, Warnungen. */
export function vorschauHtml({ zusammenfassung = '', vorschau = null, hinweise = [], fehlendeTypen = null } = {}) {
  const daten = vorschau ?? null;
  const zeilenKern = new Set(daten?.zeilen ?? []);
  const zus = zusammenfassung || daten?.zusammenfassung || '';
  const aenderungen = aenderungsZeilen(daten);
  const elemente = daten?.elemente ?? [];
  const fehlend = fehlendeTypen ?? daten?.fehlendeTypen ?? [];
  const rest = (hinweise ?? []).slice(1).filter((h) => !zeilenKern.has(h));
  const warn = rest.filter((h) => /^Achtung/.test(h));
  const notizen = rest.filter((h) => !/^Achtung/.test(h));
  const html = [];
  if (zus) html.push(`<p class="bib-zus">${esc(zus)}</p>`);
  if (fehlend.length) html.push(`<div class="bib-warn bib-entfaellt"><b>Entfallende Typen, die benutzt werden:</b><ul>${fehlend.map((f) => `<li>Typ «${esc(f.typ)}» – ${f.elemente.length} Element${f.elemente.length === 1 ? '' : 'e'} (${esc(f.elemente.slice(0, 8).join(', '))}${f.elemente.length > 8 ? ' …' : ''})</li>`).join('')}</ul></div>`);
  if (aenderungen.length) html.push(`<h4 class="bib-h">Änderungen (${aenderungen.length})</h4><ul class="bib-zeilen">${aenderungen.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>`);
  else if (daten) html.push('<p class="leise">Keine Änderungen an Kopf, Parametern, Typen, Körpern oder Anschlüssen.</p>');
  if (elemente.length) html.push(`<h4 class="bib-h">Betroffene Elemente (${elemente.length})</h4><table class="tab bib-tab"><thead><tr><th>Element</th><th>Typ</th><th>Was sich ändert</th></tr></thead><tbody>`
    + elemente.map((e) => `<tr><td>${esc(e.element)}</td><td>${esc(e.typ ?? '')}</td><td>${(e.meldungen ?? []).map(esc).join('<br>')}</td></tr>`).join('') + '</tbody></table>');
  else if (daten) html.push('<p class="leise">Kein platziertes Element ändert sich.</p>');
  if (warn.length) html.push(`<div class="bib-warn">${warn.map((t) => `<p>${esc(t)}</p>`).join('')}</div>`);
  if (notizen.length) html.push(`<div class="bib-notizen"><b>Weitere Hinweise des Kerns</b>${notizen.map((t) => `<p>${esc(t)}</p>`).join('')}</div>`);
  return html.join('');
}

/** Text der Ablehnung durch den Kern (Fehler und Vorschlag) für den Dialog. */
export function ablehnungText(r) {
  const fehler = String(r?.fehler ?? r?.meldung ?? 'Abgelehnt.');
  return { fehler, vorschlag: String(r?.vorschlag ?? '') };
}

// ================================================================================================ Familie laden

/** Zeilen der Liste im Dialog «Familie laden»: Bibliothek zuerst, dann mitgelieferte; suche filtert nur die Anzeige. */
export function ladenZeilen(familien, suche = '') {
  const q = lc(suche);
  return (familien ?? []).filter((f) => f.id || f.datei)
    .filter((f) => !q || [f.name, f.id, f.kategorie, f.hersteller, f.produkt, f.datei].some((t) => lc(t).includes(q)))
    .map((f) => ({
      key: `${f.quelle ?? 'bibliothek'}:${f.id ?? f.datei}`, id: f.id ?? null, datei: f.quelle === 'mitgeliefert' ? f.datei : null, name: f.name ?? f.datei ?? f.id, kategorie: f.kategorie ?? '',
      hersteller: f.hersteller ?? '', status: f.status ?? 'entwurf', version: f.version ?? null, versionen: f.versionen ?? null, quelle: f.quelle ?? 'bibliothek', imProjekt: f.imProjekt ?? null,
    }))
    .sort((a, b) => (a.quelle === b.quelle ? 0 : a.quelle === 'bibliothek' ? -1 : 1) || nachText(a.kategorie, b.kategorie) || nachText(a.name, b.name));
}

/** Parameter von familie_laden: aus der Liste (id bzw. mitgelieferte Datei, optional version) oder aus dem freien Feld (Datei bzw. voller Pfad). */
export function ladenParameter({ zeile = null, version = '', pfad = '' } = {}) {
  const frei = String(pfad ?? '').trim();
  if (frei) return { datei: frei };
  if (!zeile) return null;
  if (zeile.datei) return { datei: zeile.datei };
  return { id: zeile.id, ...(version ? { version: String(version) } : {}) };
}

export function ladenListeHtml(zeilen, gewaehlt = '') {
  if (!zeilen.length) return '<p class="leise bib-leer">Keine Familien in der Bibliothek.</p>';
  return `<table class="tab bib-tab klickbar"><thead><tr><th></th><th>Name</th><th>Kategorie</th><th>Hersteller</th><th>Status</th><th>Version</th><th>Quelle</th></tr></thead><tbody>`
    + zeilen.map((z) => `<tr class="bib-lade${z.key === gewaehlt ? ' gewaehlt' : ''}${z.status !== 'freigegeben' ? ' entwurf' : ''}" data-key="${esc(z.key)}"><td><input type="radio" name="bib-lade" ${z.key === gewaehlt ? 'checked' : ''} tabindex="-1" aria-label="${esc(z.name)} wählen"></td>`
      + `<td>${esc(z.name)}</td><td>${esc(z.kategorie)}</td><td>${esc(z.hersteller)}</td><td>${esc(statusName(z.status))}</td><td>${z.version ?? ''}</td>`
      + `<td>${z.quelle === 'mitgeliefert' ? 'mitgeliefert' : 'Bibliothek'}${z.imProjekt ? ` · im Projekt (${esc(z.imProjekt)}), wird aktualisiert` : ''}</td></tr>`).join('') + '</tbody></table>';
}

// ================================================================================================ Statusleiste

/** Anzeige in der Statusleiste: { text, klasse (gruen | rot | warn | leise), titel } oder null (nichts anzeigen, z. B. ohne Projekt). */
export function bibliothekStatus(info) {
  if (!info) return null;
  const n = Number(info.warteschlange) || 0;
  const schlange = n > 0 ? ` · ${n} in Warteschlange` : '';
  if (info.aktiv === false) return { text: 'Bibliothek gesperrt', klasse: 'leise', titel: info.gesperrt ?? 'Die Familienbibliothek ist gesperrt.' };
  if (info.offline) return { text: `Bibliothek offline${schlange}`, klasse: 'rot', titel: `Familienbibliothek nicht erreichbar (${info.pfad ?? '?'}): Änderungen gehen in die Warteschlange und werden später nachgeschrieben. Klicken für Einzelheiten.` };
  return { text: `Bibliothek erreichbar${schlange}`, klasse: n > 0 ? 'warn' : 'gruen', titel: `Familienbibliothek ${info.pfad ?? ''} – klicken für Einzelheiten.` };
}

/** Kleiner Hinweis nach Klick auf die Statusleiste: Pfad, Quelle, Erreichbarkeit, Warteschlange, nachgeholt, Hinweis des Kerns. */
export function bibliothekHinweisHtml(info) {
  if (!info) return '<p class="leise">Kein Projekt offen.</p>';
  const zeilen = [
    ['Pfad', info.pfad ?? '–'],
    ['Quelle', info.quelle ?? '–'],
    ['Zustand', info.aktiv === false ? `gesperrt${info.gesperrt ? ` – ${info.gesperrt}` : ''}` : info.offline ? 'nicht erreichbar (letzter bekannter Stand)' : 'erreichbar'],
    ['Stand der Liste', info.stand ? zeitText(info.stand) || String(info.stand) : '–'],
    ['In der Warteschlange', String(info.warteschlange ?? 0)],
    ['Nachgeholt', info.nachgeholt ? `${info.nachgeholt} Schreibvorgang/-vorgänge beim letzten Zugriff nachgeschrieben`
      : info.nachgeholtMerk ? `${info.nachgeholtMerk.anzahl} Schreibvorgang/-vorgänge nachgeschrieben (${zeitText(info.nachgeholtMerk.zeit)})` : 'nichts'],
  ];
  return `<table class="tab bib-info"><tbody>${zeilen.map(([k, v]) => `<tr><th>${esc(k)}</th><td>${esc(v)}</td></tr>`).join('')}</tbody></table>`
    + (info.hinweis ? `<p class="bib-hinweistext">${esc(info.hinweis)}</p>` : '')
    + ((info.fehler ?? []).length ? `<div class="bib-warn">${info.fehler.map((t) => `<p>${esc(t)}</p>`).join('')}</div>` : '')
    + '<p class="leise">Ist die Bibliothek nicht erreichbar, arbeitest du im Projekt normal weiter: Änderungen an Familien werden hier gemerkt und beim nächsten Zugriff nachgeschrieben.</p>';
}

// ================================================================================================ Darstellung der Marken

const MARKEN_KLASSEN = { 'Bibliothek': 'm-bib', 'neuere Version': 'm-neu', 'parallel geändert': 'm-par', 'noch nicht gespeichert': 'm-wart', 'Doppel?': 'm-doppel', 'Fehler': 'm-fehler' };
/** CSS-Klasse einer Marke im Projektbrowser (Farbe über Variablen in familien-bibliothek.css). */
export const markeKlasse = (marke) => MARKEN_KLASSEN[marke] ?? '';
