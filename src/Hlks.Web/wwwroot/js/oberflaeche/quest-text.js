// Dialog «Quest verbinden» (Modul XR): reine Hilfen – Ampel aus dem Zustand des Servers, Schalter der Knöpfe, Texte der Einrichtung und der Hinweise.
// Keine Fachlogik und kein DOM (Node-Test: quest.test.mjs). Den Zustand liefert GET /api/quest (Server: QuestDienst.cs); gehandelt wird über
// POST /api/quest/verbinden | wlan | ip | trennen. Der Dialog selbst: quest-dialog.js. Hintergrund: entwurf/modul-xr.md, «Start ohne Link-fähigen PC».

/** Maskiert Text für HTML. */
export const esc = (t) => String(t ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

/** Offizielle Seite der Android Platform Tools (öffnet im Browser; KICAD lädt nichts selbst herunter). */
export const PLATFORM_TOOLS_URL = 'https://developer.android.com/tools/releases/platform-tools';
/** Ordner, in den die ZIP-Datei entpackt wird; die Suche nach adb.exe nimmt ihn als ersten bekannten Ort. */
export const ENTPACK_ORDNER = '%USERPROFILE%\\platform-tools';
/** Schlüssel der Benutzereinstellung mit dem Pfad zur adb.exe. */
export const ADB_SCHLUESSEL = 'quest.adb';

/** Die vier Punkte der Ampel in fester Reihenfolge. */
export const AMPEL_PUNKTE = Object.freeze([
  { id: 'adb', titel: 'adb gefunden' },
  { id: 'quest', titel: 'Quest verbunden' },
  { id: 'frei', titel: 'Freigegeben' },
  { id: 'weiter', titel: 'Weiterleitung aktiv' },
]);

/**
 * Ampel aus dem Zustand von GET /api/quest. Rückgabe: vier Einträge { id, titel, stufe: 'ok' | 'schlecht' | 'offen', text }.
 * ok = grün, schlecht = rot (hier fehlt etwas), offen = grau (noch nicht an der Reihe, weil ein Schritt davor fehlt).
 * Ohne Zustand (noch nicht gelesen) sind alle offen.
 */
export function ampel(z) {
  const [adb, quest, frei, weiter] = AMPEL_PUNKTE.map((p) => ({ ...p, stufe: 'offen', text: '' }));
  if (!z) {
    adb.text = 'Wird geprüft …';
    return [adb, quest, frei, weiter];
  }
  if (!z.adbGefunden) {
    adb.stufe = 'schlecht';
    adb.text = z.adbHinweis || 'adb.exe wurde nicht gefunden.';
    quest.text = 'Zuerst adb einrichten.';
    frei.text = 'Zuerst adb einrichten.';
    weiter.text = 'Zuerst adb einrichten.';
    return [adb, quest, frei, weiter];
  }
  adb.stufe = 'ok';
  adb.text = `${z.adbPfad ?? 'adb'}${z.adbQuelle ? ` (${QUELLE_TEXT[z.adbQuelle] ?? z.adbQuelle})` : ''}`;
  if (z.fehler) {
    quest.stufe = 'schlecht';
    quest.text = z.fehler;
    frei.text = '';
    weiter.text = '';
    return [adb, quest, frei, weiter];
  }
  if (z.questVerbunden) {
    quest.stufe = 'ok';
    quest.text = geraeteText(z.geraete);
  } else {
    quest.stufe = 'schlecht';
    quest.text = 'Keine Quest gefunden: Kabel stecken (Datenkabel), Brille einschalten, Entwicklermodus aktiv.';
  }
  if (z.freigegeben) {
    frei.stufe = 'ok';
    frei.text = 'USB-Debugging ist erlaubt.';
  } else if (z.nichtFreigegeben) {
    frei.stufe = 'schlecht';
    frei.text = 'In der Brille «USB-Debugging erlauben» bestätigen (immer von diesem Computer).';
  } else {
    frei.text = 'Folgt, sobald die Quest verbunden ist.';
  }
  if (z.weiterleitung) {
    weiter.stufe = 'ok';
    weiter.text = `In der Quest: ${urlText(z)}`;
  } else if (z.freigegeben) {
    weiter.stufe = 'schlecht';
    weiter.text = `Noch nicht gesetzt: «Verbinden» klicken (Port ${z.port ?? '?'}).`;
  } else {
    weiter.text = 'Folgt nach der Freigabe.';
  }
  return [adb, quest, frei, weiter];
}

const QUELLE_TEXT = { einstellung: 'eingestellter Pfad', pfad: 'aus PATH', ort: 'bekannter Ort' };

/** «1WMHH… (USB), 192.168.1.50:5555 (WLAN)» – die Geräteliste in einfachen Worten. */
export function geraeteText(geraete) {
  const l = (geraete ?? []).map((g) => `${g.seriennummer} (${g.wlan ? 'WLAN' : 'USB'}${g.zustand === 'device' ? '' : `, ${ZUSTAND_TEXT[g.zustand] ?? g.zustand}`})`);
  return l.length === 0 ? 'Keine' : l.join(', ');
}
const ZUSTAND_TEXT = { unauthorized: 'nicht freigegeben', offline: 'offline', device: 'bereit' };

/** Die Adresse, die in der Quest einzugeben ist: die VR-Startseite /vr (ein Tipp, und man steht im Modell), nicht die PC-Oberfläche «/». */
export const VR_PFAD = '/vr';
export const urlText = (z) => `${(z?.url || `http://localhost:${z?.port ?? ''}`).replace(/\/+$/, '')}${VR_PFAD}`;

/** Einfache Vorprüfung der IP-Adresse für den Knopf (die Regeln gelten auf dem Server): IPv4, optional :Port. */
export function ipFormatOk(text) {
  const m = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})(?::(\d{1,5}))?$/.exec(String(text ?? '').trim());
  if (!m) return false;
  const o = m.slice(1, 5).map(Number);
  if (o.some((x) => x > 255) || o[0] === 0 || o[0] > 223 || o[0] === 127) return false;
  return m[5] === undefined || (Number(m[5]) >= 1 && Number(m[5]) <= 65535);
}

