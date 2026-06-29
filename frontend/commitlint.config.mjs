// Roda em commit-msg via .husky/commit-msg.
const config = {
  extends: ["@commitlint/config-conventional"],
  ignores: [(message) => message.includes("dependabot[bot]")],
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
