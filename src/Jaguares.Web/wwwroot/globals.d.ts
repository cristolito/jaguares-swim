// ============================================================================
//  Declaraciones de tipos para el editor (IntelliSense / checkJs).
//  Describe los GLOBALES que viven fuera de cada archivo:
//    - apiFetch / JaguaresApi / byId  -> definidos en api.js
//    - JAGUARES_CONFIG                 -> definido en config.js
//    - Swal / confetti                 -> librerías cargadas por CDN
//  Gracias a esto, el editor encuentra su definición y deja de marcar
//  "no se encuentra el nombre". No se compila ni se publica: es solo ayuda al IDE.
// ============================================================================

/** Configuración inyectada por config.js (URL base de la API). */
type JaguaresConfig = { API_BASE_URL: string };

/** Helpers del cliente de API (token y construcción de URLs). */
interface JaguaresApiClient {
    base: string;
    getToken(): string | null;
    setToken(token: string): void;
    clearToken(): void;
    url(path: string): string;
}

// --- Globales usables SIN el prefijo window. (apiFetch(...), byId(...), Swal, ...) ---

declare var JAGUARES_CONFIG: JaguaresConfig;

/**
 * Llama a la API anteponiendo la URL base y el token JWT.
 * Igual que fetch(), pero con logs y cabeceras automáticas.
 */
declare function apiFetch(path: string, options?: RequestInit): Promise<Response>;

declare const JaguaresApi: JaguaresApiClient;

/**
 * Obtiene un elemento por id. Lanza un error claro si no existe,
 * de modo que el resultado NUNCA es null (valida el dato nulo por ti).
 */
declare function byId(id: string): HTMLElement;

/** SweetAlert2 (https://sweetalert2.github.io). Firma simplificada. */
declare const Swal: {
    fire(title?: string, text?: string, icon?: string): Promise<any>;
    fire(options: Record<string, any>): Promise<any>;
    [key: string]: any;
};

/** canvas-confetti (https://github.com/catdad/canvas-confetti). */
declare function confetti(options?: Record<string, any>): Promise<void> | null;

// --- Mismos globales colgados de window (para api.js/config.js que hacen window.X = ...) ---

interface Window {
    JAGUARES_CONFIG?: JaguaresConfig;
    apiFetch: typeof apiFetch;
    JaguaresApi: JaguaresApiClient;
    byId: typeof byId;
    Swal: typeof Swal;
    confetti: typeof confetti;
}
