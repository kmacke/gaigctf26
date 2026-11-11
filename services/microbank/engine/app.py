
import hashlib
import json
import os
import time
import uuid

import numpy as np
import redis
from flask import Flask, jsonify, request

from lua_scripts import LuaScriptEngine

app = Flask(__name__)
REDIS_URL = os.environ.get('REDIS_URL', 'redis://ledger:6379')

r = redis.Redis.from_url(REDIS_URL, decode_responses=True)
lua = LuaScriptEngine(r)

import re
ID_RE = re.compile(r'^[0-9a-fA-F-]{36}$')

def _txn_id():
    return str(uuid.uuid4())

@app.route('/internal/health', methods=['GET'])
def health():
    try:
        r.ping()
        return jsonify({'status': 'ok', 'service': 'microbank-engine'})
    except Exception as e:
        return jsonify({'status': 'error', 'detail': str(e)}), 500

@app.route('/internal/init-account', methods=['POST'])
def init_account():
    data = request.json
    account_id = data['account_id']
    r.set(f'balance:{account_id}', '1000')
    r.lpush(f'txnlog:{account_id}', json.dumps({
        'transaction_id': _txn_id(),
        'type': 'init',
        'amount': 1000,
        'memo': 'Welcome bonus',
        'reference': 'INIT',
        'timestamp': str(int(time.time() * 1000))
    }))
    return jsonify({'status': 'initialized', 'balance': 1000})

@app.route('/internal/balance/<account_id>', methods=['GET'])
def get_balance(account_id):
    bal = r.get(f'balance:{account_id}')
    if bal is None:
        return jsonify({'error': 'account not found'}), 404
    return jsonify({'balance': int(bal), 'account_id': account_id})

@app.route('/internal/deposit', methods=['POST'])
def deposit():
    data = request.json
    account_id = data['account_id']
    amount = int(data['amount'])
    memo = data.get('memo', '')

    if amount <= 0:
        return jsonify({'error': 'invalid amount'}), 400

    txn_id = _txn_id()
    memo_hash = hashlib.sha256(memo.encode()).hexdigest()[:16]

    new_balance = lua.deposit(account_id, amount)

    txn = {
        'transaction_id': txn_id,
        'type': 'deposit',
        'account_id': account_id,
        'amount': amount,
        'memo': memo,
        'memo_hash': memo_hash,
        'reference': f'DEP-{txn_id[:8]}',
        'timestamp': str(int(time.time() * 1000))
    }
    r.lpush(f'txnlog:{account_id}', json.dumps(txn))
    r.lpush('txnlog:all', json.dumps(txn))

    r.set(f'txn:{txn_id}', json.dumps(txn))

    return jsonify({
        'transaction_id': txn_id,
        'type': 'deposit',
        'amount': amount,
        'new_balance': new_balance,
        'memo_hash': memo_hash,
        'reference': f'DEP-{txn_id[:8]}'
    })

@app.route('/internal/withdraw', methods=['POST'])
def withdraw():
    data = request.json
    account_id = data['account_id']
    amount = int(data['amount'])
    memo = data.get('memo', '')

    if amount <= 0:
        return jsonify({'error': 'invalid amount'}), 400

    txn_id = _txn_id()
    memo_hash = hashlib.sha256(memo.encode()).hexdigest()[:16]

    try:
        new_balance = lua.withdraw(account_id, amount)
    except ValueError as e:
        return jsonify({'error': str(e)}), 400

    txn = {
        'transaction_id': txn_id,
        'type': 'withdraw',
        'account_id': account_id,
        'amount': amount,
        'memo': memo,
        'memo_hash': memo_hash,
        'reference': f'WDR-{txn_id[:8]}',
        'timestamp': str(int(time.time() * 1000))
    }
    r.lpush(f'txnlog:{account_id}', json.dumps(txn))
    r.lpush('txnlog:all', json.dumps(txn))
    r.set(f'txn:{txn_id}', json.dumps(txn))

    return jsonify({
        'transaction_id': txn_id,
        'type': 'withdraw',
        'amount': amount,
        'new_balance': new_balance,
        'memo_hash': memo_hash,
        'reference': f'WDR-{txn_id[:8]}'
    })

