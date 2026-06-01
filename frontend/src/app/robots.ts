import type { MetadataRoute } from "next";

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech";

// Indexável só quando explicitamente habilitado (produção). Default = noindex
// total, evitando que homolog/staging (host público) seja indexado. Defesa em
// profundidade com o X-Robots-Tag do nginx no host de homolog.
const indexable = process.env.NEXT_PUBLIC_INDEXABLE === "true";

export default function robots(): MetadataRoute.Robots {
  if (!indexable) {
    return { rules: { userAgent: "*", disallow: "/" } };
  }
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: ["/admin", "/treinador", "/aluno", "/perfil", "/api/"],
    },
    sitemap: `${SITE_URL}/sitemap.xml`,
  };
}
