import { render } from 'preact'
import { Router } from 'wouter-preact'
import { App } from './app'
import './layout.css'

render(
  <Router>
    <App />
  </Router>,
  document.getElementById('app')!
)
