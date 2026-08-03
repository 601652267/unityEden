#!/usr/bin/env node

const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const projectRoot = path.resolve(__dirname, "..", "..");
const sourceRoot = process.argv[2] ||
  "/Users/zhuhaiming/Desktop/edenAssets/file";
const galleryPath = path.join(
  projectRoot,
  "Assets/EdenGallery/Resources/EdenGallery/gallery.json"
);
const destinationRoot = path.join(
  projectRoot,
  "Assets/EdenBattle/Resources/EdenBattle/Heroes"
);

function guid() {
  return crypto.randomBytes(16).toString("hex");
}

function writeIfMissing(filePath, contents) {
  if (!fs.existsSync(filePath)) {
    fs.writeFileSync(filePath, contents, "utf8");
  }
}

function folderMeta() {
  return `fileFormatVersion: 2
guid: ${guid()}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function textMeta() {
  return `fileFormatVersion: 2
guid: ${guid()}
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function textureMeta() {
  return `fileFormatVersion: 2
guid: ${guid()}
TextureImporter:
  fileIDToRecycleName: {}
  externalObjects: {}
  serializedVersion: 9
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -100
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - serializedVersion: 2
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: ${guid()}
    vertices: []
    indices: 
    edges: []
    weights: []
  spritePackingTag: 
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function copyCharacter(cardId) {
  const sourceDirectory = path.join(sourceRoot, cardId);
  const files = [
    {
      source: path.join(
        sourceDirectory,
        "battle_spine",
        `CardSpine_${cardId}.atlas.prefab`
      ),
      target: `CardSpine_${cardId}.atlas.txt`,
      meta: textMeta
    },
    {
      source: path.join(
        sourceDirectory,
        "battle_spine",
        `CardSpine_${cardId}.skel.prefab`
      ),
      target: `CardSpine_${cardId}.skel.bytes`,
      meta: textMeta
    },
    {
      source: path.join(
        sourceDirectory,
        "battle_texture",
        `CardSpine_${cardId}.png`
      ),
      target: `CardSpine_${cardId}.png`,
      meta: textureMeta
    }
  ];

  if (files.some((entry) => !fs.existsSync(entry.source))) {
    return false;
  }

  const targetDirectory = path.join(destinationRoot, cardId);
  fs.mkdirSync(targetDirectory, { recursive: true });
  writeIfMissing(`${targetDirectory}.meta`, folderMeta());
  files.forEach((entry) => {
    const targetPath = path.join(targetDirectory, entry.target);
    fs.copyFileSync(entry.source, targetPath);
    writeIfMissing(`${targetPath}.meta`, entry.meta());
  });
  return true;
}

const gallery = JSON.parse(fs.readFileSync(galleryPath, "utf8"));
const cardIds = Array.from(new Set(
  (gallery.characters || []).map((character) => character.cardId)
)).filter(Boolean).sort();

fs.mkdirSync(destinationRoot, { recursive: true });
writeIfMissing(`${destinationRoot}.meta`, folderMeta());

const imported = [];
const missing = [];
cardIds.forEach((cardId) => {
  (copyCharacter(cardId) ? imported : missing).push(cardId);
});

console.log(`EDEN_BATTLE_SPINE_IMPORT_OK imported=${imported.length}`);
console.log(`EDEN_BATTLE_SPINE_MISSING count=${missing.length} ids=${missing.join(",")}`);
