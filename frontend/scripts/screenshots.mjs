// Captura prints reais da landing usando treinador/aluno do schema develop.
// One-off: rodar com frontend (:3001) + backend (:5230) no ar. Saída: public/screenshots/*.webp (1280x900).
// Esconde sidebar/header/dev-overlay e foca no conteúdo; encaixa em 1280x900 com fit:contain
// (bg casado) pra não ser cortado pelo objectFit:cover da landing.
import { chromium } from "@playwright/test";
import sharp from "sharp";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.resolve(__dirname, "..", "public", "screenshots");
const BASE = "http://localhost:3001";
const VIEWPORT = { width: 1440, height: 1024 };
const TARGET = { width: 1280, height: 900 };

const CARLOS = { email: "carlos.screenshot@forzion.test", senha: "Screenshot123!" };
const MARIA = { email: "maria.screenshot@forzion.test", senha: "Screenshot123!" };
const TREINO_ID = "8d4b1c69-e0ce-4a36-b7cf-ad117a4b22a3"; // Treino A - Peito e Tríceps (6 ex)

const HIDE_CSS = `
  .MuiAppBar-root, header.MuiAppBar-root { display:none !important; }
  .MuiDrawer-root { display:none !important; }
  nextjs-portal, [data-nextjs-dev-tools-button], #__next-build-watcher { display:none !important; }
  main { margin-top:0 !important; padding:24px !important; }
`;

async function seedConsent(ctx) {
  await ctx.addCookies([{
    name: "consent",
    value: encodeURIComponent(JSON.stringify({ v: 1, analytics: false })),
    url: BASE,
  }]);
}

async function login(ctx, creds) {
  const page = await ctx.newPage();
  await page.goto(`${BASE}/login`, { waitUntil: "networkidle" });
  await page.getByLabel(/e-?mail/i).first().fill(creds.email);
  await page.locator('input[type="password"]').first().fill(creds.senha);
  await page.getByRole("button", { name: /entrar/i }).click();
  await page.waitForURL((u) => !u.pathname.startsWith("/login"), { timeout: 20000 });
  return page;
}

// Encaixa o png (conteúdo focado) num canvas 1280x900 com a cor de fundo da página.
async function fitToCanvas(png, bg, dest) {
  await sharp(png)
    .resize(TARGET.width, TARGET.height, { fit: "contain", background: bg })
    .flatten({ background: bg })
    .webp({ quality: 88 })
    .toFile(dest);
}

async function shoot(page, route, file, { cutAtText } = {}) {
  await page.goto(`${BASE}${route}`, { waitUntil: "networkidle" });
  await page.addStyleTag({ content: HIDE_CSS });
  await page.waitForTimeout(2500); // charts/imagens assíncronas
  await page.evaluate(() => window.scrollTo(0, 0));

  // bg = theme background.default (#F7F8FA), mesmo da seção HowItWorks → padding funde.
  const bg = { r: 247, g: 248, b: 250 };

  // `main` estica até 100dvh; clipa na altura REAL do conteúdo (primeiro filho)
  // pra não sobrar metade vazia. PAD dá respiro nas bordas.
  const PAD = 20;
  const box = await page.locator("main > *").first().boundingBox();
  let bottom = box.y + box.height;
  if (cutAtText) {
    const cutY = await page.evaluate((t) => {
      const el = [...document.querySelectorAll("h1,h2,h3,h4,h5,h6")]
        .find((e) => e.textContent.trim().toLowerCase().includes(t.toLowerCase()));
      return el ? el.getBoundingClientRect().top + window.scrollY : null;
    }, cutAtText);
    if (cutY) bottom = cutY;
  }

  const x = Math.max(0, box.x - PAD);
  const y = Math.max(0, box.y - PAD);
  const png = await page.screenshot({
    clip: { x, y, width: box.width + PAD * 2, height: Math.ceil(bottom - y + PAD) },
  });
  await fitToCanvas(png, bg, path.join(OUT, file));
  console.log(`OK ${file} <- ${route} (${Math.round(box.width)}x${Math.round(bottom - box.y)})`);
}

const browser = await chromium.launch();

const ctxT = await browser.newContext({ viewport: VIEWPORT, deviceScaleFactor: 2 });
await seedConsent(ctxT);
const pT = await login(ctxT, CARLOS);
await shoot(pT, `/treinador/treinos/${TREINO_ID}`, "ficha.webp");
await shoot(pT, "/treinador/alunos", "alunos.webp");
await ctxT.close();

const ctxA = await browser.newContext({ viewport: VIEWPORT, deviceScaleFactor: 2 });
await seedConsent(ctxA);
const pA = await login(ctxA, MARIA);
await shoot(pA, "/aluno/historico", "historico.webp", { cutAtText: "Sessões recentes" });
await ctxA.close();

await browser.close();
console.log("done");
