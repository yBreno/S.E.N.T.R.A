export default function InventoryField({
  spec,
  value,
  onChange,
  error,
  prefix = '',
  idPrefix = 'equipment-',
}) {
  const id = `${idPrefix}${prefix}${spec.name}`;
  const input = {
    id,
    name: `${prefix}${spec.name}`,
    required: spec.required,
    'aria-invalid': Boolean(error),
    'aria-describedby': error ? `${id}-error` : undefined,
  };
  return (
    <label
      className={spec.type === 'checkbox' ? 'inventory-checkbox' : ''}
      htmlFor={id}
    >
      <span>
        {spec.label}
        {spec.required && ' *'}
      </span>
      {spec.options ? (
        <select
          {...input}
          value={value ?? ''}
          onChange={(event) => onChange(event.target.value)}
        >
          <option value=''>Selecione</option>
          {spec.options.map((option) => (
            <option
              key={option.value ?? option}
              value={option.value ?? option}
            >
              {option.label ?? option}
            </option>
          ))}
        </select>
      ) : spec.type === 'checkbox' ? (
        <input
          {...input}
          type='checkbox'
          checked={Boolean(value)}
          onChange={(event) => onChange(event.target.checked)}
        />
      ) : (
        <input
          {...input}
          type={spec.type ?? 'text'}
          value={value ?? ''}
          autoComplete={spec.autoComplete}
          maxLength={spec.maxLength}
          min={spec.min}
          max={spec.max}
          step={spec.type === 'number' ? (spec.step ?? 1) : undefined}
          placeholder={spec.placeholder}
          onChange={(event) => onChange(event.target.value)}
        />
      )}
      {error && (
        <span
          className='inventory-field-error'
          id={`${id}-error`}
        >
          {error}
        </span>
      )}
    </label>
  );
}