@app.route('/internal/transfer', methods=['POST'])
def transfer():
    data = request.json
    from_account = data['from_account']
    to_account = data['to_account']
    amount = int(data['amount'])
    reference = data.get('reference', f'TXN-{_txn_id()[:8]}')
    memo = data.get('memo', '')

    if amount <= 0:
        return jsonify({'error': 'invalid amount'}), 400

    if from_account == to_account or not ID_RE.match(str(to_account)):
        return jsonify({'error': 'invalid destination account'}), 400

    if not r.exists(f'balance:{to_account}'):
        return jsonify({'error': 'destination account not found'}), 404

    txn_id = _txn_id()
    memo_hash = hashlib.sha256(memo.encode()).hexdigest()[:16]

    try:
        new_balance = lua.transfer(from_account, to_account, amount)
    except ValueError as e:
        return jsonify({'error': str(e)}), 400

    txn = {
        'transaction_id': txn_id,
        'type': 'transfer',
        'account_id': from_account,
        'to_account': to_account,
        'amount': amount,
        'memo': memo,
        'memo_hash': memo_hash,
        'reference': reference,
        'timestamp': str(int(time.time() * 1000))
    }
    r.lpush(f'txnlog:{from_account}', json.dumps(txn))
    r.lpush(f'txnlog:{to_account}', json.dumps(txn))
    r.lpush('txnlog:all', json.dumps(txn))
    r.set(f'txn:{txn_id}', json.dumps(txn))

    return jsonify({
        'transaction_id': txn_id,
        'type': 'transfer',
        'amount': amount,
        'new_balance': new_balance,
        'memo_hash': memo_hash,
        'reference': reference
    })

@app.route('/internal/batch-transfer', methods=['POST'])
def batch_transfer():
    data = request.json
    from_account = data['from_account']
    transfers = data['transfers']

    bal = r.get(f'balance:{from_account}')
    if bal is None:
        return jsonify({'error': 'account not found'}), 404

    current_balance = int(bal)

    try:
        amounts = [int(t['amount']) for t in transfers]
    except (KeyError, TypeError, ValueError):
        return jsonify({'error': 'invalid amount'}), 400
    if any(a <= 0 for a in amounts):
        return jsonify({'error': 'invalid amount'}), 400

    total = sum(amounts)
    if total > current_balance:
        return jsonify({'error': 'insufficient funds for batch'}), 400

    results = []
    for t in transfers:
        to_acct = t['to_account']
        amt = int(t['amount'])
        if to_acct == from_account or not ID_RE.match(str(to_acct)):
            results.append({'to_account': to_acct, 'status': 'skipped', 'error': 'not found'})
            continue
        if not r.exists(f'balance:{to_acct}'):
            results.append({'to_account': to_acct, 'status': 'skipped', 'error': 'not found'})
            continue
        try:
            new_bal = lua.transfer(from_account, to_acct, amt)
            txn_id = _txn_id()
            txn = {
                'transaction_id': txn_id,
                'type': 'batch-transfer',
                'account_id': from_account,
                'to_account': to_acct,
                'amount': amt,
                'memo': t.get('memo', ''),
                'reference': f'BATCH-{txn_id[:8]}',
                'timestamp': str(int(time.time() * 1000))
            }
            r.lpush(f'txnlog:{from_account}', json.dumps(txn))
            r.lpush(f'txnlog:{to_acct}', json.dumps(txn))
            r.lpush('txnlog:all', json.dumps(txn))
            r.set(f'txn:{txn_id}', json.dumps(txn))
            results.append({'to_account': to_acct, 'status': 'ok', 'amount': amt, 'transaction_id': txn_id})
        except ValueError as e:
            results.append({'to_account': to_acct, 'status': 'failed', 'error': str(e)})

    return jsonify({'results': results, 'count': len(results)})

