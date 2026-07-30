#!/usr/bin/env node

const fs = require("fs");
const path = require("path");

const projectRoot = path.resolve(__dirname, "..");
const galleryPath = path.join(
  projectRoot,
  "Assets/EdenGallery/Resources/EdenGallery/gallery.json"
);
const sceneVoicePath = path.join(
  projectRoot,
  "Assets/EdenGallery/Resources/EdenGallery/voice_catalog.json"
);
const outputPath = path.join(
  projectRoot,
  "Assets/EdenGallery/Resources/EdenGallery/character_details.json"
);
const sourcePath =
  process.argv[2] ||
  "/Users/zhuhaiming/Desktop/edenAssets/file/character_info_manifest.json";
const voiceDirectory =
  process.argv[3] || "/Users/zhuhaiming/Desktop/edenAssets/voice";

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function cleanText(value) {
  return typeof value === "string" ? value.trim() : "";
}

function voiceKey(line) {
  const source = cleanText(line.voicePath) || cleanText(line.audioFile);
  return path.basename(source, path.extname(source)).toLowerCase();
}

function guessVoiceTitle(voicePath, japanese) {
  const value = cleanText(voicePath);
  const suffixMatch = value.match(/_(\d+)$/);
  const number = suffixMatch ? suffixMatch[1] : "";
  const rules = [
    [/Interaction/i, japanese ? "好感度" : "互动语音"],
    [/_Main_/i, japanese ? "待機ボイス" : "待机语音"],
    [/_Get_/i, japanese ? "メンバー募集" : "成员招募"],
    [/_Game_/i, japanese ? "キャラクターボイス" : "角色语音"],
    [/_In_/i, japanese ? "ログインボイス" : "登录语音"],
    [/_Stage_/i, japanese ? "立ち絵獲得" : "立绘获得"],
    [/_Star_/i, japanese ? "覚醒" : "升星语音"],
    [/_Win_/i, japanese ? "勝利" : "胜利语音"],
    [/_Fail_/i, japanese ? "敗北" : "失败语音"],
    [/_Battle_/i, japanese ? "戦闘" : "战斗语音"],
    [/_Hit_/i, japanese ? "被弾" : "受击语音"],
    [/_Home_/i, japanese ? "ホーム" : "主页语音"],
    [/_Go_/i, japanese ? "出撃" : "出击语音"],
  ];
  for (const rule of rules) {
    if (rule[0].test(value)) return rule[1] + number;
  }
  return value || (japanese ? "ボイス" : "语音");
}

function normalizeVoice(raw, fallbackSort) {
  const detail = raw && raw.detail ? raw.detail : raw || {};
  const voicePath = cleanText(detail.voicePath);
  if (!voicePath) return null;
  return {
    name: cleanText(detail.name) || guessVoiceTitle(voicePath, true),
    nameCn: cleanText(detail.nameCn) || guessVoiceTitle(voicePath, false),
    voicePath,
    audioFile: cleanText(detail.audioFile) || voicePath + ".wav",
    text: cleanText(detail.text),
    textCn: cleanText(detail.textCn),
    sort:
      Number.isFinite(detail.sort) && detail.sort !== 0
        ? detail.sort
        : fallbackSort,
    bubbleX: Number(detail.bubbleX) || 0,
    bubbleY: Number(detail.bubbleY) || 0,
  };
}

function getVoicePrefix(voicePath) {
  return cleanText(voicePath).replace(
    /_(Main|Interaction|Battle|Fail|Game|Get|Go|Home|In|Stage|Star|Win).*$/i,
    ""
  );
}

function listAudioFiles(directory) {
  if (!fs.existsSync(directory)) return [];
  return fs
    .readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isFile() && /\.(wav|ogg|mp3|m4a|aac)$/i.test(entry.name))
    .map((entry) => entry.name);
}

function audioMatchesVoicePrefix(stem, prefix) {
  if (!stem.startsWith(prefix + "_")) return false;
  const category = stem.slice(prefix.length + 1);
  return /^(Main|Interaction|Battle|Fail|Game|Get|Go|Home|In|Stage|Star|Win)_/i.test(
    category
  );
}

