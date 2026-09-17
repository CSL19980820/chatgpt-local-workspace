// Read-only live view for an already running embedded dashboard. No MCP actions.
// Usage: node scripts/dashboard-preview.cjs http://127.0.0.1:<port>/   (live proxy)
//        node scripts/dashboard-preview.cjs --sample                   (built-in sample snapshot)
const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const args=process.argv.slice(2),sample=args.includes('--sample');
const upstream=sample?null:new URL(args[0]||'');
if(!sample&&(upstream.protocol!=='http:'||upstream.hostname!=='127.0.0.1'||upstream.username||upstream.password))throw Error('Expected a loopback dashboard URL or --sample');
const html=path.join(__dirname,'../src/dashboard.html');
const snapshot=sample?require('./sample-snapshot.cjs'):null;
const policy="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
let origin;
const server=http.createServer(async(req,res)=>{
  const send=(code,type,body)=>{res.writeHead(code,{'Content-Type':type+'; charset=utf-8','Cache-Control':'no-store','Content-Security-Policy':policy,'X-Content-Type-Options':'nosniff'});res.end(body)};
  if(req.headers.host!==origin.host||(req.headers.origin&&req.headers.origin!==origin.origin)||!['same-origin','none',undefined].includes(req.headers['sec-fetch-site']))return send(403,'text/plain','Local same-origin access only');
  if(req.method!=='GET')return send(405,'text/plain','GET only');
  const url=new URL(req.url,origin);
  if(url.origin!==origin.origin)return send(400,'text/plain','Invalid target');
  try{
    if(url.pathname==='/')return send(200,'text/html',await fs.promises.readFile(html,'utf8'));
    if(url.pathname!=='/api/snapshot')return send(404,'text/plain','Not found');
    if(sample){
      const value=snapshot(),thread=url.searchParams.get('thread');
      if(thread)value.activity=value.activity.filter(call=>call.thread_id===thread);
      if(thread)value.conversations=value.conversations.filter(row=>row.thread_id===thread);
      return send(200,'application/json',JSON.stringify(value));
    }
    const target=new URL('/api/snapshot',upstream);
    if(url.searchParams.has('thread'))target.searchParams.set('thread',url.searchParams.get('thread'));
    const response=await fetch(target,{signal:AbortSignal.timeout(5000),redirect:'error'});
    send(response.status,'application/json',await response.text());
  }catch{return send(502,'application/json',JSON.stringify({error:'原工作台连接不可用'}))}
});
server.headersTimeout=5000;server.requestTimeout=6000;server.maxConnections=16;
server.listen(0,'127.0.0.1',()=>{origin=new URL('http://127.0.0.1:'+server.address().port);console.log(origin.href+(sample?' (sample snapshot)':' (live proxy)'))});
