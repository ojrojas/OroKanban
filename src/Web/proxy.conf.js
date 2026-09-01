const identityTarget = process.env.IDENTITY_URL || process.env.identity__url || process.env.NG_APP_IDENTITY_AUTHORITY || "https://localhost:5086"
const target = process.env.API_URL || process.env.api__url || process.env.NG_APP_API_URL || "http://localhost:5047"
console.log(`[proxy] API target: ${target} (API_URL=${process.env.API_URL} api__url=${process.env.api__url})`);
console.log(`[proxy] Identity target: ${identityTarget}`);
module.exports = {
  "/api": {
    target: target,
    secure: false,
    changeOrigin: true,
    logLevel: "debug",
  },
  "/hubs": {
    target: target,
    secure: false,
    changeOrigin: true,
    ws: true,
    logLevel: "debug",
  },
  "/.well-known": {
    target: identityTarget,
    secure: false,
    changeOrigin: true,
    logLevel: "debug",
  },
  "/connect": {
    target: identityTarget,
    secure: false,
    changeOrigin: true,
    logLevel: "debug",
  }
};
