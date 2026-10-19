// Familie aus Unterlagen (Modul 2, C2): reine Logik ohne DOM, in Node getestet (unterlagen.test.mjs).
// Die Oberfläche entscheidet nichts Fachliches: sie macht aus Eingaben die Parameter der Befehle und Abfragen des Kerns (konflikt_entscheiden,
// wert_bestaetigen, familie_vergleichen, familie_aendern), färbt die Status der Abdeckung, sammelt die Gründe, die die Freigabe sperren
// (der Kern prüft bei familie_freigeben noch einmal und ist massgebend), rechnet Boxen und Mausklicks zwischen Bildpunkten und
// Unterlagen-Koordinaten um und führt den Stand der KI-Aufträge aus dem Ereignis «auftrag» nach.
//
// ANNAHMEN (Prüfliste):
//  - Freigabe-Gründe der Oberfläche: Fehler der Definition und der Typen, offene Konflikte, Pflichtwerte ohne Quelle (abdeckung.fehlt),
//    Masse ausserhalb der Toleranz (familie_masse). Das ist derselbe Satz wie in FamilienFreigabe.Pruefen; ändert der Kern ihn, lehnt er die
//    Freigabe selbst ab und die Oberfläche zeigt seine Meldung.
//  - Rand um einen Befund beim «Quelle ansehen»: so gross wie der Befund selbst, mindestens 80 Einheiten (PDF in pt, Bild in Pixeln) bzw. 10
//    (Zeichnungseinheiten). Anschauung, kein Wert.
//  - «Als Quelle übernehmen» setzt den Wert nur, wenn der Befund eine einzelne Zahl (bzw. bei Text-Parametern einen Text) trägt und der Mensch
//    es will; ob Wert und Quelle zusammenpassen, prüft der Kern (Fehler «Wert weicht von der genannten Quelle ab»).

import { esc, zahlLesen, klon, pruefZeilen } from './familien-text.js';

export { esc };

// ------------------------------------------------------------------------------------------------ Dateien

export const ERLAUBTE_ENDUNGEN = ['pdf', 'dwg', 'dxf', 'step', 'stp', 'iges', 'igs', 'stl', 'obj', 'ifc', 'xlsx', 'csv', 'txt', 'png', 'jpg', 'jpeg'];
export const DATEI_ACCEPT = ERLAUBTE_ENDUNGEN.map((e) => '.' + e).join(',');
export const ART_NAMEN = { html: 'Webseite', pdf: 'PDF', dwg: 'DWG', dxf: 'DXF', step: 'STEP', iges: 'IGES', stl: 'STL', obj: 'OBJ', ifc: 'IFC', xlsx: 'Excel', csv: 'CSV', typkatalog: 'Typkatalog', bild: 'Bild' };
export const ohnePruefsumme = (n) => String(n ?? '').replace(/^[0-9a-f]{12}_/i, '');
export const endungVon = (name) => (/\.([a-z0-9]+)$/i.exec(String(name ?? '').trim())?.[1] ?? '').toLowerCase();

/** true oder Fehlertext, ob die Oberfläche die Datei annimmt (der Kern entscheidet über die Art nach Endung und Inhalt). */
export function dateiPruefen(name) {
  const e = endungVon(name);
  if (!e) return `«${name}» hat keine Dateiendung.`;
  if (!ERLAUBTE_ENDUNGEN.includes(e)) return `«${name}»: Endung .${e} wird nicht gelesen (erlaubt: ${ERLAUBTE_ENDUNGEN.map((x) => '.' + x).join(' ')}). TIFF und RFA werden nicht gelesen.`;
  return true;
}

/** Zeile je Datei im Dialog: Art, Seiten, Befunde je Art, Tabellen, Hinweise (aus einem Eintrag von unterlagen_lesen). */
export function dateiZeile(u) {
  if (!u) return { art: '', seiten: '', befunde: '', tabellen: '', hinweise: [], fehler: '' };
  if (u.fehler) return { art: '', seiten: '', befunde: '', tabellen: '', hinweise: [], fehler: `${u.fehler}${u.vorschlag ? ' – ' + u.vorschlag : ''}` };
  const jeArt = Object.entries(u.jeArt ?? {}).map(([a, n]) => `${n} ${a}`);
  const tab = (u.tabellen ?? []).length + (u.tabellenWeitere ?? 0);
  return {
    art: ART_NAMEN[u.art] ?? u.art ?? '',
    seiten: u.seiten && u.art === 'pdf' ? `${u.seiten} S.` : '',   // bei DWG/DXF zählt die Datei Layouts, keine Seiten
    befunde: `${u.befunde ?? 0} Befunde${jeArt.length ? ` (${jeArt.join(', ')})` : ''}`,
    tabellen: tab ? `${tab} Tabelle${tab === 1 ? '' : 'n'}` : '',
    hinweise: [...(u.hinweise ?? [])],
    fehler: '',
  };
}

/** Kategorien für «Familie aus Unterlagen»: Klassen mit familie: true (Katalog-Kurzform), sonst die Eigenschaft «familientyp». */
export function kategorienWahl(klassen) {
  const hat = (k) => k.familie === true || (k.familie === undefined && (k.eigenschaften ?? []).some((e) => String(e).split(' ')[0] === 'familientyp'));
  return (klassen ?? []).filter(hat).map((k) => k.name).sort((a, b) => a.localeCompare(b, 'de'));
}

