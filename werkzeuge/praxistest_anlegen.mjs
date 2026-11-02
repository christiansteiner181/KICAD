// Praxistest-Projekt (Christian 02.11.2026: «ich brauche praxis test mit leerer architektur und allen basisfamilien zum testen»).
// Legt über einen laufenden KICAD-Server ein neues Projekt an: Gebäude mit drei Geschossen ohne Bauteile, Grundrisse je Geschoss für Sanitär,
// Heizung und Lüftung, Prinzipschemata, 3D – und lädt jede freigegebene Familie der Bürobibliothek (bzw. ihres Spiegels) ins Projekt.
// Schreibt mit «X-Hlks-Akteur-Art: ki» (Arbeitsregel Hauptlauf). Keine echten Projektdaten: alles fiktiv.
//
//   node werkzeuge/praxistest_anlegen.mjs --ordner <leerer Ordner> [--port 5130] [--name Praxistest] [--alle]
//   --alle: auch Familien, die (noch) nicht freigegeben sind

const arg = (n, v) => { const i = process.argv.indexOf(n); return i >= 0 ? process.argv[i + 1] : v; };
const port = arg('--port', '5130');
const ordner = arg('--ordner');
const name = arg('--name', 'Praxistest');
const alle = process.argv.includes('--alle');
if (!ordner) { console.error('--ordner fehlt'); process.exit(2); }
const basis = `http://localhost:${port}/api`;

async function post(pfad, koerper, ki = true) {
  const r = await fetch(basis + pfad, {
    method: 'POST', body: JSON.stringify(koerper ?? {}),
    headers: { 'Content-Type': 'application/json', ...(ki ? { 'X-Hlks-Akteur-Art': 'ki' } : {}) },
  });
  const t = await r.text();
  let j; try { j = JSON.parse(t); } catch { j = { text: t }; }
  if (!r.ok) throw new Error(`${pfad}: ${r.status} ${j.fehler ?? t} ${j.vorschlag ?? ''}`);
  return j;
}
async function befehl(n, par) {
  const r = await post(`/befehl/${n}`, par);
  if (!r.angenommen) throw new Error(`${n} abgelehnt: ${r.fehler} | ${r.vorschlag ?? ''}`);
  return r;
}
const neu = (klasse, kennzeichen, name, eigenschaften, mehr = {}) => befehl('element_anlegen', { klasse, kennzeichen, name, eigenschaften, ...mehr });

const status = await (await fetch(`${basis}/status`)).json();
if (status.offen) { console.error(`Auf Port ${port} ist ein Projekt offen («${status.name ?? status.ort}») – zuerst schliessen.`); process.exit(3); }

await post('/projekt/anlegen', { ordner, name }, false);
console.log(`Projekt «${name}» angelegt in ${ordner}`);

// Leere Architektur: Gebäude und Geschosse, keine Wände, Räume, Decken
await neu('Gebaeude', 'GEB-1', 'Praxistest (fiktiv)', { adresse: 'Testweg 1, 3000 Bern (fiktiv)' });
const geschosse = [['UG', 'Untergeschoss', -2700, 'U'], ['EG', 'Erdgeschoss', 0, 'E'], ['1.OG', '1. Obergeschoss', 2900, '1']];
for (const [kz, n, kote, kurz] of geschosse) await neu('Geschoss', kz, n, { kote, hoehe: kz === 'UG' ? 2700 : 2900, kurz }, { teilVon: 'GEB-1' });

// Ansichten: je Geschoss und Gewerk ein Grundriss, je Gewerk ein Prinzipschema, 3D
const gewerke = [['sanitaer', 'Sanitär', 'SAN'], ['heizung', 'Heizung/Kälte', 'HZ'], ['lueftung', 'Lüftung', 'LUE']];
for (const [g] of geschosse.map((x) => [x[0]]))
  for (const [gw, text, k] of gewerke)
    await neu('Ansicht', `AN-GR-${g}-${k}`, `Grundriss ${g} ${text}`,
      { art: 'grundriss', massstab: 50, schnitthoehe: 1000, detailgrad: 'mittel', gewerke: [gw, 'architektur'] }, { geschoss: g });
for (const [gw, text, k] of gewerke)
  await neu('Ansicht', `AN-SCHEMA-${k}`, `Prinzipschema ${text}`, { art: 'schema', detailgrad: 'mittel', gewerke: [gw] });
await neu('Ansicht', 'AN-3D', '3D Gesamt', { art: '3d', detailgrad: 'fein' });
console.log('Gebäude, 3 Geschosse und Ansichten angelegt');

// Alle Familien der Bibliothek laden
const bib = await post('/abfrage/familien_bibliothek', {});
const liste = bib.familien ?? bib.eintraege ?? [];
console.log(`Bibliothek: ${bib.ort ?? bib.bibliothek ?? '?'} – ${liste.length} Familien${bib.offline ? ' (offline, Spiegel)' : ''}`);
let gut = 0; const fehler = [];
for (const f of liste) {
  if (!alle && f.status && f.status !== 'freigegeben') { fehler.push(`${f.name}: übersprungen (Status ${f.status})`); continue; }
  try { await befehl('familie_laden', { id: f.id }); gut++; }
  catch (e) { fehler.push(`${f.name ?? f.id}: ${e.message}`); }
}
console.log(`Familien geladen: ${gut}/${liste.length}`);
for (const f of fehler) console.log('  - ' + f);