/**
 * Welche Knöpfe frei sind. z: Zustand (oder null), laeuft: ein Aufruf läuft, ip: Inhalt des Feldes.
 * Verbinden braucht eine freigegebene Quest; «Auf WLAN umstellen» eine freigegebene Quest, die noch nicht über WLAN läuft;
 * «Über WLAN verbinden» adb und eine gültig aussehende Adresse; «Trennen» eine aktive Weiterleitung.
 */
export function knoepfe(z, laeuft = false, ip = '') {
  const da = !!z && !laeuft;
  return {
    verbinden: da && !!z.freigegeben,
    wlan: da && !!z.freigegeben && !z.wlan,
    ipVerbinden: da && !!z.adbGefunden && ipFormatOk(ip),
    trennen: da && !!z.freigegeben && !!z.weiterleitung,
    neuPruefen: !laeuft,
    adbSpeichern: !laeuft,
  };
}

/** Die Einrichtung, wenn adb fehlt (Schritt für Schritt). KICAD lädt nichts herunter und installiert nichts. */
export function einrichtungSichtbar(z) {
  return !!z && z.adbGefunden === false;
}

export const EINRICHTUNG = Object.freeze([
  { nr: 1, titel: 'Android Platform Tools holen', text: 'Die ZIP-Datei von der offiziellen Android-Seite herunterladen (der Link öffnet im Browser).' },
  { nr: 2, titel: 'ZIP-Datei entpacken', text: `Nach ${ENTPACK_ORDNER} entpacken, sodass dort die Datei adb.exe liegt. Diesen Ordner sucht KICAD als Erstes.` },
  { nr: 3, titel: 'Erneut prüfen', text: 'Dann «Erneut prüfen» klicken. Liegt adb.exe woanders, den Pfad unten eintragen.' },
]);

