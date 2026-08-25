import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./app/**/*.{ts,tsx}",
    "./components/**/*.{ts,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        bg:        "#060E1A",
        surface:   "#0A1628",
        surface2:  "#0F1E38",
        surface3:  "#152540",
        border:    "#1C334F",
        borderBright: "#254466",
        "border-b": "#254466",
        accent:    "#00C49A",
        amber:     "#F5A623",
        red:       "#E0536A",
        text1:     "#C2D8F0",
        text2:     "#5B7A9B",
        text3:     "#2A4060",
      },
      fontFamily: {
        sans: ["Inter", "-apple-system", "BlinkMacSystemFont", "sans-serif"],
        mono: ["JetBrains Mono", "'Courier New'", "Consolas", "monospace"],
      },
      fontSize: {
        "2xs": ["0.65rem", { lineHeight: "1rem" }],
      },
    },
  },
  plugins: [],
};

export default config;