@app.route('/internal/fee', methods=['POST'])
def fee():
    data = request.json
    account_id = data['account_id']
    amount = int(data.get('amount', 1))
    description = data.get('description', 'service fee')

    if amount <= 0:
        return jsonify({'error': 'invalid amount'}), 400

    txn_id = _txn_id()
    try:
        new_balance = lua.fee(account_id, amount)
    except ValueError as e:
        return jsonify({'error': str(e)}), 400

    txn = {
        'transaction_id': txn_id,
        'type': 'fee',
        'account_id': account_id,
        'amount': amount,
        'memo': description,
        'reference': f'FEE-{txn_id[:8]}',
        'timestamp': str(int(time.time() * 1000))
    }
    r.lpush(f'txnlog:{account_id}', json.dumps(txn))
    r.lpush('txnlog:all', json.dumps(txn))
    r.set(f'txn:{txn_id}', json.dumps(txn))

    return jsonify({
        'transaction_id': txn_id,
        'type': 'fee',
        'amount': amount,
        'new_balance': new_balance
    })

@app.route('/internal/refund', methods=['POST'])
def refund():
    data = request.json
    account_id = data['account_id']
    transaction_id = data['transaction_id']
    reason = data.get('reason', 'customer request')

    txn_raw = r.get(f'txn:{transaction_id}')
    if txn_raw is None:
        return jsonify({'error': 'transaction not found'}), 404

    orig_txn = json.loads(txn_raw)
    if orig_txn['account_id'] != account_id:
        return jsonify({'error': 'not your transaction'}), 403
    if orig_txn.get('type') in ('refund', 'init'):
        return jsonify({'error': 'transaction not refundable'}), 400
    if not r.set(f'refunded:{transaction_id}', '1', nx=True):
        return jsonify({'error': 'already refunded'}), 400     
        
    refund_amount = int(orig_txn['amount'])
    txn_id = _txn_id()

    new_balance = lua.refund(account_id, refund_amount)

    txn = {
        'transaction_id': txn_id,
        'type': 'refund',
        'account_id': account_id,
        'amount': refund_amount,
        'memo': f'Refund: {reason}',
        'reference': f'RFD-{transaction_id[:8]}',
        'original_txn': transaction_id,
        'timestamp': str(int(time.time() * 1000))
    }
    r.lpush(f'txnlog:{account_id}', json.dumps(txn))
    r.lpush('txnlog:all', json.dumps(txn))
    r.set(f'txn:{txn_id}', json.dumps(txn))

    return jsonify({
        'transaction_id': txn_id,
        'type': 'refund',
        'amount': refund_amount,
        'new_balance': new_balance,
        'reason': reason
    })

@app.route('/internal/transactions/<account_id>', methods=['GET'])
def list_transactions(account_id):
    raw = r.lrange(f'txnlog:{account_id}', 0, 99)
    txns = [json.loads(t) for t in raw]
    return jsonify({'transactions': txns, 'count': len(txns)})

@app.route('/internal/transaction/<txn_id>', methods=['GET'])
def get_transaction(txn_id):
    raw = r.get(f'txn:{txn_id}')
    if raw is None:
        return jsonify({'error': 'not found'}), 404
    return jsonify(json.loads(raw))

@app.route('/internal/audit', methods=['GET'])
def audit():
    raw = r.lrange('txnlog:all', 0, 999)
    txns = [json.loads(t) for t in raw]
    memos = []
    for txn in txns:
        if txn.get('memo'):
            memos.append({
                'transaction_id': txn['transaction_id'],
                'type': txn['type'],
                'memo': txn['memo'],
                'account_id': txn.get('account_id', ''),
                'timestamp': txn.get('timestamp', '')
            })
    return jsonify({'audit_entries': memos, 'count': len(memos)})

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=8080, debug=False)
