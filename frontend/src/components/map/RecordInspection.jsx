// Componentes
import OperationalRecordDetails from './OperationalRecordDetails.jsx';

export default function RecordInspection({ inspection, loading, onClose }) {
  return (
    <section
      className='record-inspection'
      aria-labelledby='inspection-title'
      aria-live='polite'
    >
      <div className='filter-heading'>
        <h3 id='inspection-title'>Inspeção operacional</h3>
        {inspection && (
          <button
            className='secondary'
            onClick={onClose}
          >
            Fechar inspeção
          </button>
        )}
      </div>
      {!inspection ? (
        <p>Selecione um marcador para consultar seus detalhes operacionais.</p>
      ) : (
        <>
          {(inspection.stale || loading) && (
            <p
              className='stale-data'
              role='status'
            >
              Exibindo a última resposta válida; os dados podem estar desatualizados.
            </p>
          )}
          <OperationalRecordDetails
            kind={inspection.kind}
            record={inspection.record}
          />
        </>
      )}
    </section>
  );
}
