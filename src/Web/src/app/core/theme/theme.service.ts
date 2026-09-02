import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>(this.load());

  private load(): Theme {
    try {
      const v = localStorage.getItem('orokanban-theme') as Theme | null;
      if (v === 'dark' || v === 'light') return v;
      return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    } catch { return 'light'; }
  }

  constructor() {
    effect(() => {
      const t = this.theme();
      document.documentElement.dataset['theme'] = t;
      try { localStorage.setItem('orokanban-theme', t); } catch {}
    });
  }

  toggle(): void { this.theme.update(v => v === 'light' ? 'dark' : 'light'); }
  setTheme(v: Theme): void { this.theme.set(v); }
}