const gallery = readJson(galleryPath);
const sceneVoices = readJson(sceneVoicePath);
const source = readJson(sourcePath);
const sourceCards = source.cards || {};
const sceneVoicesByCard = new Map();
const availableAudioFiles = listAudioFiles(voiceDirectory);

for (const entry of sceneVoices.entries || []) {
  const cardId = String(entry.cardId || "");
  if (!cardId) continue;
  if (!sceneVoicesByCard.has(cardId)) sceneVoicesByCard.set(cardId, []);
  sceneVoicesByCard.get(cardId).push(...(entry.lines || []));
}

const characters = (gallery.characters || []).map((galleryCharacter) => {
  const cardId = String(galleryCharacter.cardId);
  const card = sourceCards[cardId] || {};
  const profile = card.profile || {};
  const stories = Array.isArray(card.stories) ? card.stories : [];
  const biographySource =
    (stories[1] && stories[1].detail) ||
    (stories[0] && stories[0].detail) ||
    {};
  const voicesByKey = new Map();

  for (const rawVoice of card.voiceLines || []) {
    const voice = normalizeVoice(rawVoice, voicesByKey.size + 1);
    if (voice) voicesByKey.set(voiceKey(voice), voice);
  }
  for (const rawVoice of sceneVoicesByCard.get(cardId) || []) {
    const voice = normalizeVoice(rawVoice, voicesByKey.size + 1);
    if (!voice) continue;
    const key = voiceKey(voice);
    const existing = voicesByKey.get(key);
    if (!existing) {
      voicesByKey.set(key, voice);
      continue;
    }
    if (!existing.audioFile) existing.audioFile = voice.audioFile;
    if (!existing.text) existing.text = voice.text;
    if (!existing.textCn) existing.textCn = voice.textCn;
    if (!existing.bubbleX) existing.bubbleX = voice.bubbleX;
    if (!existing.bubbleY) existing.bubbleY = voice.bubbleY;
  }

  const prefixes = new Set(
    Array.from(voicesByKey.values())
      .map((voice) => getVoicePrefix(voice.voicePath).toLowerCase())
      .filter(Boolean)
  );
  for (const audioFile of availableAudioFiles) {
    const stem = path.basename(audioFile, path.extname(audioFile));
    const lowerStem = stem.toLowerCase();
    const belongsToCharacter = Array.from(prefixes).some(
      (prefix) => audioMatchesVoicePrefix(lowerStem, prefix)
    );
    if (!belongsToCharacter || voicesByKey.has(lowerStem)) continue;
    const voice = normalizeVoice(
      {
        voicePath: stem,
        audioFile,
        text: "（字幕データなし）",
        textCn: "（暂无字幕资料）",
      },
      10000 + voicesByKey.size
    );
    voicesByKey.set(lowerStem, voice);
  }

  const voices = Array.from(voicesByKey.values()).sort((left, right) => {
    if (left.sort !== right.sort) return left.sort - right.sort;
    return left.name.localeCompare(right.name, "ja");
  });
  const birthday = profile.birthday || {};
  const biography = cleanText(biographySource.text);
  const biographyCn = cleanText(biographySource.textCn);

  return {
    cardId,
    cvName: cleanText(card.cvName),
    cvNameCn: cleanText(card.cvNameCn),
    birthdayMonth: Number(birthday.month) || 0,
    birthdayDay: Number(birthday.day) || 0,
    introduction: cleanText(card.lotteryWord) || biography,
    introductionCn: cleanText(card.lotteryWordCn) || biographyCn,
    profile: cleanText(profile.remark),
    profileCn: cleanText(profile.remarkCn),
    biography,
    biographyCn,
    voices,
  };
});

const result = {
  version: 1,
  generatedAt: new Date().toISOString(),
  characters,
};

fs.writeFileSync(outputPath, JSON.stringify(result, null, 2) + "\n", "utf8");
console.log(
  `Generated ${path.relative(projectRoot, outputPath)}: ` +
    `${characters.length} characters, ` +
    `${characters.reduce((sum, item) => sum + item.voices.length, 0)} voices`
);
