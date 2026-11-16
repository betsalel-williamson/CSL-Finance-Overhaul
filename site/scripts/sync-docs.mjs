// Copies the player guide from ../docs/client (the single source, checked by Merlon) into
// Starlight's content folder: the first "# Heading" becomes the page title, links between guide
// pages ("./page.md") become site links, and images are copied alongside.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const workshop = "https://steamcommunity.com/sharedfiles/filedetails/?id=3840721874";
// Official Steam logo (simple-icons), inlined for the hero button; quoted for YAML.
const steamIcon = fs
  .readFileSync(path.resolve(here, "../src/icons/steam.svg"), "utf8")
  .replace(/<!--[\s\S]*?-->\s*/, "")
  .trim()
  .replace(/'/g, "''");
const src = path.resolve(here, "../../docs/client");
const out = path.resolve(here, "../src/content/docs");

fs.rmSync(out, { recursive: true, force: true });
fs.mkdirSync(out, { recursive: true });
fs.cpSync(path.join(src, "img"), path.join(out, "img"), { recursive: true });

for (const file of fs.readdirSync(src).filter((f) => f.endsWith(".md"))) {
  const isIndex = file === "index.md";
  let text = fs.readFileSync(path.join(src, file), "utf8");
  const h1 = text.match(/^# (.+)$/m);
  if (!h1) throw new Error(`${file}: needs a "# Title" line`);
  text = text.replace(h1[0], "").trimStart();
  if (isIndex) text = text.replace(/^!\[[^\]]*\]\(\.\/img\/finance-overhaul\.png\)\s*/m, ""); // shown in the hero

  // ./other.md -> site link; the index sits one level higher than the other pages.
  text = text.replace(/\]\(\.\/([a-z0-9-]+)\.md(#[^)]*)?\)/g, (_, slug, hash = "") => {
    const target = slug === "index" ? "" : `${slug}/`;
    return `](${isIndex ? "./" : "../"}${target}${hash})`;
  });

  const title = h1[1].replace(/"/g, '\\"');
  const front = isIndex
    ? `---\ntitle: "${title}"\ntemplate: splash\nhero:\n  tagline: Turn the Stock Exchange into a trading desk for your city's treasury.\n  image:\n    file: ./img/finance-overhaul.png\n  actions:\n    - text: Install on Steam\n      link: ${workshop}\n      icon: '${steamIcon}'\n    - text: Read the guide\n      link: ./install-and-first-run/\n      icon: right-arrow\n      variant: minimal\n---\n\n`
    : `---\ntitle: "${title}"\n---\n\n`;
  fs.writeFileSync(path.join(out, file), front + text);
}
console.log(`Synced ${fs.readdirSync(out).length} entries from docs/client`);
