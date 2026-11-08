#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const catalogs=path.join(root,'My project/Assets/Resources/Localization');
const manifestPath=path.join(catalogs,'locales.json');
const load=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const save=(p,v)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,JSON.stringify(v,null,2)+'\n');};
const manifest=()=>load(manifestPath);
const domains=()=>fs.readdirSync(path.join(catalogs,'es')).filter(f=>f.endsWith('.json')).sort();
const entries=(code,file)=>load(path.join(catalogs,code,file)).entries;
const tokens=text=>[...text.replace(/\{\{|\}\}/g,'').matchAll(/\{([^{}]+)\}/g)].map(m=>m[1]).sort();
function table(code,errors){
 const result=new Map();
 for(const file of domains()){
  const p=path.join(catalogs,code,file);
  if(!fs.existsSync(p)){errors.push(`${code}: missing ${file}`);continue;}
  const rows=entries(code,file);
  if(!Array.isArray(rows)){errors.push(`${code}/${file}: entries must be an array`);continue;}
  for(const row of rows){
   if(!row||typeof row.key!=='string'||!row.key||typeof row.text!=='string'||!row.text.trim()){errors.push(`${code}/${file}: empty key/value ${row?.key??''}`);continue;}
   if(result.has(row.key))errors.push(`${code}: duplicate key ${row.key}`);
   result.set(row.key,{...row,domain:file});
  }
 }
 return result;
}
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);}
function validate(){
 const errors=[];const defs=manifest().locales;
 if(!defs?.length)errors.push('No locales defined.');
 if(!defs.some(l=>l.code==='es'))errors.push('Spanish source locale is required.');
 const codes=new Set();
 for(const locale of defs){if(codes.has(locale.code))errors.push(`Duplicate locale: ${locale.code}`);codes.add(locale.code);if(!/^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/.test(locale.code)||!locale.name?.trim())errors.push(`Invalid locale: ${locale.code}`);}
 const source=table('es',errors);
 if(!source.size)errors.push('Spanish source catalog is empty.');
 for(const locale of defs){const target=table(locale.code,errors);for(const [key,es] of source){const value=target.get(key);if(!value){errors.push(`${locale.code}: missing ${key}`);continue;}if(JSON.stringify(tokens(es.text))!==JSON.stringify(tokens(value.text)))errors.push(`${locale.code}: placeholders differ for ${key}`);}for(const key of target.keys())if(!source.has(key))errors.push(`${locale.code}: unknown key ${key}`);}
 const sourceDirs=['My project/Assets/Scripts','Launcher/App','Launcher/Core'];
 for(const dir of sourceDirs){for(const file of walk(path.join(root,dir)).filter(f=>f.endsWith('.cs')&&!f.includes(path.sep+'obj'+path.sep)&&!f.includes(path.sep+'bin'+path.sep))){const text=fs.readFileSync(file,'utf8');for(const m of text.matchAll(/L10n\.T\("([^"\\]+)"/g)){if(!source.has(m[1]))errors.push(`${path.relative(root,file)}: undefined key ${m[1]}`);}}}
 if(errors.length){console.error(`Localization validation failed (${errors.length}):\n`+errors.join('\n'));process.exitCode=1;return;}
 console.log(`Localization complete: ${source.size} keys × ${defs.length} locales; no missing values, duplicate keys or placeholder mismatches.`);
}
function add(code,name){
 if(!/^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/.test(code??'')||!name?.trim())throw Error('Usage: add <locale-code> <native-language-name>');
 const defs=manifest();if(defs.locales.some(l=>l.code===code))throw Error(`Locale already exists: ${code}`);
 for(const file of domains())save(path.join(catalogs,code,file),{entries:entries('es',file).map(e=>({key:e.key,text:''}))});
 defs.locales.push({code,name});save(manifestPath,defs);
 console.log(`Created ${code}. Fill its translations before validation and Unity catalog synchronization.`);
}
function csvCell(v){return '"'+String(v??'').replaceAll('"','""')+'"';}
function exportCsv(code,out){
 const errors=[];const translated=table(code,errors);const rows=[['domain','key','es',code]];
 for(const file of domains())for(const e of entries('es',file))rows.push([file,e.key,e.text,translated.get(e.key)?.text??'']);
 fs.writeFileSync(path.resolve(out),rows.map(row=>row.map(csvCell).join(',')).join('\r\n')+'\r\n');
 console.log(`Exported ${rows.length-1} entries to ${out}`);
}
function parseCsv(text){
 const rows=[];let row=[],cell='',quoted=false;
 for(let i=0;i<text.length;i++){const c=text[i];if(c==='"'){if(quoted&&text[i+1]==='"'){cell+='"';i++;}else quoted=!quoted;}else if(c===','&&!quoted){row.push(cell);cell='';}else if((c==='\n'||c==='\r')&&!quoted){if(c==='\r'&&text[i+1]==='\n')i++;row.push(cell);if(row.some(Boolean))rows.push(row);row=[];cell='';}else cell+=c;}
 if(quoted)throw Error('Unclosed quote in CSV.');if(cell||row.length){row.push(cell);rows.push(row);}return rows;
}
function importCsv(code,input){
 if(!manifest().locales.some(l=>l.code===code))throw Error(`Unsupported locale: ${code}`);
 const rows=parseCsv(fs.readFileSync(path.resolve(input),'utf8').replace(/^\uFEFF/,''));const header=rows.shift();
 if(header?.join('|')!==['domain','key','es',code].join('|'))throw Error('CSV header does not match the selected locale.');
 const base=new Map(domains().flatMap(file=>entries('es',file).map(e=>[e.key,{...e,domain:file}])));const updates=new Map();
 for(const [domain,key,source,text] of rows){if(!base.has(key)||base.get(key).domain!==domain||base.get(key).text!==source)throw Error(`Unknown or outdated CSV row: ${key}`);if(updates.has(key))throw Error(`Duplicate CSV row: ${key}`);if(!text?.trim()||JSON.stringify(tokens(source))!==JSON.stringify(tokens(text)))throw Error(`Empty translation or changed placeholders: ${key}`);updates.set(key,text);}
 if(updates.size!==base.size)throw Error('CSV does not contain every source key.');
 for(const file of domains())save(path.join(catalogs,code,file),{entries:entries('es',file).map(e=>({key:e.key,text:updates.get(e.key)}))});
 validate();
}
try{
 const [command='validate',arg1,arg2]=process.argv.slice(2);
 if(command==='validate')validate();else if(command==='add')add(arg1,arg2);else if(command==='export')exportCsv(arg1,arg2);else if(command==='import')importCsv(arg1,arg2);else throw Error('Commands: validate | add <code> <name> | export <code> <csv-path> | import <code> <csv-path>');
}catch(error){console.error(error.message);process.exitCode=1;}