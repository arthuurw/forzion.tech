// Convencional Commits enforcement.
// Roda em commit-msg via .husky/commit-msg.
export default {
  extends: ["@commitlint/config-conventional"],
  rules: {
    // Permite os scopes usados no projeto.
    "scope-enum": [
      2,
      "always",
      ["frontend", "backend", "infra", "ci", "deps", "tests", "docs", ""],
    ],
    "header-max-length": [2, "always", 100],
    "body-max-line-length": [1, "always", 200],
  },
};
