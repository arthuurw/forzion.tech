/**
 * Placeholder usado como ARG default no Dockerfile (stage `builder`) para
 * satisfazer o guard de build do next.config quando JWT_SECRET ainda não
 * está disponível. O runtime guard em `instrumentation.ts` rejeita esse
 * valor para impedir que um container suba sem o segredo real.
 *
 * Fonte da verdade: este arquivo. O Dockerfile mantém o literal em
 * `ARG JWT_SECRET=...` (não suporta import). O teste em
 * `buildPlaceholder.test.ts` garante que os dois valores nunca divergem.
 */
export const JWT_SECRET_BUILD_PLACEHOLDER = "build-placeholder-not-used-at-runtime";
