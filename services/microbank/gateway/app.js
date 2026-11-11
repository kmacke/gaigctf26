const express = require('express');
const session = require('express-session');
const RedisStore = require('connect-redis').default;
const { createClient } = require('redis');
const cookieParser = require('cookie-parser');
const bodyParser = require('body-parser');
const axios = require('axios');
const crypto = require('crypto');
const { v4: uuidv4 } = require('uuid');
const ejs = require('ejs');
const fs = require('fs');
const path = require('path');

const sanitize = require('./middleware/sanitize');

const app = express();
const PORT = process.env.PORT || 3000;
const REDIS_URL = process.env.REDIS_URL || 'redis://ledger:6379';
const ENGINE_URL = process.env.ENGINE_URL || 'http://engine:8080';

const redisClient = createClient({ url: REDIS_URL });
redisClient.on('error', (err) => console.error('Redis error:', err));

(async () => {
  await redisClient.connect();
  console.log('Connected to Redis');
})();

app.set('view engine', 'ejs');
app.set('views', path.join(__dirname, 'views'));
app.use(express.static(path.join(__dirname, 'public')));
app.use(cookieParser());
app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true }));

app.use(session({
  store: new RedisStore({ client: redisClient }),
  secret: process.env.SESSION_SECRET || 'microbank-s3cret-k3y-2026',
  resave: false,
  saveUninitialized: false,
  cookie: { maxAge: 3600000 }
}));

app.use(sanitize);

function safeHeader(value) {
  if (!value) return '';
  return String(value).replace(/[^\x20-\x7E]/g, (ch) => encodeURIComponent(ch));
}

app.get('/api/health', (req, res) => {
  res.json({ status: 'ok', service: 'microbank-gateway' });
});

app.post('/api/register', async (req, res) => {
  try {
    const { username, password, nickname } = req.body;
    if (!username || !password) {
      return res.status(400).json({ error: 'username and password required' });
    }

    const accountId = uuidv4();
    const passwordHash = crypto.createHash('sha256').update(password).digest('hex');

    const userKey = `user:${username}`;
    const exists = await redisClient.exists(userKey);
    if (exists) {
      return res.status(409).json({ error: 'username already exists' });
    }

    await redisClient.hSet(userKey, {
      account_id: accountId,
      username: username,
      password_hash: passwordHash,
      nickname: nickname || username,
      created_at: Date.now().toString()
    });

    await redisClient.set(`account:${accountId}`, username);

    await axios.post(`${ENGINE_URL}/internal/init-account`, {
      account_id: accountId
    });

    req.session.user = { username, account_id: accountId, nickname: nickname || username };
    res.status(201).json({
      account_id: accountId,
      username: username,
      message: 'Account created successfully'
    });
  } catch (err) {
    console.error('register error:', err.message);
    res.status(500).json({ error: 'registration failed' });
  }
});

app.post('/api/login', async (req, res) => {
  try {
    const { username, password } = req.body;
    if (!username || !password) {
      return res.status(400).json({ error: 'username and password required' });
    }

    const userKey = `user:${username}`;
    const user = await redisClient.hGetAll(userKey);
    if (!user || !user.account_id) {
      return res.status(401).json({ error: 'invalid credentials' });
    }

    const passwordHash = crypto.createHash('sha256').update(password).digest('hex');
    if (user.password_hash !== passwordHash) {
      return res.status(401).json({ error: 'invalid credentials' });
    }

    req.session.user = {
      username: user.username,
      account_id: user.account_id,
      nickname: user.nickname
    };
    res.json({ account_id: user.account_id, message: 'Login successful' });
  } catch (err) {
    console.error('login error:', err.message);
    res.status(500).json({ error: 'login failed' });
  }
});

app.post('/api/logout', (req, res) => {
  req.session.destroy();
  res.json({ message: 'Logged out' });
});

function requireAuth(req, res, next) {
  const token = req.headers['x-auth-token'];
  if (token) {
    try {
      const decoded = Buffer.from(token, 'base64').toString();
      const [username, passwordHash] = decoded.split(':');
      redisClient.hGetAll(`user:${username}`).then(user => {
        if (user && user.password_hash === passwordHash) {
          req.user = { username: user.username, account_id: user.account_id, nickname: user.nickname };
          return next();
        }
        return res.status(401).json({ error: 'invalid token' });
      }).catch(() => res.status(401).json({ error: 'auth error' }));
      return;
    } catch {
      return res.status(401).json({ error: 'invalid token format' });
    }
  }
  if (req.session && req.session.user) {
    req.user = req.session.user;
    return next();
  }
  return res.status(401).json({ error: 'authentication required' });
}

