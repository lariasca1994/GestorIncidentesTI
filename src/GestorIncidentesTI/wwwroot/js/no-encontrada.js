// Muestra la dirección que no existe (como texto, nunca como HTML).
(function () {
    var ruta = location.pathname.slice(0, 80);
    document.querySelectorAll(".ruta").forEach(function (c) { c.textContent = ruta; });
})();
