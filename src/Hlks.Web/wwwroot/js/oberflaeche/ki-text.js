// Reine Teile der KI-Palette «Claude» (Modul 3c, Teil O) – ohne DOM, in Node getestet (ki-palette.test.mjs).
//
//  - formatieren(text)        Antworttext (Markdown-Auszug) → Blöcke als DATEN: Absätze, Überschriften, Listen, Zitate, Code, Tabellen, Linie;
//                             Inline: Text, Fett, Kursiv, Code, Link. Es entsteht nie HTML-Text: ki-darstellung.js baut daraus Element für Element
//                             (createElement/textContent). Eingeschleustes wie <script> oder <img onerror=…> bleibt damit nackter Text.
//  - linkSicher(url)          nur http, https und mailto; alles andere (javascript:, data:, vbscript:, file:, relative Adressen) ergibt null
//  - Kontext: kontextChips · kontextKoerper (was mit der Frage mitgeht; mit Internet nie etwas) · frageKoerper
//  - Texte: fehlerText, ablehnungText (Zurücknehmen abgelehnt), werkzeugZeile, gespraechLabel, kontextText, zeit- und Grössentexte
//  - Bild: bildGroesse, dataUrlBytes (≤ 4 MB)
//  - Merken: wahlLesen/wahlSchreiben (nur Gespräch, Modell und Internet-Schalter je Projekt – keine Nachrichten)
// Schnittstelle des Servers: src/Hlks.Web/OBERFLAECHE.md, Abschnitt «KI-Palette: Schnittstelle (3c, Teil K)».

export const GRENZEN = { textZeichen: 8000, auswahl: 200, bildBytes: 4194304, titelZeichen: 200 };
// Wortlaut nach der Sicherheitsprüfung 3c, B3d: mit Internet ist das Modell weg, Herstellerunterlagen und Familien bleiben lesbar
export const INTERNET_HINWEIS = 'ohne Modellzugriff; Unterlagen und Familien lesbar';
export const INTERNET_TIPP = 'Mit Internet arbeitet Claude ohne Modellzugriff und ohne Projektkontext: weder Ansicht noch Geschoss, Auswahl oder Bild gehen mit, und Claude kann das Modell weder lesen noch ändern. Lesbar bleiben Herstellerunterlagen (zweck «familie») und Familien.';
export const BILD_MAX_KANTE = 1600;

// ------------------------------------------------------------------------------------------------ Links

