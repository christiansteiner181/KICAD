// Setzt das Design (hell/dunkel) vor dem ersten Zeichnen, damit die Seite nicht von hell nach dunkel flackert.
// Klassisches Skript (kein Modul), steht in index.html im <head> vor den Stilen. Vorgabe: dunkel.
// Gemerkt wird unter «hlks.design» (gleicher Schlüssel wie merken in zustand.js, Wert als JSON-Text).
(function () {
  var design = 'dunkel';
  try {
    var t = localStorage.getItem('hlks.design');
    if (t && JSON.parse(t) === 'hell') design = 'hell';
  } catch (e) { /* ohne localStorage bleibt es bei der Vorgabe */ }
  document.documentElement.setAttribute('data-design', design);
})();
