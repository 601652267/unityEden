#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const sourcePublic = process.argv[2] || '/Users/zhuhaiming/Desktop/edenvue/public';
const unityProject = process.argv[3] || path.resolve(__dirname, '..');
const characterLimit = Number(process.argv[4] || 10);
const sourceManifestPath = path.join(sourcePublic, 'gallery-assets', 'manifest.json');
const resourceRoot = path.join(
  unityProject,
  'Assets',
  'EdenGallery',
  'Resources',
  'EdenGallery',
);

if (!Number.isInteger(characterLimit) || characterLimit <= 0) {
  throw new Error(`Invalid character limit: ${process.argv[4]}`);
}
if (!fs.existsSync(sourceManifestPath)) {
  throw new Error(`Manifest not found: ${sourceManifestPath}`);
}
if (!resourceRoot.endsWith(path.join('Resources', 'EdenGallery'))) {
  throw new Error(`Refusing to replace unexpected output path: ${resourceRoot}`);
}

const sourceManifest = JSON.parse(fs.readFileSync(sourceManifestPath, 'utf8'));
const sourceCharacters = Array.isArray(sourceManifest)
  ? sourceManifest
  : sourceManifest.characters;
if (!Array.isArray(sourceCharacters)) {
  throw new Error('manifest.json does not contain a characters array.');
}

fs.rmSync(resourceRoot, {recursive: true, force: true});
fs.mkdirSync(resourceRoot, {recursive: true});

let copiedFiles = 0;
let copiedBytes = 0;

function sourceFile(webPath) {
  const normalized = String(webPath || '').replace(/^\/+/, '');
  const relative = normalized.replace(/^gallery-assets\//, 'gallery-assets/');
  return path.join(sourcePublic, relative);
}

function copy(source, destination) {
  if (!source || !fs.existsSync(source)) {
    throw new Error(`Required source asset not found: ${source}`);
  }
  fs.mkdirSync(path.dirname(destination), {recursive: true});
  fs.copyFileSync(source, destination);
  copiedFiles += 1;
  copiedBytes += fs.statSync(source).size;
}

function withoutExtension(resourcePath) {
  return resourcePath.replace(/\.[^/.]+$/, '');
}

function resourcePath(...parts) {
  return ['EdenGallery'].concat(parts).join('/');
}

function copyImage(webPath, folder, section) {
  if (!webPath) return '';
  const source = sourceFile(webPath);
  const filename = path.basename(source);
  const destination = path.join(resourceRoot, 'Characters', folder, section, filename);
  copy(source, destination);
  return withoutExtension(resourcePath('Characters', folder, section, filename));
}

function copySpineLayer(layer, folder) {
  const atlasSource = sourceFile(layer.atlasPath);
  const skeletonSource = sourceFile(layer.skeletonPath);
  const atlasBase = path.basename(atlasSource).replace(/\.atlas\.prefab$/i, '');
  const skeletonBase = path.basename(skeletonSource).replace(/\.skel\.prefab$/i, '');
  const spineFolder = path.join(resourceRoot, 'Characters', folder, 'Spine');
  const atlasFilename = `${atlasBase}.atlas.txt`;
  const skeletonFilename = `${skeletonBase}.skel.bytes`;
  copy(atlasSource, path.join(spineFolder, atlasFilename));
  copy(skeletonSource, path.join(spineFolder, skeletonFilename));

  const texturePaths = (layer.texturePaths || []).map((webPath) => {
    const source = sourceFile(webPath);
    const filename = path.basename(source);
    copy(source, path.join(spineFolder, filename));
    return withoutExtension(resourcePath('Characters', folder, 'Spine', filename));
  });

  return {
    type: 'spine',
    name: layer.stageName || atlasBase,
    atlasPath: resourcePath('Characters', folder, 'Spine', `${atlasBase}.atlas`),
    skeletonPath: resourcePath('Characters', folder, 'Spine', `${skeletonBase}.skel`),
    texturePaths,
    imagePath: '',
    skinName: layer.skinName || '',
    roleLayer: layer.isRoleLayer === true,
    backgroundLayer: layer.isBackgroundLayer === true,
    fullscreen: false,
    displayScale: Number(layer.displayScale) > 0 ? Number(layer.displayScale) : 1,
    offsetX: Number(layer.layerOffsetX || layer.offsetX || 0) * 0.01,
    offsetY: Number(layer.layerOffsetY || layer.offsetY || 0) * 0.01,
  };
}

function copyStaticLayer(layer, folder) {
  const imagePath = layer.imagePath || layer.staticImagePath || (layer.texturePaths || [])[0];
  return {
    type: 'image',
    name: layer.stageName || path.basename(imagePath || 'StaticImage'),
    atlasPath: '',
    skeletonPath: '',
    texturePaths: [],
    imagePath: copyImage(imagePath, folder, 'Images'),
    skinName: '',
    roleLayer: false,
    backgroundLayer: layer.isBackgroundLayer === true,
    fullscreen: true,
    displayScale: Number(layer.displayScale) > 0 ? Number(layer.displayScale) : 1,
    offsetX: 0,
    offsetY: 0,
  };
}

const characters = sourceCharacters.slice(0, characterLimit).map((character) => {
  const cardId = String(character.cardId || character.id);
  const portraitSource = character.portraitPath || character.iconPath || '';
  const portraitPath = copyImage(portraitSource, cardId, 'Portrait');
  const stages = (character.stages || []).map((stage, stageIndex) => {
    const folder = String(stage.folder || `${cardId}_${stageIndex + 1}`);
    const backgroundSource = (stage.backgroundPaths || [])[0] || '';
    const backgroundPath = copyImage(backgroundSource, folder, 'Images');
    const layers = (stage.layers || []).map((layer) => {
      const isImage = layer.isStaticImageLayer === true ||
        (!layer.atlasPath && !layer.skeletonPath && Boolean(layer.imagePath));
      return isImage ? copyStaticLayer(layer, folder) : copySpineLayer(layer, folder);
    });
    return {
      folder,
      label: `立绘${stageIndex + 1}`,
      sceneSized: stage.allowSceneSized === true || Boolean(backgroundPath),
      backgroundPath,
      layers,
    };
  });

  return {
    cardId,
    name: character.name || character.characterName || cardId,
    portraitPath,
    stages,
  };
});

const outputManifest = {
  version: '1.0.0',
  generatedAt: new Date().toISOString(),
  sourceManifest: sourceManifestPath,
  characterCount: characters.length,
  stageCount: characters.reduce((total, character) => total + character.stages.length, 0),
  characters,
};

fs.writeFileSync(
  path.join(resourceRoot, 'gallery.json'),
  `${JSON.stringify(outputManifest, null, 2)}\n`,
);

const report = {
  characters: outputManifest.characterCount,
  stages: outputManifest.stageCount,
  files: copiedFiles + 1,
  bytes: copiedBytes,
  output: resourceRoot,
};
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