/** Sichere Adresse oder null. Erlaubt sind nur absolute http-, https- und mailto-Adressen ohne Leer- und Steuerzeichen. */
export function linkSicher(url) {
  const t = String(url ?? '').trim();
  if (!t || t.length > 2000) return null;
  if (/[\s\u0000-\u001f\u007f-\u009f<>"'`\\]/.test(t)) return null;
  let u;
  try { u = new URL(t); } catch { return null; }
  if (!['http:', 'https:', 'mailto:'].includes(u.protocol)) return null;
  if (u.protocol !== 'mailto:' && !u.hostname) return null;
  return u.href;
}

// ------------------------------------------------------------------------------------------------ Inline

const text = (s) => ({ t: 'text', s });

/** Zeichenweise: `code`, **fett**, *kursiv* / _kursiv_, [Text](Adresse). Unvollständige Auszeichnung bleibt Text. */
export function inlineLesen(roh, tiefe = 0) {
  const s = String(roh ?? '');
  const aus = [];
  let puffer = '';
  const leeren = () => { if (puffer) { aus.push(text(puffer)); puffer = ''; } };
  let i = 0;
  while (i < s.length) {
    const c = s[i];
    if (c === '\\' && i + 1 < s.length && /[\\`*_\[\]()#|>~-]/.test(s[i + 1])) { puffer += s[i + 1]; i += 2; continue; }
    if (c === '`') {
      let n = 1; while (s[i + n] === '`') n++;
      const zu = '`'.repeat(n);
      const e = s.indexOf(zu, i + n);
      if (e > i + n) { leeren(); aus.push({ t: 'code', s: s.slice(i + n, e).replace(/^ (.*) $/, '$1') }); i = e + n; continue; }
    }
    if (tiefe < 4 && (c === '*' || c === '_')) {
      const doppelt = s[i + 1] === c;
      const marke = doppelt ? c + c : c;
      const start = i + marke.length;
      // _ nur an Wortgrenzen (snake_case bleibt Text)
      const vorher = i > 0 ? s[i - 1] : ' ';
      if (!(c === '_' && /[A-Za-z0-9ÄÖÜäöüß]/.test(vorher))) {
        let e = s.indexOf(marke, start);
        while (e > 0 && s[e - 1] === '\\') e = s.indexOf(marke, e + 1);
        if (e > start && !/^\s/.test(s.slice(start, start + 1)) && !/\s$/.test(s.slice(start, e)) && !(c === '_' && /[A-Za-z0-9ÄÖÜäöüß]/.test(s[e + marke.length] ?? ' '))) {
          leeren();
          aus.push({ t: doppelt ? 'fett' : 'kursiv', kinder: inlineLesen(s.slice(start, e), tiefe + 1) });
          i = e + marke.length;
          continue;
        }
      }
    }
    if (c === '[' && tiefe < 4) {
      const m = /^\[([^\]\n]{1,300})\]\(([^()\s]{1,2000})\)/.exec(s.slice(i));
      if (m) {
        const href = linkSicher(m[2]);
        leeren();
        if (href) aus.push({ t: 'link', href, kinder: inlineLesen(m[1], tiefe + 1) });
        else aus.push(text(`${m[1]} (${m[2]})`));   // unsichere Adresse: nur als Text, nie als Link
        i += m[0].length;
        continue;
      }
    }
    puffer += c;
    i++;
  }
  leeren();
  return aus;
}

// ------------------------------------------------------------------------------------------------ Blöcke

const zellen = (z) => {
  let t = z.trim();
  if (t.startsWith('|')) t = t.slice(1);
  if (t.endsWith('|') && !t.endsWith('\\|')) t = t.slice(0, -1);
  const teile = [];
  let aktuell = '';
  for (let i = 0; i < t.length; i++) {
    if (t[i] === '\\' && t[i + 1] === '|') { aktuell += '|'; i++; continue; }
    if (t[i] === '|') { teile.push(aktuell.trim()); aktuell = ''; continue; }
    aktuell += t[i];
  }
  teile.push(aktuell.trim());
  return teile;
};
const istTrennzeile = (z) => z.includes('|') && /^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$/.test(z);
const ausrichtung = (z) => zellen(z).map((c) => (c.startsWith(':') && c.endsWith(':') ? 'mitte' : c.endsWith(':') ? 'rechts' : 'links'));

/** Antworttext → Blöcke. Siehe Kopf. Hat nie Seiteneffekte und erzeugt keinen HTML-Text. */
export function formatieren(roh, tiefe = 0) {
  const zeilen = String(roh ?? '').replace(/\r\n?/g, '\n').split('\n');
  const bloecke = [];
  let i = 0;
  const leer = (z) => z.trim() === '';
  const blockStart = (z, naechste) =>
    /^(`{3,}|~{3,})/.test(z.trimStart()) || /^#{1,6}\s/.test(z) || /^\s*([-*+]|\d{1,3}[.)])\s+/.test(z) || /^>\s?/.test(z)
    || /^\s*([-*_])(\s*\1){2,}\s*$/.test(z) || (z.includes('|') && naechste !== undefined && istTrennzeile(naechste));
  while (i < zeilen.length) {
    const z = zeilen[i];
    if (leer(z)) { i++; continue; }
    const zaun = /^(\s*)(`{3,}|~{3,})\s*([\w+#.-]*)\s*$/.exec(z);
    if (zaun) {
      const marke = zaun[2];
      const inhalt = [];
      i++;
      while (i < zeilen.length && !new RegExp(`^\\s*${marke[0]}{${marke.length},}\\s*$`).test(zeilen[i])) { inhalt.push(zeilen[i]); i++; }
      i++;   // schliessender Zaun (fehlt er, endet der Block mit dem Text)
      bloecke.push({ art: 'code', sprache: zaun[3] || '', s: inhalt.join('\n') });
      continue;
    }
    const ue = /^(#{1,6})\s+(.*?)\s*#*\s*$/.exec(z);
    if (ue) { bloecke.push({ art: 'ueberschrift', stufe: Math.min(ue[1].length, 4), kinder: inlineLesen(ue[2]) }); i++; continue; }
    if (/^\s*([-*_])(\s*\1){2,}\s*$/.test(z)) { bloecke.push({ art: 'linie' }); i++; continue; }
    if (z.includes('|') && i + 1 < zeilen.length && istTrennzeile(zeilen[i + 1]) && zellen(z).length === zellen(zeilen[i + 1]).length) {
      const kopf = zellen(z).map((c) => inlineLesen(c));
      const aus = ausrichtung(zeilen[i + 1]);
      const reihen = [];
      i += 2;
      while (i < zeilen.length && !leer(zeilen[i]) && zeilen[i].includes('|')) {
        const c = zellen(zeilen[i]);
        reihen.push(kopf.map((_, k) => inlineLesen(c[k] ?? '')));
        i++;
      }
      bloecke.push({ art: 'tabelle', kopf, ausrichtung: aus, zeilen: reihen });
      continue;
    }
    if (/^>\s?/.test(z) && tiefe < 4) {   // tiefer verschachtelte Zitate sind nur noch Text (kein Stapelüberlauf bei bösen Eingaben)
      const teile = [];
      while (i < zeilen.length && /^>\s?/.test(zeilen[i])) { teile.push(zeilen[i].replace(/^>\s?/, '')); i++; }
      bloecke.push({ art: 'zitat', bloecke: formatieren(teile.join('\n'), tiefe + 1) });
      continue;
    }
    const li = /^(\s*)([-*+]|\d{1,3}[.)])\s+(.*)$/.exec(z);
    if (li) {
      const geordnet = /\d/.test(li[2]);
      const punkte = [];
      while (i < zeilen.length) {
        const m = /^(\s*)([-*+]|\d{1,3}[.)])\s+(.*)$/.exec(zeilen[i]);
        if (!m || /\d/.test(m[2]) !== geordnet) break;
        let t = m[3];
        i++;
        // Fortsetzungszeilen (eingerückt, kein neuer Punkt) gehören zum Punkt
        while (i < zeilen.length && !leer(zeilen[i]) && /^\s{2,}\S/.test(zeilen[i]) && !/^(\s*)([-*+]|\d{1,3}[.)])\s+/.test(zeilen[i])) { t += ' ' + zeilen[i].trim(); i++; }
        punkte.push({ kinder: inlineLesen(t), einzug: Math.floor(m[1].length / 2) });
      }
      bloecke.push({ art: 'liste', geordnet, start: geordnet ? Number(/\d+/.exec(li[2])[0]) : 1, punkte });
      continue;
    }
    // Absatz: aufeinanderfolgende Zeilen bis zur Leerzeile oder dem nächsten Block
    const absatz = [];
    while (i < zeilen.length && !leer(zeilen[i]) && !(absatz.length > 0 && blockStart(zeilen[i], zeilen[i + 1]))) { absatz.push(zeilen[i].trim()); i++; }
    const kinder = [];
    absatz.forEach((a, k) => { if (k > 0) kinder.push({ t: 'umbruch' }); kinder.push(...inlineLesen(a)); });
    bloecke.push({ art: 'absatz', kinder });
  }
  return bloecke;
}

/** Klartext eines Inline-Baums bzw. Blocks (für Tests, Tooltips und «Kopieren»). */
export function klartext(x) {
  if (Array.isArray(x)) return x.map(klartext).join('');
  if (!x) return '';
  if (x.t === 'umbruch') return '\n';
  if (x.t) return x.s ?? klartext(x.kinder);
  switch (x.art) {
    case 'code': return x.s;
    case 'liste': return x.punkte.map((p) => klartext(p.kinder)).join('\n');
    case 'tabelle': return [x.kopf, ...x.zeilen].map((r) => r.map(klartext).join('\t')).join('\n');
    case 'zitat': return x.bloecke.map(klartext).join('\n');
    case 'linie': return '';
    default: return klartext(x.kinder);
  }
}

// ------------------------------------------------------------------------------------------------ Kontext

const einzahl = (n, e, m) => `${n} ${n === 1 ? e : m}`;

/**
 * Die Kontext-Chips unter dem Eingabefeld. daten = { ansicht: {kennzeichen, name, art} | null, geschoss: {kennzeichen, name} | null, auswahl: [kz], bildMoeglich: bool },
 * ab = { ansicht, geschoss, auswahl, bild: bool } (true = mitschicken), internet: bool.
 * Mit Internet sind alle Chips ausgegraut und gesperrt (der Server verwirft den Kontext ohnehin).
 */
export function kontextChips(daten, ab, internet) {
  const d = daten ?? {};
  const chips = [];
  const sperr = internet ? INTERNET_TIPP : null;
  const chip = (id, vorhanden, textAn, tipp, vorgabeAn = true) => ({
    id, vorhanden, text: textAn, an: !internet && vorhanden && (ab?.[id] ?? vorgabeAn), gesperrt: !!internet || !vorhanden, tipp: sperr ?? tipp,
  });
  const a = d.ansicht;
  chips.push(chip('ansicht', !!a, a ? `Ansicht: ${a.name || a.kennzeichen}` : 'Keine Ansicht', a ? `Aktive Ansicht ${a.kennzeichen}${a.art ? ` (${a.art})` : ''} mit der Frage mitschicken` : 'Es ist keine Ansicht aktiv'));
  const g = d.geschoss;
  chips.push(chip('geschoss', !!g, g ? `Geschoss: ${g.name || g.kennzeichen}` : 'Kein Geschoss', g ? `Geschoss ${g.kennzeichen} mit der Frage mitschicken (geht die Ansicht mit, ergänzt der Server ihr Geschoss selbst)` : 'Die aktive Ansicht gehört zu keinem Geschoss'));
  const n = (d.auswahl ?? []).length;
  const auswahlText = n === 0 ? 'Keine Auswahl' : n === 1 ? `Auswahl: ${d.auswahl[0]}` : `Auswahl: ${einzahl(n, 'Element', 'Elemente')}`;
  chips.push(chip('auswahl', n > 0, auswahlText,
    n > 0 ? `Gewählt: ${d.auswahl.slice(0, 12).join(', ')}${n > 12 ? ` … (${n})` : ''}${n > GRENZEN.auswahl ? `. Mitgeschickt werden die ersten ${GRENZEN.auswahl}.` : ''}` : 'Es ist nichts gewählt'));
  chips.push(chip('bild', !!d.bildMoeglich, 'Bild der Ansicht', d.bildMoeglich ? 'Ein Bild der aktiven Ansicht mitschicken, damit Claude den Plan sehen kann (Vorgabe: aus)' : 'Diese Ansicht lässt sich nicht als Bild mitschicken', false));
  return chips;
}

/** Der Kontext der Frage. Mit Internet immer leer ({}): es geht nichts aus dem Projekt hinaus. bild = data-URL oder null. */
export function kontextKoerper(daten, ab, internet, bild = null) {
  if (internet) return {};
  const d = daten ?? {};
  const k = {};
  const an = (id, vorgabe = true) => (ab?.[id] ?? vorgabe);
  if (d.ansicht && an('ansicht')) k.ansicht = d.ansicht.kennzeichen;
  if (d.geschoss && an('geschoss')) k.geschoss = d.geschoss.kennzeichen;
  if ((d.auswahl ?? []).length > 0 && an('auswahl')) k.auswahl = d.auswahl.slice(0, GRENZEN.auswahl);
  if (bild && an('bild', false)) k.bild = bild;
  return k;
}

/** Körper von POST …/frage. */
export function frageKoerper({ text: t, kontext, modell, internet }) {
  const k = { text: String(t ?? '').trim(), kontext: kontext ?? {} };
  if (modell) k.modell = modell;
  k.internet = !!internet;
  return k;
}

/** Prüft die Frage vor dem Senden: null = in Ordnung, sonst der Hinweis für den Menschen. */
export function frageProbleme(t) {
  const s = String(t ?? '').trim();
  if (!s) return 'Bitte eine Frage eingeben.';
  if (s.length > GRENZEN.textZeichen) return `Die Frage ist zu lang (${s.length} von höchstens ${GRENZEN.textZeichen} Zeichen).`;
  return null;
}

// ------------------------------------------------------------------------------------------------ Texte

export function zeitText(iso) {
  const d = new Date(iso);
  if (!iso || Number.isNaN(d.getTime())) return '';
  const p = (n) => String(n).padStart(2, '0');
  const heute = new Date();
  const gleicherTag = d.toDateString() === heute.toDateString();
  return gleicherTag ? `${p(d.getHours())}:${p(d.getMinutes())}` : `${p(d.getDate())}.${p(d.getMonth() + 1)}. ${p(d.getHours())}:${p(d.getMinutes())}`;
}

export function dauerText(sek) {
  if (typeof sek !== 'number' || !Number.isFinite(sek) || sek < 0) return '';
  if (sek < 60) return `${Math.max(1, Math.round(sek))} s`;
  return `${Math.floor(sek / 60)} min ${Math.round(sek % 60)} s`;
}

const MODELLNAMEN = { sonnet: 'Sonnet', opus: 'Opus', haiku: 'Haiku' };
export const modellName = (id, modelle = []) => modelle.find((m) => m.id === id)?.name ?? MODELLNAMEN[id] ?? (id || '');

/** Text im Gesprächswahlfeld: «Titel · 3 Fragen · 20.10. 14:05». Eine beschädigte Gesprächsdatei (Server: lesbar false, Sicherheitsprüfung 3c, B10)
 *  erscheint als «nicht lesbar – Datei 2026….json». */
export function gespraechLabel(g) {
  if (!g) return '';
  if (g.lesbar === false) return `nicht lesbar – Datei ${String(g.datei ?? g.id ?? '').split(/[\\/]/).pop()}`;
  const teile = [g.titel || 'Neues Gespräch'];
  if (typeof g.fragen === 'number') teile.push(einzahl(g.fragen, 'Frage', 'Fragen'));
  const z = zeitText(g.geaendert);
  if (z) teile.push(z);
  if (g.laeuft) teile.push('läuft');
  return teile.join(' · ');
}

/** Meldung eines Werkzeugs für Menschen: die Antwort des Kerns ist oft ein (gekürztes) JSON mit «fehler» und «vorschlag» – dann nur diese beiden. */
export function werkzeugMeldung(m) {
  const s = String(m ?? '').trim();
  if (!s.startsWith('{')) return s;
  const feld = (name) => {
    const r = new RegExp(`"${name}"\\s*:\\s*"((?:[^"\\\\]|\\\\.)*)"`).exec(s);
    if (!r) return '';
    try { return JSON.parse(`"${r[1]}"`); } catch { return r[1]; }
  };
  const t = [feld('fehler'), feld('vorschlag')].filter(Boolean).join(' – ');
  return t || s;
}

/** Einzeiler zu einem Werkzeugaufruf. */
export function werkzeugZeile(w) {
  const zeichen = w?.ok === true ? '✓' : w?.ok === false ? '✗' : '…';
  return { zeichen, name: w?.name ?? '', kurz: w?.kurz ?? '', meldung: werkzeugMeldung(w?.meldung), zustand: w?.ok === true ? 'ok' : w?.ok === false ? 'fehler' : 'laeuft' };
}

export function werkzeugSumme(werkzeuge = []) {
  const n = werkzeuge.length;
  const fehl = werkzeuge.filter((w) => w.ok === false).length;
  const offen = werkzeuge.filter((w) => w.ok === null || w.ok === undefined).length;
  let t = einzahl(n, 'Werkzeugaufruf', 'Werkzeugaufrufe');
  if (fehl) t += `, ${fehl} mit Fehler`;
  if (offen) t += `, ${offen} läuft`;
  return t;
}

/** Meldung zu einer fehlgeschlagenen oder abgebrochenen Antwort (nachricht.fehlerart, nachricht.fehler). */
export function fehlerText(n, einst = null) {
  if (!n) return '';
  if (n.status === 'abgebrochen') return 'Abgebrochen.';
  if (n.status !== 'fehler') return '';
  const grund = n.fehler ? ` ${n.fehler}` : '';
  switch (n.fehlerart) {
    case 'anmeldung': return 'Claude ist nicht angemeldet. Bitte unter «Heute» mit Claude anmelden und die Frage wiederholen.';
    case 'zeitlimit': return `Zeitlimit überschritten${einst?.zeitlimitMinuten ? ` (${einst.zeitlimitMinuten} Minuten)` : ''}. Die Frage war zu gross oder Claude hat nicht geantwortet.${grund ? ` (${n.fehler})` : ''}`;
    case 'start': return `Claude konnte nicht gestartet werden.${grund}`;
    case 'mcp': return `Die Verbindung zum Modell (MCP) ist ausgefallen.${grund}`;
    case 'abgebrochen': return 'Abgebrochen.';
    default: return `Fehler.${grund}`.trim();
  }
}

/** Fehler des Servers ({ fehler, vorschlag }) als Satz für den Menschen. Vorschläge, die einen Endpunkt nennen («POST /api/…»), sind für Programme geschrieben und entfallen. */
export function serverFehlerText(daten, status) {
  const d = daten ?? {};
  const fehler = d.fehler || `Fehler ${status ?? ''}`.trim();
  let vorschlag = typeof d.vorschlag === 'string' && !/\/api\//.test(d.vorschlag) ? d.vorschlag : '';
  if (!vorschlag && status === 409 && /arbeitet gerade/.test(fehler)) vorschlag = 'Bitte warten, bis die Antwort da ist, oder sie dort abbrechen.';
  return [fehler, vorschlag].filter(Boolean).join(' – ');
}

/** Hinweis, wenn Claude nicht verfügbar ist (Einstellungen). */
export function verfuegbarText(einst) {
  if (!einst) return '';
  if (einst.verfuegbar !== false) return '';
  return `Claude ist auf diesem Rechner nicht verfügbar.${einst.grund ? ` ${einst.grund}` : ''}`;
}

/**
 * Das Zurücknehmen wurde abgelehnt (409 u. a.): verständlicher Text. antwort = JSON des Servers { fehler, vorschlag, ablehnung, spaeter?: [{paket, akteur, akteurArt, zeit, befehl, elemente}], gesperrt? }.
 * Ergebnis { titel, zeilen: [Text], spaeter: [Text] }.
 */
export function ablehnungText(antwort, status = 409) {
  const a = antwort ?? {};
  const spaeter = (a.spaeter ?? []).map((s) => {
    const wer = s.akteurArt === 'ki' ? 'Claude' : s.akteur ? `${s.akteur}` : 'jemand';
    const was = s.befehl ? `${s.befehl}` : 'eine Änderung';
    const el = (s.elemente ?? []).length ? ` an ${(s.elemente).slice(0, 5).join(', ')}${s.elemente.length > 5 ? ' …' : ''}` : '';
    const wann = zeitText(s.zeit);
    return `${wer}: ${was}${el}${wann ? ` (${wann})` : ''}`;
  });
  const zeilen = [];
  let titel;
  switch (a.ablehnung) {
    case 'spaeter_geaendert':
      titel = 'Nicht zurückgenommen: Danach wurde dasselbe noch einmal geändert.';
      zeilen.push('Es wurde nichts verändert. Nehmen Sie zuerst die späteren Änderungen zurück (von hinten nach vorn) oder ändern Sie von Hand.');
      break;
    case 'schon_zurueckgenommen':
      titel = 'Diese Änderung ist schon zurückgenommen.';
      break;
    case 'verwendet':
      titel = 'Nicht zurückgenommen: Ein von Claude angelegtes Element wird inzwischen verwendet.';
      zeilen.push('Löschen Sie zuerst, was darauf aufbaut, oder entfernen Sie das Element von Hand.');
      break;
    case 'gesperrt':
      titel = 'Nicht zurückgenommen: Elemente sind reserviert.';
      zeilen.push(`Die Elemente sind bei jemand anderem in Bearbeitung${(a.gesperrt ?? []).length ? ` (${a.gesperrt.slice(0, 5).map((g) => g.element ?? g).join(', ')})` : ''}.`);
      break;
    case 'unbekannt':
      titel = 'Diese Änderung gibt es nicht mehr.';
      break;
    case 'fremd':
      titel = 'Nur Änderungen von Claude lassen sich hier zurücknehmen.';
      break;
    case 'ablage':
      titel = 'Die Projektablage ist nicht erreichbar.';
      zeilen.push('Bitte die Verbindung zur Ablage prüfen und erneut versuchen.');
      break;
    default:
      titel = status === 409 ? 'Zurücknehmen im Moment nicht möglich.' : 'Zurücknehmen fehlgeschlagen.';
  }
  if (a.fehler && !/^(spaeter_geaendert|schon_zurueckgenommen|verwendet|gesperrt|unbekannt|fremd|ablage)$/.test(a.fehler) && a.ablehnung !== 'spaeter_geaendert') zeilen.push(a.fehler);
  if (a.vorschlag && a.ablehnung !== 'spaeter_geaendert' && !/\/api\//.test(a.vorschlag)) zeilen.push(a.vorschlag);
  return { titel, zeilen, spaeter };
}

/** Wie der Mensch den Kontext einer früheren Frage sieht (kleine Chips unter seiner Nachricht). kontextVerworfen: Internet. */
export function kontextText(n) {
  const k = n?.kontext ?? {};
  const aus = [];
  if (k.ansicht) aus.push(k.ansicht.gefunden === false ? `Ansicht ${k.ansicht.kennzeichen} (nicht gefunden)` : `Ansicht: ${k.ansicht.name || k.ansicht.kennzeichen}`);
  if (k.geschoss) aus.push(`Geschoss: ${k.geschoss.name || k.geschoss.kennzeichen}`);
  if ((k.auswahl ?? []).length) aus.push(k.auswahl.length === 1 ? `Auswahl: ${k.auswahl[0]}` : `Auswahl: ${einzahl(k.auswahl.length, 'Element', 'Elemente')}`);
  if (k.bild) aus.push('Bild der Ansicht');
  return aus;
}

/** Hinweis an der Frage, wenn der Server Reservierungen des Menschen für die Auswahl freigegeben hat (Nachricht.auswahlFreigegeben = Kennzeichen); sonst null. */
export function freigabeHinweis(n) {
  const l = Array.isArray(n?.auswahlFreigegeben) ? n.auswahlFreigegeben.filter((x) => typeof x === 'string' && x) : [];
  if (l.length === 0) return null;
  return { text: 'Auswahl für Claude freigegeben', tipp: `Ihre Reservierung von ${l.slice(0, 12).join(', ')}${l.length > 12 ? ` … (${l.length})` : ''} wurde für Claude freigegeben. Beim nächsten eigenen Ändern reserviert KICAD wieder.` };
}

// ------------------------------------------------------------------------------------------------ Bild

/** Grösse für das Bild: längste Kante höchstens max, nie vergrössern, mindestens 1. */
export function bildGroesse(b, h, max = BILD_MAX_KANTE, faktor = 1) {
  const bb = Math.max(1, Number(b) || 1), hh = Math.max(1, Number(h) || 1);
  const f = Math.min(1, max / Math.max(bb, hh)) * faktor;
  return { b: Math.max(1, Math.round(bb * f)), h: Math.max(1, Math.round(hh * f)) };
}

/** Bytes einer data-URL (Base64-Teil). */
export function dataUrlBytes(url) {
  const t = String(url ?? '');
  const i = t.indexOf(',');
  if (i < 0) return 0;
  const b64 = t.slice(i + 1);
  const pad = b64.endsWith('==') ? 2 : b64.endsWith('=') ? 1 : 0;
  return Math.max(0, Math.floor(b64.length * 3 / 4) - pad);
}

export const bildPasst = (url, grenze = GRENZEN.bildBytes) => /^data:image\/(png|jpeg);base64,/.test(String(url ?? '')) && dataUrlBytes(url) > 0 && dataUrlBytes(url) <= grenze;

// ------------------------------------------------------------------------------------------------ Merken (nur Wahl, keine Nachrichten)

export const MERKEN_SCHLUESSEL = 'ki';
const MAX_PROJEKTE = 30;

/** Gemerkte Wahl für ein Projekt: { gespraech: id|null, modell: id|null, internet: bool|null }. Ungültiges wird verworfen. */
export function wahlLesen(roh, ort) {
  const leer = { gespraech: null, modell: null, internet: null };
  if (!roh || typeof roh !== 'object' || Array.isArray(roh) || !ort) return leer;
  const w = roh[ort];
  if (!w || typeof w !== 'object') return leer;
  return {
    gespraech: typeof w.gespraech === 'string' && /^[A-Za-z0-9_.-]{1,80}$/.test(w.gespraech) ? w.gespraech : null,
    modell: typeof w.modell === 'string' && /^[a-z0-9_.-]{1,40}$/i.test(w.modell) ? w.modell : null,
    internet: typeof w.internet === 'boolean' ? w.internet : null,
  };
}

/** Neue Sammlung mit der Wahl des Projekts (die zuletzt benutzten höchstens MAX_PROJEKTE). Gibt nie Nachrichten oder Texte weiter. */
export function wahlSchreiben(roh, ort, wahl) {
  const basis = roh && typeof roh === 'object' && !Array.isArray(roh) ? { ...roh } : {};
  if (!ort) return basis;
  delete basis[ort];
  const neu = {};
  const alt = wahlLesen({ [ort]: wahl }, ort);
  neu[ort] = Object.fromEntries(Object.entries(alt).filter(([, v]) => v !== null));
  const rest = Object.entries(basis).slice(-(MAX_PROJEKTE - 1));
  return { ...Object.fromEntries(rest), ...neu };
}

/** Wahl des Gesprächs: gemerkte Id, wenn es sie gibt, sonst das neueste; null, wenn es keine Gespräche gibt. Nicht lesbare (B10) nie. */
export function gespraechWaehlen(liste, gemerkt) {
  const l = (liste ?? []).filter((g) => g.lesbar !== false);
  if (gemerkt && l.some((g) => g.id === gemerkt)) return gemerkt;
  return l[0]?.id ?? null;
}

/** Hinweis zu einer nicht lesbaren Gesprächsdatei (Tooltip): voller Pfad und Fehler. */
export function nichtLesbarTipp(g) {
  return g?.lesbar === false ? `Die Gesprächsdatei ist nicht lesbar: ${g.datei ?? g.id}${g.fehler ? ` (${g.fehler})` : ''}. Datei prüfen oder aus dem Ordner entfernen.` : '';
}

/** Modell: gemerktes, wenn bekannt, sonst die Vorgabe des Servers, sonst «sonnet». */
export function modellWaehlen(einst, gemerkt) {
  const ids = (einst?.modelle ?? []).map((m) => m.id);
  if (gemerkt && (ids.length === 0 || ids.includes(gemerkt))) return gemerkt;
  return einst?.vorgabeModell ?? 'sonnet';
}

export function internetWaehlen(einst, gemerkt) {
  if (internetGesperrt(einst)) return false;
  return typeof gemerkt === 'boolean' ? gemerkt : !!einst?.internetVorgabe;
}

/** Grund, wenn der Server den Internet-Schalter sperrt (Sicherheitsprüfung 3c: internetErlaubt false), sonst null. */
export function internetGesperrt(einst) {
  return einst?.internetErlaubt === false ? (einst.internetGrund || 'Internet in der Palette vorläufig gesperrt') : null;
}
