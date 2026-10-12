import http from 'node:http';
import https from 'node:https';
import { readFile } from 'node:fs/promises';
import { resolve, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import QRCode from 'qrcode';
import { attachBridge } from './bridge.js';
import { experimentHandler } from './experiments.js';

const root = fileURLToPath(new URL('../public/', import.meta.url));
const saveExperiment = experimentHandler(fileURLToPath(new URL('../data/experiments/',import.meta.url)));
const types = {'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.svg':'image/svg+xml'};
const handler = async (req,res) => {
  res.setHeader('X-Content-Type-Options','nosniff');
  res.setHeader('Referrer-Policy','no-referrer');
  res.setHeader('Cache-Control','no-store');
  res.setHeader('Content-Security-Policy', "default-src 'self'; connect-src 'self' ws: wss:; img-src 'self' data:; media-src 'self' blob:; style-src 'self'; script-src 'self'; frame-ancestors 'none'");
  try {
    const url = new URL(req.url, 'http://localhost');
    if(await saveExperiment(req,res,url))return;
    if (url.pathname === '/health') { res.setHeader('Content-Type','application/json'); return res.end('{"ok":true}'); }
    if (url.pathname === '/api/qr') {
      const target = url.searchParams.get('url') ?? '';
      if (target.length > 2048 || !/^https?:\/\//.test(target)) { res.writeHead(400); return res.end('Invalid URL'); }
      res.setHeader('Content-Type','image/svg+xml'); return res.end(await QRCode.toString(target,{type:'svg',margin:2}));
    }
    const pathname = url.pathname === '/' ? '/index.html' : url.pathname === '/monitor' ? '/monitor.html' : url.pathname === '/lab' ? '/lab.html' : url.pathname;
    const path = resolve(root, '.' + decodeURIComponent(pathname));
    if (!path.startsWith(root) || !types[extname(path)]) { res.writeHead(404); return res.end('Not found'); }
    res.setHeader('Content-Type',types[extname(path)]);
    res.end(await readFile(path));
  } catch { res.writeHead(404); res.end('Not found'); }
};
const tls = process.env.TLS_CERT && process.env.TLS_KEY;
const server = tls ? https.createServer({cert:await readFile(process.env.TLS_CERT),key:await readFile(process.env.TLS_KEY)},handler) : http.createServer(handler);
attachBridge(server);
const port = Number(process.env.PORT || 8787), host = process.env.HOST || '127.0.0.1';
server.listen(port,host,() => console.log(`Sensor Bridge: ${tls?'https':'http'}://localhost:${port}\nPC monitor: ${tls?'https':'http'}://localhost:${port}/monitor\nPhone requires trusted HTTPS. See README.md or npm run share.`));
