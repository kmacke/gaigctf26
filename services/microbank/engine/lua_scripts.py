class LuaScriptEngine:
    def __init__(self, redis_client):
        self.r = redis_client

    def deposit(self, account_id, amount):
        script = f"""
            local bal = redis.call('GET', 'balance:{account_id}')
            if not bal then
                return redis.error_reply('account not found')
            end
            local new_bal = tonumber(bal) + {amount}
            redis.call('SET', 'balance:{account_id}', tostring(new_bal))
            return new_bal
        """
        result = self.r.eval(script, 0)
        return int(result)

    def withdraw(self, account_id, amount):
        script = f"""
            local bal = redis.call('GET', 'balance:{account_id}')
            if not bal then
                return redis.error_reply('account not found')
            end
            if tonumber(bal) < {amount} then
                return redis.error_reply('insufficient funds')
            end
            local new_bal = tonumber(bal) - {amount}
            redis.call('SET', 'balance:{account_id}', tostring(new_bal))
            return new_bal
        """
        result = self.r.eval(script, 0)
        if isinstance(result, bytes):
            msg = result.decode()
            if 'insufficient' in msg or 'not found' in msg:
                raise ValueError(msg)
        return int(result)

    def transfer(self, from_account, to_account, amount):
        script = f"""
            local src_bal = redis.call('GET', 'balance:{from_account}')
            if not src_bal then
                return redis.error_reply('source account not found')
            end
            if tonumber(src_bal) < {amount} then
                return redis.error_reply('insufficient funds')
            end
            local dst_bal = redis.call('GET', 'balance:{to_account}')
            if not dst_bal then
                return redis.error_reply('destination account not found')
            end
            local new_src = tonumber(src_bal) - {amount}
            local new_dst = tonumber(dst_bal) + {amount}
            redis.call('SET', 'balance:{from_account}', tostring(new_src))
            redis.call('SET', 'balance:{to_account}', tostring(new_dst))
            return new_src
        """
        result = self.r.eval(script, 0)
        if isinstance(result, bytes):
            msg = result.decode()
            if 'insufficient' in msg or 'not found' in msg:
                raise ValueError(msg)
        return int(result)

    def batch_transfer(self, from_account, to_accounts, amounts):
        set_commands = ""
        total = sum(amounts)
        for to_acct, amt in zip(to_accounts, amounts):
            set_commands += f"""
            local dst_{to_acct[:8]} = redis.call('GET', 'balance:{to_acct}')
            if dst_{to_acct[:8]} then
                redis.call('SET', 'balance:{to_acct}', tostring(tonumber(dst_{to_acct[:8]}) + {amt}))
            end
            """

        script = f"""
            local src_bal = redis.call('GET', 'balance:{from_account}')
            if not src_bal then
                return redis.error_reply('source not found')
            end
            if tonumber(src_bal) < {total} then
                return redis.error_reply('insufficient funds')
            end
            redis.call('SET', 'balance:{from_account}', tostring(tonumber(src_bal) - {total}))
            {set_commands}
            return tonumber(src_bal) - {total}
        """
        result = self.r.eval(script, 0)
        if isinstance(result, bytes):
            msg = result.decode()
            if 'insufficient' in msg or 'not found' in msg:
                raise ValueError(msg)
        return int(result)

    def fee(self, account_id, amount):
        script = f"""
            local bal = redis.call('GET', 'balance:{account_id}')
            if not bal then
                return redis.error_reply('account not found')
            end
            if tonumber(bal) < {amount} then
                return redis.error_reply('insufficient funds for fee')
            end
            local new_bal = tonumber(bal) - {amount}
            redis.call('SET', 'balance:{account_id}', tostring(new_bal))
            return new_bal
        """
        result = self.r.eval(script, 0)
        if isinstance(result, bytes):
            msg = result.decode()
            if 'insufficient' in msg or 'not found' in msg:
                raise ValueError(msg)
        return int(result)

    def refund(self, account_id, amount):
        script = f"""
            local bal = redis.call('GET', 'balance:{account_id}')
            if not bal then
                return redis.error_reply('account not found')
            end
            local new_bal = tonumber(bal) + {amount}
            redis.call('SET', 'balance:{account_id}', tostring(new_bal))
            return new_bal
        """
        result = self.r.eval(script, 0)
        return int(result)
