// Botón de tema claro/oscuro (antes iba en línea en cada layout).
(function () {
    var boton = document.getElementById("toggleTema");
    if (!boton) return;
    boton.addEventListener("click", function () {
        var actual = document.documentElement.getAttribute("data-theme") || "dark";
        var nuevo = actual === "dark" ? "light" : "dark";
        document.documentElement.setAttribute("data-theme", nuevo);
        try { localStorage.setItem("tema", nuevo); } catch (e) { /* sin almacenamiento */ }
    });
})();
