// Player guide for Finance Overhaul, published to GitHub Pages.
// Pages are synced from ../docs/client (the single source) by scripts/sync-docs.mjs.
import { defineConfig } from "astro/config";
import starlight from "@astrojs/starlight";

export default defineConfig({
  site: "https://betsalel-williamson.github.io",
  base: "/CSL-Finance-Overhaul",
  integrations: [
    starlight({
      title: "Finance Overhaul",
      description: "Player guide for the Cities: Skylines Stock Exchange mod",
      social: [
        {
          icon: "github",
          label: "GitHub",
          href: "https://github.com/betsalel-williamson/CSL-Finance-Overhaul",
        },
      ],
      sidebar: [
        { label: "Welcome", link: "/" },
        {
          label: "Getting started",
          items: ["about-this-guide", "install-and-first-run"],
        },
        {
          label: "Features",
          items: [
            "trading-and-orders",
            "stock-monitor",
            "led-ticker",
            "city-index-fund",
            "city-bonds",
            "notices",
          ],
        },
        { label: "Reference", items: ["saves", "options"] },
      ],
    }),
  ],
});
