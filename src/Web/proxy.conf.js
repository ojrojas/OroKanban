const target = process.env.API_URL || process.env.api__url
console.log(`[proxy] API target: ${target}`);
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
  }
};
