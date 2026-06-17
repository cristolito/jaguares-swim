// ============================================================================
//  Configuración del frontend cuando se sirve desde el HOST COMBINADO.
//  La API vive en el MISMO origen, por eso API_BASE_URL queda vacío:
//  apiFetch usará rutas relativas (/api/...) hacia este mismo servidor.
// ============================================================================
window.JAGUARES_CONFIG = {
    API_BASE_URL: ""
};