app.get('/api/account', requireAuth, async (req, res) => {
  try {
    const balResp = await axios.get(`${ENGINE_URL}/internal/balance/${req.user.account_id}`);
    res.json({
      account_id: req.user.account_id,
      username: req.user.username,
      nickname: req.user.nickname,
      balance: balResp.data.balance
    });
  } catch (err) {
    console.error('account error:', err.message);
    res.status(500).json({ error: 'failed to fetch account' });
  }
});

app.put('/api/account/nickname', requireAuth, async (req, res) => {
  try {
    const { nickname } = req.body;
    if (!nickname) return res.status(400).json({ error: 'nickname required' });

    await redisClient.hSet(`user:${req.user.username}`, 'nickname', nickname);
    req.session.user.nickname = nickname;
    res.json({ message: 'Nickname updated', nickname });
  } catch (err) {
    res.status(500).json({ error: 'update failed' });
  }
});

app.post('/api/deposit', requireAuth, async (req, res) => {
  try {
    const { amount, memo } = req.body;
    if (!amount || amount <= 0) {
      return res.status(400).json({ error: 'invalid amount' });
    }

    const resp = await axios.post(`${ENGINE_URL}/internal/deposit`, {
      account_id: req.user.account_id,
      amount: amount,
      memo: memo || ''
    });

    const txn = resp.data;

    res.setHeader('X-Transaction-Id', txn.transaction_id);
    res.setHeader('X-Transaction-Ref', safeHeader(txn.reference || ''));
    res.setHeader('X-Account-Nickname', safeHeader(req.user.nickname));
    res.setHeader('X-Memo-Hash', txn.memo_hash || '');
    res.setHeader('X-Timestamp', new Date().toISOString());
    res.setHeader('X-Engine-Version', '1.0.0');
    res.setHeader('X-Request-Id', uuidv4());

    res.json(txn);
  } catch (err) {
    console.error('deposit error:', err.message);
    res.status(500).json({ error: 'deposit failed' });
  }
});

app.post('/api/withdraw', requireAuth, async (req, res) => {
  try {
    const { amount, memo } = req.body;
    if (!amount || amount <= 0) {
      return res.status(400).json({ error: 'invalid amount' });
    }

    const resp = await axios.post(`${ENGINE_URL}/internal/withdraw`, {
      account_id: req.user.account_id,
      amount: amount,
      memo: memo || ''
    });

    const txn = resp.data;
    res.setHeader('X-Transaction-Id', txn.transaction_id);
    res.setHeader('X-Transaction-Ref', safeHeader(txn.reference || ''));
    res.setHeader('X-Account-Nickname', safeHeader(req.user.nickname));
    res.setHeader('X-Memo-Hash', txn.memo_hash || '');
    res.setHeader('X-Timestamp', new Date().toISOString());
    res.setHeader('X-Engine-Version', '1.0.0');
    res.setHeader('X-Request-Id', uuidv4());

    res.json(txn);
  } catch (err) {
    console.error('withdraw error:', err.message);
    res.status(err.response?.status || 500).json({ error: err.response?.data?.error || 'withdraw failed' });
  }
});

app.post('/api/transfer', requireAuth, async (req, res) => {
  try {
    const { to_account, amount, reference, memo } = req.body;
    if (!to_account || !amount || amount <= 0) {
      return res.status(400).json({ error: 'to_account and valid amount required' });
    }

    const resp = await axios.post(`${ENGINE_URL}/internal/transfer`, {
      from_account: req.user.account_id,
      to_account: to_account,
      amount: amount,
      reference: reference || `TXN-${Date.now()}`,
      memo: memo || ''
    });

    const txn = resp.data;
    res.setHeader('X-Transaction-Id', txn.transaction_id);
    res.setHeader('X-Transaction-Ref', safeHeader(txn.reference || ''));
    res.setHeader('X-Account-Nickname', safeHeader(req.user.nickname));
    res.setHeader('X-Memo-Hash', txn.memo_hash || '');
    res.setHeader('X-Timestamp', new Date().toISOString());
    res.setHeader('X-Engine-Version', '1.0.0');
    res.setHeader('X-Request-Id', uuidv4());

    res.json(txn);
  } catch (err) {
    console.error('transfer error:', err.message);
    res.status(err.response?.status || 500).json({ error: err.response?.data?.error || 'transfer failed' });
  }
});

