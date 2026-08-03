#!/usr/bin/env node

const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const projectRoot = path.resolve(__dirname, "..", "..");
const cardId = process.argv[2];
if (!/^\d{8}$/.test(cardId || "")) {
  throw new Error("Usage: node prepareSkillBundles.js <8-digit-card-id>");
}
const missingReportFlagIndex = process.argv.indexOf("--missing-report");
const missingReportPath = missingReportFlagIndex >= 0
  ? process.argv[missingReportFlagIndex + 1]
  : "";
if (missingReportFlagIndex >= 0 && !missingReportPath) {
  throw new Error("--missing-report requires a report path");
}

const bundleSourceRoot =
  "/Users/zhuhaiming/Desktop/edenAssetsObb/main.19.jp.co.yoozoo.projectyellow/assets/ResEx";
const extractedRoot = "/Users/zhuhaiming/Desktop/edenAssets/file";
const seedRoot = path.join(
  projectRoot,
  "Assets/StreamingAssets/Skill11300018Original"
);
const bundleOutputRoot = path.join(
  projectRoot,
  `Assets/StreamingAssets/Skill${cardId}Original`
);
const videoOutputRoot = path.join(
  projectRoot,
  `Assets/StreamingAssets/Skill${cardId}`
);
const retainedOutputBundles = fs.existsSync(bundleOutputRoot)
  ? fs.readdirSync(bundleOutputRoot).filter((fileName) =>
      fileName.toLowerCase().endsWith(".aab"))
  : [];

const characterSpecificSeedPatterns = [
  /^eft_/i,
  /^m_cardspine_/i,
  /^mov_/i,
  /^st_cardspine_/i,
  /^st_labi_/i,
  /^st_sfx_labi_/i,
  /^st_sfx_tx_labi_(?!sphere)/i
];

const extraDependencies = {
  "11301006": [
    "eft_fx_11301006_attack_hit.aab",
    "eft_fx_11301006_skill2.aab",
    "eft_fx_11301006_skill_hit.aab"
  ],
  "11301023": [
    "st_ctl_waterslash01.aab",
    "st_fx_glow_005_sh.aab",
    "st_sfx_ctl_waterslash02.aab",
    "st_sfx_jingjiniao_chuanmao.aab",
    "st_sfx_tx_007.aab",
    "st_sfx_tx_235.aab",
    "st_sfx_tx_296.aab",
    "st_sfx_tx_680.aab",
    "st_sfx_tx_glow_005.aab",
    "st_sfx_tx_labi_sphere.aab",
    "st_sequence_cp_201.aab",
    "st_sequence_cp_222.aab",
    "st_sfx_common_bglight.aab",
    "st_sfx_jingjiniao_suolian.aab",
    "st_sfx_jingjiniao_suolian_2.aab",
    "st_sfx_tx_014.aab",
    "st_sfx_tx_152.aab",
    "st_sfx_tx_492.aab",
    "st_sfx_tx_529.aab",
    "st_sfx_tx_675.aab",
    "st_sfx_tx_water_002_sh.aab",
    "st_sfx_tx_water_014.aab",
    "st_beam.aab",
    "st_comon_shaow.aab"
  ]
};

function makeGuid() {
  return crypto.randomBytes(16).toString("hex");
}

