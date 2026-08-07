import type { Preview } from "@storybook/nextjs";
import { ThemeProvider, CssBaseline } from "@mui/material";
import { mswLoader } from "msw-storybook-addon/csf3";
import theme from "../src/lib/theme";
import { handlers } from "../src/test/msw/handlers";

const preview: Preview = {
  loaders: [mswLoader()],
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
