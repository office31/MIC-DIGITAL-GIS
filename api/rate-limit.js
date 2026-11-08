function createRateLimiter({
  limit = 10,
  windowMs = 10 * 60 * 1000,
  keyGenerator = (req) => req.user?.userId,
} = {}) {
  if (!Number.isInteger(limit) || limit < 1) throw new Error("limit must be a positive integer");
  if (!Number.isInteger(windowMs) || windowMs < 1000) throw new Error("windowMs must be at least 1000");

  const buckets = new Map();
  let requestsSinceSweep = 0;

  function sweep(now) {
    for (const [key, bucket] of buckets) {
      if (bucket.resetAt <= now) buckets.delete(key);
    }
  }

  return function rateLimit(req, res, next) {
    const key = keyGenerator(req);
    if (!key) return next();

    const now = Date.now();
    let bucket = buckets.get(key);
    if (!bucket || bucket.resetAt <= now) {
      bucket = { count: 0, resetAt: now + windowMs };
      buckets.set(key, bucket);
    }
    bucket.count += 1;

    res.setHeader("RateLimit-Limit", String(limit));
    res.setHeader("RateLimit-Remaining", String(Math.max(0, limit - bucket.count)));
    res.setHeader("RateLimit-Reset", String(Math.ceil(bucket.resetAt / 1000)));

    if (bucket.count > limit) {
      const retryAfter = Math.max(1, Math.ceil((bucket.resetAt - now) / 1000));
      res.setHeader("Retry-After", String(retryAfter));
      return res.status(429).json({ error: "Too many projects created. Try again later." });
    }

    requestsSinceSweep += 1;
    if (requestsSinceSweep >= 1000 || buckets.size > 5000) {
      sweep(now);
      requestsSinceSweep = 0;
    }
    return next();
  };
}

module.exports = { createRateLimiter };
