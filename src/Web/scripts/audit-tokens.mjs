import { readdirSync, readFileSync, statSync } from 'fs';
import { join } from 'path';

const srcDir = join(process.cwd(), 'src');
const tokensFile = join(process.cwd(), 'src/app/shared/tokens/tokens.scss');
let violations = [];

function walk(dir) {
  const entries = readdirSync(dir, { withFileTypes: true });
  for (const e of entries) {
    const p = join(dir, e.name);
    if (e.isDirectory()) {
      if (e.name === 'node_modules' || e.name === '.angular' || e.name === 'dist') continue;
      walk(p);
    } else if (e.isFile() && (p.endsWith('.ts') || p.endsWith('.scss') || p.endsWith('.html'))) {
      if (p.includes('tokens.scss') || p.includes('layout.scss')) continue;
      const content = readFileSync(p, 'utf8');
      // Simple check: hard-coded hex colors outside tokens, or box-shadow literals not using var(--shadow
      const hexRe = /#[0-9a-fA-F]{3,6}\b/g;
      const shadowRe = /box-shadow\s*:/g;
      let m;
      while ((m = hexRe.exec(content)) !== null) {
        // allow #FFF / #fff in comments? just flag
        if (p.endsWith('.scss') && content.slice(m.index - 20, m.index).includes('var(')) continue;
        // allow in tokens files only (already skipped)
        violations.push(`${p}:${content.slice(0,m.index).split('\n').length} hard-coded ${m[0]}`);
      }
      if (shadowRe.test(content) && !content.includes('var(--shadow')) {
        violations.push(`${p} hard-coded box-shadow without var(--shadow)`);
      }
    }
  }
}

walk(join(process.cwd(), 'src'));
if (violations.length === 0) {
  console.log('audit-tokens: 0 violations — PASS');
} else {
  console.log(`audit-tokens: ${violations.length} violations`);
  violations.slice(0,20).forEach(v => console.log(' -', v));
  if (violations.length > 0) process.exitCode = 1;
}
