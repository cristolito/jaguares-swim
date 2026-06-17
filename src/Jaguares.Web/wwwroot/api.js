// ============================================================================
//  Cliente de API de Jaguares Swim
//  Expone apiFetch(path, options): como fetch(), pero
//    1) antepone la URL base de la API (config.js)
//    2) adjunta el token JWT (Bearer) si el usuario inició sesión
//  Además ofrece helpers para guardar/leer/borrar el token y construir URLs.
// ============================================================================
(function () {
    var TOKEN_KEY = "jaguares_token";
    var API_BASE = (window.JAGUARES_CONFIG && window.JAGUARES_CONFIG.API_BASE_URL) || "";
    console.log("[API] Cliente cargado. API base URL:", API_BASE === "" ? "(vacío => mismo origen, rutas relativas /api/...)" : API_BASE);

    window.JaguaresApi = {
        base: API_BASE,
        getToken: function () { return localStorage.getItem(TOKEN_KEY); },
        setToken: function (t) { localStorage.setItem(TOKEN_KEY, t); },
        clearToken: function () { localStorage.removeItem(TOKEN_KEY); },
        // Construye una URL absoluta hacia la API (útil para imágenes/comprobantes).
        url: function (path) {
            if (!path) return API_BASE;
            return /^https?:\/\//.test(path) ? path : API_BASE + path;
        }
    };

    // Obtiene un elemento por id y VALIDA que exista: si el HTML no lo tiene,
    // lanza un error claro en vez de devolver null (evita "Cannot read ... of null").
    // El editor (checkJs) lo trata como HTMLElement no-nulo gracias a globals.d.ts.
    window.byId = function (id) {
        var el = document.getElementById(id);
        if (!el) throw new Error("No se encontró el elemento con id '" + id + "' en el DOM.");
        return el;
    };

    window.apiFetch = function (path, options) {
        /** @type {RequestInit} */
        var opts = Object.assign({}, options || {});

        var headers = new Headers(opts.headers || {});
        var token = localStorage.getItem(TOKEN_KEY);
        if (token) headers.set("Authorization", "Bearer " + token);
        opts.headers = headers;

        var metodo = (opts.method || "GET").toUpperCase();
        var fullUrl = /^https?:\/\//.test(path) ? path : API_BASE + path;

        // Log de diagnóstico: muestra en la consola del navegador (F12) CADA llamada a la API,
        // el momento en que se dispara, la URL final y el resultado/tiempo.
        var inicio = (window.performance && performance.now) ? performance.now() : Date.now();
        console.log("[API] --> " + metodo + " " + fullUrl);

        return fetch(fullUrl, opts)
            .then(function (resp) {
                var ms = Math.round(((window.performance && performance.now) ? performance.now() : Date.now()) - inicio);
                console.log("[API] <-- " + metodo + " " + fullUrl + " => " + resp.status + " " + resp.statusText + " (" + ms + " ms)");
                return resp;
            })
            .catch(function (err) {
                var ms = Math.round(((window.performance && performance.now) ? performance.now() : Date.now()) - inicio);
                console.error("[API] xxx " + metodo + " " + fullUrl + " FALLÓ tras " + ms + " ms:", err);
                throw err;
            });
    };
})();
