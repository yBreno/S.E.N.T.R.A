// Bibliotecas externas
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

// Estilos
import './index.css';

// Componentes
import App from './App.jsx';

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
