function stripEjsDelimiters(value) {
  if (typeof value !== 'string') return value;
  return value.replace(/<%/g, '').replace(/%>/g, '').replace(/<%-/g, '').replace(/<%=/g, '');
}

function sanitizeObject(obj) {
  if (typeof obj === 'string') {
    return stripEjsDelimiters(obj);
  }
  if (Array.isArray(obj)) {
    return obj.map(sanitizeObject);
  }
  if (obj && typeof obj === 'object') {
    const cleaned = {};
    for (const [key, val] of Object.entries(obj)) {
      cleaned[key] = sanitizeObject(val);
    }
    return cleaned;
  }
  return obj;
}

module.exports = function sanitizeMiddleware(req, res, next) {
  if (req.body && typeof req.body === 'object') {
    req.body = sanitizeObject(req.body);
  }
  if (req.query && typeof req.query === 'object') {
    for (const [key, val] of Object.entries(req.query)) {
      if (typeof val === 'string') {
        req.query[key] = stripEjsDelimiters(val);
      }
    }
  }
  next();
};
