// Espelha o teto do PaginacaoFilter do backend (forzion.tech.Api/Filters/PaginacaoFilter.cs):
// tamanhoPagina > 100 retorna HTTP 400. Telas que carregam "tudo" de uma vez (pickers de
// Autocomplete, agregados) pedem este máximo; pedir acima quebra a chamada inteira.
export const MAX_PAGE_SIZE = 100;
