export const environment = {
  production: false,
  apiUrl: (import.meta as any).env?.NG_APP_API_URL ?? '/api',
  // Usar https para que el discovery no haga 307 http→https (el navegador no sigue bien el redirect del fetch).
  // Si ves status 0 en F12, abre https://localhost:5086/.well-known/openid-configuration en el navegador y acepta el cert autofirmado de Aspire.
  identityAuthority: (import.meta as any).env?.NG_APP_IDENTITY_AUTHORITY ?? 'https://localhost:5086',
};
