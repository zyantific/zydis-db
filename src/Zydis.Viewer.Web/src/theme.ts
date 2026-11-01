import { actionIcon, type ActionIcon } from './icons';

export type Theme = 'system' | 'light' | 'dark';

const THEME_KEY = 'zydis-viewer.theme';
const ORDER: Theme[] = ['system', 'light', 'dark'];
const ICONS: Record<Theme, ActionIcon> = { dark: 'themeDark', light: 'themeLight', system: 'themeSystem' };
const LABELS: Record<Theme, string> = { dark: 'Dark theme', light: 'Light theme', system: 'Theme follows the system' };

// The stylesheet picks its colors through color-scheme, so a theme is the data-theme attribute of the document and
// nothing else. index.html applies the stored value before the first paint, this mirrors it for a change at runtime.
export function applyTheme(theme: Theme): void {
  if (theme === 'system') {
    delete document.documentElement.dataset.theme;
  } else {
    document.documentElement.dataset.theme = theme;
  }
}

export function readTheme(): Theme {
  try {
    const stored = localStorage.getItem(THEME_KEY);
    return stored === 'light' || stored === 'dark' ? stored : 'system';
  } catch {
    // Blocked storage leaves the theme with the system setting.
    return 'system';
  }
}

// Cycles system, light, dark. The label names the theme in use, so a screen reader hears the state and not the action.
export function createThemeToggle(): HTMLButtonElement {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'icon-button theme-toggle';
  let theme = readTheme();

  const render = (): void => {
    button.replaceChildren(actionIcon(ICONS[theme]));
    button.setAttribute('aria-label', LABELS[theme]);
    button.title = `${LABELS[theme]}. Click to switch.`;
  };

  button.addEventListener('click', () => {
    theme = ORDER[(ORDER.indexOf(theme) + 1) % ORDER.length];
    applyTheme(theme);
    try {
      if (theme === 'system') {
        localStorage.removeItem(THEME_KEY);
      } else {
        localStorage.setItem(THEME_KEY, theme);
      }
    } catch {
      // The choice is a convenience, so blocked or full storage only costs the next visit its theme.
    }
    render();
  });

  render();
  return button;
}