/** Parameter für POST /api/auftraege. */
export function auftragParameter({ unterlagen, kategorie, hinweis, familie, internet = true }) {
  const p = { art: 'familie_aus_unterlagen', unterlagen: [...unterlagen], internet: internet !== false };   // Schalter je Auftrag, Vorgabe ein (entwurf/modul-2.md «Internet-Recherche»)
  if (kategorie) p.kategorie = kategorie;
  if (String(hinweis ?? '').trim()) p.hinweis = String(hinweis).trim();
  if (familie) p.familie = familie;
  return p;
}

/** Prüfung vor dem Start: true oder Text. */
export function startPruefen({ dateien, ersetzen, familie }) {
  const ok = (dateien ?? []).filter((d) => d.status === 'bereit');
  if ((dateien ?? []).some((d) => d.status === 'laedt' || d.status === 'liest')) return 'Die Dateien werden noch hochgeladen bzw. gelesen.';
  if (ok.length === 0) return 'Zuerst mindestens eine Unterlage hineinziehen (PDF, DWG/DXF, STEP, Tabelle, Bild …).';
  if (ersetzen && !familie) return 'Welche Familie soll ersetzt werden?';
  return true;
}

// ------------------------------------------------------------------------------------------------ KI-Aufträge (Ereignis «auftrag»)

const RANG = { wartet: 0, laeuft: 1, fertig: 2, fehler: 2, abgebrochen: 2 };
export const auftragLaeuft = (a) => a?.status === 'wartet' || a?.status === 'laeuft';
export const auftragEnde = (a) => !!a && !auftragLaeuft(a);

/** Führt den Stand der Aufträge nach (Map id → Auftrag); ein älterer Stand ersetzt nie einen neueren. Liefert die Map (neu). */
export function auftragUebernehmen(stand, auftrag) {
  const neu = new Map(stand ?? []);
  if (!auftrag?.id) return neu;
  const alt = neu.get(auftrag.id);
  if (alt) {
    const rn = RANG[auftrag.status] ?? 0, ra = RANG[alt.status] ?? 0;
    if (ra > rn) return neu;
    if (ra === rn && (alt.schrittAnzahl ?? 0) > (auftrag.schrittAnzahl ?? 0)) return neu;
    // das Ereignis trägt den Bericht nicht: die volle Antwort (GET /api/auftraege/{id}) bleibt
    if (ra === rn && ra === 2 && alt.ergebnis && 'bericht' in alt.ergebnis && !(auftrag.ergebnis && 'bericht' in auftrag.ergebnis)) return neu;
  }
  neu.set(auftrag.id, auftrag);
  return neu;
}

/** Der Auftrag, den der Dialog zeigt: der laufende, sonst der zuletzt gestartete. */
export function aktuellerAuftrag(stand) {
  const l = [...(stand?.values() ?? [])];
  return l.find(auftragLaeuft) ?? l.sort((a, b) => String(b.start).localeCompare(String(a.start)))[0] ?? null;
}

export function dauerText(sekunden) {
  const s = Math.max(0, Math.round(Number(sekunden) || 0));
  const m = Math.floor(s / 60);
  return m > 0 ? `${m} min ${String(s % 60).padStart(2, '0')} s` : `${s} s`;
}

export const STATUS_AUFTRAG = {
  wartet: { text: 'wartet', klasse: 'laeuft' }, laeuft: { text: 'läuft', klasse: 'laeuft' }, fertig: { text: 'fertig', klasse: 'fertig' },
  fehler: { text: 'Fehler', klasse: 'fehler' }, abgebrochen: { text: 'abgebrochen', klasse: 'abgebrochen' },
};

/** Letzte Schritte des Protokolls als [{ zeit: 'hh:mm:ss', text }] (neueste zuletzt). */
export function schrittZeilen(auftrag, n = 8) {
  const zeit = (iso) => { const d = new Date(iso); return Number.isNaN(d.getTime()) ? '' : d.toLocaleTimeString('de-CH', { hour12: false }); };
  return (auftrag?.schritte ?? []).slice(-n).map((s) => ({ zeit: zeit(s.zeit), text: String(s.text ?? '') }));
}

/** Angelegte oder geänderte Familien des Ergebnisses: [{ kennzeichen, aktion, offeneKonflikte, … }], nur solche, die es im Modell gibt. */
export function ergebnisFamilien(auftrag) {
  return (auftrag?.ergebnis?.familien ?? []).filter((f) => f.kennzeichen && f.aktion !== 'geloescht' && f.imModell !== false);
}

// ------------------------------------------------------------------------------------------------ Abdeckung

