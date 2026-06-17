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

    window.apiFetch = function (path, options) {
        options = options || {};
        var opts = {};
        for (var k in options) { if (Object.prototype.hasOwnProperty.call(options, k)) opts[k] = options[k]; }

        var headers = new Headers(options.headers || {});
        var token = localStorage.getItem(TOKEN_KEY);
        if (token) headers.set("Authorization", "Bearer " + token);
        opts.headers = headers;

        var fullUrl = /^https?:\/\//.test(path) ? path : API_BASE + path;
        return fetch(fullUrl, opts);
    };
})();
