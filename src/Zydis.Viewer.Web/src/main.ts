import '@fontsource/ibm-plex-mono/latin-400.css';
import '@fontsource/ibm-plex-mono/latin-500.css';
import '@fontsource/ibm-plex-sans/latin-400.css';
import '@fontsource/ibm-plex-sans/latin-500.css';
import '@fontsource/ibm-plex-sans/latin-600.css';
import './styles.css';

import { mountApp } from './app';

const root = document.getElementById('app');
if (root === null) {
  throw new Error('index.html has no #app element.');
}

mountApp(root);
