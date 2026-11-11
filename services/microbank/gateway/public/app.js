let authToken = null;
let currentUser = null;

function api(method, path, body) {
  const opts = {
    method,
    headers: { 'Content-Type': 'application/json' },
  };
  if (authToken) opts.headers['X-Auth-Token'] = authToken;
  if (body) opts.body = JSON.stringify(body);
  return fetch(path, opts).then(async r => {
    const data = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(data.error || `HTTP ${r.status}`);
    return data;
  });
}

function toast(msg, type) {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.className = `toast ${type}`;
  el.classList.remove('hidden');
  setTimeout(() => el.classList.add('hidden'), 3000);
}

function showView(name) {
  document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
  const target = document.getElementById(`view-${name}`);
  if (target) target.classList.add('active');
  if (name === 'dashboard') loadDashboard();
  if (name === 'history') loadHistory();
  if (name === 'settings') loadSettings();
}

function switchTab(tab) {
  document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
  document.getElementById('form-login').classList.add('hidden');
  document.getElementById('form-register').classList.add('hidden');
  document.getElementById('auth-error').classList.add('hidden');
  if (tab === 'login') {
    document.getElementById('form-login').classList.remove('hidden');
    document.querySelectorAll('.tab')[0].classList.add('active');
  } else {
    document.getElementById('form-register').classList.remove('hidden');
    document.querySelectorAll('.tab')[1].classList.add('active');
  }
}

function sha256(str) {
  function rr(n,x){return(x>>>n)|(x<<(32-n))}
  function S(x,y){var l=(x&0xFFFF)+(y&0xFFFF),h=(x>>>16)+(y>>>16)+(l>>>16);return(h<<16)|(l&0xFFFF)}
  var K=[0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
    0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
    0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
    0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
    0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
    0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
    0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
    0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2];
  var H=[0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19];
  var b=[];for(var i=0;i<str.length;i++){var c=str.charCodeAt(i);if(c<128)b.push(c);else if(c<2048){b.push(192|(c>>6));b.push(128|(c&63));}else{b.push(224|(c>>12));b.push(128|((c>>6)&63));b.push(128|(c&63));}}
  var l=b.length*8;b.push(0x80);while(b.length%64!==56)b.push(0);
  b.push(0,0,0,0);b.push((l>>>24)&0xFF,(l>>>16)&0xFF,(l>>>8)&0xFF,l&0xFF);
  for(var o=0;o<b.length;o+=64){
    var w=[];for(i=0;i<16;i++)w[i]=(b[o+i*4]<<24)|(b[o+i*4+1]<<16)|(b[o+i*4+2]<<8)|b[o+i*4+3];
    for(i=16;i<64;i++){var s0=rr(7,w[i-15])^rr(18,w[i-15])^(w[i-15]>>>3),s1=rr(17,w[i-2])^rr(19,w[i-2])^(w[i-2]>>>10);w[i]=S(S(S(w[i-16],s0),w[i-7]),s1);}
    var a=H[0],bb=H[1],c2=H[2],d=H[3],e=H[4],f=H[5],g=H[6],h=H[7];
    for(i=0;i<64;i++){var S1=rr(6,e)^rr(11,e)^rr(25,e),ch=(e&f)^(~e&g),t1=S(S(S(S(h,S1),ch),K[i]),w[i]);var S0=rr(2,a)^rr(13,a)^rr(22,a),mj=(a&bb)^(a&c2)^(bb&c2),t2=S(S0,mj);h=g;g=f;f=e;e=S(d,t1);d=c2;c2=bb;bb=a;a=S(t1,t2);}
    H[0]=S(H[0],a);H[1]=S(H[1],bb);H[2]=S(H[2],c2);H[3]=S(H[3],d);H[4]=S(H[4],e);H[5]=S(H[5],f);H[6]=S(H[6],g);H[7]=S(H[7],h);
  }
  return H.map(function(v){return('00000000'+((v<0?v+4294967296:v).toString(16))).slice(-8)}).join('');
}

