// Ansichtsarten, die in einem späteren Modul kommen (Schnitt, Ansicht: 1a). Das Schema hat seit Modul 3 (C2) eine eigene Ansicht (schema.js).
const MODUL = { schnitt: '1a', ansicht: '1a' };
export default { erstellen(behaelter, ansicht) { const art = ansicht?.werte?.art ?? '?'; behaelter.innerHTML = `<div class="leer-hinweis">Ansichtsart «${art}» folgt in Modul ${MODUL[art] ?? 'später'}.<br>Das Element ${ansicht?.kennzeichen ?? ''} ist im Modell bereits angelegt.</div>`; return {}; } };