function defaultMeta() {
  return `fileFormatVersion: 2
guid: ${makeGuid()}
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function folderMeta() {
  return `fileFormatVersion: 2
guid: ${makeGuid()}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function writeMetaIfMissing(targetPath, folder) {
  const metaPath = `${targetPath}.meta`;
  if (!fs.existsSync(metaPath)) {
    fs.writeFileSync(metaPath, folder ? folderMeta() : defaultMeta(), "utf8");
  }
}

function stripUnityBundle(sourcePath) {
  const source = fs.readFileSync(sourcePath);
  const signature = Buffer.from("UnityFS", "ascii");
  const offset = source.indexOf(signature);
  if (offset < 0) {
    throw new Error(`UnityFS signature missing: ${sourcePath}`);
  }
  return { bytes: source.slice(offset), removedPrefixBytes: offset };
}

function sha256(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex");
}

function addBundleFromSource(fileName, manifest) {
  const sourcePath = path.join(bundleSourceRoot, fileName);
  if (!fs.existsSync(sourcePath)) {
    return false;
  }
  const bundle = stripUnityBundle(sourcePath);
  const targetPath = path.join(bundleOutputRoot, fileName);
  fs.writeFileSync(targetPath, bundle.bytes);
  writeMetaIfMissing(targetPath, false);
  manifest.push({
    file: fileName,
    source: sourcePath,
    removed_prefix_bytes: bundle.removedPrefixBytes,
    size: bundle.bytes.length,
    sha256: sha256(bundle.bytes)
  });
  return true;
}

function addSeedBundle(fileName, manifest) {
  const sourcePath = path.join(seedRoot, fileName);
  if (!fs.existsSync(sourcePath)) {
    return false;
  }
  const targetPath = path.join(bundleOutputRoot, fileName);
  const bytes = fs.readFileSync(sourcePath);
  fs.writeFileSync(targetPath, bytes);
  writeMetaIfMissing(targetPath, false);
  manifest.push({
    file: fileName,
    source: sourcePath,
    removed_prefix_bytes: 0,
    size: bytes.length,
    sha256: sha256(bytes)
  });
  return true;
}

function addDependenciesFromMissingReport(reportPath, manifest) {
  if (!reportPath) {
    return { added: 0, unresolved: [] };
  }
  if (!fs.existsSync(reportPath)) {
    throw new Error(`Missing dependency report: ${reportPath}`);
  }

  const sourceFiles = fs.readdirSync(bundleSourceRoot);
  const sourceByLowerName = new Map(
    sourceFiles.map((fileName) => [fileName.toLowerCase(), fileName])
  );
  const resourceNames = new Set();
  const report = fs.readFileSync(reportPath, "utf8");
  for (const line of report.split(/\r?\n/)) {
    const markerIndex = line.indexOf("missing=");
    if (markerIndex < 0) {
      continue;
    }
    const value = line.slice(markerIndex + "missing=".length).trim();
    if (!value || value === "none") {
      continue;
    }
    for (const token of value.split(",")) {
      const resourceName = token.trim().replace(/^(texture|mesh):/i, "");
      if (resourceName) {
        resourceNames.add(resourceName);
      }
    }
  }

  const unresolved = [];
  let added = 0;
  for (const resourceName of Array.from(resourceNames).sort()) {
    const candidates = [
      `st_${resourceName}.aab`,
      `${resourceName}.aab`
    ];
    let sourceFileName = "";
    for (const candidate of candidates) {
      const actualName = sourceByLowerName.get(candidate.toLowerCase());
      if (actualName) {
        sourceFileName = actualName;
        break;
      }
    }
    if (!sourceFileName) {
      unresolved.push(resourceName);
      continue;
    }
    if (manifest.some((entry) =>
      entry.file.toLowerCase() === sourceFileName.toLowerCase())) {
      continue;
    }
    if (addBundleFromSource(sourceFileName, manifest)) {
      added++;
    }
  }
  return { added, unresolved };
}

fs.mkdirSync(bundleOutputRoot, { recursive: true });
fs.mkdirSync(videoOutputRoot, { recursive: true });
writeMetaIfMissing(bundleOutputRoot, true);
writeMetaIfMissing(videoOutputRoot, true);

const manifest = [];
const seedManifestPath = path.join(seedRoot, "manifest.json");
const seedManifest = JSON.parse(fs.readFileSync(seedManifestPath, "utf8"));
seedManifest.forEach((entry) => {
  const fileName = entry.file;
  if (!characterSpecificSeedPatterns.some((pattern) => pattern.test(fileName))) {
    addSeedBundle(fileName, manifest);
  }
});

const requiredCoreBundles = [
  `m_cardspine_${cardId}.aab`,
  `st_cardspine_${cardId}.aab`,
  `eft_fx_${cardId}_attack.aab`,
  `eft_fx_${cardId}_attack_2.aab`,
  `eft_fx_${cardId}_skill.aab`,
  `eft_fx_${cardId}_skill_2.aab`,
  `eft_fx_timeline_${cardId}_xp.aab`
];
const escapedCardId = cardId.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const characterEffectPattern = new RegExp(
  `^eft_fx_(?:timeline_)?${escapedCardId}_.+\\.aab$`,
  "i"
);
const discoveredCharacterBundles = fs.readdirSync(bundleSourceRoot)
  .filter((fileName) => characterEffectPattern.test(fileName));
const coreBundles = Array.from(new Set([
  ...requiredCoreBundles,
  ...discoveredCharacterBundles
])).sort();
const missingCore = [];
coreBundles.forEach((fileName) => {
  if (!addBundleFromSource(fileName, manifest)) {
    if (requiredCoreBundles.includes(fileName)) {
      missingCore.push(fileName);
    }
  }
});

(extraDependencies[cardId] || []).forEach((fileName) => {
  if (!manifest.some((entry) => entry.file === fileName)) {
    addBundleFromSource(fileName, manifest);
  }
});

// A successful dependency-closure pass must survive later routine runs. The
// latest inspector report may already say missing=none, so it no longer lists
// the dependencies that produced that result. Retain the AABs already present
// in this character's isolated output directory and refresh them from ResEx.
let retainedDependencyCount = 0;
retainedOutputBundles.forEach((fileName) => {
  if (manifest.some((entry) =>
    entry.file.toLowerCase() === fileName.toLowerCase())) {
    return;
  }
  if (addBundleFromSource(fileName, manifest)) {
    retainedDependencyCount++;
    return;
  }

  const targetPath = path.join(bundleOutputRoot, fileName);
  if (!fs.existsSync(targetPath)) {
    return;
  }
  const bytes = fs.readFileSync(targetPath);
  manifest.push({
    file: fileName,
    source: targetPath,
    removed_prefix_bytes: 0,
    size: bytes.length,
    sha256: sha256(bytes)
  });
  retainedDependencyCount++;
});

const reportDependencies = addDependenciesFromMissingReport(
  missingReportPath,
  manifest
);

manifest.sort((left, right) => left.file.localeCompare(right.file));
const manifestPath = path.join(bundleOutputRoot, "manifest.json");
fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
writeMetaIfMissing(manifestPath, false);

const sourceVideoRoot = path.join(extractedRoot, cardId, "video");
let videoName = "";
if (fs.existsSync(sourceVideoRoot)) {
  videoName = fs.readdirSync(sourceVideoRoot).find((fileName) =>
    fileName.toLowerCase() === `fx_timeline_${cardId}_xp.m4v`
  ) || "";
}
if (videoName) {
  const targetVideoPath = path.join(videoOutputRoot, videoName);
  fs.copyFileSync(path.join(sourceVideoRoot, videoName), targetVideoPath);
  writeMetaIfMissing(targetVideoPath, false);
}

console.log(
  `EDEN_SKILL_BUNDLES_READY id=${cardId} bundles=${manifest.length}` +
  ` missingCore=${missingCore.join(",") || "none"}` +
  ` video=${videoName || "missing"}` +
  ` retained=${retainedDependencyCount}` +
  ` reportDeps=${reportDependencies.added}` +
  ` unresolved=${reportDependencies.unresolved.join(",") || "none"}`
);
