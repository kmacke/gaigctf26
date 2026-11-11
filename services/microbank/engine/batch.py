
import numpy as np

def calculate_batch_total(amounts):
    arr = np.array(amounts, dtype=np.int32)
    total = np.int32(np.vectorize(lambda x: x)(arr).sum())
    return int(total)

def validate_batch(current_balance, amounts):
    total = calculate_batch_total(amounts)
    return total <= current_balance, total