/** Status der Abdeckung → Klasse und Text (CSS: .fu-st-<klasse>). */
export const STATUS = [
  { id: 'geprüft', klasse: 'geprueft', text: 'geprüft', hinweis: 'Der Wert stimmt mit seiner Quelle überein.' },
  { id: 'ungeprüft-bild', klasse: 'bild', text: 'ungeprüft (Bild)', hinweis: 'Aus einem Bild oder einer Zeichnung gelesen – nicht nachrechenbar.' },
  { id: 'fehlt', klasse: 'fehlt', text: 'fehlt', hinweis: 'Keine Quelle: Wert bestätigen oder eine Quelle nennen.' },
  { id: 'konflikt', klasse: 'konflikt', text: 'Konflikt', hinweis: 'Quellen widersprechen sich: ein Mensch entscheidet.' },
  { id: 'mensch', klasse: 'mensch', text: 'Mensch', hinweis: 'Von einem Menschen gesetzt, bestätigt oder entschieden.' },
];
export const statusInfo = (s) => STATUS.find((x) => x.id === s) ?? { id: s, klasse: 'unbekannt', text: String(s ?? '–'), hinweis: '' };

function wertText(w) {
  if (w === undefined || w === null) return '';
  if (typeof w === 'number') return String(Math.round(w * 1e6) / 1e6);
  if (typeof w === 'boolean') return w ? 'ja' : 'nein';
  return String(w);
}
export { wertText };

function zelleVon(schluessel, e, parameter, typ) {
  if (!e) return null;
  return { schluessel, parameter, typ: typ ?? null, status: e.status, wert: e.wert ?? null, quellen: [...(e.quellen ?? [])], vermerk: e.vermerk ?? '', pflicht: e.pflicht === true, fehlt: e.fehlt === true, konflikt: e.konflikt ?? null, bestaetigt: e.bestaetigt ?? null };
}

/** Tabelle Parameter × Typ aus definition.abdeckung. Zeilen: Parameter der Definition mit mindestens einem Eintrag; Spalten: Vorgabe und die Typen. */
export function abdeckungTabelle(def) {
  const abd = def?.abdeckung ?? {};
  const typen = (def?.typen ?? []).map((t) => t.name);
  const benutzt = new Set();
  const zeilen = [];
  for (const p of def?.parameter ?? []) {
    const vorgabe = zelleVon(p.name, abd[p.name], p.name, null);
    const jeTyp = typen.map((t) => zelleVon(`${t}/${p.name}`, abd[`${t}/${p.name}`], p.name, t));
    if (abd[p.name]) benutzt.add(p.name);
    for (const t of typen) if (abd[`${t}/${p.name}`]) benutzt.add(`${t}/${p.name}`);
    if (!vorgabe && jeTyp.every((x) => !x)) continue;
    zeilen.push({ parameter: p.name, einheit: p.einheit ?? '', gruppe: p.gruppe ?? '', vorgabe, typen: jeTyp });
  }
  const uebrig = Object.keys(abd).filter((k) => !benutzt.has(k));
  return { typen, zeilen, uebrig: uebrig.map((k) => zelleVon(k, abd[k], k, null)) };
}

/** Zahl der Einträge je Status. */
export function abdeckungSummen(def) {
  const s = Object.fromEntries(STATUS.map((x) => [x.id, 0]));
  for (const e of Object.values(def?.abdeckung ?? {})) s[e.status] = (s[e.status] ?? 0) + 1;
  return s;
}

/** Darf ein Wert bestätigt werden? Nur ohne Quelle (fehlt) oder ungeprüft-bild; bei Konflikt entscheidet man stattdessen. */
export const bestaetigbar = (z) => !!z && (z.status === 'fehlt' || z.status === 'ungeprüft-bild');

// ------------------------------------------------------------------------------------------------ Konflikte

/** Die Werte eines Konflikts als Wahl: [{ index, wert, text, quellen, stellen, texte, vorschlag, vorrang }]. */
export function konfliktOptionen(k) {
  return (k?.werte ?? []).map((w, i) => ({
    index: i, wert: w.wert, text: `${wertText(w.wert)}${k.einheit ? ' ' + k.einheit : ''}`,
    quellen: [...(w.quellen ?? [])], stellen: [...(w.stellen ?? [])], texte: [...(w.text ?? [])],
    vorschlag: k.vorschlag !== undefined && Math.abs(Number(w.wert) - Number(k.vorschlag)) < 1e-9, vorrang: w.vorrang ?? '',
  }));
}

export const konfliktTitel = (k) => `«${k.parameter}»${k.typ ? ` · Typ ${k.typ}` : ''}`;

/** Parameter für konflikt_entscheiden aus der Wahl: wahl = { index } (einer der Werte) oder { wert } (anderer Wert, als Text). */
export function konfliktEntscheid(familie, konflikt, wahl, begruendung) {
  if (!familie || !konflikt?.id) return { fehler: 'Familie oder Konflikt fehlt.' };
  let wert;
  if (wahl?.index !== undefined && wahl.index !== null && wahl.index !== '') {
    const o = konfliktOptionen(konflikt)[Number(wahl.index)];
    if (!o) return { fehler: 'Diesen Wert gibt es im Konflikt nicht.' };
    wert = o.wert;
  } else if (wahl?.wert !== undefined && String(wahl.wert).trim() !== '') {
    const z = zahlLesen(wahl.wert);
    wert = z !== null ? z : String(wahl.wert).trim();
  } else return { fehler: 'Zuerst einen der Werte wählen.' };
  const p = { familie, konflikt: konflikt.id, wert };
  if (String(begruendung ?? '').trim()) p.begruendung = String(begruendung).trim();
  return { parameter: p };
}

