// Estilos
import './Brand.css';

export default function Brand() {
  return (
    <div className='brand'>
      <span className='brand-image'>
        <img
          className='brand-logo'
          src='/brand/sentra-logo.png'
          alt='S.E.N.T.R.A.'
          width='2172'
          height='724'
          decoding='async'
        />
      </span>
      <p className='brand-tagline'>Sistema Estratégico de Navegação, Tráfego e Resposta Ativa</p>
    </div>
  );
}
