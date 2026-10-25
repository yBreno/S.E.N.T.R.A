import { Component } from 'react';

export default class ModuleBoundary extends Component {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <section
        className='panel'
        role='alert'
      >
        <h2>Não foi possível carregar o módulo</h2>
        <p>Confira sua conexão e recarregue a página para tentar novamente.</p>
        <button onClick={() => window.location.reload()}>Recarregar página</button>
      </section>
    ) : (
      this.props.children
    );
  }
}
