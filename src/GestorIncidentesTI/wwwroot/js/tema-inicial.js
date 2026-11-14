// Tema guardado antes de pintar (páginas estáticas: 404, sin conexión).
(function () {
    var tema = "dark";
    try { tema = localStorage.getItem("tema") || "dark"; } catch (e) { /* sin almacenamiento */ }
    document.documentElement.setAttribute("data-theme", tema);
})();