app.post('/api/batch-transfer', requireAuth, async (req, res) => {
  try {
    const { transfers } = req.body;
    if (!Array.isArray(transfers) || transfers.length === 0) {
      return res.status(400).json({ error: 'transfers array required' });
    }

    const resp = await axios.post(`${ENGINE_URL}/internal/batch-transfer`, {
      from_account: req.user.account_id,
      transfers: transfers
    });

    res.json(resp.data);
  } catch (err) {
    console.error('batch-transfer error:', err.message);
    res.status(err.response?.status || 500).json({ error: err.response?.data?.error || 'batch transfer failed' });
  }
});

app.get('/api/transactions', requireAuth, async (req, res) => {
  try {
    const resp = await axios.get(`${ENGINE_URL}/internal/transactions/${req.user.account_id}`);
    res.json(resp.data);
  } catch (err) {
    res.status(500).json({ error: 'failed to fetch transactions' });
  }
});

app.get('/api/transaction/:txn_id', requireAuth, async (req, res) => {
  try {
    const resp = await axios.get(`${ENGINE_URL}/internal/transaction/${req.params.txn_id}`, {
      params: { account_id: req.user.account_id }
    });
    const txn = resp.data;
    if (txn.account_id !== req.user.account_id && txn.to_account !== req.user.account_id) {
      delete txn.memo;
    }
    res.json(txn);
  } catch (err) {
    res.status(err.response?.status || 500).json({ error: 'transaction not found' });
  }
});

app.get('/api/memo/:txn_id', requireAuth, async (req, res) => {
  try {
    const resp = await axios.get(`${ENGINE_URL}/internal/transaction/${req.params.txn_id}`);
    res.json({ memo: resp.data.memo, transaction_id: req.params.txn_id });
  } catch (err) {
    res.status(404).json({ error: 'transaction not found' });
  }
});

app.get('/api/statement', requireAuth, async (req, res) => {
  try {
    const balResp = await axios.get(`${ENGINE_URL}/internal/balance/${req.user.account_id}`);
    const txnResp = await axios.get(`${ENGINE_URL}/internal/transactions/${req.user.account_id}`);

    const userData = await redisClient.hGetAll(`user:${req.user.username}`);
    let nickname = userData.nickname || req.user.username;

    nickname = nickname.normalize('NFKC');

    const templatePath = path.join(__dirname, 'views', 'statement.ejs');
    let templateSrc = fs.readFileSync(templatePath, 'utf8');
    templateSrc = templateSrc.replace('NICKNAME_PLACEHOLDER', nickname);

    const html = ejs.render(templateSrc, {
      account_id: req.user.account_id,
      balance: balResp.data.balance,
      transactions: txnResp.data.transactions || []
    });
    res.send(html);
  } catch (err) {
    console.error('statement error:', err.message);
    res.status(500).json({ error: 'failed to render statement' });
  }
});

app.get('/api/admin/audit', requireAuth, async (req, res) => {
  try {
    const balResp = await axios.get(`${ENGINE_URL}/internal/balance/${req.user.account_id}`);
    if (balResp.data.balance < 1000000000) {
      return res.status(403).json({ error: 'insufficient balance for audit access (need 1B+)' });
    }

    const resp = await axios.get(`${ENGINE_URL}/internal/audit`);
    res.json(resp.data);
  } catch (err) {
    console.error('audit error:', err.message);
    res.status(500).json({ error: 'audit failed' });
  }
});

app.post('/api/fee', requireAuth, async (req, res) => {
  try {
    const { amount, description } = req.body;
    const resp = await axios.post(`${ENGINE_URL}/internal/fee`, {
      account_id: req.user.account_id,
      amount: amount || 1,
      description: description || 'service fee'
    });
    res.json(resp.data);
  } catch (err) {
    res.status(err.response?.status || 500).json({ error: err.response?.data?.error || 'fee failed' });
  }
});

app.post('/api/refund', requireAuth, async (req, res) => {
  try {
    const { transaction_id, reason } = req.body;
    if (!transaction_id) return res.status(400).json({ error: 'transaction_id required' });

    const resp = await axios.post(`${ENGINE_URL}/internal/refund`, {
      account_id: req.user.account_id,
      transaction_id: transaction_id,
      reason: reason || 'customer request'
    });
    res.json(resp.data);
  } catch (err) {
    res.status(err.response?.status || 500).json({ error: err.response?.data?.error || 'refund failed' });
  }
});

app.listen(PORT, '0.0.0.0', () => {
  console.log(`MicroBank Gateway listening on port ${PORT}`);
});
