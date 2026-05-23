import type { Preview } from "@storybook/nextjs";
import { ThemeProvider, CssBaseline } from "@mui/material";
import { initialize, mswLoader } from "msw-storybook-addon";
import theme from "../src/lib/theme";
import { handlers } from "../src/test/msw/handlers";

// Inicializa Service Worker MSW para interceptar HTTP em stories.
// Requer public/mockServiceWorker.js (gerado via "npx msw init public/").
initialize({
  onUnhandledRequest: "warn",
  serviceWorker: { url: "/mockServiceWorker.js" },
});

const preview: Preview = {
  loaders: [mswLoader],
  parameters: {
    msw: { handlers },
    layout: "centered",
    a11y: {
      element: "#storybook-root",
      manual: false,
    },
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
  },
  decorators: [
    (Story) => (
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <Story />
      </ThemeProvider>
    ),
  ],
};

export default preview;