async function computeToken(username, password) {
  var hashHex;
  if (typeof crypto !== 'undefined' && crypto.subtle) {
    var enc = new TextEncoder();
    var hashBuf = await crypto.subtle.digest('SHA-256', enc.encode(password));
    hashHex = Array.from(new Uint8Array(hashBuf)).map(function(b){return b.toString(16).padStart(2,'0')}).join('');
  } else {
    hashHex = sha256(password);
  }
  return btoa(username + ':' + hashHex);
}

async function handleLogin(e) {
  e.preventDefault();
  const user = document.getElementById('login-user').value;
  const pass = document.getElementById('login-pass').value;
  try {
    await api('POST', '/api/login', { username: user, password: pass });
    authToken = await computeToken(user, pass);
    currentUser = { username: user };
    enterApp();
  } catch (err) {
    showAuthError(err.message);
  }
  return false;
}

async function handleRegister(e) {
  e.preventDefault();
  const user = document.getElementById('reg-user').value;
  const pass = document.getElementById('reg-pass').value;
  const nick = document.getElementById('reg-nick').value;
  try {
    const data = await api('POST', '/api/register', { username: user, password: pass, nickname: nick || user });
    authToken = await computeToken(user, pass);
    currentUser = { username: user, account_id: data.account_id };
    enterApp();
    toast('Account created!', 'success');
  } catch (err) {
    showAuthError(err.message);
  }
  return false;
}

function showAuthError(msg) {
  const el = document.getElementById('auth-error');
  el.textContent = msg;
  el.classList.remove('hidden');
}

function enterApp() {
  document.getElementById('navbar').classList.add('visible');
  showView('dashboard');
}

function logout() {
  api('POST', '/api/logout').catch(() => {});
  authToken = null;
  currentUser = null;
  document.getElementById('navbar').classList.remove('visible');
  document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
  document.getElementById('view-auth').classList.add('active');
  document.getElementById('login-user').value = '';
  document.getElementById('login-pass').value = '';
}

