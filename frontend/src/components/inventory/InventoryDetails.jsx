export default function InventoryDetails({ entries }) {
  return <dl className="inventory-details">{entries.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value === true ? 'Sim' : value === false ? 'Não' : value === null || value === undefined || value === '' ? 'Não informado' : String(value)}</dd></div>)}</dl>;
}
