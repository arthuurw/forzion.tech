// Roda em commit-msg via .husky/commit-msg.
const config = {
  extends: ["@commitlint/config-conventional"],
  ignores: [
    (message) => message.includes("dependabot[bot]"),
    // GitHub squash-merge anexa " (#NNN)" ao título do PR automaticamente,
    // fora do controle de quem escreveu a mensagem original. Sem o sufixo
    // o header respeita o limite — só o auto-append estoura.
    (message) => {
      const header = message.split("\n")[0];
      const semSufixoDePr = header.replace(/ \(#\d+\)$/, "");
      return header.length > 100 && semSufixoDePr.length <= 100;
    },
  ],
  rules: {
    "scope-enum": [
      2,
      "always",
      ["frontend", "backend", "infra", "ci", "deps", "tests", "docs", ""],
    ],
    "header-max-length": [2, "always", 100],
    "body-max-line-length": [1, "always", 200],
  },
};

export default config;
