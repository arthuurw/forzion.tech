import type { MetadataRoute } from "next";

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: ["/admin", "/treinador", "/aluno", "/perfil", "/api/"],
    },
    sitemap: `${SITE_URL}/sitemap.xml`,
  };
}
