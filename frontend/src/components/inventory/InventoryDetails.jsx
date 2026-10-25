function formatDetailValue(value) {
  if (value === true) return 'Sim';
  if (value === false) return 'Não';
  if (value === null || value === undefined || value === '') return 'Não informado';
  return String(value);
}

export default function InventoryDetails({ entries }) {
  return (
    <dl className='inventory-details'>
      {entries.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{formatDetailValue(value)}</dd>
        </div>
      ))}
    </dl>
  );
}