async function loadDashboard() {
  try {
    const acct = await api('GET', '/api/account');
    currentUser = acct;
    document.getElementById('balance-display').textContent = `$${Number(acct.balance).toLocaleString()}`;
    document.getElementById('account-id-display').textContent = acct.account_id;

    const hist = await api('GET', '/api/transactions');
    const tbody = document.querySelector('#recent-txns tbody');
    tbody.innerHTML = '';
    (hist.transactions || []).slice(0, 8).forEach(txn => {
      const tr = document.createElement('tr');
      const ts = new Date(parseInt(txn.timestamp)).toLocaleString();
      tr.innerHTML = `
        <td><span class="type-${txn.type}">${txn.type}</span></td>
        <td>$${Number(txn.amount).toLocaleString()}</td>
        <td>${esc(txn.reference || '-')}</td>
        <td>${ts}</td>
      `;
      tbody.appendChild(tr);
    });
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function quickDeposit(e) {
  e.preventDefault();
  const amount = parseInt(document.getElementById('dep-amount').value);
  const memo = document.getElementById('dep-memo').value;
  try {
    await api('POST', '/api/deposit', { amount, memo });
    toast(`Deposited $${amount}`, 'success');
    document.getElementById('dep-amount').value = '';
    document.getElementById('dep-memo').value = '';
    loadDashboard();
  } catch (err) { toast(err.message, 'error'); }
  return false;
}

async function quickWithdraw(e) {
  e.preventDefault();
  const amount = parseInt(document.getElementById('wd-amount').value);
  const memo = document.getElementById('wd-memo').value;
  try {
    await api('POST', '/api/withdraw', { amount, memo });
    toast(`Withdrew $${amount}`, 'success');
    document.getElementById('wd-amount').value = '';
    document.getElementById('wd-memo').value = '';
    loadDashboard();
  } catch (err) { toast(err.message, 'error'); }
  return false;
}

async function handleTransfer(e) {
  e.preventDefault();
  const to = document.getElementById('xfer-to').value;
  const amount = parseInt(document.getElementById('xfer-amount').value);
  const ref = document.getElementById('xfer-ref').value;
  const memo = document.getElementById('xfer-memo').value;
  const el = document.getElementById('xfer-result');
  try {
    const data = await api('POST', '/api/transfer', { to_account: to, amount, reference: ref, memo });
    el.className = 'result-msg success';
    el.textContent = `Transfer complete — TXN: ${data.transaction_id}`;
    toast(`Sent $${amount}`, 'success');
    document.getElementById('xfer-amount').value = '';
    document.getElementById('xfer-memo').value = '';
  } catch (err) {
    el.className = 'result-msg error';
    el.textContent = err.message;
  }
  return false;
}

function addBatchRow() {
  const container = document.getElementById('batch-rows');
  const row = document.createElement('div');
  row.className = 'batch-row';
  row.innerHTML = `
    <input type="text" placeholder="Recipient Account ID" class="batch-to">
    <input type="number" placeholder="Amount" class="batch-amt" min="1">
    <input type="text" placeholder="Memo" class="batch-memo">
  `;
  container.appendChild(row);
}

async function submitBatch() {
  const rows = document.querySelectorAll('.batch-row');
  const transfers = [];
  rows.forEach(row => {
    const to = row.querySelector('.batch-to').value;
    const amt = parseInt(row.querySelector('.batch-amt').value);
    const memo = row.querySelector('.batch-memo').value;
    if (to && amt > 0) transfers.push({ to_account: to, amount: amt, memo });
  });
  if (transfers.length === 0) return toast('Add at least one transfer', 'error');
  const el = document.getElementById('batch-result');
  try {
    const data = await api('POST', '/api/batch-transfer', { transfers });
    el.className = 'result-msg success';
    el.textContent = `Batch complete — ${data.count} transfers processed`;
    toast('Batch sent!', 'success');
  } catch (err) {
    el.className = 'result-msg error';
    el.textContent = err.message;
  }
}

async function loadHistory() {
  try {
    const hist = await api('GET', '/api/transactions');
    const tbody = document.querySelector('#history-table tbody');
    tbody.innerHTML = '';
    (hist.transactions || []).forEach(txn => {
      const tr = document.createElement('tr');
      const ts = new Date(parseInt(txn.timestamp)).toLocaleString();
      tr.innerHTML = `
        <td style="font-family:monospace;font-size:11px">${esc(txn.transaction_id.substring(0, 8))}…</td>
        <td><span class="type-${txn.type}">${txn.type}</span></td>
        <td>$${Number(txn.amount).toLocaleString()}</td>
        <td style="font-family:monospace;font-size:11px">${esc((txn.to_account || txn.account_id || '-').substring(0, 8))}…</td>
        <td>${esc(txn.reference || '-')}</td>
        <td>${esc((txn.memo || '-').substring(0, 30))}</td>
        <td>${ts}</td>
      `;
      tbody.appendChild(tr);
    });
  } catch (err) { toast(err.message, 'error'); }
}

async function loadSettings() {
  try {
    const acct = await api('GET', '/api/account');
    document.getElementById('set-account-id').value = acct.account_id;
    document.getElementById('set-username').value = acct.username;
    document.getElementById('set-nickname').value = acct.nickname || '';
  } catch (err) { toast(err.message, 'error'); }
}

async function updateNickname(e) {
  e.preventDefault();
  const nick = document.getElementById('set-nickname').value;
  const el = document.getElementById('settings-result');
  try {
    await api('PUT', '/api/account/nickname', { nickname: nick });
    el.className = 'result-msg success';
    el.textContent = 'Nickname updated';
    toast('Nickname updated', 'success');
  } catch (err) {
    el.className = 'result-msg error';
    el.textContent = err.message;
  }
  return false;
}

async function handleFee(e) {
  e.preventDefault();
  const amount = parseInt(document.getElementById('fee-amount').value);
  const desc = document.getElementById('fee-desc').value;
  try {
    await api('POST', '/api/fee', { amount, description: desc });
    toast(`Fee of $${amount} charged`, 'info');
  } catch (err) { toast(err.message, 'error'); }
  return false;
}

function esc(s) {
  const d = document.createElement('div');
  d.textContent = s;
  return d.innerHTML;
}
