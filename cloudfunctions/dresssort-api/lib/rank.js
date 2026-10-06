/**
 * 通关排行榜，只有一张榜：已通关关数。
 *
 * 数据模型（collection: rankings，每人一条）：
 *   {
 *     _id,
 *     userId, platform,
 *     cleared: number,        // 已通关关数
 *     reachedAt: number (ms), // 第一次到达这个关数的时间，同分先到者排前面
 *     displayName, avatarUrl,
 *     createdAt, updatedAt
 *   }
 *
 * 索引：userId 唯一；cleared desc + reachedAt asc。
 */

const crypto = require('crypto');
const { requireUser } = require('./auth');
const { getDb, getRankingCollection } = require('./db');
const { httpError } = require('./http');
const { readEnvPrefer, gameKeyUpper } = require('./config');

const DEFAULT_MAX_CLEARED = 500;
const DEFAULT_LIST_LIMIT = 50;
const MAX_LIST_LIMIT = 100;

function envInt(suffix, fallback) {
  const n = Number(readEnvPrefer(`${gameKeyUpper()}_${suffix}`));
  return Number.isFinite(n) && n > 0 ? Math.floor(n) : fallback;
}

// GM / 测试账号：逗号分隔的 userId，不写榜，免得一键全通关的进度挂在榜首。
function blockedUsers() {
  return new Set(
    readEnvPrefer(`${gameKeyUpper()}_RANK_BLOCKED_UIDS`)
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean),
  );
}

function normalizeCleared(value) {
  const n = Number(value);
  if (!Number.isFinite(n) || n < 0) {
    throw httpError(400, 'BAD_CLEARED', 'cleared 必须为非负整数');
  }
  const cleared = Math.floor(n);
  const max = envInt('RANK_MAX_CLEARED', DEFAULT_MAX_CLEARED);
  if (cleared > max) {
    throw httpError(400, 'CLEARED_TOO_HIGH', `cleared 超出上限: ${cleared} > ${max}`);
  }
  return cleared;
}

function normalizeLimit(value) {
  const n = Number(value);
  if (!Number.isFinite(n) || n <= 0) return DEFAULT_LIST_LIMIT;
  return Math.min(Math.floor(n), MAX_LIST_LIMIT);
}

function displayNameForUser(userId) {
  const digest = crypto.createHash('sha1').update(String(userId)).digest('hex');
  const suffix = String(parseInt(digest.slice(0, 8), 16) % 10000).padStart(4, '0');
  return `裙友${suffix}`;
}

function sanitizeDisplayName(value, fallback) {
  const text = String(value || '').trim().replace(/[\r\n\t]/g, ' ').slice(0, 16);
  // 微信拒绝授权时会给一个统一的「微信用户」，当没给。
  if (!text || text === '微信用户') return fallback;
  return text;
}

function sanitizeAvatarUrl(value, fallback) {
  const text = String(value || '').trim();
  if (!text || text.length > 1024 || !/^https?:\/\//i.test(text)) return fallback || '';
  return text;
}

function publicRecord(doc, userId, rank) {
  if (!doc) return null;
  return {
    rank: rank || 0,
    cleared: Number(doc.cleared) || 0,
    displayName: doc.displayName || displayNameForUser(doc.userId || ''),
    avatarUrl: doc.avatarUrl || '',
    isMe: !!userId && doc.userId === userId,
  };
}

// 编辑器用 anon 身份联调。微信玩家看不到 anon 记录，编辑器里能看到全部，
// 这样本地调界面有数据可看，线上榜也不会混进测试号。
function audience(platform) {
  return platform === 'anon' ? {} : { platform: getDb().command.neq('anon') };
}

async function findMine(col, userId, platform) {
  const res = await col.where({ userId }).limit(1).get();
  const doc = (res && Array.isArray(res.data) && res.data[0]) || null;
  if (!doc || !(Number(doc.cleared) > 0)) return null;
  const _ = getDb().command;
  const cleared = Number(doc.cleared);
  const reachedAt = Number(doc.reachedAt) || 0;
  const scope = audience(platform);
  const [ahead, tied] = await Promise.all([
    col.where({ ...scope, cleared: _.gt(cleared) }).count(),
    col.where({ ...scope, cleared, reachedAt: _.lt(reachedAt) }).count(),
  ]);
  const rank = (Number(ahead && ahead.total) || 0) + (Number(tied && tied.total) || 0) + 1;
  return { rank, doc };
}

async function handleSubmit(req) {
  const { userId, platform } = requireUser(req);
  const body = req.body || {};
  const cleared = normalizeCleared(body.cleared);
  if (blockedUsers().has(userId)) {
    return { updated: false, reason: 'BLOCKED', record: null };
  }

  const col = getRankingCollection();
  const res = await col.where({ userId }).limit(1).get();
  const existing = (res && Array.isArray(res.data) && res.data[0]) || null;
  const now = Date.now();
  const displayName = sanitizeDisplayName(
    body.displayName,
    (existing && existing.displayName) || displayNameForUser(userId),
  );
  const avatarUrl = sanitizeAvatarUrl(body.avatarUrl, (existing && existing.avatarUrl) || '');

  if (!existing) {
    if (cleared <= 0) return { updated: false, reason: 'NO_PROGRESS', record: null };
    const docData = {
      userId,
      platform,
      cleared,
      reachedAt: now,
      displayName,
      avatarUrl,
      createdAt: now,
      updatedAt: now,
    };
    await col.add(docData);
    return { updated: true, mode: 'insert', record: publicRecord(docData, userId, 0) };
  }

  const prev = Number(existing.cleared) || 0;
  const patch = {};
  // 只升不降：清档重玩、换设备拿到旧档都不会把榜上的成绩拉下来。
  if (cleared > prev) {
    patch.cleared = cleared;
    patch.reachedAt = now;
  }
  if (existing.displayName !== displayName) patch.displayName = displayName;
  if ((existing.avatarUrl || '') !== avatarUrl) patch.avatarUrl = avatarUrl;
  if (Object.keys(patch).length === 0) {
    return { updated: false, reason: 'NOT_BETTER', record: publicRecord(existing, userId, 0) };
  }
  patch.updatedAt = now;
  await col.doc(existing._id).update(patch);
  return {
    updated: true,
    mode: patch.cleared !== undefined ? 'update' : 'profile_update',
    record: publicRecord({ ...existing, ...patch }, userId, 0),
  };
}

async function handleList(req) {
  const { userId, platform } = requireUser(req);
  const limit = normalizeLimit((req.body || {}).limit);
  const col = getRankingCollection();
  const res = await col
    .where({ ...audience(platform), cleared: getDb().command.gt(0) })
    .orderBy('cleared', 'desc')
    .orderBy('reachedAt', 'asc')
    .limit(limit)
    .get();
  const rows = res && Array.isArray(res.data) ? res.data : [];
  const list = rows.map((doc, i) => publicRecord(doc, userId, i + 1));
  const mine = await findMine(col, userId, platform);
  return { list, mine: mine ? publicRecord(mine.doc, userId, mine.rank) : null };
}

module.exports = {
  handleSubmit,
  handleList,
};
