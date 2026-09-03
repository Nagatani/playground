// Lightweight HTTP server for MoonBit WASM Playground
// Uses only Node.js standard modules (no npm dependencies needed)

const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = parseInt(process.env.PORT || '8080', 10);
const WEB_ROOT = path.join(__dirname, 'web');

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.wasm': 'application/wasm',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

const server = http.createServer((req, res) => {
  const parsedUrl = new URL(req.url, `http://${req.headers.host}`);
  let pathname = parsedUrl.pathname;

  // Root redirect to /moonbit/
  if (pathname === '/') {
    res.writeHead(302, { 'Location': '/moonbit/' });
    res.end();
    return;
  }

  // Support both /moonbit/... and direct /...
  let relativePath = pathname;
  if (pathname === '/moonbit' || pathname === '/moonbit/') {
    relativePath = '/index.html';
  } else if (pathname.startsWith('/moonbit/')) {
    relativePath = pathname.slice('/moonbit'.length);
  }

  // Sanitize path to prevent directory traversal
  const safePath = path.normalize(relativePath).replace(/^(\.\.[\/\\])+/, '');
  const filePath = path.join(WEB_ROOT, safePath);

  // Security check: ensure path is within WEB_ROOT
  if (!filePath.startsWith(WEB_ROOT)) {
    res.writeHead(403, { 'Content-Type': 'text/plain' });
    res.end('403 Forbidden');
    return;
  }

  fs.stat(filePath, (err, stats) => {
    if (err || !stats.isFile()) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end(`404 Not Found: ${pathname}`);
      return;
    }

    const ext = path.extname(filePath).toLowerCase();
    const contentType = MIME_TYPES[ext] || 'application/octet-stream';

    const headers = {
      'Content-Type': contentType,
      'Content-Length': stats.size,
      'Cache-Control': 'no-cache, no-store, must-revalidate',
      'Access-Control-Allow-Origin': '*',
    };

    res.writeHead(200, headers);
    fs.createReadStream(filePath).pipe(res);
  });
});

server.listen(PORT, () => {
  console.log(`\x1b[32m=====================================================\x1b[0m`);
  console.log(`\x1b[36m   MoonBit WASM Interactive Playground is Running!   \x1b[0m`);
  console.log(`\x1b[32m=====================================================\x1b[0m`);
  console.log(`\n  Local URL: \x1b[1m\x1b[33mhttp://localhost:${PORT}/moonbit/\x1b[0m\n`);
  console.log(`  Press Ctrl+C to stop the server.\n`);
});

server.on('error', (err) => {
  if (err.code === 'EADDRINUSE') {
    const nextPort = PORT + 1;
    console.log(`Port ${PORT} is busy, trying port ${nextPort}...`);
    server.listen(nextPort);
  } else {
    console.error('Server error:', err);
  }
});
