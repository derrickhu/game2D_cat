/**
 * 游戏圈每日发帖任务。
 *
 * 客户端 wx.getGameClubData 拿到的是用 session_key 加密的数据，
 * 登录时把 session_key 存进 wxSessions，这里解密后只回当天发帖数。
 * 奖励在客户端发（跟着存档走），服务端只负责证明「今天确实发过」。
 *
 * 集合：wxSessions（userId 唯一索引）
 */

const crypto = require('crypto');
const { httpError } = require('./http');
const { getDb } = require('./db');
const { getCollectionName } = require('./config');

// wx.getGameClubData 的 dataType：6 = 当天在本游戏圈发表的动态数。
const DAILY_POST_TYPE = 6;

function getSessionCollection() {
  return getDb().collection(getCollectionName('wxSessions'));
}

async function upsertWxSession(userId, sessionKey) {
  if (!sessionKey) return;
  const col = getSessionCollection();
  const now = Date.now();
  const res = await col.where({ userId }).limit(1).get();
  const existing = (res && Array.isArray(res.data) && res.data[0]) || null;
  if (existing && existing._id) {
    await col.doc(existing._id).update({ sessionKey, updatedAt: now });
    return;
  }
  await col.add({ userId, sessionKey, updatedAt: now });
}

async function readWxSessionKey(userId) {
  const res = await getSessionCollection().where({ userId }).limit(1).get();
  const doc = (res && Array.isArray(res.data) && res.data[0]) || null;
  return doc && typeof doc.sessionKey === 'string' ? doc.sessionKey : '';
}

function decrypt(sessionKey, encryptedData, iv) {
  try {
    const decipher = crypto.createDecipheriv(
      'aes-128-cbc',
      Buffer.from(sessionKey, 'base64'),
      Buffer.from(iv, 'base64'),
    );
    decipher.setAutoPadding(true);
    const text = Buffer.concat([
      decipher.update(Buffer.from(encryptedData, 'base64')),
      decipher.final(),
    ]).toString('utf8');
    return JSON.parse(text);
  } catch (error) {
    throw httpError(400, 'DECRYPT_FAIL', (error && error.message) || '游戏圈数据解密失败');
  }
}

function typeOf(entry) {
  const t = entry && entry.dataType;
  return typeof t === 'number' ? t : Number((t && t.type) || 0);
}

async function handleDailyPost(req) {
  // auth.js 登录时会反过来调这里存 session，所以懒加载避免循环依赖。
  const { requireUser } = require('./auth');
  const { userId, platform } = requireUser(req);
  if (platform !== 'wx') throw httpError(400, 'NOT_WX', '游戏圈只在微信里可用');
  const body = req.body || {};
  const encryptedData = String(body.encryptedData || '').trim();
  const iv = String(body.iv || '').trim();
  if (!encryptedData || !iv) throw httpError(400, 'BAD_GAME_CLUB_PAYLOAD', 'encryptedData / iv 缺失');
  const sessionKey = await readWxSessionKey(userId);
  if (!sessionKey) throw httpError(400, 'NO_WX_SESSION', '微信 session 未就绪，请重新登录');
  const data = decrypt(sessionKey, encryptedData, iv);
  const list = Array.isArray(data && data.dataList) ? data.dataList : [];
  const item = list.find((entry) => typeOf(entry) === DAILY_POST_TYPE);
  const n = Number(item && item.value);
  return { postCount: Number.isFinite(n) && n > 0 ? Math.floor(n) : 0 };
}

module.exports = {
  DAILY_POST_TYPE,
  upsertWxSession,
  handleDailyPost,
};