/** Hinweise in einfachen Worten, in der Reihenfolge der Einrichtung der Brille. */
export const HINWEISE = Object.freeze([
  'Entwicklermodus: in der Meta-Horizon-App am Handy einschalten. Dafür braucht es einmalig eine Entwickler-Organisation bei Meta (kostenlos).',
  'Ein USB-C-Kabel, das auch Daten überträgt (nicht nur laden), verbindet die Brille mit dem PC. Es geht auch ohne Kabel: einmal mit Kabel «Auf WLAN umstellen».',
  'In der Brille erscheint «USB-Debugging erlauben». Dort «Immer von diesem Computer erlauben» wählen und bestätigen.',
  'Steht «Keine Quest gefunden», obwohl das Kabel steckt: den Meta-ADB-Treiber installieren (developers.meta.com, braucht Administratorrechte). KICAD installiert den Treiber nicht selbst.',
  'Danach in der Quest den Browser öffnen und die Adresse eingeben, die unten steht (http://localhost:<Port>/vr, die VR-Startseite mit dem Knopf «Eintreten»). Mit «Trennen» wird die Weiterleitung wieder entfernt.',
]);

/** Text über den Knöpfen: was als Nächstes zu tun ist. */
export function naechsterSchritt(z) {
  if (!z) return 'Der Zustand wird gelesen …';
  if (!z.adbGefunden) return 'Zuerst adb einrichten (Schritte unten).';
  if (z.fehler) return z.fehler;
  if (z.weiterleitung) return `Fertig. In der Quest den Browser öffnen und ${urlText(z)} eingeben.`;
  if (z.freigegeben) return '«Verbinden» klicken.';
  if (z.nichtFreigegeben) return 'In der Brille «USB-Debugging erlauben» bestätigen, dann «Erneut prüfen».';
  return 'Quest per Kabel anschliessen (oder die WLAN-Adresse eintragen), dann «Erneut prüfen».';
}

/** Fehler der Schnittstelle als Anzeige { text, vorschlag }. */
export function fehlerAnzeige(err) {
  return { text: err?.message || String(err ?? 'Unbekannter Fehler.'), vorschlag: err?.vorschlag || null };
}

// ------------------------------------------------------------------------------------------ Brille automatisch verbinden (MAC statt IP)
// Statusanzeige «Brille» in der Statusleiste (quest-status.js) und der Schalter im Dialog. Der Server (QuestDienst.cs, POST /api/quest/automatik) macht je Aufruf genau einen
// Schritt; hier steht nur, wann die Oberfläche ihn aufruft und wie sie das Ergebnis zeigt. Hintergrund: entwurf/modul-xr.md, Idee MAC.

/** Schlüssel der Benutzereinstellungen: «Brille automatisch verbinden» (Ja/Nein, Vorgabe nein) und die gemerkten Brillen. */
export const AUTOMATIK_SCHLUESSEL = 'quest.automatisch';
export const BRILLEN_SCHLUESSEL = 'quest.brillen';
/** Takt der Oberfläche, solange die Brille nicht bereit ist (nur adb devices und die Nachbartabelle, nie ein Ping-Durchlauf). */
export const TAKT_MS = 10000;
/** Anlässe des Schritts; nur «takt» darf keinen Ping-Durchlauf auslösen (der Server prüft das ebenfalls). */
export const ANLAESSE = Object.freeze(['takt', 'start', 'klick', 'ansicht3d']);

const MAC = /^([0-9a-f]{2}:){5}[0-9a-f]{2}$/;