/** Parameter für wert_bestaetigen aus einer Zelle der Abdeckung. */
export function bestaetigenParameter(familie, zelle, begruendung) {
  if (!familie || !zelle) return { fehler: 'Familie oder Wert fehlt.' };
  if (!bestaetigbar(zelle)) return { fehler: `Der Status «${statusInfo(zelle.status).text}» wird nicht bestätigt.` };
  const p = { familie, parameter: zelle.parameter };
  if (zelle.typ) p.typ = zelle.typ;
  if (String(begruendung ?? '').trim()) p.begruendung = String(begruendung).trim();
  return { parameter: p };
}

// ------------------------------------------------------------------------------------------------ Freigabe

/**
 * Gründe, die die Freigabe sperren: [{ art, text, ziel }]. ziel: { abschnitt: 'fehler' | 'konflikt' | 'abdeckung' | 'masse', … }.
 * masse = Antwort von familie_masse oder null (dann entfallen die Masse – der Kern prüft bei der Freigabe selbst).
 */
export function freigabeGruende(fam, masse) {
  const aus = [];
  if (!fam) return aus;
  const def = fam.definition ?? {};
  for (const z of pruefZeilen(fam)) aus.push({ art: 'fehler', text: `${z.art}${z.stelle ? ' · ' + z.stelle : ''}: ${z.meldung}`, ziel: { abschnitt: 'fehler', ziel: z.ziel } });
  for (const k of def.konflikte ?? []) {
    if (k.entscheid) continue;
    aus.push({ art: 'konflikt', text: `Konflikt offen: ${konfliktTitel(k)} – ${(k.werte ?? []).map((w) => wertText(w.wert)).join(' / ')}`, ziel: { abschnitt: 'konflikt', id: k.id } });
  }
  for (const [schluessel, e] of Object.entries(def.abdeckung ?? {}))
    if (e.fehlt === true) aus.push({ art: 'fehlt', text: `Pflichtwert ohne Quelle: ${schluessel.replace('/', ' · ')}`, ziel: { abschnitt: 'abdeckung', schluessel } });
  for (const t of masse?.typen ?? [])
    for (const a of t.abweichungen ?? [])
      if (a.bestanden === false) aus.push({ art: 'mass', text: `Mass ${a.mass}, Typ ${a.typ}: Soll ${wertText(a.soll)}, Ist ${wertText(a.ist)} (Toleranz ±${wertText(a.toleranz)} mm)`, ziel: { abschnitt: 'masse', schluessel: `${a.typ}/${a.mass}` } });
  return aus;
}

export const freigabeMoeglich = (fam, masse) => !!fam && fam.status !== 'freigegeben' && freigabeGruende(fam, masse).length === 0;

/** Hinweis zu Werten aus Bildern (sperrt nicht, die Freigabe zählt sie). */
export function bildHinweis(def) {
  const n = abdeckungSummen(def)['ungeprüft-bild'] ?? 0;
  return n > 0 ? `${n} Wert${n === 1 ? '' : 'e'} aus Bildern gelesen (ungeprüft) – die Freigabe nennt ihre Zahl.` : '';
}

// ------------------------------------------------------------------------------------------------ Massvergleich

export const massStatus = (z) => (z.bestanden === true ? 'ok' : z.bestanden === false ? 'schlecht' : 'offen');

/** Flache Zeilen aus familie_masse bzw. der Abweichungsliste des Kontrollbilds. */
export function masseZeilen(antwort) {
  if (Array.isArray(antwort)) return antwort;
  return (antwort?.typen ?? []).flatMap((t) => t.abweichungen ?? []);
}

// ------------------------------------------------------------------------------------------------ Passpunkte und Kontrollbild

const num = (t) => { const z = zahlLesen(t); return z; };

/**
 * Passpunkte aus den acht Feldern { a_u, a_v, a_x, a_y, b_u, b_v, b_x, b_y } (Texte). Alles leer → { leer: true }; sonst müssen es zwei
 * vollständige Paare sein. Ergebnis { passpunkte: [{ familie: [u, v], unterlage: [x, y] }, …] } oder { fehler }.
 */
export function passpunkteLesen(f) {
  const namen = ['a_u', 'a_v', 'a_x', 'a_y', 'b_u', 'b_v', 'b_x', 'b_y'];
  const leer = namen.filter((n) => String(f?.[n] ?? '').trim() === '');
  if (leer.length === namen.length) return { leer: true };
  if (leer.length > 0) return { fehler: 'Passpunkte: zwei Paare mit je vier Zahlen nötig (Familie u, v und Unterlage x, y).' };
  const z = Object.fromEntries(namen.map((n) => [n, num(f[n])]));
  const schlecht = namen.find((n) => z[n] === null);
  if (schlecht) return { fehler: `Passpunkte: «${f[schlecht]}» ist keine Zahl.` };
  if (Math.hypot(z.a_u - z.b_u, z.a_v - z.b_v) < 1e-9) return { fehler: 'Passpunkte: die beiden Punkte der Familie fallen zusammen.' };
  if (Math.hypot(z.a_x - z.b_x, z.a_y - z.b_y) < 1e-9) return { fehler: 'Passpunkte: die beiden Punkte der Unterlage fallen zusammen.' };
  return { passpunkte: [{ familie: [z.a_u, z.a_v], unterlage: [z.a_x, z.a_y] }, { familie: [z.b_u, z.b_v], unterlage: [z.b_x, z.b_y] }] };
}

