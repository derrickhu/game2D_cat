/**
 * 一裙又一裙后端（CloudBase 云函数 + HTTP 访问服务），照 blackrosa-api。
 *
 * 路由（POST JSON）：
 *   /login           wx code2session / anon，签发 JWT
 *   /rank/submit     上报已通关关数（只升不降），顺带更新昵称头像
 *   /rank/list       通关榜前 N 名 + 自己的名次
 *   /gameclub/daily  解密 wx.getGameClubData，回当天游戏圈发帖数
 *   /health          健康检查
 *
 * 环境变量：
 *   GAME_KEY=dresssort
 *   DRESSSORT_JWT_SECRET                         必填
 *   DRESSSORT_WX_APPID / DRESSSORT_WX_SECRET     微信 code2session
 *   DRESSSORT_TOKEN_TTL_SEC                      可选
 *   DRESSSORT_RANK_MAX_CLEARED（默认 500）/ DRESSSORT_RANK_BLOCKED_UIDS（逗号分隔）   可选
 *
 * 集合：dresssort_rankings（userId 唯一索引；cleared desc + reachedAt asc）
 *       dresssort_wxSessions（userId 唯一索引）
 */

const { handleLogin } = require('./lib/auth');
const { handleSubmit, handleList } = require('./lib/rank');
const { handleDailyPost } = require('./lib/game-club');
const { respond, parseEvent, preflight } = require('./lib/http');

const ROUTES = {
  'GET /health': async () => ({ ok: true, ts: Date.now() }),
  'POST /health': async () => ({ ok: true, ts: Date.now() }),
  'POST /login': handleLogin,
  'POST /rank/submit': handleSubmit,
  'POST /rank/list': handleList,
  'POST /gameclub/daily': handleDailyPost,
};

exports.main = async (event, context) => {
  try {
    if (event && event.httpMethod === 'OPTIONS') {
      return preflight();
    }

    const req = parseEvent(event);
    const key = `${req.method} ${req.path}`;
    const handler = ROUTES[key];
    if (!handler) {
      return respond(404, { ok: false, code: 'NOT_FOUND', error: `no route: ${key}` });
    }

    const result = await handler(req, context);
    if (result && typeof result === 'object' && 'statusCode' in result) {
      return result;
    }
    return respond(200, { ok: true, data: result });
  } catch (error) {
    const code = error && error.code ? error.code : 'INTERNAL';
    const status = error && error.status ? error.status : 500;
    const message = (error && error.message) || String(error);
    console.error('[dresssort-api] error:', code, message, error && error.stack);
    const out = { ok: false, code, error: message };
    if (error && error.data !== undefined) {
      out.data = error.data;
    }
    return respond(status, out);
  }
};
