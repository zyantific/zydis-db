import './styles.css';

import { mountApp } from './app';

const root = document.getElementById('app');
if (root === null) {
  throw new Error('index.html has no #app element.');
}

mountApp(root);