/** Parameter für familie_vergleichen aus dem Formular: { familie, typ, unterlage, ansicht, seite?, detailgrad?, breite?, felder (Passpunkte) }. */
export function vergleichParameter(form) {
  if (!form?.typ) return { fehler: 'Typ wählen.' };
  if (!form.unterlage) return { fehler: 'Unterlage wählen.' };
  if (!form.ansicht) return { fehler: 'Ansicht wählen.' };
  const p = { familie: form.familie, typ: form.typ, unterlage: form.unterlage, ansicht: form.ansicht };
  if (form.art === 'pdf' && form.seite) {
    const s = Number(form.seite);
    if (!Number.isInteger(s) || s < 1) return { fehler: 'Seite: ganze Zahl ab 1.' };
    p.seite = s;
  }
  if (form.detailgrad) p.detailgrad = form.detailgrad;
  p.breite = form.breite ?? 1200;
  const pp = passpunkteLesen(form.felder ?? {});
  if (pp.fehler) return { fehler: pp.fehler };
  if (pp.passpunkte) p.passpunkte = pp.passpunkte;
  return { parameter: p };
}

/** Wahlfolge beim Anklicken der Passpunkte: Punkt 1 Familie → Punkt 1 Unterlage → Punkt 2 Familie → Punkt 2 Unterlage → von vorn. */
export const PICK_FOLGE = [['a', 'familie'], ['a', 'unterlage'], ['b', 'familie'], ['b', 'unterlage']];
export const pickText = (i) => { const [p, w] = PICK_FOLGE[((i % 4) + 4) % 4]; return `Passpunkt ${p === 'a' ? 1 : 2} in der ${w === 'familie' ? 'Familienansicht' : 'Unterlage'} anklicken`; };

/** Familien-Klick (u, v) → Felder des Passpunkts (a oder b), gerundet auf 0.1 mm. */
export function pickFamilie(punkt, u, v) { const r = (x) => String(Math.round(x * 10) / 10); return { [`${punkt}_u`]: r(u), [`${punkt}_v`]: r(v) }; }
export function pickUnterlage(punkt, x, y) { const r = (z) => String(Math.round(z * 100) / 100); return { [`${punkt}_x`]: r(x), [`${punkt}_y`]: r(y) }; }

/** Ist die Antwort von familie_vergleichen schematisch (PDF-Massbild)? */
export const istSchematisch = (r) => r?.massstaeblich === false;
export const SCHEMATISCH_TEXT = 'schematisch, Überlagerung nur Anschauung';

/** Zusammenfassung des geometrischen Vergleichs (nur bei massstäblichen Unterlagen): Zeilen Text. */
export function geometrischZeilen(g) {
  if (!g) return [];
  const z = [];
  if (g.hinweis) z.push(g.hinweis);
  if (g.kanten) z.push(`Kanten der Zeichnung: ${g.kanten.bestanden} von ${g.kanten.anzahl} innerhalb der Toleranz${g.kanten.groessterAbstand !== null && g.kanten.groessterAbstand !== undefined ? `, grösster Abstand ${g.kanten.groessterAbstand} mm` : ''}.`);
  for (const m of g.masse ?? []) z.push(`Mass ${wertText(m.wert)} ${m.einheit ?? ''} (${m.befund}): Endpunkte ${m.bestanden ? 'auf' : 'NEBEN'} den Kanten der Familie (Abstand ${m.abstandPunkt1} / ${m.abstandPunkt2} mm, Toleranz ${m.toleranz})`.replace(/\s+/g, ' '));
  for (const a of g.anschluesse ?? []) z.push(`Anschluss ${a.anschluss ?? a.name ?? ''}: ${a.bestanden === false ? 'Abweichung' : 'Lage stimmt'}${a.abstand !== undefined ? ` (${a.abstand} mm)` : ''}`.replace(/\s+/g, ' '));
  return z;
}

// ------------------------------------------------------------------------------------------------ Befunde und Bilder

// ------------------------------------------------------------------------------------------------ Unterlagen aus dem Internet (Modul 2, Internet-Recherche)
// Der Kern legt sie mit unterlage_aus_url ab: original = URL, abgerufen = Zeit (Eigenschaften der Unterlage). Die Oberfläche zeigt nur, was dort steht.
// ANNAHME: die Grösse steht in werte.bytes bzw. werte.groesse (Byte), falls der Kern sie nennt; fehlt sie, zeigt die Spalte «–». Art «html» (Webseite) hat kein Seitenbild.