/** Die gemerkten Brillen aus einem Einstellungswert: nur Einträge mit Seriennummer und gültiger MAC. */
export function brillenListe(wert) {
  if (!Array.isArray(wert)) return [];
  return wert.filter((b) => b && typeof b === 'object' && typeof b.seriennummer === 'string' && b.seriennummer !== '' && typeof b.mac === 'string' && MAC.test(b.mac)).map((b) => ({
    seriennummer: b.seriennummer, mac: b.mac, letzteIp: typeof b.letzteIp === 'string' ? b.letzteIp : null, zuletzt: typeof b.zuletzt === 'string' ? b.zuletzt : null,
  }));
}

/** Die Statusanzeige ist nur sichtbar, wenn die Automatik an ist oder schon eine Brille gemerkt ist. */
export const statusSichtbar = (automatisch, brillen) => automatisch === true || (brillen ?? []).length > 0;

/** Ruft die Oberfläche im Takt auf? Nur bei eingeschalteter Automatik, ohne laufenden Aufruf und solange die Brille nicht bereit (grün) ist. */
export const taktNoetig = ({ automatisch, antwort, laeuft }) => automatisch === true && !laeuft && antwort?.status !== 'bereit';

/** Darf die Oberfläche den Schritt aufrufen? Bei ausgeschalteter Automatik nur auf ausdrücklichen Klick. */
export const schrittErlaubt = (anlass, automatisch) => ANLAESSE.includes(anlass) && (anlass === 'klick' || automatisch === true);

const GRUND_TEXT = {
  kein_adb: 'adb fehlt', nicht_freigegeben: 'nicht freigegeben', fehler: 'Fehler', nicht_gefunden: 'nicht gefunden', aus: 'Automatik aus',
};

/**
 * Anzeige der Statusleiste: { stufe: 'gruen' | 'grau' | 'gelb', text, titel }. grün = bereit, gelb = sucht (Aufruf läuft oder der Server meldet «sucht»),
 * grau = nicht gefunden (auch: adb fehlt, nicht freigegeben, Fehler, Automatik aus).
 */
export function brilleAnzeige(antwort, laeuft = false, automatisch = false) {
  const tipp = 'Klick: Brille jetzt suchen und verbinden · Rechtsklick: Dialog «Quest verbinden»';
  if (laeuft) return { stufe: 'gelb', text: 'Brille: sucht …', titel: `Die Brille wird gesucht.\n${tipp}` };
  if (!antwort) {
    return automatisch ? { stufe: 'gelb', text: 'Brille: sucht …', titel: `Die Brille wird gesucht.\n${tipp}` }
      : { stufe: 'grau', text: 'Brille: nicht verbunden', titel: `Brille gemerkt, Automatik aus.\n${tipp}` };
  }
  const zusatz = [antwort.meldung, antwort.hinweis].filter(Boolean).join('\n');
  if (antwort.status === 'bereit') return { stufe: 'gruen', text: 'Brille bereit', titel: `${zusatz || 'Die Brille ist verbunden.'}\n${tipp}` };
  if (antwort.status === 'sucht') return { stufe: 'gelb', text: 'Brille: sucht …', titel: `${zusatz || 'Die Brille wird gesucht.'}\n${tipp}` };
  return { stufe: 'grau', text: `Brille: ${GRUND_TEXT[antwort.status] ?? 'nicht gefunden'}`, titel: `${zusatz || 'Die Brille wurde nicht gefunden.'}\n${tipp}` };
}

/** «1WMHH000000000 · MAC aa:bb:cc:dd:ee:01 · zuletzt 192.168.1.50» – eine gemerkte Brille in einfachen Worten. */
export const brilleText = (b) => `${b.seriennummer} · MAC ${b.mac} · zuletzt ${b.letzteIp ?? 'unbekannt'}`;

/** Die Liste ohne die Brille mit dieser MAC; null, wenn nichts übrig bleibt (die Einstellung wird dann zurückgesetzt). */
export function ohneBrille(liste, mac) {
  const rest = (liste ?? []).filter((b) => b.mac !== mac);
  return rest.length === 0 ? null : rest;
}
