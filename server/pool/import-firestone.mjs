// One-time import of the public Battlegrounds base data into the Hearthlens D1 database.
//
// Run this once (and only when you deliberately want to refresh the base data):
//   node import-firestone.mjs            -> writes migrations/0004_meta.sql
//   npx wrangler d1 execute hearthlens-pool --remote --file=migrations/0004_meta.sql --yes
//
// After that the app and the Worker never call zerotoheroes.com again: /v1/meta is served from D1.
// The download is cached in .cache/firestone, so re-running does not fetch the files again unless one is missing.
//
// The 92 MB comp-stats file is mostly raw final-board dumps (`heroStats[].finalBoards`). It is reduced to the
// aggregates the advisor can use (placement, placement per MMR, hero placement per comp). Everything else is kept.

import { mkdir, readFile, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";

const ROOT = "https://static.zerotoheroes.com";
const BRACKETS = [100, 50, 25, 10, 1];
const CACHE = path.join(import.meta.dirname, ".cache", "firestone");
const OUT = path.join(import.meta.dirname, "migrations", "0004_meta.sql");
const CHUNK = 50_000;

const SOURCES = {
  heroes: (b) => `api/bgs/hero-stats/mmr-${b}/last-patch/overview-from-hourly.gz.json`,
  cards: (b) => `api/bgs/card-stats/mmr-${b}/last-patch/overview-from-hourly.gz.json`,
  trinkets: () => "api/bgs/trinket-stats/last-patch/overview-from-hourly.gz.json",
  comps: () => "api/bgs/comp-stats/last-patch/overview-from-hourly.gz.json",
  strategies: () => "hearthstone/data/battlegrounds-strategies/bgs-comps-strategies.gz.json",
};

const r3 = (x) => (typeof x === "number" && Number.isFinite(x) ? Math.round(x * 1000) / 1000 : 0);
const list = (x) => (Array.isArray(x) ? x : []);

async function load(name, url) {
  const file = path.join(CACHE, name);
  if (!existsSync(file)) {
    process.stdout.write(`downloading ${url}\n`);
    const response = await fetch(url);
    if (!response.ok) throw new Error(`${url}: HTTP ${response.status}`);
    await mkdir(CACHE, { recursive: true });
    await writeFile(file, Buffer.from(await response.arrayBuffer()));
  }
  return JSON.parse(await readFile(file, "utf8"));
}

/** Everything the app reads, plus the fields that may be useful later, in short arrays to keep the payload small. */
function compact(heroesByBracket, cardsByBracket, trinketStats, compStats, strategies) {
  const heroes = {};
  for (const [bracket, doc] of Object.entries(heroesByBracket)) {
    heroes[bracket] = list(doc?.heroStats).filter((h) => h?.heroCardId).map((h) => {
      let top4 = 0;
      let first = 0;
      for (const p of list(h.placementDistribution)) {
        const share = (Number(p?.percentage) || 0) / 100;
        if (p?.rank >= 1 && p?.rank <= 4) top4 += share;
        if (p?.rank === 1) first = share;
      }
      return [
        String(h.heroCardId),
        Math.max(0, Number(h.dataPoints) || 0),
        Math.max(0, Number(h.totalOffered) || 0),
        Math.max(0, Number(h.totalPicked) || 0),
        r3(h.averagePosition),
        r3(Math.min(1, top4)),
        r3(first),
        list(h.tribeStats).filter((t) => Number.isInteger(t?.tribe)).map((t) => [t.tribe, r3(t.impactAveragePosition)]),
      ];
    });
  }

  const cards = {};
  for (const [bracket, doc] of Object.entries(cardsByBracket)) {
    cards[bracket] = list(doc?.cardStats).filter((c) => c?.cardId).map((c) => [
      String(c.cardId),
      Math.max(0, Number(c.totalPlayed) || 0),
      r3(c.averagePlacement),
      list(c.turnStats)
        .filter((t) => Number.isInteger(t?.turn) && Number(t.totalPlayed) > 0)
        .map((t) => [t.turn, Math.max(0, Number(t.totalPlayed) || 0), r3(t.averagePlacement)]),
    ]);
  }

  const trinkets = list(trinketStats).filter((t) => t?.trinketCardId).map((t) => [
    String(t.trinketCardId),
    Math.max(0, Number(t.dataPoints) || 0),
    r3(t.pickRate),
    r3(t.averagePlacement),
    list(t.averagePlacementAtMmr)
      .filter((m) => Number.isInteger(m?.mmr))
      .map((m) => [m.mmr, Math.max(0, Number(m.dataPoints) || 0), r3(m.placement)]),
  ]);

  const comps = list(compStats).filter((c) => c?.archetype).map((c) => [
    String(c.archetype),
    Math.max(0, Number(c.dataPoints) || 0),
    r3(c.averagePlacement),
    list(c.averagePlacementAtMmr)
      .filter((m) => Number.isInteger(m?.mmr))
      .map((m) => [m.mmr, Math.max(0, Number(m.dataPoints) || 0), r3(m.placement)]),
    list(c.heroStats)
      .filter((h) => h?.heroCardId)
      .map((h) => [String(h.heroCardId), Math.max(0, Number(h.dataPoints) || 0), r3(h.averagePlacement)]),
  ]);

  const guides = list(strategies)
    .filter((s) => typeof s?.compId === "string" && s.compId.trim().length > 0)
    .map((s) => {
      const english = list(s.tips).find((t) => t?.language === "enUS") ?? list(s.tips)[0];
      return {
        id: s.compId.trim(),
        name: String(s.name ?? ""),
        power: String(s.powerLevel ?? ""),
        difficulty: String(s.difficulty ?? ""),
        tribes: list(s.forcedTribes).map((t) => (typeof t === "number" ? t : String(t))),
        cards: list(s.cards)
          .filter((c) => c?.cardId)
          .map((c) => [String(c.cardId), String(c.status || "ADDON")]),
        tip: english?.tip ? String(english.tip) : "",
        when: english?.whenToCommit ? String(english.whenToCommit) : "",
      };
    });

  return { heroes, cards, trinkets, comps, guides };
}

function sqlFor(json) {
  const chunks = [];
  for (let i = 0; i < json.length; i += CHUNK) chunks.push(json.slice(i, i + CHUNK));
  const lines = [
    "-- Base Battlegrounds data, imported once. Served by /v1/meta; the app never calls a third-party stats API.",
    "-- Generated by server/pool/import-firestone.mjs.",
    "CREATE TABLE IF NOT EXISTS meta (seq INTEGER PRIMARY KEY, body TEXT NOT NULL);",
    "DELETE FROM meta;",
  ];
  chunks.forEach((chunk, index) => {
    lines.push(`INSERT INTO meta (seq, body) VALUES (${index + 1}, '${chunk.replaceAll("'", "''")}');`);
  });
  lines.push("");
  return { sql: lines.join("\n"), chunks: chunks.length };
}

const heroesByBracket = {};
const cardsByBracket = {};
for (const bracket of BRACKETS) heroesByBracket[bracket] = await load(`heroes-${bracket}.json`, ROOT + "/" + SOURCES.heroes(bracket));
for (const bracket of BRACKETS) cardsByBracket[bracket] = await load(`cards-${bracket}.json`, ROOT + "/" + SOURCES.cards(bracket));
const trinketDoc = await load("trinkets.json", ROOT + "/" + SOURCES.trinkets());
const compDoc = await load("comp-stats.json", ROOT + "/" + SOURCES.comps());
const strategies = await load("strategies.json", ROOT + "/" + SOURCES.strategies());

const percentiles = list(heroesByBracket[100]?.mmrPercentiles)
  .filter((p) => Number.isInteger(p?.percentile) && Number.isInteger(p?.mmr))
  .map((p) => [p.percentile, p.mmr]);

const dates = [
  heroesByBracket[100]?.lastUpdateDate,
  cardsByBracket[100]?.lastUpdateDate,
  trinketDoc?.lastUpdateDate,
  compDoc?.lastUpdateDate,
].filter((d) => typeof d === "string" && d.length > 0).sort();

const body = compact(heroesByBracket, cardsByBracket, trinketDoc?.trinketStats, compDoc?.compStats, strategies);
const meta = {
  v: 1,
  updated: dates.at(-1) ?? new Date().toISOString(),
  percentiles,
  ...body,
};
const json = JSON.stringify(meta);
const { sql, chunks } = sqlFor(json);
await writeFile(OUT, sql, "utf8");

const size = (value) => `${(JSON.stringify(value).length / 1024).toFixed(1)} KB`;
console.log(`updated ${meta.updated}`);
console.log(`heroes   ${Object.keys(meta.heroes).map((b) => `${b}:${meta.heroes[b].length}`).join(" ")}  (${size(meta.heroes)})`);
console.log(`cards    ${Object.keys(meta.cards).map((b) => `${b}:${meta.cards[b].length}`).join(" ")}  (${size(meta.cards)})`);
console.log(`trinkets ${meta.trinkets.length}  (${size(meta.trinkets)})`);
console.log(`comps    ${meta.comps.length}  (${size(meta.comps)})`);
console.log(`guides   ${meta.guides.length}  (${size(meta.guides)})`);
console.log(`total    ${(json.length / 1024).toFixed(1)} KB in ${chunks} rows -> ${OUT}`);