/** Ist der Text eine http(s)-Adresse? (nur solche werden als Link gezeigt) */
export const istUrl = (t) => /^https?:\/\/[^\s<>"']+$/i.test(String(t ?? '').trim());

/** Link auf eine Adresse (neuer Tab, ohne Verweis auf diese Seite); keine http(s)-Adresse: nur Text. */
export function urlLink(url) {
  const u = String(url ?? '').trim();
  return istUrl(u) ? `<a class="fu-url" href="${esc(u)}" target="_blank" rel="noopener noreferrer">${esc(u)}</a>` : esc(u);
}

export function groesseText(bytes) {
  const b = Number(bytes);
  if (!Number.isFinite(b) || b <= 0) return '';
  return b < 1024 ? `${b} B` : b < 1048576 ? `${Math.round(b / 102.4) / 10} kB` : `${Math.round(b / 104857.6) / 10} MB`;
}

export function zeitKurz(iso) {
  const m = /^(\d{4}-\d{2}-\d{2})[T ](\d{2}:\d{2})/.exec(String(iso ?? ''));
  return m ? `${m[1]} ${m[2]}` : String(iso ?? '');
}

/** Herstellerunterlagen aus dem Internet (original = http(s)-Adresse): [{ kz, url, art, groesse, abgerufen, datei }], nach Kennzeichen. */
export function internetUnterlagen(elemente) {
  return (elemente ?? []).filter((e) => e.klasse === 'Unterlage' && e.werte?.zweck === 'familie' && istUrl(e.werte?.original))
    .map((e) => ({ kz: e.kennzeichen, url: String(e.werte.original).trim(), art: e.werte.art ?? '', groesse: groesseText(e.werte.bytes ?? e.werte.groesse), abgerufen: zeitKurz(e.werte.abgerufen), datei: ohnePruefsumme(e.werte.datei ?? '') }))
    .sort((a, b) => a.kz.localeCompare(b.kz, 'de', { numeric: true }));
}

/** HTML der Liste «Aus dem Internet abgerufen» (leer, wenn nichts abgerufen wurde). */
export function internetListeHtml(liste) {
  if (!liste?.length) return '';
  return `<div class="fu-inet-kopf">Aus dem Internet abgerufen (${liste.length})</div><table class="tab fu-inet"><thead><tr><th>Unterlage</th><th>Adresse</th><th>Art</th><th>Grösse</th><th>Abgerufen</th></tr></thead><tbody>${liste.map((u) => `<tr data-kz="${esc(u.kz)}"><td>${esc(u.kz)}</td><td class="fu-inet-url">${urlLink(u.url)}</td><td>${esc(ART_NAMEN[u.art] ?? u.art)}</td><td>${esc(u.groesse || '–')}</td><td>${esc(u.abgerufen || '–')}</td></tr>`).join('')}</tbody></table>`;
}

/** Unterlagen, die eine Familie belegen: Verweise der Definition und Unterlage-Elemente mit Bezug auf die Familie. */
export function unterlagenKandidaten(def, familieKz, elemente) {
  const aus = [];
  const sicht = new Set();
  const nimm = (praefix, ref, datei, art, quelle, el) => {
    const key = praefix || ref || datei;
    if (!key || sicht.has(key)) return;
    sicht.add(key);
    const url = istUrl(el?.werte?.original) ? String(el.werte.original).trim() : '';
    aus.push({ praefix: praefix ?? '', ref: ref ?? datei, datei: datei ?? ref, art: art ?? '', quelle, url, abgerufen: url ? zeitKurz(el.werte.abgerufen) : '' });
  };
  for (const e of elemente ?? []) {
    if (e.klasse !== 'Unterlage' || e.werte?.zweck !== 'familie') continue;
    const datei = String(e.werte?.datei ?? '');
    const vorFam = e.bezuege?.familie;
    if (vorFam && vorFam !== familieKz) continue;
    const praefix = /^[0-9a-f]{12}_/i.test(datei) ? datei.slice(0, 12).toLowerCase() : '';
    nimm(praefix, e.kennzeichen, ohnePruefsumme(datei) || e.name, e.werte?.art, vorFam ? 'bezug' : 'frei', e);
  }
  for (const u of def?.unterlagen ?? []) {
    const praefix = String(u.pruefsumme ?? '').slice(0, 12).toLowerCase();
    const schon = aus.find((x) => x.praefix && x.praefix === praefix);
    if (schon) { schon.quelle = 'definition'; continue; }
    const el = (elemente ?? []).find((e) => e.id === u.unterlage || e.kennzeichen === u.unterlage);
    nimm(praefix, el?.kennzeichen ?? u.unterlage ?? u.datei, u.datei, u.art, 'definition', el);
  }
  return aus;
}

/** Unterlagen der Familie für Auswahlfelder: zuerst die belegenden (Definition, Bezug), nach Dateiname. */
export function unterlagenDerFamilie(def, familieKz, elemente) {
  const l = unterlagenKandidaten(def, familieKz, elemente).filter((u) => u.quelle !== 'frei');
  return l.sort((a, b) => String(a.datei).localeCompare(String(b.datei), 'de', { numeric: true }));
}

/** Unterlage zu einer Befund-Id «<Prüfsumme12>#<n>»: Kandidat oder null. */
export function unterlageZuBefund(kandidaten, befundId) {
  const praefix = String(befundId ?? '').split('#')[0].toLowerCase();
  return (kandidaten ?? []).find((u) => u.praefix && u.praefix === praefix) ?? null;
}

const kurz = (t, n) => { const s = String(t ?? '').replace(/\s+/g, ' ').trim(); return s.length > n ? s.slice(0, n - 1) + '…' : s; };

/** Kurztext eines Befunds für Listen. */
export function befundText(b) {
  if (!b) return '';
  const w = b.wert;
  let t = '';
  if (w === undefined || w === null) t = '';
  else if (typeof w === 'object') {
    if (b.art === 'tabelle') t = `${w.zeilen ?? '?'} Zeilen × ${w.spalten ?? '?'} Spalten${Array.isArray(w.kopf) ? ' · ' + kurz(w.kopf.join(' | '), 60) : ''}`;
    else if (w.name !== undefined) t = kurz(w.name, 50);
    else t = kurz(JSON.stringify(w), 60);
  } else t = kurz(w, 60);
  if (b.einheit) t += ` ${b.einheit}`;
  return t.trim();
}

export function befundStelle(b) {
  if (!b) return '';
  const t = [];
  if (b.seite !== undefined && b.seite !== null) t.push(`Seite ${b.seite}`);
  if (b.layout) t.push(`Blatt ${b.layout}`);
  if (b.layer) t.push(`Layer ${b.layer}`);
  const z = b.zusatz ?? {};
  if (z.zeile !== undefined && z.spalte !== undefined) t.push(`Zeile ${z.zeile}, Spalte ${z.spalte}`);
  return t.join(' · ');
}

const grossKlein = (box) => ({ x0: Math.min(box[0], box[2]), y0: Math.min(box[1], box[3]), x1: Math.max(box[0], box[2]), y1: Math.max(box[1], box[3]) });

/** Ausschnitt [x0, y0, x1, y1] um die Box eines Befunds mit Rand (Annahme siehe oben). art: pdf | bild | dwg | dxf … */
export function befundAusschnitt(box, art) {
  if (!Array.isArray(box) || box.length !== 4 || box.some((x) => !Number.isFinite(x))) return null;
  const b = grossKlein(box);
  const mass = Math.max(b.x1 - b.x0, b.y1 - b.y0);
  const rand = Math.max(mass, art === 'pdf' || art === 'bild' ? 80 : 10);
  return [b.x0 - rand, b.y0 - rand, b.x1 + rand, b.y1 + rand].map((x) => Math.round(x * 1000) / 1000);
}

const yUnten = (art) => art === 'bild';

/** Box eines Befunds im Bild: { x, y, b, h } in Bildpunkten (links oben, mindestens 6 px). bereich = Bereich der Antwort von unterlage_bild. */
export function boxImBild(box, bereich, breite, hoehe, art) {
  if (!Array.isArray(box) || !Array.isArray(bereich) || !(breite > 0) || !(hoehe > 0)) return null;
  const b = grossKlein(box), [x0, y0, x1, y1] = bereich;
  const sx = breite / (x1 - x0), sy = hoehe / (y1 - y0);
  const x = (b.x0 - x0) * sx, w = (b.x1 - b.x0) * sx;
  const y = yUnten(art) ? (b.y0 - y0) * sy : (y1 - b.y1) * sy, h = (b.y1 - b.y0) * sy;
  const rund = (z) => Math.round(z * 10) / 10;
  const bb = Math.max(w, 6), hh = Math.max(h, 6);
  return { x: rund(x - (bb - w) / 2), y: rund(y - (hh - h) / 2), b: rund(bb), h: rund(hh) };
}

/** Bildpunkt (px, py von links oben) → Unterlagen-Koordinaten [x, y]. */
export function bildPunktZuUnterlage(px, py, bereich, breite, hoehe, art) {
  const [x0, y0, x1, y1] = bereich;
  const x = x0 + (px * (x1 - x0)) / breite;
  const y = yUnten(art) ? y0 + (py * (y1 - y0)) / hoehe : y1 - (py * (y1 - y0)) / hoehe;
  return [Math.round(x * 100) / 100, Math.round(y * 100) / 100];
}

/** Welcher der Befunde (mit box) liegt unter dem Punkt [x, y] der Unterlage? Der kleinste gewinnt. */
export function befundUnterPunkt(befunde, punkt) {
  let beste = null, flaeche = Infinity;
  for (const b of befunde ?? []) {
    if (!Array.isArray(b.box) || b.box.length !== 4) continue;
    const g = grossKlein(b.box);
    const tol = Math.max((g.x1 - g.x0), (g.y1 - g.y0)) * 0.05;
    if (punkt[0] < g.x0 - tol || punkt[0] > g.x1 + tol || punkt[1] < g.y0 - tol || punkt[1] > g.y1 + tol) continue;
    const f = Math.max((g.x1 - g.x0) * (g.y1 - g.y0), 1e-9);
    if (f < flaeche) { flaeche = f; beste = b; }
  }
  return beste;
}

// ------------------------------------------------------------------------------------------------ Befund-Browser: als Quelle übernehmen

/** Zahl, die ein Befund trägt (Zelle, Wort, Masszahl …) oder null; Text für Text-Parameter. */
export function befundWert(b, datentyp) {
  const w = b?.wert;
  if (w === undefined || w === null || typeof w === 'object') return null;
  if (datentyp === 'text' || datentyp === 'medium') return typeof w === 'string' ? w.trim() || null : String(w);
  if (datentyp === 'ja_nein') return null;
  if (typeof w === 'number') return w;
  return zahlLesen(w);
}

/**
 * ops für familie_aendern: die Befund-Id wird Quelle der markierten Zelle (und auf Wunsch ihr Wert).
 * zelle: { art: 'typ', typ, parameter } | { art: 'parameter', parameter }. Ergebnis { ops, text } oder { fehler }.
 */
export function quelleUebernehmenOps(def, zelle, befund, { wertUebernehmen = false } = {}) {
  if (!befund?.id) return { fehler: 'Zuerst einen Befund wählen.' };
  if (!zelle) return { fehler: 'Zuerst eine Zelle im Typkatalog (oder einen Parameter) markieren.' };
  const p = (def?.parameter ?? []).find((x) => x.name === zelle.parameter);
  if (!p) return { fehler: `Den Parameter «${zelle.parameter}» gibt es nicht mehr.` };
  const wert = wertUebernehmen ? befundWert(befund, p.datentyp) : null;
  if (wertUebernehmen && wert === null) return { fehler: 'Dieser Befund trägt keinen einzelnen Wert – nur die Quelle übernehmen.' };
  const dazu = (liste) => [...new Set([...(liste ?? []), befund.id])];
  if (zelle.art === 'typ') {
    if (p.formel !== undefined && p.formel !== null) return { fehler: `«${p.name}» hat eine Formel – ihre Quellen sind Belege, kein Wert.` };
    const t = klon((def.typen ?? []).find((x) => x.name === zelle.typ));
    if (!t) return { fehler: `Den Typ «${zelle.typ}» gibt es nicht mehr.` };
    t.werte = { ...(t.werte ?? {}) };
    t.quellen = { ...(t.quellen ?? {}) };
    t.quellen[p.name] = dazu(t.quellen[p.name]);
    if (wert !== null) t.werte[p.name] = wert;
    return { ops: [{ op: 'typ_setzen', typ: t }], text: `Quelle ${befund.id} für ${p.name} im Typ ${t.name}${wert !== null ? ` (Wert ${wertText(wert)})` : ''}` };
  }
  const q = klon(p);
  if (q.formel !== undefined && q.formel !== null) return { fehler: `«${p.name}» hat eine Formel – ihre Quellen sind Belege, kein Wert.` };
  q.quellen = dazu(q.quellen);
  if (wert !== null) q.wert = wert;
  return { ops: [{ op: 'parameter_setzen', parameter: q }], text: `Quelle ${befund.id} für ${p.name}${wert !== null ? ` (Wert ${wertText(wert)})` : ''}` };
}

/** Text der markierten Zelle. */
export const zelleText = (z) => (z ? (z.art === 'typ' ? `Typ ${z.typ} · ${z.parameter}` : `Parameter ${z.parameter} (Vorgabewert)`) : '');

/** Merkt eine fokussierte Zelle des Editors: Typkatalog (data-p in tr[data-zeile]) oder Parameterzeile. */
export function zelleMerken(tab, zeileName, datenP) {
  if (tab === 'typen' && zeileName && datenP) return { art: 'typ', typ: zeileName, parameter: datenP };
  if (tab === 'parameter' && zeileName) return { art: 'parameter', parameter: zeileName };
  return undefined;
}

// ------------------------------------------------------------------------------------------------ Texte zu Quellen (aus quelle-ansehen.js)

/** Voller Text eines Befunds (Wert, bei Zellen mit Spaltenkopf und Zeile); Objekte als JSON. */
export function befundVolltext(b) {
  const w = b?.wert;
  const z = b?.zusatz ?? {};
  let t = w === undefined || w === null ? '' : typeof w === 'object' ? JSON.stringify(w, null, 1) : String(w);
  if (b?.einheit) t += ` ${b.einheit}`;
  const kopf = z.kopf ? `Spalte «${z.kopf}»` : '';
  const ort = z.zeile !== undefined && z.spalte !== undefined ? `Tabellenzelle Zeile ${z.zeile}, Spalte ${z.spalte}` : '';
  return [t, [ort, kopf].filter(Boolean).join(' · '), z.text && z.text !== t ? `Text: ${z.text}` : ''].filter(Boolean).join('\n');
}


/** Eine Zeile Text zu einem geladenen Befund: «datenblatt.pdf · Seite 1 · Zeile 1, Spalte 3 · 67». */
export function quellenZeile(e) {
  if (!e) return '';
  if (e.fehler) return `${e.id}: ${e.fehler}`;
  const b = e.befund;
  return [ohnePruefsumme(e.unterlage.datei), befundStelle(b), `${b.art}${befundText(b) ? ' ' + befundText(b) : ''}`].filter(Boolean).join(' · ') + (e.unterlage.url ? ` · aus dem Internet: ${e.unterlage.url}` : '');
}

/** Wie quellenZeile, aber als HTML mit Link auf die Adresse (Unterlagen aus dem Internet). */
export function quellenHtml(e) {
  if (!e) return '';
  if (e.fehler || !e.unterlage?.url) return esc(quellenZeile(e));
  const b = e.befund;
  const text = [ohnePruefsumme(e.unterlage.datei), befundStelle(b), `${b.art}${befundText(b) ? ' ' + befundText(b) : ''}`].filter(Boolean).join(' · ');
  return `${esc(text)} · aus dem Internet: ${urlLink(e.unterlage.url)}`;
}

